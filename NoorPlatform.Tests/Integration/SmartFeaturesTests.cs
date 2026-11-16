using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NoorPlatform.Infrastructure.Data;
using Xunit;

namespace NoorPlatform.Tests.Integration;

/// <summary>
/// الميزات الذكية: جدول المراجعة والخريطة الحرارية، مركز الإشعارات، منع تكرار جلسات المزامنة،
/// تحليلات المشرف، والتقرير الشهري لولي الأمر.
/// </summary>
public class SmartFeaturesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!";
    private readonly WebApplicationFactory<Program> _factory;

    static SmartFeaturesTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
    }

    public SmartFeaturesTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("NoorTestDb_SmartFeatures");
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

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        r.StatusCode.Should().Be(HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task ReviewSchedule_And_Heatmap_ReflectSessions_AndRespectAccess()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();

        // تسميع قديم لسورة النبأ (فات موعد مراجعته) وتسميع ممتاز اليوم لسورة الملك
        (await teacher.PostAsJsonAsync("/api/hifz", new
        {
            studentId, surahName = "النبأ", verses = "1-40", type = "Memorization", evaluation = "جيد",
            date = DateTime.UtcNow.AddDays(-40)
        })).EnsureSuccessStatusCode();
        (await teacher.PostAsJsonAsync("/api/hifz", new
        {
            studentId, surahName = "الملك", verses = "1-30", type = "Memorization", evaluation = "ممتاز"
        })).EnsureSuccessStatusCode();

        var student = await StudentAsync();
        var schedule = await Json(await student.GetAsync("/api/hifz/review-schedule/my"));
        var due = schedule.GetProperty("due").EnumerateArray().Select(e => e.GetProperty("name").GetString()).ToList();
        due.Should().Contain("النبأ");
        due.Should().NotContain("الملك", "السورة رُوجعت اليوم بتقييم ممتاز");

        var heat = await Json(await student.GetAsync("/api/hifz/heatmap/my"));
        var surahs = heat.GetProperty("surahs").EnumerateArray().ToList();
        surahs.Should().HaveCount(114);
        var mulk = surahs.First(s => s.GetProperty("number").GetInt32() == 67);
        mulk.GetProperty("retention").GetDouble().Should().Be(100);
        mulk.GetProperty("level").GetString().Should().Be("strong");
        var naba = surahs.First(s => s.GetProperty("number").GetInt32() == 78);
        naba.GetProperty("retention").GetDouble().Should().BeLessThan(60);
        heat.GetProperty("juz").GetArrayLength().Should().Be(30);

        // ولي الأمر يرى خريطة ابنه، ولا يرى طالباً آخر
        var parent = await ParentAsync();
        (await parent.GetAsync($"/api/hifz/heatmap/{studentId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await parent.GetAsync("/api/hifz/review-schedule/987654")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Hifz_WithClientRequestId_IsIdempotent()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();
        var requestId = Guid.NewGuid().ToString("N");
        object Body() => new { studentId, surahName = "القلم", verses = "1-10", type = "Memorization", evaluation = "ممتاز", clientRequestId = requestId };

        var first = await Json(await teacher.PostAsJsonAsync("/api/hifz", Body()));
        var points = await Db(db => db.Students.Where(s => s.Id == studentId).Select(s => s.Points).FirstAsync());

        var replay = await Json(await teacher.PostAsJsonAsync("/api/hifz", Body()));
        replay.GetProperty("id").GetInt32().Should().Be(first.GetProperty("id").GetInt32());
        replay.GetProperty("duplicate").GetBoolean().Should().BeTrue();

        (await Db(db => db.HifzRecords.CountAsync(r => r.ClientRequestId == requestId))).Should().Be(1);
        (await Db(db => db.Students.Where(s => s.Id == studentId).Select(s => s.Points).FirstAsync()))
            .Should().Be(points, "إعادة الإرسال لا تضاعف النقاط");
    }

    [Fact]
    public async Task Inbox_ReceivesHifzAndAbsenceNotifications_AndMarksRead()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();
        var parent = await ParentAsync();
        (await parent.PostAsync("/api/inbox/read-all", null)).EnsureSuccessStatusCode();

        (await teacher.PostAsJsonAsync("/api/hifz", new { studentId, surahName = "الطارق", verses = "1-17", type = "Revision", evaluation = "جيد جداً" }))
            .EnsureSuccessStatusCode();
        (await teacher.PostAsJsonAsync("/api/attendance/bulk", new { date = "2026-03-03", records = new[] { new { studentId, status = "UnexcusedAbsence" } } }))
            .EnsureSuccessStatusCode();

        var count = await Json(await parent.GetAsync("/api/inbox/unread-count"));
        count.GetProperty("unread").GetInt32().Should().BeGreaterThanOrEqualTo(2);

        var list = await Json(await parent.GetAsync("/api/inbox?unreadOnly=true"));
        var kinds = list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("kind").GetString()).ToList();
        kinds.Should().Contain("Hifz").And.Contain("Absence");

        var id = list.GetProperty("items")[0].GetProperty("id").GetInt32();
        (await parent.PostAsync($"/api/inbox/{id}/read", null)).EnsureSuccessStatusCode();

        // لا يمكن لمستخدم آخر تعليم إشعار غيره
        var student = await StudentAsync();
        (await student.PostAsync($"/api/inbox/{id}/read", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await parent.PostAsync("/api/inbox/read-all", null)).EnsureSuccessStatusCode();
        (await Json(await parent.GetAsync("/api/inbox/unread-count"))).GetProperty("unread").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Announcement_NotifiesTargetRole()
    {
        var admin = await AdminAsync();
        (await admin.PostAsJsonAsync("/api/announcements", new { title = "إجازة", content = "إجازة يوم الخميس", target = "Students" }))
            .EnsureSuccessStatusCode();
        var student = await StudentAsync();
        var list = await Json(await student.GetAsync("/api/inbox"));
        list.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("kind").GetString() == "Announcement");
    }

    [Fact]
    public async Task Analytics_IsAdminOnly_AndReturnsComparisons()
    {
        var teacher = await TeacherAsync();
        (await teacher.GetAsync("/api/analytics/at-risk")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var admin = await AdminAsync();
        var risk = await Json(await admin.GetAsync("/api/analytics/at-risk?minScore=0"));
        risk.GetProperty("windowDays").GetInt32().Should().Be(30);
        risk.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);

        var perf = await Json(await admin.GetAsync("/api/analytics/performance"));
        perf.GetProperty("circles").GetArrayLength().Should().BeGreaterThan(0);
        perf.GetProperty("teachers").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MonthlyReport_PreviewAndManualSend_LogsAndNotifiesParent()
    {
        var studentId = await SeedStudentIdAsync();
        var teacher = await TeacherAsync();

        var preview = await Json(await teacher.GetAsync($"/api/parent-reports/preview/{studentId}"));
        preview.GetProperty("text").GetString().Should().Contain("التقرير الشهري");

        var sent = await Json(await teacher.PostAsJsonAsync("/api/parent-reports/send", new { studentIds = new[] { studentId } }));
        var status = sent.GetProperty("results")[0].GetProperty("status").GetString();
        status.Should().BeOneOf("Prepared", "NoPhone", "Sent");

        (await Db(db => db.MonthlyParentReports.CountAsync(r => r.StudentId == studentId && r.Trigger == "Manual")))
            .Should().BeGreaterThan(0);

        var parent = await ParentAsync();
        var list = await Json(await parent.GetAsync("/api/inbox"));
        list.GetProperty("items").EnumerateArray()
            .Should().Contain(i => i.GetProperty("kind").GetString() == "MonthlyReport");

        // الطالب وولي الأمر لا يرسلان التقارير
        (await parent.PostAsJsonAsync("/api/parent-reports/send", new { all = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // المحفّظ لا يرسل لطالب خارج نطاقه
        (await teacher.PostAsJsonAsync("/api/parent-reports/send", new { studentIds = new[] { 987654 } })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
