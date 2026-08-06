using System.Net;
using System.Net.Http.Json;
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
/// اختبار تكامل لـ AuthController.Login باستخدام WebApplicationFactory
/// يستخدم InMemory Database بدلاً من SQL Server لسرعة التنفيذ والعزل
/// </summary>
public class AuthLoginTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    static AuthLoginTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!");
    }

    public AuthLoginTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "Jwt:Key", "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!" },
                    { "Jwt:Issuer", "TestIssuer" },
                    { "Jwt:Audience", "TestAudience" },
                    { "Jwt:ExpiryDays", "1" }
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<NoorDbContext>>();
                services.RemoveAll<NoorDbContext>();
                
                services.AddDbContext<NoorDbContext>(options =>
                {
                    options.UseInMemoryDatabase("NoorTestDb_Integration");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
            });
        });
    }

    /// <summary>
    /// عند إرسال بيانات دخول خاطئة (رقم غير موجود)، يجب أن يُرجع 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Phone = "0999999999",
            Password = "WrongPassword123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// عند إرسال بيانات ناقصة (بدون كلمة مرور)، يجب أن يُرجع 400 BadRequest
    /// </summary>
    [Fact]
    public async Task Login_MissingPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Phone = "0912345678",
            Password = ""   // فارغة — ستفشل في [Required, MinLength(6)]
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// عند إنشاء مستخدم صالح وتسجيل الدخول بأرقامه الصحيحة، يجب أن يُرجع 200 + JWT Token
    /// </summary>
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        // Arrange: إنشاء مستخدم اختباري في قاعدة البيانات
        const string testPhone = "0911223344";
        const string testPassword = "Test@12345";

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        var user = new User
        {
            UserName = "218911223344",
            PhoneNumber = "218911223344",
            FullName = "طالب اختباري",
            Role = UserRole.Student,
            IsActive = true,
            MustChangePassword = false
        };

        var createResult = await userManager.CreateAsync(user, testPassword);
        createResult.Succeeded.Should().BeTrue(
            because: "يجب أن ينجح إنشاء المستخدم الاختباري: " +
                     string.Join(", ", createResult.Errors.Select(e => e.Description)));

        // Act: تسجيل الدخول بالرقم المحلي
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            Phone = testPhone,
            Password = testPassword
        });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.Token.Should().NotBeNullOrWhiteSpace("يجب أن يُرجع JWT Token صالح");
        body.User.Should().NotBeNull();
        body.User!.FullName.Should().Be("طالب اختباري");
        body.User.Role.ToString().Should().Be("Student");
    }

    // DTO لتفكيك استجابة Login
    private record LoginResponse(
        string Token,
        bool MustChangePassword,
        LoginUserDto? User);

    private record LoginUserDto(
        int Id,
        string FullName,
        string Phone,
        string? Email,
        string Role);
}

// ─── مطلوب لـ WebApplicationFactory: يجعل Program قابلة للوصول كـ entry point ───
// هذا السطر ضروري فقط إذا لم يكن Program.cs يحتوي على class صريح
// WebApplicationFactory<Program> يبحث عن هذا النوع كنقطة دخول
