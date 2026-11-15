using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using NoorPlatform.Api.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

public class OtpServiceTests
{
    private const string Phone = "218912345678";

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static (OtpService Service, ManualTimeProvider Time) Create()
    {
        var time = new ManualTimeProvider();
        return (new OtpService(new MemoryCache(new MemoryCacheOptions()), time), time);
    }

    private static string WrongCode(string code) => code == "000000" ? "111111" : "000000";

    [Fact]
    public void Issue_ReturnsSixDigitCode()
    {
        var (svc, _) = Create();
        var r = svc.Issue(Phone);
        r.Status.Should().Be(OtpIssueStatus.Issued);
        r.Code.Should().MatchRegex(@"^\d{6}$");
    }

    [Fact]
    public void ValidCode_Verifies_AndIsConsumedOnlyOnce()
    {
        var (svc, _) = Create();
        var code = svc.Issue(Phone).Code!;

        svc.Verify(Phone, code).Should().Be(OtpVerifyStatus.Valid);
        svc.Consume(Phone, code).Should().Be(OtpVerifyStatus.Valid);
        svc.Consume(Phone, code).Should().Be(OtpVerifyStatus.Expired);
    }

    [Fact]
    public void Code_ExpiresAfterFiveMinutes()
    {
        var (svc, time) = Create();
        var code = svc.Issue(Phone).Code!;

        time.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        svc.Verify(Phone, code).Should().Be(OtpVerifyStatus.Valid);

        time.Advance(TimeSpan.FromSeconds(1));
        svc.Verify(Phone, code).Should().Be(OtpVerifyStatus.Expired);
    }

    [Fact]
    public void FifthWrongAttempt_InvalidatesCode()
    {
        var (svc, _) = Create();
        var code = svc.Issue(Phone).Code!;
        var wrong = WrongCode(code);

        for (var i = 0; i < OtpService.MaxAttempts - 1; i++)
            svc.Verify(Phone, wrong).Should().Be(OtpVerifyStatus.Invalid);

        svc.Verify(Phone, wrong).Should().Be(OtpVerifyStatus.TooManyAttempts);
        // حتى الرمز الصحيح يُرفض بعد تجاوز المحاولات
        svc.Verify(Phone, code).Should().NotBe(OtpVerifyStatus.Valid);
    }

    [Fact]
    public void Resend_BlockedWithin60Seconds_ThenAllowed()
    {
        var (svc, time) = Create();
        var first = svc.Issue(Phone).Code!;

        time.Advance(TimeSpan.FromSeconds(30));
        var blocked = svc.Issue(Phone);
        blocked.Status.Should().Be(OtpIssueStatus.Cooldown);
        blocked.RetryAfterSeconds.Should().Be(30);

        time.Advance(TimeSpan.FromSeconds(30));
        var second = svc.Issue(Phone);
        second.Status.Should().Be(OtpIssueStatus.Issued);

        // الرمز الجديد يُبطل القديم
        if (second.Code != first)
            svc.Verify(Phone, first).Should().Be(OtpVerifyStatus.Invalid);
        svc.Verify(Phone, second.Code!).Should().Be(OtpVerifyStatus.Valid);
    }

    [Fact]
    public void PerPhoneHourlyLimit_IsEnforced()
    {
        var (svc, time) = Create();
        for (var i = 0; i < OtpService.MaxSendsPerHour; i++)
        {
            svc.Issue(Phone).Status.Should().Be(OtpIssueStatus.Issued);
            time.Advance(OtpService.ResendCooldown);
        }

        svc.Issue(Phone).Status.Should().Be(OtpIssueStatus.TooManyRequests);

        time.Advance(TimeSpan.FromHours(1));
        svc.Issue(Phone).Status.Should().Be(OtpIssueStatus.Issued);
    }

    [Fact]
    public void Codes_AreIsolatedPerPhone()
    {
        var (svc, _) = Create();
        var code = svc.Issue(Phone).Code!;
        svc.Verify("218923456789", code).Should().Be(OtpVerifyStatus.Expired);
        svc.Verify(Phone, code).Should().Be(OtpVerifyStatus.Valid);
    }

    [Fact]
    public void NoCodeRequested_ReturnsExpired()
    {
        var (svc, _) = Create();
        svc.Verify(Phone, "123456").Should().Be(OtpVerifyStatus.Expired);
    }
}
