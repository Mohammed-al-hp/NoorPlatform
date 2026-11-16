namespace NoorPlatform.Core.Entities;

/// <summary>سجل التقرير الشهري المُرسل لولي أمر طالب — يمنع تكرار الإرسال التلقائي لنفس الشهر.</summary>
public class MonthlyParentReport
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;

    /// <summary>Sent = أُرسل عبر واتساب | Prepared = جُهّز (واتساب غير مُعدّ) | Failed | NoPhone</summary>
    public string Status { get; set; } = "Prepared";

    /// <summary>Auto = الخدمة المجدولة | Manual = زر المشرف/المحفّظ</summary>
    public string Trigger { get; set; } = "Auto";
    public int? SentByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
