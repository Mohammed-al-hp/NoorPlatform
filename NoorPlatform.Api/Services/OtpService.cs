using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;

namespace NoorPlatform.Api.Services;

public enum OtpIssueStatus
{
    Issued,
    /// <summary>طُلب رمز قبل أقل من 60 ثانية.</summary>
    Cooldown,
    /// <summary>تجاوز الحد الأقصى للإرسال خلال ساعة لنفس الرقم.</summary>
    TooManyRequests
}

public enum OtpVerifyStatus
{
    Valid,
    Invalid,
    /// <summary>لا يوجد رمز فعّال (منتهي، مستهلك، أو لم يُطلب).</summary>
    Expired,
    TooManyAttempts
}

public record OtpIssueResult(OtpIssueStatus Status, string? Code, int RetryAfterSeconds);

/// <summary>
/// إدارة رموز التحقق لمرة واحدة (OTP) لاستعادة كلمة المرور.
/// تُحفظ الرموز في الذاكرة فقط (IMemoryCache) ومُجزّأة بـ HMAC-SHA256 — لا جداول إضافية
/// في قاعدة البيانات، والرمز لا يُخزَّن نصاً أبداً. إعادة تشغيل الخادم تُبطل الرموز المعلّقة.
/// </summary>
public class OtpService
{
    public const int CodeLength = 6;
    public const int MaxAttempts = 5;
    public const int MaxSendsPerHour = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SendWindow = TimeSpan.FromHours(1);

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    // مفتاح عشوائي لكل تشغيل: يكفي لأن الرموز نفسها لا تعيش بعد إعادة التشغيل
    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);

    public OtpService(IMemoryCache cache, TimeProvider time)
    {
        _cache = cache;
        _time = time;
    }

    private sealed class OtpState
    {
        public byte[]? Hash;
        public DateTimeOffset ExpiresAt;
        public int Attempts;
        public DateTimeOffset? LastSentAt;
        public readonly List<DateTimeOffset> SendHistory = new();
    }

    private static string Key(string phone) => "otp:" + phone;

    private OtpState GetState(string phone) =>
        _cache.GetOrCreate(Key(phone), e =>
        {
            // مدة بقاء الحالة في الذاكرة تغطي نافذة حد الإرسال بالساعة
            e.SlidingExpiration = SendWindow;
            return new OtpState();
        })!;

    /// <summary>
    /// يُنشئ رمزاً جديداً (ويُبطل السابق). يُستدعى لكل رقم سواء وُجد حسابه أم لا
    /// حتى تبقى حدود الإرسال والاستجابة متطابقة (منع تعداد المستخدمين).
    /// </summary>
    public OtpIssueResult Issue(string normalizedPhone)
    {
        var state = GetState(normalizedPhone);
        var now = _time.GetUtcNow();

        lock (state)
        {
            if (state.LastSentAt is { } last && now - last < ResendCooldown)
                return new OtpIssueResult(OtpIssueStatus.Cooldown, null,
                    (int)Math.Ceiling((ResendCooldown - (now - last)).TotalSeconds));

            state.SendHistory.RemoveAll(t => now - t >= SendWindow);
            if (state.SendHistory.Count >= MaxSendsPerHour)
            {
                var retry = SendWindow - (now - state.SendHistory.Min());
                return new OtpIssueResult(OtpIssueStatus.TooManyRequests, null, (int)Math.Ceiling(retry.TotalSeconds));
            }

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            state.Hash = ComputeHash(normalizedPhone, code);
            state.ExpiresAt = now + Lifetime;
            state.Attempts = 0;
            state.LastSentAt = now;
            state.SendHistory.Add(now);

            return new OtpIssueResult(OtpIssueStatus.Issued, code, 0);
        }
    }

    /// <summary>يتحقق من الرمز دون استهلاكه (خطوة verify-otp).</summary>
    public OtpVerifyStatus Verify(string normalizedPhone, string code) => Check(normalizedPhone, code, consume: false);

    /// <summary>يتحقق من الرمز ويستهلكه عند النجاح (خطوة reset-password) — لا يُقبل مرة ثانية.</summary>
    public OtpVerifyStatus Consume(string normalizedPhone, string code) => Check(normalizedPhone, code, consume: true);

    private OtpVerifyStatus Check(string normalizedPhone, string code, bool consume)
    {
        if (!_cache.TryGetValue(Key(normalizedPhone), out OtpState? state) || state == null)
            return OtpVerifyStatus.Expired;

        var now = _time.GetUtcNow();
        lock (state)
        {
            if (state.Hash == null || now >= state.ExpiresAt)
            {
                state.Hash = null;
                return OtpVerifyStatus.Expired;
            }

            if (state.Attempts >= MaxAttempts)
            {
                state.Hash = null;
                return OtpVerifyStatus.TooManyAttempts;
            }

            var candidate = (code ?? string.Empty).Trim();
            var ok = candidate.Length == CodeLength &&
                     CryptographicOperations.FixedTimeEquals(state.Hash, ComputeHash(normalizedPhone, candidate));

            if (!ok)
            {
                state.Attempts++;
                if (state.Attempts >= MaxAttempts)
                {
                    state.Hash = null;
                    return OtpVerifyStatus.TooManyAttempts;
                }
                return OtpVerifyStatus.Invalid;
            }

            if (consume)
                state.Hash = null;
            return OtpVerifyStatus.Valid;
        }
    }

    private byte[] ComputeHash(string phone, string code) =>
        HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(phone + ":" + code));
}
