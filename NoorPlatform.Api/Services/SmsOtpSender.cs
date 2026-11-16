using Microsoft.Extensions.Options;

namespace NoorPlatform.Api.Services;

/// <summary>قنوات إرسال رمز التحقق.</summary>
public static class OtpChannels
{
    public const string WhatsApp = "WhatsApp";
    public const string Sms = "Sms";
    /// <summary>القناتان مفعّلتان ويختار المستخدم عند الطلب.</summary>
    public const string Both = "Both";

    public static bool IsValidSetting(string? value) =>
        value is WhatsApp or Sms or Both;

    /// <summary>القنوات المتاحة للمستخدم حسب إعداد النظام.</summary>
    public static string[] Available(string? setting) => setting switch
    {
        Sms => new[] { Sms },
        Both => new[] { WhatsApp, Sms },
        _ => new[] { WhatsApp }
    };

    /// <summary>القناة الفعلية: اختيار المستخدم إن كان متاحاً، وإلا أول قناة متاحة.</summary>
    public static string Resolve(string? setting, string? requested)
    {
        var available = Available(setting);
        var match = available.FirstOrDefault(c => string.Equals(c, requested, StringComparison.OrdinalIgnoreCase));
        return match ?? available[0];
    }
}

/// <summary>
/// إعدادات مزود رسائل SMS (قسم "Sms" في appsettings أو متغيرات البيئة Sms__*).
/// مصممة لمزود HTTP عام: يُرسل POST بصيغة JSON إلى ApiUrl ومعه مفتاح API في الترويسة.
/// </summary>
public class SmsSettings
{
    public const string SectionName = "Sms";

    /// <summary>اسم المزود (للتوثيق والسجلات) — مثل: Almadar، Libyana، Twilio.</summary>
    public string Provider { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    /// <summary>اسم المرسل الظاهر للمستلم.</summary>
    public string SenderId { get; set; } = "Noor";
    /// <summary>اسم ترويسة المفتاح (افتراضياً Authorization بصيغة Bearer).</summary>
    public string ApiKeyHeader { get; set; } = "Authorization";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>إرسال رمز التحقق عبر SMS — جاهز للربط مع مزود الخدمة بمجرد تعبئة SmsSettings.</summary>
public class SmsOtpSender
{
    private readonly SmsSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsOtpSender> _logger;
    private readonly IHostEnvironment _env;

    public SmsOtpSender(IOptions<SmsSettings> options, IHttpClientFactory httpClientFactory,
        ILogger<SmsOtpSender> logger, IHostEnvironment env)
    {
        _settings = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _env = env;
    }

    public async Task<bool> SendAsync(string normalizedPhone, string code, CancellationToken ct = default)
    {
        if (!_settings.IsConfigured)
        {
            if (_env.IsDevelopment())
                _logger.LogWarning("⚠️ مزود SMS غير مُعيَّن — رمز التحقق (تطوير فقط) للرقم {Phone}: {Code}", normalizedPhone, code);
            else
                _logger.LogWarning("⚠️ مزود SMS غير مُعيَّن — تعذر إرسال رمز التحقق");
            return false;
        }

        try
        {
            var payload = new
            {
                to = "+" + normalizedPhone,
                from = _settings.SenderId,
                message = $"منصة نور: رمز التحقق {code} صالح 5 دقائق. لا تشاركه مع أحد."
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, _settings.ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };
            var headerValue = _settings.ApiKeyHeader.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                ? $"Bearer {_settings.ApiKey}"
                : _settings.ApiKey;
            request.Headers.TryAddWithoutValidation(_settings.ApiKeyHeader, headerValue);

            var client = _httpClientFactory.CreateClient("Sms");
            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("مزود SMS ({Provider}) رد بالحالة {Status}", _settings.Provider, (int)response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إرسال رمز التحقق عبر SMS");
            return false;
        }
    }
}

/// <summary>يوجّه رمز التحقق إلى القناة المطلوبة (واتساب أو SMS).</summary>
public class OtpDispatcher : IOtpSender
{
    private readonly WhatsAppOtpSender _whatsApp;
    private readonly SmsOtpSender _sms;

    public OtpDispatcher(WhatsAppOtpSender whatsApp, SmsOtpSender sms)
    {
        _whatsApp = whatsApp;
        _sms = sms;
    }

    public Task<bool> SendAsync(string normalizedPhone, string code, CancellationToken ct = default) =>
        _whatsApp.SendAsync(normalizedPhone, code, ct);

    public Task<bool> SendAsync(string normalizedPhone, string code, string channel, CancellationToken ct = default) =>
        channel == OtpChannels.Sms
            ? _sms.SendAsync(normalizedPhone, code, ct)
            : _whatsApp.SendAsync(normalizedPhone, code, ct);
}
