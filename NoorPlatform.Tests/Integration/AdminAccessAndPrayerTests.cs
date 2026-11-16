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
/// المدير العام وصلاحيات المشرف، حصر بيانات تسجيل الطلاب بالمشرفين، قائمة الانتظار،
/// ونموذج تسجيل الصلاة التفصيلي مع التحقق من دخول وقت الأذان.
/// </summary>
public class AdminAccessAndPrayerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JwtKey = "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!";
    private readonly WebApplicationFactory<Program> _factory;

    static AdminAccessAndPrayerTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", JwtKey);
    }

    public AdminAccessAndPrayerTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase("NoorTestDb_AdminAccessPrayer");
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
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private Task<HttpClient> SuperAdminAsync() => LoginAsync("0912345678", "Admin123!");
    private Task<HttpClient> TeacherAsync() => LoginAsync("0912345679", "Teacher123!");
    private Task<HttpClient> StudentAsync() => LoginAsync("0931234567", "Student123!");

    /// <summary>مشرف عادي (ليس مديراً عاماً) بصلاحيات محددة — يُنشأ مباشرة في قاعدة البيانات.</summary>
    private async Task<(HttpClient Client, int UserId)> LimitedAdminAsync(string userName, AdminPermission permissions)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            if (await um.FindByNameAsync(userName) == null)
            {
                var user = new User
                {
                    UserName = userName, PhoneNumber = userName, FullName = "مشرف محدود",
                    Role = UserRole.Admin, IsActive = true, MustChangePassword = false,
                    IsSuperAdmin = false, AdminPermissions = permissions
                };
                (await um.CreateAsync(user, "Limited123!")).Succeeded.Should().BeTrue();
                await um.AddToRoleAsync(user, "Admin");
            }
        }
        var id = await WithDbAsync(db => db.Users.Where(u => u.UserName == userName).Select(u => u.Id).FirstAsync());
        var local = "0" + userName[3..];
        return (await LoginAsync(local, "Limited123!"), id);
    }

    private async Task<T> WithDbAsync<T>(Func<NoorDbContext, Task<T>> action)
    {
        using var scope = _factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<NoorDbContext>());
    }

    // ─── المدير العام ───

    [Fact]
    public async Task SeededAdmin_IsSuperAdmin_WithArabicRoleLabel()
    {
        var client = await SuperAdminAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        me.GetProperty("isSuperAdmin").GetBoolean().Should().BeTrue();
        me.GetProperty("roleLabel").GetString().Should().Be("مدير عام");

        var teacher = await TeacherAsync();
        (await teacher.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("roleLabel").GetString().Should().Be("محفّظ");
    }

    [Fact]
    public async Task LimitedAdmin_CannotChangeSettings_OrManageAdmins_ButSuperAdminCan()
    {
        var (limited, limitedId) = await LimitedAdminAsync("218911110001", AdminPermission.ManageFinance);

        var settings = new { centerName = "مركز نور التجريبي" };
        (await limited.PutAsJsonAsync("/api/settings", settings)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await limited.PostAsJsonAsync("/api/users/admin", new { fullName = "مشرف جديد", phone = "0911110009" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var superAdminId = await WithDbAsync(db => db.Users.Where(u => u.UserName == "218912345678").Select(u => u.Id).FirstAsync());
        (await limited.DeleteAsync($"/api/users/{superAdminId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var super = await SuperAdminAsync();
        (await super.PutAsJsonAsync("/api/settings", settings)).StatusCode.Should().Be(HttpStatusCode.OK);

        // المدير العام يمنح صلاحية الإعدادات فيصبح التعديل متاحاً فوراً دون إعادة دخول
        var grant = await super.PutAsJsonAsync($"/api/users/{limitedId}/admin-access",
            new { permissions = new[] { "ManageFinance", "ManageSettings" } });
        grant.StatusCode.Should().Be(HttpStatusCode.OK);
        (await limited.PutAsJsonAsync("/api/settings", settings)).StatusCode.Should().Be(HttpStatusCode.OK);

        // المشرف المحدود لا يستطيع منح نفسه صلاحيات
        (await limited.PutAsJsonAsync($"/api/users/{limitedId}/admin-access", new { isSuperAdmin = true }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LimitedAdmin_WithoutDelete_CannotDeleteStudent()
    {
        var (limited, _) = await LimitedAdminAsync("218911110002", AdminPermission.ManageFinance);
        var studentId = await WithDbAsync(db => db.Students.Select(s => s.Id).FirstAsync());
        (await limited.DeleteAsync($"/api/students/{studentId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LastSuperAdmin_CannotBeDemoted()
    {
        var super = await SuperAdminAsync();
        var id = await WithDbAsync(db => db.Users.Where(u => u.UserName == "218912345678").Select(u => u.Id).FirstAsync());
        var res = await super.PutAsJsonAsync($"/api/users/{id}/admin-access", new { isSuperAdmin = false });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── حصر تسجيل/تعديل بيانات الطلاب بالمشرفين ───

    [Fact]
    public async Task Teacher_CannotCreateOrUpdateStudentRegistration()
    {
        var teacher = await TeacherAsync();
        var create = await teacher.PostAsJsonAsync("/api/students", new
        {
            fullName = "طالب من المحفظ",
            dateOfBirth = "2015-01-01",
            guardianName = "ولي",
            parentPhone = "0921234000",
            guardianRelationship = "Father",
            registrationDate = "2026-01-01"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var studentId = await WithDbAsync(db => db.Students.Where(s => s.User.UserName == "218931234567").Select(s => s.Id).FirstAsync());
        var update = await teacher.PutAsJsonAsync($"/api/students/{studentId}", new { fullName = "تعديل" });
        update.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── قائمة الانتظار: الحلقة المفضلة ───

    [Fact]
    public async Task WaitingList_StoresPreferredCircle()
    {
        var super = await SuperAdminAsync();
        var circle = await WithDbAsync(db => db.Circles.Select(c => new { c.Id, c.Name }).FirstAsync());
        var res = await super.PostAsJsonAsync("/api/waiting-list", new
        {
            fullName = "طالب منتظر",
            parentName = "ولي",
            parentPhone = "0925550001",
            preferredCircleId = circle.Id
        });
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var entry = (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("entry");
        entry.GetProperty("preferredCircleId").GetInt32().Should().Be(circle.Id);
        entry.GetProperty("preferredCircleName").GetString().Should().Be(circle.Name);

        var bad = await super.PostAsJsonAsync("/api/waiting-list", new
        {
            fullName = "طالب آخر", parentName = "ولي", parentPhone = "0925550002", preferredCircleId = 999999
        });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ─── الصلاة ───

    [Fact]
    public async Task Prayer_PerPrayerLogging_LocksEachPrayer_AndComputesJamaahOnlyInMosque()
    {
        var student = await StudentAsync();
        var day = LibyaTime.Today.AddDays(-3).ToString("yyyy-MM-dd");

        var first = await student.PostAsJsonAsync("/api/pedagogical/prayer/my", new
        {
            date = day,
            prayers = new object[]
            {
                new { prayer = "Fajr", prayed = true, inMosque = true, onTime = true },
                new { prayer = "Dhuhr", prayed = true, inMosque = false, onTime = true },
                new { prayer = "Asr", prayed = false, inMosque = true, onTime = true }
            }
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("prayedCount").GetInt32().Should().Be(2);
        body.GetProperty("mosquePrayerCount").GetInt32().Should().Be(1);

        // إضافة صلاة لاحقة في اليوم نفسه مسموحة، وتكرار صلاة مسجّلة مرفوض
        (await student.PostAsJsonAsync("/api/pedagogical/prayer/my", new
        {
            date = day,
            prayers = new[] { new { prayer = "Maghrib", prayed = true, inMosque = true, onTime = true } }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await student.PostAsJsonAsync("/api/pedagogical/prayer/my", new
        {
            date = day,
            prayers = new[] { new { prayer = "Fajr", prayed = true, inMosque = false, onTime = false } }
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var logs = await student.GetFromJsonAsync<JsonElement>("/api/pedagogical/prayer/my");
        var log = logs.EnumerateArray().First(l => l.GetProperty("date").GetString() == day);
        log.GetProperty("entries").GetArrayLength().Should().Be(4);
        log.GetProperty("mosquePrayerCount").GetInt32().Should().Be(2);
        var asr = log.GetProperty("entries").EnumerateArray().First(e => e.GetProperty("prayer").GetString() == "Asr");
        asr.GetProperty("inMosque").GetBoolean().Should().BeFalse("صلاة لم تُؤدَّ لا تُحتسب في المسجد");
    }

    [Fact]
    public async Task Prayer_FutureDay_IsRejected()
    {
        var student = await StudentAsync();
        var res = await student.PostAsJsonAsync("/api/pedagogical/prayer/my", new
        {
            date = LibyaTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
            prayers = new[] { new { prayer = "Fajr", prayed = true, inMosque = true, onTime = true } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Prayer_Today_RejectsPrayersWhoseAdhanHasNotStarted()
    {
        var student = await StudentAsync();
        var now = LibyaTime.Now;
        var today = now.Date;
        var times = PrayerTimes.ForDate(today);
        var pending = times.Where(t => t.Value > now.AddMinutes(1)).Select(t => t.Key).ToList();
        if (pending.Count == 0) return; // بعد العشاء: كل الصلوات دخل وقتها — لا يوجد ما يُختبر الآن

        var res = await student.PostAsJsonAsync("/api/pedagogical/prayer/my", new
        {
            date = today.ToString("yyyy-MM-dd"),
            prayers = new[] { new { prayer = pending.Last().ToString(), prayed = true, inMosque = true, onTime = true } }
        });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be("PRAYER_NOT_YET_DUE");
    }

    [Fact]
    public async Task PrayerTimes_Endpoint_ReturnsFivePrayersForMisrata()
    {
        var student = await StudentAsync();
        var res = await student.GetFromJsonAsync<JsonElement>("/api/pedagogical/prayer-times");
        res.GetProperty("city").GetString().Should().Be("مصراتة");
        res.GetProperty("prayers").GetArrayLength().Should().Be(5);
    }

    [Fact]
    public async Task PublicSettings_ExposeCenterNameAndOtpChannels_Anonymously()
    {
        var client = _factory.CreateClient();
        var res = await client.GetFromJsonAsync<JsonElement>("/api/settings/public");
        res.GetProperty("centerName").GetString().Should().NotBeNullOrWhiteSpace();
        res.GetProperty("otpChannels").EnumerateArray().Select(c => c.GetString()).Should().Contain("WhatsApp");
    }
}
