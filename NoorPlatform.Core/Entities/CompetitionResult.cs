namespace NoorPlatform.Core.Entities;

public class CompetitionResult
{
    public int Id { get; set; }
    
    public int CompetitionId { get; set; }
    public Competition Competition { get; set; } = null!;

    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public double HifzScore { get; set; }
    public double TajweedScore { get; set; }
    public double TafseerScore { get; set; }
    
    // إجمالي الدرجات يحسب آلياً بجمع الفروع الثلاثة
    public double TotalScore => HifzScore + TajweedScore + TafseerScore;

    public string Feedback { get; set; } = string.Empty;
}
