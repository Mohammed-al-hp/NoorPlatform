using Microsoft.Extensions.Options;

namespace NoorPlatform.Api.Services;

/// <summary>
/// قناة إرسال رمز التحقق (OTP) — واتساب حالياً، ويمكن استبدالها بـ SMS لاحقاً
/// بتسجيل تطبيق آخر في Program.cs دون تعديل منطق OtpService.
/// </summary>
public interface IOtpSender
{
    /// <param name="normalizedPhone">الرقم بصيغة التخزين 2189XXXXXXXX</param>
    Task<bool> SendAsync(string normalizedPhone, string code, CancellationToken ct = default);
}

/// <summary>إرسال رمز التحقق عبر WhatsApp Cloud API باستخدام WhatsAppSettings الموجودة.</summary>
public class WhatsAppOtpSender : IOtpSender
{
    private readonly WhatsAppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppOtpSender> _logger;
    private readonly IHostEnvironment _env;

    public WhatsAppOtpSender(
        IOptions<WhatsAppSettings> options,
        IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppOtpSender> logger,
        IHostEnvironment env)
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
            // وضع المحاكاة: في بيئة التطوير فقط نطبع الرمز في السجل لتسهيل الاختبار اليدوي
            if (_env.IsDevelopment())
                _logger.LogWarning("⚠️ واتساب غير مُعيَّن — رمز التحقق (تطوير فقط) للرقم {Phone}: {Code}", normalizedPhone, code);
            else
                _logger.LogWarning("⚠️ واتساب غير مُعيَّن — تعذر إرسال رمز التحقق");
            return false;
        }

        try
        {
            var to = LibyanPhone.ForWhatsApp(normalizedPhone);
            if (string.IsNullOrEmpty(to)) return false;

            var payload = new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { body = $"منصة نور: رمز التحقق لإعادة تعيين كلمة المرور هو {code}\nصالح لمدة 5 دقائق. لا تشاركه مع أحد." }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://graph.facebook.com/v19.0/{_settings.PhoneNumberId}/messages");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.AccessToken}");
            request.Content = JsonContent.Create(payload);

            var client = _httpClientFactory.CreateClient("WhatsApp");
            var response = await client.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إرسال رمز التحقق عبر واتساب");
            return false;
        }
    }
}
