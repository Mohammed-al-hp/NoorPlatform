namespace NoorPlatform.Core.Entities;

public enum RecordType
{
    Memorization, // تسميع جديد
    Revision      // مراجعة
}

public class HifzRecord
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string SurahName { get; set; } = string.Empty;
    public string? ToSurahName { get; set; }
    public string Verses { get; set; } = string.Empty; // e.g., "1-10"
    public string StartVerseText { get; set; } = string.Empty;
    public string EndVerseText { get; set; } = string.Empty;
    /// <summary>Questions | Sequential — للمراجعة فقط</summary>
    public string? RevisionMode { get; set; }
    /// <summary>JSON: أسئلة المراجعة أو تفاصيل إضافية</summary>
    public string? SessionDetailsJson { get; set; }

    // ✅ إصلاح 1: عدد الآيات الفعلي يُحسب تلقائياً من حقل Verses
    public int VerseCount { get; set; } = 0;

    public RecordType Type { get; set; }
    public string Evaluation { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // Helper: يحسب VerseCount من نص مثل "1-10" أو "5" أو "كاملة"
    public static int ParseVerseCount(string verses)
    {
        if (string.IsNullOrWhiteSpace(verses)) return 0;

        // نمط "من-إلى" مثل "1-10"
        var parts = verses.Trim().Split('-');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out var from) &&
            int.TryParse(parts[1].Trim(), out var to) &&
            to >= from)
        {
            return to - from + 1;
        }

        // رقم مفرد مثل "20"
        if (int.TryParse(verses.Trim(), out var single) && single > 0)
            return single;

        // نص مثل "كاملة" — نُقدّر 0 ويُرك للمحفظ
        return 0;
    }
}