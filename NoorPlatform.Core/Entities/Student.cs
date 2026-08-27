namespace NoorPlatform.Core.Entities;

public class Student
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int? ParentId { get; set; }
    public Parent? Parent { get; set; }
    
    public string Level { get; set; } = "مبتدئ"; 
    public int? CircleId { get; set; }
    public Circle? Circle { get; set; }
    public string ParentPhone { get; set; } = string.Empty;
    public string GuardianName { get; set; } = string.Empty;
    public GuardianRelationship? GuardianRelationship { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public string? StudentPhone { get; set; }
    public string? Residence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; } = false;

    public int Points { get; set; }
    public string? Badges { get; set; }

    public List<Attendance> Attendances { get; set; } = new();
    public List<HifzRecord> HifzRecords { get; set; } = new();
    public List<ExamResult> ExamResults { get; set; } = new();
    public List<CircleEnrollment> ExtraEnrollments { get; set; } = new();
    public List<OralExamSession> OralExamSessions { get; set; } = new();
    public List<MatnRecord> MatnRecords { get; set; } = new();
    public List<StudentMonthlyTarget> MonthlyTargets { get; set; } = new();
    public List<DressRecord> DressRecords { get; set; } = new();
    public List<PrayerDailyLog> PrayerLogs { get; set; } = new();
    public List<ParentHomeFeedback> ParentHomeFeedbacks { get; set; } = new();
}
