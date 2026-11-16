namespace NoorPlatform.Core.Entities;

public enum AnnouncementTarget
{
    All,       // الجميع
    Teachers,  // المحفظون فقط
    Students,  // الطلاب فقط
    Parents    // أولياء الأمور فقط
}

public class Announcement
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // حقول مطلوبة في الواجهة الأمامية
    public AnnouncementTarget Target { get; set; } = AnnouncementTarget.All;
    public string Color { get; set; } = "#10b981"; // اللون الافتراضي أخضر

    /// <summary>المحفّظ الناشر (null = إعلان عام من الإدارة). إعلان المحفّظ يظهر لطلاب حلقاته وأولياء أمورهم فقط.</summary>
    public int? TeacherId { get; set; }
    public Teacher? Teacher { get; set; }
}
