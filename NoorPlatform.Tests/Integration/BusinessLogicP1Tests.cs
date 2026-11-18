using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using Xunit;

namespace NoorPlatform.Tests.Integration;

/// <summary>
/// اختبارات المرحلة P1: اتساق النقاط، التحقق من التسميع، الفواتير، الأدوار، المكتبة، نطاق الإعلانات، والردود.
/// </summary>
public class BusinessLogicP1Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!";
    private readonly WebApplicationFactory<Program> _factory;

    static BusinessLogicP1Tests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
    }

    public BusinessLogicP1Tests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", JwtKey },
                    { "Jwt:ExpiryDays", "1" }
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<NoorDbContext>>();
                services.RemoveAll<NoorDbContext>();
                services.AddDbContext<NoorDbContext>(options =>
                {
                    options.UseInMemoryDatabase("NoorTestDb_BusinessLogicP1");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
    }

    private async Task<HttpClient> LoginAsync(string phone, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Phone = phone, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> AdminAsync() => LoginAsync("0912345678", "Admin123!");
    private Task<HttpClient> TeacherAsync() => LoginAsync("0912345679", "Teacher123!");
    private Task<HttpClient> ParentAsync() => LoginAsync("0921234567", "Parent123!");
    private Task<HttpClient> StudentAsync() => LoginAsync("0931234567", "Student123!");

    private async Task<T> Db<T>(Func<NoorDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NoorDbContext>());
    }

    private Task<int> SeedStudentIdAsync() =>
        Db(db => db.Students.Where(s => s.User.UserName == "218931234567").Select(s => s.Id).FirstAsync());

    private Task<int> PointsAsync(int studentId) =>
        Db(db => db.Students.Where(s => s.Id == studentId).Select(s => s.Points).FirstAsync());

    // ─── النقاط: تغيير حالة الحضور يعدّل الرصيد بالفرق ───
    [Fact]
    public async Task Attendance_StatusChange_AdjustsPoints()
    {
        var studentId = await SeedStudentIdAsync();
        var admin = await AdminAsync();
        var before = await PointsAsync(studentId);

        object Body(string status) => new { date = "2026-02-02", records = new[] { new { studentId, status } } };

        (await admin.PostAsJsonAsync("/api/attendance/bulk", Body("Present"))).EnsureSuccessStatusCode();
        (await PointsAsync(studentId)).Should().Be(before + 10);

        // إعادة الحفظ بنفس الحالة لا تضاعف النقاط
        (await admin.PostAsJsonAsync("/api/attendance/bulk", Body("Present"))).EnsureSuccessStatusCode();
        (await PointsAsync(studentId)).Should().Be(before + 10);

        (await admin.PostAsJsonAsync("/api/attendance/bulk", Body("UnexcusedAbsence"))).EnsureSuccessStatusCode();
        (await PointsAsync(studentId)).Should().Be(before);
    }

    // ─── النقاط والتحقق في التسميع ───
    [Fact]
    public async Task Hifz_PointsOnlyForPassing_RevokedOnDelete_AndValidated()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();
        var before = await PointsAsync(studentId);

        var failing = await teacher.PostAsJsonAsync("/api/hifz", new { studentId, surahName = "الملك", verses = "1-5", type = "Memorization", evaluation = "يحتاج مراجعة" });
        failing.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PointsAsync(studentId)).Should().Be(before, "الجلسة غير الناجحة لا تمنح نقاطاً");

        var passing = await teacher.PostAsJsonAsync("/api/hifz", new { studentId, surahName = "الملك", verses = "6-10", type = "Memorization", evaluation = "ممتاز" });
        passing.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await passing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        (await PointsAsync(studentId)).Should().Be(before + 50);

        // تعديل التقييم إلى غير ناجح يسحب النقاط
        var edit = await teacher.PutAsJsonAsync($"/api/hifz/{id}", new { surahName = "الملك", verses = "6-10", type = "Memorization", evaluation = "يحتاج مراجعة" });
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PointsAsync(studentId)).Should().Be(before);

        (await teacher.PutAsJsonAsync($"/api/hifz/{id}", new { surahName = "الملك", verses = "6-10", type = "Memorization", evaluation = "ممتاز" })).EnsureSuccessStatusCode();
        (await teacher.DeleteAsync($"/api/hifz/{id}")).EnsureSuccessStatusCode();
        (await PointsAsync(studentId)).Should().Be(before, "حذف الجلسة يسحب نقاطها");

        var invalid = await teacher.PostAsJsonAsync("/api/hifz", new { studentId, surahName = "", type = "Memorization", evaluation = "ممتاز" });
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var future = await teacher.PostAsJsonAsync("/api/hifz", new { studentId, surahName = "الملك", type = "Memorization", evaluation = "ممتاز", date = DateTime.UtcNow.AddDays(10) });
        future.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── الفواتير: الإلغاء يخفيها عن ولي الأمر ويمنع تحصيلها ───
    [Fact]
    public async Task Payment_Cancel_HidesFromParent_AndBlocksPay()
    {
        var studentId = await SeedStudentIdAsync();
        var admin = await AdminAsync();

        (await admin.PostAsJsonAsync("/api/payments", new { studentId, amount = 50, description = "", dueDate = DateTime.UtcNow.AddDays(5) }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "الوصف مطلوب");

        var create = await admin.PostAsJsonAsync("/api/payments", new { studentId, amount = 75, description = "اشتراك فبراير", dueDate = DateTime.UtcNow.AddDays(5) });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        (await admin.PutAsJsonAsync($"/api/payments/{id}", new { amount = 80, description = "اشتراك فبراير (معدّل)", dueDate = DateTime.UtcNow.AddDays(5) }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.PostAsync($"/api/payments/{id}/cancel", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.PostAsync($"/api/payments/{id}/pay", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var parent = await ParentAsync();
        var list = await parent.GetFromJsonAsync<JsonElement>("/api/payments/parent");
        list.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Should().NotContain(id);
    }

    // ─── لا يُغيَّر دور حساب مرتبط بملف ───
    [Fact]
    public async Task User_RoleChange_RejectedForProfileAccount()
    {
        var admin = await AdminAsync();
        var userId = await Db(db => db.Users.Where(u => u.UserName == "218931234567").Select(u => u.Id).FirstAsync());
        var res = await admin.PutAsJsonAsync($"/api/users/{userId}", new { role = "Teacher" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Db(db => db.Users.Where(u => u.Id == userId).Select(u => u.Role).FirstAsync())).Should().Be(UserRole.Student);
    }

    // ─── المكتبة: رفض ملف بامتداد PDF ومحتوى غير PDF ───
    [Fact]
    public async Task Library_RejectsFakePdf()
    {
        var teacher = await TeacherAsync();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("ملف"), "Title");
        form.Add(new StringContent("متن"), "Category");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "File", "fake.pdf");

        var res = await teacher.PostAsync("/api/library/upload", form);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── إعلان المحفّظ يصل لطلاب حلقاته فقط ───
    [Fact]
    public async Task TeacherAnnouncement_VisibleOnlyToOwnStudents()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            var u = new User { UserName = "218915559001", PhoneNumber = "218915559001", FullName = "طالب حلقة أخرى", Role = UserRole.Student, IsActive = true, MustChangePassword = false };
            (await um.CreateAsync(u, "Other12345")).Succeeded.Should().BeTrue();
            await um.AddToRoleAsync(u, "Student");
            db.Students.Add(new Student { UserId = u.Id, ParentPhone = "218910000001", GuardianName = "ولي" });
            await db.SaveChangesAsync();
        }

        var teacher = await TeacherAsync();
        var title = "إعلان حلقة الفجر " + Guid.NewGuid().ToString("N")[..6];
        (await teacher.PostAsJsonAsync("/api/announcements", new { title, content = "تنبيه للطلاب", target = "Students" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var own = await (await StudentAsync()).GetFromJsonAsync<JsonElement>("/api/announcements");
        own.EnumerateArray().Select(a => a.GetProperty("title").GetString()).Should().Contain(title);

        var other = await (await LoginAsync("0915559001", "Other12345")).GetFromJsonAsync<JsonElement>("/api/announcements");
        other.EnumerateArray().Select(a => a.GetProperty("title").GetString()).Should().NotContain(title);
    }

    // ─── الرد على رسائل ولي الأمر ───
    [Fact]
    public async Task Admin_ReplyToParentMessage_AppearsInParentSent()
    {
        var parent = await ParentAsync();
        var send = await parent.PostAsJsonAsync("/api/messages", new { recipientType = "Admin", content = "استفسار عن موعد الحلقة" });
        send.StatusCode.Should().Be(HttpStatusCode.OK);
        var msgId = (await send.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageId").GetInt32();

        // ولي الأمر لا يرسل بنوع Parent
        (await parent.PostAsJsonAsync("/api/messages", new { recipientType = "Parent", content = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // المحفّظ لا يرد على رسالة موجهة للإدارة
        (await (await TeacherAsync()).PostAsJsonAsync($"/api/messages/{msgId}/reply", new { content = "رد" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await AdminAsync();
        (await admin.PostAsJsonAsync($"/api/messages/{msgId}/reply", new { content = "الحلقة بعد الفجر يومياً" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var sent = await parent.GetFromJsonAsync<JsonElement>("/api/messages/sent");
        var msg = sent.EnumerateArray().First(m => m.GetProperty("id").GetInt32() == msgId);
        msg.GetProperty("replies").EnumerateArray().Select(r => r.GetProperty("content").GetString())
            .Should().Contain("الحلقة بعد الفجر يومياً");
    }

    // ─── المشرف يبدأ رسالة لولي أمر، وولي الأمر يرد داخل المحادثة ───
    [Fact]
    public async Task Staff_StartsThreadWithParent_ParentRepliesInThread()
    {
        var admin = await AdminAsync();
        var parentUserId = await Db(db => db.Users.Where(u => u.UserName == "218921234567").Select(u => u.Id).FirstAsync());

        var recipients = await admin.GetFromJsonAsync<JsonElement>("/api/messages/parent-recipients");
        recipients.EnumerateArray().Select(p => p.GetProperty("userId").GetInt32()).Should().Contain(parentUserId);

        var start = await admin.PostAsJsonAsync("/api/messages/to-parent", new { parentUserId, content = "تذكير باجتماع أولياء الأمور" });
        start.StatusCode.Should().Be(HttpStatusCode.OK);
        var rootId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("messageId").GetInt32();

        var parent = await ParentAsync();
        var threads = await parent.GetFromJsonAsync<JsonElement>("/api/messages/threads");
        threads.EnumerateArray().Should().Contain(t => t.GetProperty("id").GetInt32() == rootId && t.GetProperty("direction").GetString() == "in");

        (await parent.PostAsJsonAsync($"/api/messages/{rootId}/parent-reply", new { content = "سأحضر إن شاء الله" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // الرد يظهر في صندوق الإدارة داخل المحادثة نفسها كرسالة غير مقروءة
        var inbox = await admin.GetFromJsonAsync<JsonElement>("/api/messages/inbox");
        var thread = inbox.EnumerateArray().First(t => t.GetProperty("id").GetInt32() == rootId);
        thread.GetProperty("isRead").GetBoolean().Should().BeFalse();
        thread.GetProperty("replies").EnumerateArray().Select(r => r.GetProperty("content").GetString())
            .Should().Contain("سأحضر إن شاء الله");

        // ولي أمر آخر لا يستطيع الرد على محادثة لا تخصه، والمحفّظ لا يرد على محادثة الإدارة
        (await (await TeacherAsync()).PostAsJsonAsync($"/api/messages/{rootId}/reply", new { content = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── المتن: الجزء نفسه لا يُحتسب مرتين (نافذة التسميع + صفحة التقييم التربوي) ───
    [Fact]
    public async Task Matn_SamePortionMemorizedTwice_IsRejected_AndPointsCountedOnce()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();
        var before = await PointsAsync(studentId);
        var body = new { studentId, matnName = "تحفة الأطفال", portion = "الأبيات 1-10", type = "Memorization", evaluation = "ممتاز" };

        (await teacher.PostAsJsonAsync("/api/pedagogical/matn", body)).StatusCode.Should().Be(HttpStatusCode.OK);
        var afterFirst = await PointsAsync(studentId);
        afterFirst.Should().Be(before + 20);

        // نفس الجزء بصيغة مختلفة قليلاً (تشكيل/مسافات) في يوم آخر
        var dup = await teacher.PostAsJsonAsync("/api/pedagogical/matn",
            new { studentId, matnName = "تُحفة الأطفال ", portion = "الأبيات  1-10", type = "Memorization", evaluation = "جيد", date = DateTime.UtcNow.Date.AddDays(-3) });
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PointsAsync(studentId)).Should().Be(afterFirst, "الجزء المكرر لا يمنح نقاطاً");

        // جزء جديد يُقبل
        (await teacher.PostAsJsonAsync("/api/pedagogical/matn",
            new { studentId, matnName = "تحفة الأطفال", portion = "الأبيات 11-20", type = "Memorization", evaluation = "ضعيف ويحتاج مراجعة" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await PointsAsync(studentId)).Should().Be(afterFirst, "الجلسة الراسبة لا تمنح نقاطاً");
    }

    // ─── الدخول: رقم ليبي من 10 أرقام أو اسم مستخدم الطالب فقط ───
    [Theory]
    [InlineData("966912345678")]      // بادئة دولة أخرى
    [InlineData("091234567")]         // 9 أرقام
    [InlineData("admin@noor.local")]  // بريد إلكتروني
    public async Task Login_RejectsNonLibyanPhoneAndEmail(string login)
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { Phone = login, Password = "Admin123!" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_AcceptsLocalAndInternationalLibyanFormats()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/login", new { Phone = "0912345678", Password = "Admin123!" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/login", new { Phone = "+218912345678", Password = "Admin123!" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── المتصدرون: المحفّظ يرى طلابه افتراضياً ويمكنه عرض المركز، والإدارة ترى المركز ───
    [Fact]
    public async Task Leaderboard_TeacherDefaultsToOwnStudents_CanSwitchToCenter()
    {
        var teacher = await TeacherAsync();
        var mine = await teacher.GetFromJsonAsync<JsonElement>("/api/dashboard/leaderboard");
        mine.EnumerateArray().Should().OnlyContain(s => s.GetProperty("isMine").GetBoolean());

        var center = await teacher.GetAsync("/api/dashboard/leaderboard?scope=center");
        center.StatusCode.Should().Be(HttpStatusCode.OK);
        var centerItems = (await center.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
        centerItems.Count.Should().BeGreaterThanOrEqualTo(mine.GetArrayLength());

        var admin = await (await AdminAsync()).GetFromJsonAsync<JsonElement>("/api/dashboard/leaderboard?scope=mine");
        admin.GetArrayLength().Should().Be(centerItems.Count, "الإدارة ترى المركز كاملاً دائماً");
    }
}
