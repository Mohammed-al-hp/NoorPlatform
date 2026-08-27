namespace NoorPlatform.Core.Entities;

/// <summary>التزام يومي باللباس المطلوب في الحلقة.</summary>
public class DressRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; }
    public bool IsCompliant { get; set; } = true;
    /// <summary>درجة اليوم من 10 (مثلاً 10 ملتزم، 0 مخلّ).</summary>
    public double ScoreOutOf10 { get; set; } = 10;
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }
}
