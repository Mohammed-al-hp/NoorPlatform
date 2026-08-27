namespace NoorPlatform.Core.Entities;

/// <summary>فترة تقييم عامة (مثلاً 3–4 أشهر مثل فترات المدرسة).</summary>
public class EvaluationPeriod
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? CircleId { get; set; }
    public Circle? Circle { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<StudentPeriodEvaluation> StudentEvaluations { get; set; } = new();
}

/// <summary>تقييم طالب لفترة محددة — يجمع الحضور والحفظ والتقدم والمتون واللباس.</summary>
public class StudentPeriodEvaluation
{
    public int Id { get; set; }
    public int PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    // أبعاد التقييم الأساسي (من 100 أو نسب قابلة للوزن)
    public double AttendanceScore { get; set; }
    /// <summary>جودة الحفظ: اختبارات شفوية + تسميع يومي (حفظ جديد).</summary>
    public double HifzScore { get; set; }
    /// <summary>جودة المراجعة اليومية من سجلات الحفظ من نوع Revision.</summary>
    public double RevisionScore { get; set; }
    public double ProgressScore { get; set; }
    public double MatnScore { get; set; }
    public double DressScore { get; set; }

    public double OverallScore { get; set; }
    public string GradeLabel { get; set; } = string.Empty; // ممتاز / جيد جداً / جيد / مقبول / ضعيف
    public string? SheikhNotes { get; set; }

    // استرشادية — لا تُدمج تلقائياً في التقييم الأصلي إلا إذا رغب المركز
    public double? PrayerAdvisoryScore { get; set; }
    public double? ParentHomeAdvisoryScore { get; set; }
    public bool IncludeAdvisoryInOverall { get; set; }

    public int EvaluatedByUserId { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}
