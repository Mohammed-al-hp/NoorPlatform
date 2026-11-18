using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

public enum HifzAgeBand
{
    Age5To7,
    Age8To10,
    Age11Plus
}

/// <summary>إعدادات التقييم الشهري — تُقرأ من PlatformSettings.</summary>
public record MonthlyHifzRatingConfig(int TargetAge5To7, int TargetAge8To10, int TargetAge11Plus, IReadOnlyList<string> PassingEvaluations)
{
    public static readonly MonthlyHifzRatingConfig Default =
        new(4, 6, 8, GradeScale.PassingGrades);

    public static MonthlyHifzRatingConfig From(PlatformSettings? s)
    {
        if (s == null) return Default;
        return new MonthlyHifzRatingConfig(
            s.HifzRatingTargetAge5To7 > 0 ? s.HifzRatingTargetAge5To7 : Default.TargetAge5To7,
            s.HifzRatingTargetAge8To10 > 0 ? s.HifzRatingTargetAge8To10 : Default.TargetAge8To10,
            s.HifzRatingTargetAge11Plus > 0 ? s.HifzRatingTargetAge11Plus : Default.TargetAge11Plus,
            GradeScale.PassingGrades);
    }
}

public record MonthlyHifzRatingResult(
    int Year,
    int Month,
    int? AgeYears,
    HifzAgeBand AgeBand,
    int Target,
    int SuccessfulSessions,
    int TotalMemorizationSessions,
    double Score,
    string? Warning);

/// <summary>
/// منطق التقييم الشهري للطالب (من 10) بحسب عدد جلسات الحفظ الجديد الناجحة في الشهر الميلادي:
/// الدرجة = min(10, round(العدد / الهدف × 10, 1)) والهدف حسب العمر.
/// </summary>
public static class MonthlyHifzRating
{
    /// <summary>العمر بالسنوات الكاملة في تاريخ مرجعي.</summary>
    public static int? AgeOn(DateTime? dateOfBirth, DateTime reference)
    {
        if (dateOfBirth == null) return null;
        var dob = dateOfBirth.Value.Date;
        var age = reference.Year - dob.Year;
        if (reference.Date < dob.AddYears(age)) age--;
        return age < 0 ? null : age;
    }

    /// <summary>أقل من 5 سنوات أو عمر غير معروف ← شريحة 5–7.</summary>
    public static HifzAgeBand BandFor(int? age) => age switch
    {
        >= 11 => HifzAgeBand.Age11Plus,
        >= 8 => HifzAgeBand.Age8To10,
        _ => HifzAgeBand.Age5To7
    };

    public static int TargetFor(HifzAgeBand band, MonthlyHifzRatingConfig cfg) => band switch
    {
        HifzAgeBand.Age11Plus => cfg.TargetAge11Plus,
        HifzAgeBand.Age8To10 => cfg.TargetAge8To10,
        _ => cfg.TargetAge5To7
    };

    public static double Score(int count, int target)
    {
        if (target <= 0) return 0;
        return Math.Min(10, Math.Round(count / (double)target * 10, 1, MidpointRounding.AwayFromZero));
    }

    /// <summary>توحيد النص العربي للمقارنة: إزالة التشكيل والتنوين والتطويل والمسافات الزائدة.</summary>
    public static string NormalizeEvaluation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            if (c is >= 'ً' and <= 'ٟ' or 'ٰ' or 'ـ') continue; // تشكيل، ألف خنجرية، تطويل
            sb.Append(c switch { 'أ' or 'إ' or 'آ' => 'ا', 'ى' => 'ي', _ => c });
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static bool IsPassing(string? evaluation, IReadOnlyList<string> passing)
    {
        var e = NormalizeEvaluation(evaluation);
        if (e.Length == 0) return false;
        foreach (var p in passing)
            if (NormalizeEvaluation(p) == e) return true;
        return false;
    }

    public static (DateTime Start, DateTime End) MonthRange(int year, int month)
    {
        var start = new DateTime(year, month, 1);
        return (start, start.AddMonths(1));
    }

    /// <summary>
    /// يحسب التقييم الشهري. العمر يُحتسب في آخر يوم من الشهر المختار.
    /// </summary>
    public static MonthlyHifzRatingResult Calculate(
        IEnumerable<HifzRecord> records,
        DateTime? dateOfBirth,
        int year,
        int month,
        MonthlyHifzRatingConfig cfg)
    {
        var (start, end) = MonthRange(year, month);
        var memorization = records
            .Where(r => r.Type == RecordType.Memorization && r.Date >= start && r.Date < end)
            .ToList();
        var successful = memorization.Count(r => GradeScale.IsPassing(r.Evaluation));

        var age = AgeOn(dateOfBirth, end.AddDays(-1));
        var band = BandFor(age);
        var target = TargetFor(band, cfg);

        string? warning = age switch
        {
            null => "تاريخ ميلاد الطالب غير مسجل — طُبّق هدف شريحة 5–7 سنوات",
            < 5 => "عمر الطالب أقل من 5 سنوات — طُبّق هدف شريحة 5–7 سنوات",
            _ => null
        };

        return new MonthlyHifzRatingResult(year, month, age, band, target, successful, memorization.Count,
            Score(successful, target), warning);
    }
}
