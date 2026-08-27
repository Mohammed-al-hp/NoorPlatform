namespace NoorPlatform.Core.Entities;

public enum HomePracticeRating
{
    Excellent = 5,   // ممتاز
    VeryGood = 4,    // جيد جداً
    Good = 3,        // جيد
    Acceptable = 2,  // مقبول
    Weak = 1         // ضعيف
}

/// <summary>ملاحظة أسبوعية من ولي الأمر عن متابعة الحفظ في البيت (استرشادية).</summary>
public class ParentHomeFeedback
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public int ParentId { get; set; }
    public Parent Parent { get; set; } = null!;

    /// <summary>بداية الأسبوع (عادة السبت أو الاثنين حسب إعداد المركز).</summary>
    public DateTime WeekStartDate { get; set; }
    public HomePracticeRating Rating { get; set; } = HomePracticeRating.Good;
    public string? Notes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
