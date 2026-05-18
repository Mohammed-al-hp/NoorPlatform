namespace NoorPlatform.Core.Entities;

public enum AttendanceStatus
{
    Present,
    Absent,
    Late
}

public class Attendance
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
}
