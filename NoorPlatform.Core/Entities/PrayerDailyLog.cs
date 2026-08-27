namespace NoorPlatform.Core.Entities;

/// <summary>
/// سجل يومي للصلاة يرسله الطالب — يُقفل بعد الإرسال ولا يعدّله إلا الشيخ.
/// نقاط استرشادية.
/// </summary>
public class PrayerDailyLog
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; }

    public bool PrayedInMosque { get; set; }
    public bool OnTime { get; set; }
    /// <summary>اختياري: عدد الصلوات في المسجد هذا اليوم (0–5).</summary>
    public int MosquePrayerCount { get; set; }

    public bool IsLocked { get; set; } = true;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? StudentNote { get; set; }

    public string? SheikhOverrideNote { get; set; }
    public int? OverriddenByUserId { get; set; }
    public DateTime? OverriddenAt { get; set; }
}
