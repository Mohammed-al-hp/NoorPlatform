using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;
using Xunit;

namespace NoorPlatform.Tests.Integration;

/// <summary>
/// اختبارات إصلاحات المرحلة P0: صلاحيات المحفّظ، الشهادات، فرض تغيير كلمة المرور، وتكرار الحضور.
/// تعتمد على بيانات DbInitializer التجريبية (مشرف، محفّظ، طالب في «حلقة الفجر»).
/// </summary>
public class SecurityP0Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!";
    private readonly WebApplicationFactory<Program> _factory;

    static SecurityP0Tests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
    }

    public SecurityP0Tests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("NoorTestDb_SecurityP0");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
    }

    private async Task<(HttpClient Client, string Token)> LoginAsync(string phone, string password)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Phone = phone, Password = password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, token);
    }

    private Task<(HttpClient Client, string Token)> AdminAsync() => LoginAsync("0912345678", "Admin123!");
    private Task<(HttpClient Client, string Token)> TeacherAsync() => LoginAsync("0912345679", "Teacher123!");
    private Task<(HttpClient Client, string Token)> StudentAsync() => LoginAsync("0931234567", "Student123!");

    private async Task<T> WithDbAsync<T>(Func<NoorDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NoorDbContext>());
    }

    private Task<int> SeedStudentIdAsync() =>
        WithDbAsync(db => db.Students.Where(s => s.User.UserName == "218931234567").Select(s => s.Id).FirstAsync());

    /// <summary>طالب خارج حلقات المحفّظ (بدون حلقة) — يُنشأ مباشرة في قاعدة البيانات.</summary>
    private async Task<int> CreateForeignStudentAsync(string userName)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
        var user = new User { UserName = userName, PhoneNumber = userName, FullName = "طالب من حلقة أخرى", Role = UserRole.Student, IsActive = true };
        (await um.CreateAsync(user, "Foreign123!")).Succeeded.Should().BeTrue();
        var student = new Student { UserId = user.Id, ParentPhone = "218910000000", GuardianName = "ولي أمر" };
        db.Students.Add(student);
        await db.SaveChangesAsync();
        return student.Id;
    }

    // ─── 1) المحفّظ لا يكتسب صلاحية على طالب خارج حلقاته عبر الحلقة الإضافية ───
    [Fact]
    public async Task Teacher_CannotEnrollForeignStudent_InExtraCircle()
    {
        var (teacher, _) = await TeacherAsync();
        var create = await teacher.PostAsJsonAsync("/api/circles", new { name = "مراجعة إضافية", isExtra = true });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var circleId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("circleId").GetInt32();

        var foreignId = await CreateForeignStudentAsync("218915550001");

        var enrollForeign = await teacher.PostAsJsonAsync($"/api/circles/{circleId}/enrollments", new { studentIds = new[] { foreignId } });
        enrollForeign.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ولا يزال ممنوعاً من قراءة بيانات الطالب
        (await teacher.GetAsync($"/api/students/{foreignId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // طالب حلقته الرسمية مسموح
        var ownId = await SeedStudentIdAsync();
        var enrollOwn = await teacher.PostAsJsonAsync($"/api/circles/{circleId}/enrollments", new { studentIds = new[] { ownId } });
        enrollOwn.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── 2) نص الإنجاز المخصص في الشهادة للمحفّظ/المشرف فقط ───
    [Fact]
    public async Task Certificate_CustomText_IgnoredForStudent_ValidatedForStaff()
    {
        var studentId = await SeedStudentIdAsync();
        var longText = new string('ح', 150);

        // الطالب: النص المخصص يُتجاهل (لا يصل لمرحلة التحقق من الطول) — الشهادة بالنص المحسوب
        var (student, _) = await StudentAsync();
        var asStudent = await student.GetAsync($"/api/reports/certificate/{studentId}?surah={Uri.EscapeDataString(longText)}");
        asStudent.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);

        // المحفّظ: النص المخصص مقبول لكن بطول محدود
        var (teacher, _) = await TeacherAsync();
        var asTeacher = await teacher.GetAsync($"/api/reports/certificate/{studentId}?surah={Uri.EscapeDataString(longText)}");
        asTeacher.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── 3) فرض تغيير كلمة المرور المؤقتة على مستوى الخادم ───
    [Fact]
    public async Task MustChangePassword_BlocksApi_UntilChanged_AndNewTokenWorks()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User { UserName = "218915550002", PhoneNumber = "218915550002", FullName = "مستخدم مؤقت", Role = UserRole.Parent, IsActive = true, MustChangePassword = true };
            (await um.CreateAsync(user, "Temp12345")).Succeeded.Should().BeTrue();
            await um.AddToRoleAsync(user, "Parent");
        }

        var (client, oldToken) = await LoginAsync("0915550002", "Temp12345");

        var blocked = await client.GetAsync("/api/announcements");
        blocked.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()
            .Should().Be("MUST_CHANGE_PASSWORD");

        var change = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Temp12345", newPassword = "NewPass12345" });
        change.StatusCode.Should().Be(HttpStatusCode.OK);
        var newToken = (await change.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        newToken.Should().NotBeNullOrWhiteSpace();

        // التوكن القديم أُبطل — لذلك يجب على التطبيقات حفظ التوكن الجديد
        (await client.GetAsync("/api/announcements")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        (await client.GetAsync("/api/announcements")).StatusCode.Should().Be(HttpStatusCode.OK);
        oldToken.Should().NotBe(newToken);
    }

    // ─── 4) الحفظ الجماعي لا يُنشئ سجل حضور مكرراً لليوم نفسه ───
    [Fact]
    public async Task BulkAttendance_ReusesExistingDayRecord_AndDedupesRequest()
    {
        var studentId = await SeedStudentIdAsync();
        var circleId = await WithDbAsync(db => db.Students.Where(s => s.Id == studentId).Select(s => s.CircleId!.Value).FirstAsync());
        var day = new DateTime(2026, 3, 10);

        // سجل قديم بدون حلقة (كما تنشئه نقطة الرصد الفردي بدون circleId)
        await WithDbAsync(async db =>
        {
            db.Attendances.Add(new Attendance { StudentId = studentId, Date = day, Status = AttendanceStatus.UnexcusedAbsence });
            return await db.SaveChangesAsync();
        });

        var (admin, _) = await AdminAsync();
        var res = await admin.PostAsJsonAsync("/api/attendance/bulk", new
        {
            date = "2026-03-10",
            circleId,
            records = new object[]
            {
                new { studentId, status = "Late" },
                new { studentId, status = "Present" },   // تكرار في الطلب — آخر حالة هي المعتمدة
                new { studentId, status = "NotAStatus" }
            }
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("saved").GetInt32().Should().Be(1);
        body.GetProperty("skipped").GetInt32().Should().Be(1);

        var records = await WithDbAsync(db => db.Attendances
            .Where(a => a.StudentId == studentId && a.Date >= day && a.Date < day.AddDays(1))
            .ToListAsync());
        records.Should().HaveCount(1);
        records[0].Status.Should().Be(AttendanceStatus.Present);
        records[0].CircleId.Should().Be(circleId);
    }

    [Fact]
    public async Task BulkAttendance_UnknownStudent_Returns400()
    {
        var (admin, _) = await AdminAsync();
        var res = await admin.PostAsJsonAsync("/api/attendance/bulk", new
        {
            date = LibyaTime.Today.ToString("yyyy-MM-dd"),
            records = new object[] { new { studentId = 987654, status = "Present" } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
