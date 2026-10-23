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
        var mem = records.Where(r => r.Type == RecordType.Memorization).ToList();
        var total = 0;

        // داخل السورة الواحدة تُدمج النطاقات المتداخلة فلا تُحتسب إعادة التسميع مرتين
        var ranged = mem.Where(r => IsSingleSurah(r) && TryRange(r.Verses, out _, out _)).ToList();
        foreach (var g in ranged.GroupBy(r => r.SurahName))
        {
            var ranges = g.Select(r => { TryRange(r.Verses, out var f, out var t); return (f, t); })
                          .OrderBy(x => x.f).ToList();
            int curFrom = 0, curTo = -1;
            foreach (var (f, t) in ranges)
            {
                if (curTo < 0) { curFrom = f; curTo = t; }
                else if (f <= curTo + 1) curTo = Math.Max(curTo, t);
                else { total += curTo - curFrom + 1; curFrom = f; curTo = t; }
            }
            if (curTo >= 0) total += curTo - curFrom + 1;
        }

        // السجلات الممتدة عبر أكثر من سورة تُحتسب كما سُجّلت
        total += mem.Except(ranged).Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses));

        return Math.Min(Math.Round((double)total / TotalQuranVerses * 100, 1), 100.0);
    }

    private static bool IsSingleSurah(HifzRecord r) =>
        string.IsNullOrEmpty(r.ToSurahName) || r.ToSurahName == r.SurahName;

    private static bool TryRange(string? v, out int from, out int to)
    {
        from = to = 0;
        if (string.IsNullOrWhiteSpace(v)) return false;
        var parts = v.Trim().Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out from) && int.TryParse(parts[1].Trim(), out to))
            return from > 0 && to >= from;
        if (parts.Length == 1 && int.TryParse(parts[0].Trim(), out from) && from > 0) { to = from; return true; }
        return false;
    }
}
