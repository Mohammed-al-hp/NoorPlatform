namespace NoorPlatform.Core.Entities;

public enum MatnRecordType
{
    Memorization = 0,
    Revision = 1
}

/// <summary>سجل حفظ/مراجعة متون (مثل منظومة الجزرية وغيرها).</summary>
public class MatnRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string MatnName { get; set; } = string.Empty;
    public string Portion { get; set; } = string.Empty;
    public MatnRecordType Type { get; set; }
    public string Evaluation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int RecordedByUserId { get; set; }
}
