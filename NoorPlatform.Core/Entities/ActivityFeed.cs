using System;

namespace NoorPlatform.Core.Entities;

public class ActivityFeed
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string UserName { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty; // e.g., "Attendance", "Hifz", "Exam", "Certificate", "Student"
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Icon { get; set; } = "📌";
    public string Color { get; set; } = "blue"; // e.g., "green", "blue", "purple", "teal"
}
