namespace NoorPlatform.Core.Entities;

/// <summary>إشعار داخل التطبيق لمستخدم واحد (جرس الإشعارات في الويب والهاتف).</summary>
public class UserNotification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>النوع: Hifz | Absence | Announcement | MessageReply | Message | MonthlyReport | System</summary>
    public string Kind { get; set; } = "System";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>الصفحة التي يفتحها الإشعار في الواجهة (مثل memorization أو messages).</summary>
    public string? Link { get; set; }

    /// <summary>الطالب المعني بالإشعار إن وُجد (لتوجيه ولي الأمر لصفحة الابن).</summary>
    public int? StudentId { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}
