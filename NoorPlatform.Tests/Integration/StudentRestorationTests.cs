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

/// <summary>عودة الطالب بعد المغادرة: اكتشاف الطالب المؤرشف واستعادته مع سجلاته.</summary>
public class StudentRestorationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    static StudentRestorationTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!");
    }

    public StudentRestorationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    // لا نغيّر Issuer/Audience: Program.cs يقرأها قبل تطبيق هذا الإعداد فيختلف المُصدِر عن المُتحقق
                    { "Jwt:Key", "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!" },
                    { "Jwt:ExpiryDays", "1" }
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<NoorDbContext>>();
                services.RemoveAll<NoorDbContext>();
                services.AddDbContext<NoorDbContext>(options =>
                {
                    options.UseInMemoryDatabase("NoorTestDb_StudentRestoration");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
    }

    /// <summary>حساب المشرف الافتراضي من DbInitializer (بيئات غير الإنتاج).</summary>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Phone = "0912345678", Password = "Admin123!" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static object StudentPayload(string phone, string parentPhone, object? extra = null)
    {
        var dict = new Dictionary<string, object?>
        {
            ["fullName"] = "عمر خالد المبروك",
            ["phone"] = phone,
            ["guardianName"] = "خالد المبروك",
            ["parentPhone"] = parentPhone,
            ["guardianRelationship"] = "Father",
            ["dateOfBirth"] = "2016-03-01",
            ["registrationDate"] = "2026-01-10"
        };
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties())
                dict[p.Name] = p.GetValue(extra);
        return dict;
    }

    [Fact]
    public async Task ArchivedStudent_IsDetected_ThenRestored_KeepingHistory()
    {
        var client = await AdminClientAsync();

        var create = await client.PostAsJsonAsync("/api/students", StudentPayload("0941111111", "0921111111"));
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var studentId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentId").GetInt32();

        (await client.PostAsJsonAsync("/api/hifz", new
        {
            studentId, surahName = "الفاتحة", verses = "1-7", type = "Memorization", evaluation = "ممتاز"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.DeleteAsync($"/api/students/{studentId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        // إعادة التسجيل بنفس الرقم ← 409 مع بيانات الطالب السابق
        var again = await client.PostAsJsonAsync("/api/students", StudentPayload("0941111111", "0921111111"));
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var conflict = await again.Content.ReadFromJsonAsync<JsonElement>();
        conflict.GetProperty("code").GetString().Should().Be("ARCHIVED_STUDENT_FOUND");
        conflict.GetProperty("canRestore").GetBoolean().Should().BeTrue();
        conflict.GetProperty("archivedStudent").GetProperty("id").GetInt32().Should().Be(studentId);

        // تأكيد الاستعادة
        var restore = await client.PostAsJsonAsync("/api/students",
            StudentPayload("0941111111", "0921111111", new { restoreArchivedStudentId = studentId, level = "متوسط" }));
        restore.StatusCode.Should().Be(HttpStatusCode.OK, await restore.Content.ReadAsStringAsync());
        var restored = await restore.Content.ReadFromJsonAsync<JsonElement>();
        restored.GetProperty("restored").GetBoolean().Should().BeTrue();
        restored.GetProperty("studentId").GetInt32().Should().Be(studentId, "لا يُنشأ طالب جديد");

        // السجلات التاريخية باقية
        var hifz = await client.GetFromJsonAsync<JsonElement>($"/api/hifz/student/{studentId}");
        hifz.GetArrayLength().Should().Be(1);

        var details = await client.GetFromJsonAsync<JsonElement>($"/api/students/{studentId}");
        details.GetProperty("level").GetString().Should().Be("متوسط");
    }

    [Fact]
    public async Task ArchivedStudentWithoutPhone_IsMatchedByNameAndParentPhone()
    {
        var client = await AdminClientAsync();
        var payload = StudentPayload("", "0922222222");

        var create = await client.PostAsJsonAsync("/api/students", payload);
        create.StatusCode.Should().Be(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
        var studentId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentId").GetInt32();
        (await client.DeleteAsync($"/api/students/{studentId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await client.PostAsJsonAsync("/api/students", payload);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // ولي الأمر نفسه يبقى (لا يُنشأ حساب ولي أمر مكرر)
        var ignore = await client.PostAsJsonAsync("/api/students", StudentPayload("", "0922222222", new { ignoreArchivedMatch = true }));
        ignore.StatusCode.Should().Be(HttpStatusCode.OK, await ignore.Content.ReadAsStringAsync());
        (await ignore.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("studentId").GetInt32().Should().NotBe(studentId);
    }

    [Fact]
    public async Task PhoneOfActiveAccount_ReturnsClearConflictMessage()
    {
        var client = await AdminClientAsync();
        // رقم المشرف نفسه
        var res = await client.PostAsJsonAsync("/api/students", StudentPayload("0912345678", "0923333333"));
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString()
            .Should().Contain("حساب نشط آخر");
    }
}
