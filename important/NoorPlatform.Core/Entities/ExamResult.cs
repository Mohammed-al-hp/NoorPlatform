namespace NoorPlatform.Core.Entities;

public class ExamResult
{
    public int Id { get; set; }
    public int ExamId { get; set; }
    public Exam Exam { get; set; } = null!;
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public double Score { get; set; }
    public double MaxScore { get; set; }
    public string Feedback { get; set; } = string.Empty;
}
