namespace NoorPlatform.Core.Entities;

/// <summary>هدف شهري فردي بعدد الأثمان المطلوبة من الطالب.</summary>
public class StudentMonthlyTarget
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }

    /// <summary>عدد الأثمان المطلوب (يحدده شيخ الحلقة حسب عمر/مستوى الطالب).</summary>
    public int TargetAthmanCount { get; set; } = 8;

    /// <summary>ما أنجزه الطالب فعلياً هذا الشهر.</summary>
    public int AchievedAthmanCount { get; set; }

    /// <summary>درجة التقدم المحسوبة أو المعدّلة يدوياً (من 10).</summary>
    public double ProgressScoreOutOf10 { get; set; }

    /// <summary>وضع خاص: لوحة مراجعة / مسابقة — الهدف مخفّض أو معفى.</summary>
    public bool IsSpecialMode { get; set; }
    public string? SpecialModeNote { get; set; }
    public string? Notes { get; set; }

    public int? SetByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
