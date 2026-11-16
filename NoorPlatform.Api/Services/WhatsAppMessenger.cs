using Microsoft.Extensions.Options;

namespace NoorPlatform.Api.Services;

public enum WhatsAppSendResult
{
    Sent,
    /// <summary>واتساب غير مُعدّ (وضع المحاكاة) — الرسالة جاهزة لإرسالها يدوياً عبر رابط wa.me.</summary>
    NotConfigured,
    InvalidPhone,
    Failed
}

/// <summary>إرسال رسائل نصية عبر WhatsApp Cloud API (Meta) — مشترك بين الخدمات المجدولة ونقاط الاتصال.</summary>
public class WhatsAppMessenger
{
    private readonly WhatsAppSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppMessenger> _logger;

    public WhatsAppMessenger(IOptions<WhatsAppSettings> options, IHttpClientFactory httpClientFactory,
        ILogger<WhatsAppMessenger> logger)
    {
        _settings = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsConfigured => _settings.IsConfigured;

    /// <summary>رابط wa.me لفتح المحادثة برسالة جاهزة (للإرسال اليدوي عند عدم إعداد الـ API).</summary>
    public static string? ShareLink(string? phone, string message)
    {
        var to = LibyanPhone.ForWhatsApp(phone);
        return string.IsNullOrEmpty(to) ? null : $"https://wa.me/{to}?text={Uri.EscapeDataString(message)}";
    }

    public async Task<WhatsAppSendResult> SendTextAsync(string? phone, string message, CancellationToken ct = default)
    {
        var to = LibyanPhone.ForWhatsApp(phone);
        if (string.IsNullOrEmpty(to)) return WhatsAppSendResult.InvalidPhone;
        if (!_settings.IsConfigured) return WhatsAppSendResult.NotConfigured;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://graph.facebook.com/v19.0/{_settings.PhoneNumberId}/messages");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_settings.AccessToken}");
            request.Content = JsonContent.Create(new
            {
                messaging_product = "whatsapp",
                to,
                type = "text",
                text = new { body = message }
            });
            var response = await _httpClientFactory.CreateClient("WhatsApp").SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return WhatsAppSendResult.Sent;
            _logger.LogWarning("فشل إرسال رسالة واتساب: {Status}", (int)response.StatusCode);
            return WhatsAppSendResult.Failed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "خطأ في إرسال رسالة واتساب");
            return WhatsAppSendResult.Failed;
        }
    }
}
