using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

/// <summary>حساب درجات الاختبار الشفوي والتقدم الشهري حسب ملاحظات المشرف.</summary>
public static class PedagogicalGrading
{
    public static double ScoreQuestion(
     int hesitation,
     int alerts,
     int openings,
     double hesitationPenalty = 0.5,
     double alertPenalty = 1,
     double openingPenalty = 2)
    {
        var score = 100.0
            - Math.Max(0, hesitation) * hesitationPenalty
            - Math.Max(0, alerts) * alertPenalty
            - Math.Max(0, openings) * openingPenalty;
        return Math.Clamp(score, 0, 100);
    }

    public static string ImpressionFromPercent(double percent) => percent switch
    {
        >= 90 => "ممتاز",
        >= 80 => "جيد جداً",
        >= 70 => "جيد",
        >= 50 => "مقبول",
        _ => "ضعيف"
    };

    public static (double OverallPercent, string Grade, bool IsMemorized, int TotalOpenings, string FailReason) AggregateSession(
        IEnumerable<(int Hesitation, int Alerts, int Openings, double? ManualScore)> questions,
        int maxOpeningsPerQuestion = 1,
        double hesitationPenalty = 0.5,
        double alertPenalty = 1,
        double openingPenalty = 2,
        double passThreshold = 50)
    {
        var list = questions.ToList();
        if (list.Count == 0)
            return (0, "ضعيف", false, 0, "لا توجد أسئلة");

        var scored = list.Select(q =>
        {
            var openings = Math.Max(0, q.Openings);
            var cancelled = openings > maxOpeningsPerQuestion;
            // السؤال الملغي درجته صفر
            var pct = cancelled
                ? 0.0
                : q.ManualScore ?? ScoreQuestion(q.Hesitation, q.Alerts, q.Openings,
                    hesitationPenalty, alertPenalty, openingPenalty);
            return (pct, openings, cancelled);
        }).ToList();

        var totalOpenings = scored.Sum(s => s.openings);
        var cancelledCount = scored.Count(s => s.cancelled);
        var counted = scored.Where(s => !s.cancelled).ToList();
        if (counted.Count == 0)
            return (0, "ضعيف", false, totalOpenings, "أُلغيت جميع الأسئلة بسبب تجاوز الفتحات المسموحة");
        var overall = Math.Round(counted.Average(s => s.pct), 1);

        string failReason = "";
        if (overall < passThreshold)
            failReason = "الدرجة أقل من حد النجاح";

        var isMemorized = failReason.Length == 0;
        return (overall, ImpressionFromPercent(overall), isMemorized, totalOpenings, failReason);
    }

    /// <summary>
    /// تحويل إنجاز الأثمان مقابل الهدف إلى درجة من 10.
    /// مثال المشرف: 7–8 من 8 → 10، 6/8 → 10، 5/8 → 8 … قابل للتعديل.
    /// </summary>
    public static double ProgressScoreOutOf10(
int achieved, int target,
double threshold10 = 0.75, double threshold9 = 0.625, double threshold8 = 0.5,
double threshold6 = 0.375, double threshold4 = 0.25)
    {
        if (target <= 0) return achieved > 0 ? 10 : 0;
        if (threshold10 <= 0 || threshold9 <= 0 || threshold8 <= 0 || threshold6 <= 0 || threshold4 <= 0)
        {
            threshold10 = 0.75; threshold9 = 0.625; threshold8 = 0.5; threshold6 = 0.375; threshold4 = 0.25;
        }
        var ratio = (double)achieved / target;
        if (ratio >= threshold10) return 10;
        if (ratio >= threshold9) return 9;
        if (ratio >= threshold8) return 8;
        if (ratio >= threshold6) return 6;
        if (ratio >= threshold4) return 4;
        if (ratio > 0) return 2;
        return 0;
    }

    public static string RatingLabel(HomePracticeRating r) => r switch
    {
        HomePracticeRating.Excellent => "ممتاز",
        HomePracticeRating.VeryGood => "جيد جداً",
        HomePracticeRating.Good => "جيد",
        HomePracticeRating.Acceptable => "مقبول",
        HomePracticeRating.Weak => "ضعيف",
        _ => "—"
    };

    /// <summary>تحويل تقييم نصي (ممتاز…ضعيف) إلى نسبة مئوية.</summary>
    public static double? MapEvaluationToPercent(string? evaluation)
    {
        if (string.IsNullOrWhiteSpace(evaluation))
            return null;

        var e = evaluation.Trim();
        if (e.Contains("يحتاج مراجعة", StringComparison.Ordinal) || e.Contains("غير حافظ", StringComparison.Ordinal)) return 0;
        if (e.Contains("ممتاز", StringComparison.Ordinal)) return 100;
        if (e.Contains("جيد جدا", StringComparison.Ordinal) || e.Contains("جيد جدًا", StringComparison.Ordinal))
            return 85;
        if (e.Equals("جيد", StringComparison.Ordinal) || e.StartsWith("جيد ", StringComparison.Ordinal))
            return 70;
        if (e.Contains("مقبول", StringComparison.Ordinal)) return 50;
        if (e.Contains("ضعيف", StringComparison.Ordinal)) return 30;

        return e.ToLowerInvariant() switch
        {
            "excellent" => 100,
            "verygood" or "very good" => 85,
            "good" => 70,
            "acceptable" => 50,
            "weak" => 30,
            _ => null
        };
    }

    /// <summary>
    /// درجة استرشادية للصلاة: مسجد 40% + وقت 30% + عدد صلوات المسجد/5 × 30%.
    /// </summary>
    public static double? PrayerAdvisoryFromLogs(
        IEnumerable<(bool PrayedInMosque, bool OnTime, int MosquePrayerCount)> logs)
    {
        var list = logs.ToList();
        if (list.Count == 0) return null;

        var dayScores = list.Select(p =>
        {
            var mosque = p.PrayedInMosque ? 40.0 : 0;
            var onTime = p.OnTime ? 30.0 : 0;
            var count = Math.Clamp(p.MosquePrayerCount, 0, 5) / 5.0 * 30.0;
            return mosque + onTime + count;
        });

        return Math.Round(dayScores.Average(), 1);
    }

    /// <summary>متوسط موزون للأبعاد الأساسية (+ الاسترشادي اختيارياً).</summary>
    public static double ComputeWeightedOverall(
        IEnumerable<(double Score, double Weight)> coreDimensions,
        double? prayerAdvisory,
        double? parentHomeAdvisory,
        bool includeAdvisory,
        double weightPrayer = 1,
        double weightParent = 1)
    {
        var parts = coreDimensions
            .Where(d => d.Weight > 0)
            .Select(d => (d.Score, d.Weight))
            .ToList();

        if (includeAdvisory)
        {
            if (prayerAdvisory.HasValue && weightPrayer > 0)
                parts.Add((prayerAdvisory.Value, weightPrayer));
            if (parentHomeAdvisory.HasValue && weightParent > 0)
                parts.Add((parentHomeAdvisory.Value, weightParent));
        }

        if (parts.Count == 0) return 0;

        var totalWeight = parts.Sum(p => p.Weight);
        if (totalWeight <= 0) return Math.Round(parts.Average(p => p.Score), 1);

        return Math.Round(parts.Sum(p => p.Score * p.Weight) / totalWeight, 1);
    }
}
