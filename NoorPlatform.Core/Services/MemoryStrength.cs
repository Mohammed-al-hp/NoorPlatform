namespace NoorPlatform.Core.Services;

/// <summary>جلسة واحدة (تسميع أو مراجعة) لسورة معيّنة كما تراها خوارزمية التكرار المتباعد.</summary>
public readonly record struct ReviewSession(DateTime Date, bool IsRevision, int Quality);

/// <summary>حالة حفظ سورة بعد تحليل كل جلساتها.</summary>
public sealed record SurahMemoryState(
    int Stage,
    int Sessions,
    DateTime FirstDate,
    DateTime LastDate,
    int LastQuality,
    int IntervalDays,
    DateTime DueDate);

/// <summary>
/// خوارزمية مراجعة متباعدة مستوحاة من SM-2 ومكيّفة للحلقات:
/// <list type="bullet">
/// <item>كل جلسة تقييمها «ممتاز/جيد جداً» ترفع السورة مرحلة، فتتباعد المراجعة (1، 3، 7، 14، 30، 60، 120 يوماً).</item>
/// <item>«جيد» يُبقي المرحلة، و«مقبول/ضعيف» يُرجعها مرحلتين ويجعل المراجعة التالية بعد يوم واحد.</item>
/// <item>نسبة الثبات تبدأ من جودة آخر جلسة وتضمحل أسياً، فتصل إلى 80٪ تقريباً عند موعد المراجعة.</item>
/// </list>
/// منطق نقي بلا قاعدة بيانات — يُستخدم لجدول المراجعة وللخريطة الحرارية معاً.
/// </summary>
public static class MemoryStrength
{
    public static readonly int[] IntervalsDays = { 1, 3, 7, 14, 30, 60, 120 };

    /// <summary>نسبة الثبات المتبقية عند حلول موعد المراجعة تماماً.</summary>
    public const double RetentionAtDue = 0.8;

    /// <summary>جودة الجلسة من 1 إلى 5 حسب التقييم النصي المستخدم في المنصة.</summary>
    public static int Quality(string? evaluation)
    {
        var e = (evaluation ?? string.Empty).Trim();
        if (e.Length == 0) return 3;
        if (e.Contains("ممتاز")) return 5;
        if (e.StartsWith("جيد جد") || e.StartsWith("جيد جـ")) return 4;
        if (e.StartsWith("جيد")) return 3;
        // «ضعيف ويحتاج مراجعة» جلسة راسبة: يُفحص قبل «يحتاج» حتى لا يُعامل كـ«مقبول»
        if (e.Contains("ضعيف") || e.Contains("إعادة") || e.Contains("اعادة") || e.Contains("راسب")) return 1;
        if (e.Contains("مقبول") || e.Contains("يحتاج")) return 2;
        return 3;
    }

    /// <summary>معامل الإتقان حسب جودة آخر جلسة (ممتاز = 1).</summary>
    public static double QualityFactor(int quality) => quality switch
    {
        >= 5 => 1.0,
        4 => 0.92,
        3 => 0.8,
        2 => 0.6,
        _ => 0.45
    };

    public static SurahMemoryState? Evaluate(IEnumerable<ReviewSession> sessions)
    {
        var ordered = sessions.OrderBy(s => s.Date).ToList();
        if (ordered.Count == 0) return null;

        var stage = 0;
        foreach (var s in ordered)
        {
            if (s.Quality >= 4) stage = Math.Min(stage + 1, IntervalsDays.Length - 1);
            else if (s.Quality <= 2) stage = Math.Max(stage - 2, 0);
        }

        var last = ordered[^1];
        var interval = last.Quality <= 2 ? 1 : IntervalsDays[stage];
        var lastDate = last.Date.Date;
        return new SurahMemoryState(stage, ordered.Count, ordered[0].Date.Date, lastDate,
            last.Quality, interval, lastDate.AddDays(interval));
    }

    /// <summary>نسبة الثبات (0–100) في يوم معيّن.</summary>
    public static double Retention(SurahMemoryState state, DateTime today)
    {
        var days = Math.Max(0, (today.Date - state.LastDate).TotalDays);
        var decay = Math.Pow(RetentionAtDue, days / state.IntervalDays);
        return Math.Round(100 * QualityFactor(state.LastQuality) * decay, 1);
    }

    /// <summary>أيام التأخر عن موعد المراجعة (سالبة = لم يحن بعد).</summary>
    public static int OverdueDays(SurahMemoryState state, DateTime today) =>
        (int)(today.Date - state.DueDate).TotalDays;

    /// <summary>أولوية المراجعة: كلما زاد التأخر نسبةً إلى الفترة وقلّ الثبات ارتفعت.</summary>
    public static double Priority(SurahMemoryState state, DateTime today)
    {
        var elapsed = Math.Max(0, (today.Date - state.LastDate).TotalDays);
        return Math.Round(elapsed / state.IntervalDays + (100 - Retention(state, today)) / 100, 3);
    }

    /// <summary>تصنيف لوني للخريطة الحرارية.</summary>
    public static string Level(double retention) => retention switch
    {
        >= 90 => "strong",
        >= 75 => "good",
        >= 60 => "fading",
        >= 40 => "weak",
        _ => "critical"
    };
}
