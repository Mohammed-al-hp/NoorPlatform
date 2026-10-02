using NoorPlatform.Core.Entities;

namespace NoorPlatform.Api.Services;

/// <summary>
/// حاسبة تقدم الحفظ — مشتركة بين جميع الـ Controllers
/// القرآن الكريم = 6,236 آية
/// </summary>
public static class HifzProgressCalculator
{
    public const int TotalQuranVerses = 6236;

    /// <summary>
    /// حساب نسبة تقدم الحفظ الحقيقية بناءً على سجلات التسميع.
    /// </summary>
    public static double Calculate(IEnumerable<HifzRecord> records)
    {
        var totalVerses = records
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0
                        ? r.VerseCount
                        : HifzRecord.ParseVerseCount(r.Verses));

        return Math.Min(Math.Round((double)totalVerses / TotalQuranVerses * 100, 1), 100.0);
    }
}
