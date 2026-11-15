using FluentAssertions;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

public class MonthlyHifzRatingTests
{
    private static readonly MonthlyHifzRatingConfig Cfg = MonthlyHifzRatingConfig.Default;

    private static HifzRecord Mem(DateTime date, string evaluation = "ممتاز") =>
        new() { Date = date, Type = RecordType.Memorization, Evaluation = evaluation };

    /// <summary>تاريخ ميلاد يجعل العمر في آخر يوم من نوفمبر 2026 مساوياً لـ age.</summary>
    private static DateTime DobForAge(int age) => new(2026 - age, 6, 1);

    private static IEnumerable<HifzRecord> Sessions(int count) =>
        Enumerable.Range(1, count).Select(d => Mem(new DateTime(2026, 11, d)));

    // ─── الحدود: 3 / 4 / 5 جلسات لطالب عمره 6 (الهدف 4) ───
    [Theory]
    [InlineData(3, 7.5)]
    [InlineData(4, 10.0)]
    [InlineData(5, 10.0)] // لا تتجاوز 10
    public void Score_ForAge5To7_AtBoundaries(int sessions, double expected)
    {
        var r = MonthlyHifzRating.Calculate(Sessions(sessions), DobForAge(6), 2026, 11, Cfg);
        r.Target.Should().Be(4);
        r.SuccessfulSessions.Should().Be(sessions);
        r.Score.Should().Be(expected);
    }

    [Theory]
    [InlineData(7, HifzAgeBand.Age5To7, 4)]
    [InlineData(8, HifzAgeBand.Age8To10, 6)]
    [InlineData(10, HifzAgeBand.Age8To10, 6)]
    [InlineData(11, HifzAgeBand.Age11Plus, 8)]
    public void AgeBands_AtBoundaries(int age, HifzAgeBand band, int target)
    {
        var r = MonthlyHifzRating.Calculate(Array.Empty<HifzRecord>(), DobForAge(age), 2026, 11, Cfg);
        r.AgeYears.Should().Be(age);
        r.AgeBand.Should().Be(band);
        r.Target.Should().Be(target);
        r.Warning.Should().BeNull();
    }

    [Fact]
    public void Age_IsMeasuredAtEndOfSelectedMonth()
    {
        // يبلغ 8 سنوات يوم 30 نوفمبر ← يُعامل كـ 8 في تقييم نوفمبر، وكـ 7 في أكتوبر
        var dob = new DateTime(2018, 11, 30);
        MonthlyHifzRating.Calculate(Array.Empty<HifzRecord>(), dob, 2026, 11, Cfg).Target.Should().Be(6);
        MonthlyHifzRating.Calculate(Array.Empty<HifzRecord>(), dob, 2026, 10, Cfg).Target.Should().Be(4);
    }

    [Fact]
    public void UnknownAge_UsesAge5To7Band_WithWarning()
    {
        var r = MonthlyHifzRating.Calculate(Sessions(2), null, 2026, 11, Cfg);
        r.Target.Should().Be(4);
        r.Score.Should().Be(5.0);
        r.Warning.Should().NotBeNull();
    }

    [Fact]
    public void UnderFive_UsesAge5To7Band_WithWarning()
    {
        var r = MonthlyHifzRating.Calculate(Array.Empty<HifzRecord>(), DobForAge(4), 2026, 11, Cfg);
        r.AgeBand.Should().Be(HifzAgeBand.Age5To7);
        r.Warning.Should().Contain("أقل من 5");
    }

    [Fact]
    public void MonthBoundaries_IncludeFirstAndLastDay_ExcludeNeighbouringMonths()
    {
        var records = new[]
        {
            Mem(new DateTime(2026, 10, 31, 23, 59, 59)), // الشهر السابق
            Mem(new DateTime(2026, 11, 1, 0, 0, 0)),     // بداية الشهر
            Mem(new DateTime(2026, 11, 30, 23, 59, 59)), // نهاية الشهر
            Mem(new DateTime(2026, 12, 1, 0, 0, 0))      // الشهر التالي
        };
        var r = MonthlyHifzRating.Calculate(records, DobForAge(9), 2026, 11, Cfg);
        r.SuccessfulSessions.Should().Be(2);
        r.Score.Should().Be(3.3); // 2/6×10 = 3.33 → 3.3
    }

    [Fact]
    public void OnlySuccessfulMemorizationSessions_AreCounted()
    {
        var d = new DateTime(2026, 11, 10);
        var records = new[]
        {
            Mem(d, "ممتاز"),
            Mem(d, "جيد جدًا"),   // تنوين على الألف — نفس "جيد جداً"
            Mem(d, "جيد"),
            Mem(d, "ضعيف"),        // يُحتسب ناجحاً حسب القرار المعتمد
            Mem(d, "يحتاج مراجعة"),
            Mem(d, ""),
            new HifzRecord { Date = d, Type = RecordType.Revision, Evaluation = "ممتاز" }
        };
        // الافتراضي المعتمد: ممتاز، جيد جداً، جيد، ضعيف — "يحتاج مراجعة" والفارغ لا يُحتسبان
        var r = MonthlyHifzRating.Calculate(records, DobForAge(12), 2026, 11, Cfg);
        r.SuccessfulSessions.Should().Be(4);
        r.TotalMemorizationSessions.Should().Be(6);
        r.Score.Should().Be(5.0); // 4/8×10
    }

    [Fact]
    public void Config_FromSettings_UsesCustomTargetsAndPassingList()
    {
        var cfg = MonthlyHifzRatingConfig.From(new PlatformSettings
        {
            HifzRatingTargetAge5To7 = 2,
            HifzRatingTargetAge8To10 = 3,
            HifzRatingTargetAge11Plus = 5,
            HifzRatingPassingEvaluations = "ممتاز"
        });
        var records = new[] { Mem(new DateTime(2026, 11, 2)), Mem(new DateTime(2026, 11, 3), "جيد") };
        var r = MonthlyHifzRating.Calculate(records, DobForAge(6), 2026, 11, cfg);
        r.Target.Should().Be(2);
        r.SuccessfulSessions.Should().Be(1);
        r.Score.Should().Be(5.0);
    }

    [Fact]
    public void Config_WithZeroOrEmptyValues_FallsBackToDefaults()
    {
        var cfg = MonthlyHifzRatingConfig.From(new PlatformSettings
        {
            HifzRatingTargetAge5To7 = 0,
            HifzRatingTargetAge8To10 = 0,
            HifzRatingTargetAge11Plus = 0,
            HifzRatingPassingEvaluations = ""
        });
        cfg.TargetAge5To7.Should().Be(4);
        cfg.TargetAge8To10.Should().Be(6);
        cfg.TargetAge11Plus.Should().Be(8);
        cfg.PassingEvaluations.Should().Contain("ممتاز");
    }
}
