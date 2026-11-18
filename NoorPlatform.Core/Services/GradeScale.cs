using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

/// <summary>تقدير جلسة التسميع.</summary>
public enum SessionGrade
{
    Excellent,   // ممتاز
    VeryGood,    // جيد جداً
    Good,        // جيد
    Acceptable,  // مقبول (للمراجعة فقط)
    Weak,        // ضعيف ويحتاج مراجعة — جلسة راسبة تُعاد لاحقاً
    Unknown
}

/// <summary>
/// سلّم التقديرات المعتمد للحفظ والمراجعة:
/// ممتاز 85–100%، جيد جداً 75–84%، جيد 65–74%، مقبول 50–64%، ضعيف ويحتاج مراجعة 49% فأقل.
/// الحفظ الجديد بلا «مقبول»: ما دون 65% ضعيف ويحتاج مراجعة.
/// التقدير «ضعيف ويحتاج مراجعة» جلسة راسبة: لا تُحتسب ولا تمنح نقاطاً.
/// </summary>
public static class GradeScale
{
    public const string Excellent = "ممتاز";
    public const string VeryGood = "جيد جداً";
    public const string Good = "جيد";
    public const string Acceptable = "مقبول";
    public const string Weak = "ضعيف ويحتاج مراجعة";

    public static readonly IReadOnlyList<string> MemorizationGrades = new[] { Excellent, VeryGood, Good, Weak };
    public static readonly IReadOnlyList<string> RevisionGrades = new[] { Excellent, VeryGood, Good, Acceptable, Weak };

    /// <summary>التقديرات الناجحة (للعرض في التقارير).</summary>
    public static readonly IReadOnlyList<string> PassingGrades = new[] { Excellent, VeryGood, Good, Acceptable };

    public const double ExcellentMin = 85;
    public const double VeryGoodMin = 75;
    public const double GoodMin = 65;
    public const double AcceptableMin = 50;

    public static IReadOnlyList<string> GradesFor(RecordType type) =>
        type == RecordType.Memorization ? MemorizationGrades : RevisionGrades;

    /// <summary>التقدير من النسبة المئوية حسب نوع الجلسة.</summary>
    public static string FromPercent(double percent, RecordType type = RecordType.Revision) => percent switch
    {
        >= ExcellentMin => Excellent,
        >= VeryGoodMin => VeryGood,
        >= GoodMin => Good,
        >= AcceptableMin when type == RecordType.Revision => Acceptable,
        _ => Weak
    };

    /// <summary>يقرأ تقديراً نصياً (عربي أو إنجليزي، بتشكيل أو دونه، بالصيغ القديمة مثل «ضعيف» و«يحتاج مراجعة»).</summary>
    public static SessionGrade Parse(string? evaluation)
    {
        var e = MonthlyHifzRating.NormalizeEvaluation(evaluation);
        if (e.Length == 0) return SessionGrade.Unknown;

        if (e.Contains("ضعيف") || e.Contains("يحتاج مراجعه") || e.Contains("يحتاج مراجعة") ||
            e.Contains("غير حافظ") || e.Contains("راسب"))
            return SessionGrade.Weak;
        if (e.Contains("ممتاز")) return SessionGrade.Excellent;
        if (e.Contains("جيد جدا")) return SessionGrade.VeryGood;
        if (e.Contains("مقبول")) return SessionGrade.Acceptable;
        if (e == "جيد" || e.StartsWith("جيد ")) return SessionGrade.Good;

        return e.ToLowerInvariant().Replace(" ", "") switch
        {
            "excellent" => SessionGrade.Excellent,
            "verygood" => SessionGrade.VeryGood,
            "good" => SessionGrade.Good,
            "acceptable" => SessionGrade.Acceptable,
            "weak" or "review" or "needsreview" => SessionGrade.Weak,
            _ => SessionGrade.Unknown
        };
    }

    /// <summary>الجلسة ناجحة: تقدير معروف غير «ضعيف ويحتاج مراجعة».</summary>
    public static bool IsPassing(string? evaluation) =>
        Parse(evaluation) is not (SessionGrade.Weak or SessionGrade.Unknown);

    /// <summary>الاسم المعتمد للتقدير (يوحّد الصيغ القديمة).</summary>
    public static string Label(SessionGrade grade) => grade switch
    {
        SessionGrade.Excellent => Excellent,
        SessionGrade.VeryGood => VeryGood,
        SessionGrade.Good => Good,
        SessionGrade.Acceptable => Acceptable,
        SessionGrade.Weak => Weak,
        _ => string.Empty
    };
}
