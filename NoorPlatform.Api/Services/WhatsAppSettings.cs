namespace NoorPlatform.Api.Services;

/// <summary>
/// إعدادات WhatsApp Business API — تُقرأ مرة واحدة عند بدء التشغيل عبر IOptions
/// بدلاً من قراءة IConfiguration في كل استدعاء لتجنب تسرب البيانات الحساسة في Memory traces
/// </summary>
public class WhatsAppSettings
{
    public const string SectionName = "WhatsApp";

    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// تحقق أن الإعدادات مكتملة وليست القيم الافتراضية (وضع المحاكاة)
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(PhoneNumberId) &&
        !string.IsNullOrEmpty(AccessToken) &&
        !AccessToken.StartsWith("EAABXXXX", StringComparison.Ordinal);
}
