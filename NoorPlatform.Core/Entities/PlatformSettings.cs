namespace NoorPlatform.Core.Entities;

public class PlatformSettings
{
    public int Id { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    // ─── معلومات المركز الإضافية ───
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // ─── إعدادات الدوام ───
    /// <summary>أيام الدوام — مخزنة كـ CSV مثل "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء"</summary>
    public string WorkDays { get; set; } = "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء,الخميس";
    /// <summary>وقت بداية الدوام مثل "08:00"</summary>
    public string WorkStartTime { get; set; } = "08:00";
    /// <summary>وقت نهاية الدوام مثل "12:00"</summary>
    public string WorkEndTime { get; set; } = "12:00";

    // ─── إعدادات المالية ───
    public decimal DefaultMonthlyFee { get; set; } = 0;
    public string Currency { get; set; } = "د.ل";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}