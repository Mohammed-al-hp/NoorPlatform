namespace NoorPlatform.Core.Entities;

public class Circle
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
    public string Time { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Icon { get; set; } = "✨";

    /// <summary>حلقة إضافية (صيف، عطلة، رغبة الشيخ) وليست الحلقة الرسمية الثابتة.</summary>
    public bool IsExtra { get; set; }

    /// <summary>تاريخ جلسة الحلقة الإضافية (للحلقات ذات الموعد المحدد).</summary>
    public DateTime? SessionDate { get; set; }

    /// <summary>ربط اختياري بالحلقة الرسمية الأم.</summary>
    public int? ParentCircleId { get; set; }
    public Circle? ParentCircle { get; set; }

    public List<Student> Students { get; set; } = new();
    public List<CircleEnrollment> Enrollments { get; set; } = new();
}
