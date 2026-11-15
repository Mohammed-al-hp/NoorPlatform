using System.Collections.Concurrent;
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
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using Xunit;

namespace NoorPlatform.Tests.Integration;

/// <summary>تدفق "نسيت كلمة المرور" الكامل عبر OTP مع مُرسل وهمي يلتقط الرمز.</summary>
public class ForgotPasswordOtpTests : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class CapturingOtpSender : IOtpSender
    {
        public ConcurrentDictionary<string, string> Sent { get; } = new();
        public Task<bool> SendAsync(string normalizedPhone, string code, CancellationToken ct = default)
        {
            Sent[normalizedPhone] = code;
            return Task.FromResult(true);
        }
    }

    private readonly WebApplicationFactory<Program> _factory;
    private readonly CapturingOtpSender _sender = new();

    static ForgotPasswordOtpTests()
    {
        Environment.SetEnvironmentVariable("Jwt__Key", "ThisIsAVerySecureAndLongMockKeyForTestingPurposes12345!");
    }

    public ForgotPasswordOtpTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
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
                    options.UseInMemoryDatabase("NoorTestDb_ForgotPassword");
                    options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning));
                });
                services.RemoveAll<IOtpSender>();
                services.AddSingleton<IOtpSender>(_sender);
            });
        });
    }

    private async Task CreateUserAsync(string normalized, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var um = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        if (await um.FindByNameAsync(normalized) != null) return;
        var r = await um.CreateAsync(new User
        {
            UserName = normalized,
            PhoneNumber = normalized,
            FullName = "مستخدم استعادة",
            Role = UserRole.Parent,
            IsActive = true,
            MustChangePassword = false
        }, password);
        r.Succeeded.Should().BeTrue();
    }

    private async Task<string> WaitForCodeAsync(string normalized)
    {
        for (var i = 0; i < 50; i++)
        {
            if (_sender.Sent.TryGetValue(normalized, out var c)) return c;
            await Task.Delay(20);
        }
        throw new TimeoutException("لم يُرسل الرمز");
    }

    [Fact]
    public async Task UnknownPhone_GetsSameResponseAsKnownPhone()
    {
        await CreateUserAsync("218931111111", "OldPass123");
        var client = _factory.CreateClient();

        var known = await client.PostAsJsonAsync("/api/auth/forgot-password", new { Phone = "0931111111" });
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { Phone = "0939999999" });

        known.StatusCode.Should().Be(HttpStatusCode.OK);
        unknown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await unknown.Content.ReadAsStringAsync()).Should().Be(await known.Content.ReadAsStringAsync());

        await Task.Delay(200);
        _sender.Sent.ContainsKey("218939999999").Should().BeFalse("لا يُرسل رمز لرقم غير مسجل");
    }

    [Fact]
    public async Task FullFlow_ResetsPassword_AndCodeCannotBeReused()
    {
        const string normalized = "218932222222";
        await CreateUserAsync(normalized, "OldPass123");
        var client = _factory.CreateClient();

        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { Phone = "0932222222" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var code = await WaitForCodeAsync(normalized);

        (await client.PostAsJsonAsync("/api/auth/verify-otp", new { Phone = "0932222222", Code = code }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PostAsJsonAsync("/api/auth/reset-password", new { Phone = "0932222222", Code = code, NewPassword = "NewPass456" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        // الرمز يُستهلك مرة واحدة
        (await client.PostAsJsonAsync("/api/auth/reset-password", new { Phone = "0932222222", Code = code, NewPassword = "Another789" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PostAsJsonAsync("/api/auth/login", new { Phone = "0932222222", Password = "NewPass456" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/login", new { Phone = "0932222222", Password = "OldPass123" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SecondRequestWithin60Seconds_Returns429()
    {
        var client = _factory.CreateClient();
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { Phone = "0933333333" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("/api/auth/forgot-password", new { Phone = "0933333333" }))
            .StatusCode.Should().Be((HttpStatusCode)429);
    }
}
