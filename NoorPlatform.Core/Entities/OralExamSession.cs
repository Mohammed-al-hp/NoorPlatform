namespace NoorPlatform.Core.Entities;

/// <summary>نوع الاختبار الشفوي: سرد كامل أو تسمية أثمان/مواضع.</summary>
public enum OralExamKind
{
    FullRecitation = 0,  // سرد كامل
    AthmanSampling = 1   // تسمية أثمان / مواضع آيات
}

/// <summary>جلسة اختبار شفوي لطالب واحد (سرد أو أثمان).</summary>
public class OralExamSession
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int? CircleId { get; set; }
    public Circle? Circle { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public OralExamKind Kind { get; set; } = OralExamKind.AthmanSampling;
    /// <summary>نطاق الاختبار مثل: ربع، جزء عم، من البقرة 1 إلى ...</summary>
    public string ScopeLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }

    /// <summary>حد الفتحات عبر الجلسة لاعتبار الطالب غير حافظ (قابل للتعديل).</summary>
    public int MaxOpeningsBeforeFail { get; set; } = 3;

    public double OverallPercent { get; set; }
    public string OverallGrade { get; set; } = string.Empty;
    public bool IsConsideredMemorized { get; set; } = true;

    public int RecordedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<OralExamQuestion> Questions { get; set; } = new();
}

/// <summary>سؤال مستقل = ثمن أو موضع آيات داخل الجلسة.</summary>
public class OralExamQuestion
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public OralExamSession Session { get; set; } = null!;
    public int OrderIndex { get; set; }
    /// <summary>مثال: الثمن الأول، من آية كذا...</summary>
    public string Label { get; set; } = string.Empty;

    public int HesitationCount { get; set; }  // تردد
    public int AlertCount { get; set; }       // تنبيه
    public int OpeningCount { get; set; }     // فتح

    /// <summary>درجة السؤال 0–100 من انطباع الشيخ أو محسوبة.</summary>
    public double ScorePercent { get; set; }
    public string Impression { get; set; } = string.Empty;
    public bool IsPassed { get; set; } = true;
}
