namespace NoorPlatform.Core.Entities;

/// <summary>
/// تسجيل طالب في حلقة إضافية (غير حلقته الرسمية).
/// </summary>
public class CircleEnrollment
{
    public int Id { get; set; }
    public int CircleId { get; set; }
    public Circle Circle { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
}
