using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;

namespace NoorPlatform.Api.Services;

/// <summary>موضع الطالب الحالي في الحفظ الجديد (أبعد نقطة وصل إليها بالترتيب العكسي).</summary>
/// <param name="SurahNumber">رقم السورة التي وصل إليها.</param>
/// <param name="SurahName">اسمها.</param>
/// <param name="LastVerse">آخر آية حفظها منها.</param>
/// <param name="VerseCount">عدد آيات السورة.</param>
public sealed record HifzPosition(int SurahNumber, string SurahName, int LastVerse, int VerseCount)
{
    /// <summary>أتمّ السورة كاملة — الجلسة القادمة تبدأ بالسورة التي قبلها في المصحف.</summary>
    public bool SurahCompleted => LastVerse >= VerseCount;

    /// <summary>السورة التي يحفظ فيها الآن (أو التالية إن أتمّ الحالية). null = أتمّ القرآن.</summary>
    public QuranSurah? CurrentSurah => SurahCompleted
        ? (SurahNumber > 1 ? QuranIndex.Surahs[SurahNumber - 2] : null)
        : QuranIndex.Surahs[SurahNumber - 1];

    /// <summary>وصف مختصر للعرض: «يحفظ الآن سورة النازعات — وصل إلى الآية 18 من 46».</summary>
    public string Describe()
    {
        if (!SurahCompleted)
            return $"سورة {SurahName} — وصل إلى الآية {LastVerse} من {VerseCount}";
        var next = CurrentSurah;
        return next == null
            ? "أتمّ حفظ القرآن الكريم"
            : $"أتمّ سورة {SurahName} — التالية: سورة {next.Name}";
    }
}

/// <summary>
/// حاسبة «المحفوظ» من القرآن — مشتركة بين جميع الـ Controllers.
/// <para>
/// المعتمد في المركز أن الطلاب يحفظون بالترتيب العكسي (من الناس إلى الفاتحة). لذلك أبعد موضع وصل إليه الطالب
/// في الحفظ الجديد يعني أنه أتمّ كل السور التي بعده في المصحف، حتى لو لم تُسجَّل جلساتها في المنصة
/// (مثل الطالب الذي سُجّلت أولى جلساته في النازعات: محفوظه يشمل جزء عمّ حتى النازعات).
/// </para>
/// <para>النسبة = (آيات السور بعد سورة الموضع + الآيات التي وصل إليها في سورة الموضع) ÷ 6214 (العدّ المدني الأول المعتمد في رواية قالون).</para>
/// <para>هذه نسبة «كمّ حفظ من القرآن» وليست مستوى إتقانه؛ الإتقان تقيسه جلسات المراجعة (خريطة الإتقان).</para>
/// <para>الجلسة الراسبة («ضعيف ويحتاج مراجعة») لا تُقدّم الموضع لأن الطالب سيعيدها.</para>
/// </summary>
public static class HifzProgressCalculator
{
    public static readonly int TotalQuranVerses = QuranIndex.Surahs.Sum(s => s.VerseCount); // 6214

    /// <summary>عدد آيات كل سورة وما بعدها في المصحف (للحساب السريع).</summary>
    private static readonly int[] VersesFromSurahToEnd = BuildSuffixSums();

    private static int[] BuildSuffixSums()
    {
        var sums = new int[QuranIndex.Surahs.Count + 2];
        for (var n = QuranIndex.Surahs.Count; n >= 1; n--)
            sums[n] = sums[n + 1] + QuranIndex.Surahs[n - 1].VerseCount;
        return sums;
    }

    /// <summary>نسبة المحفوظ من القرآن (0–100).</summary>
    public static double Calculate(IEnumerable<HifzRecord> records)
    {
        var verses = MemorizedVerses(records);
        return Math.Min(Math.Round((double)verses / TotalQuranVerses * 100, 1), 100.0);
    }

    /// <summary>عدد الآيات المحفوظة بالترتيب العكسي حتى الموضع الحالي.</summary>
    public static int MemorizedVerses(IEnumerable<HifzRecord> records)
    {
        var pos = CurrentPosition(records);
        if (pos == null) return 0;
        return VersesFromSurahToEnd[pos.SurahNumber + 1] + Math.Min(pos.LastVerse, pos.VerseCount);
    }

    /// <summary>أبعد موضع في الحفظ الجديد: أصغر رقم سورة، ثم أكبر آية فيها. null = لا جلسات حفظ ناجحة.</summary>
    public static HifzPosition? CurrentPosition(IEnumerable<HifzRecord> records)
    {
        HifzPosition? best = null;
        foreach (var r in records)
        {
            if (r.Type != RecordType.Memorization) continue;
            if (GradeScale.Parse(r.Evaluation) == SessionGrade.Weak) continue;
            var point = EndPoint(r);
            if (point == null) continue;
            if (best == null ||
                point.SurahNumber < best.SurahNumber ||
                (point.SurahNumber == best.SurahNumber && point.LastVerse > best.LastVerse))
                best = point;
        }
        return best;
    }

    /// <summary>
    /// النقطة التي انتهت عندها الجلسة. الجلسة الممتدة عبر سورتين تنتهي في السورة الأقرب للفاتحة:
    /// إن كانت هي «إلى سورة» فآخر آية هي رقم النهاية، وإلا فالسورة كاملة.
    /// </summary>
    private static HifzPosition? EndPoint(HifzRecord r)
    {
        var from = QuranIndex.Find(r.SurahName);
        if (from == null) return null;
        var to = QuranIndex.Find(r.ToSurahName) ?? from;
        var hasRange = TryRange(r.Verses, out _, out var endVerse);

        var lower = from.Number <= to.Number ? from : to;
        int lastVerse;
        if (from.Number == to.Number)
            lastVerse = hasRange ? endVerse : lower.VerseCount; // «كاملة» أو نص غير رقمي = السورة كاملة
        else
            lastVerse = lower.Number == to.Number && hasRange ? endVerse : lower.VerseCount;

        lastVerse = Math.Clamp(lastVerse, 1, lower.VerseCount);
        return new HifzPosition(lower.Number, lower.Name, lastVerse, lower.VerseCount);
    }

    private static bool TryRange(string? v, out int from, out int to)
    {
        from = to = 0;
        if (string.IsNullOrWhiteSpace(v)) return false;
        var parts = v.Trim().Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out from) && int.TryParse(parts[1].Trim(), out to))
            return from > 0 && to > 0;
        if (parts.Length == 1 && int.TryParse(parts[0].Trim(), out from) && from > 0) { to = from; return true; }
        return false;
    }
}
