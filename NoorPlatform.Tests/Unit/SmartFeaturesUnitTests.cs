using FluentAssertions;
using NoorPlatform.Api.Controllers;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

public class MemoryStrengthTests
{
    private static readonly DateTime D0 = new(2026, 1, 1);

    [Theory]
    [InlineData("ممتاز", 5)]
    [InlineData("جيد جداً", 4)]
    [InlineData("جيد جدًا", 4)]
    [InlineData("جيد", 3)]
    [InlineData("مقبول", 2)]
    [InlineData("يحتاج مراجعة", 2)]
    [InlineData("ضعيف", 1)]
    [InlineData("", 3)]
    public void Quality_MapsArabicEvaluations(string evaluation, int expected) =>
        MemoryStrength.Quality(evaluation).Should().Be(expected);

    [Fact]
    public void Evaluate_ExcellentSessions_GrowInterval()
    {
        var sessions = new[]
        {
            new ReviewSession(D0, false, 5),
            new ReviewSession(D0.AddDays(1), true, 5),
            new ReviewSession(D0.AddDays(4), true, 5)
        };
        var state = MemoryStrength.Evaluate(sessions)!;
        state.Stage.Should().Be(3);
        state.IntervalDays.Should().Be(14);
        state.DueDate.Should().Be(D0.AddDays(18));
    }

    [Fact]
    public void Evaluate_WeakSession_ResetsToTomorrow()
    {
        var state = MemoryStrength.Evaluate(new[]
        {
            new ReviewSession(D0, false, 5),
            new ReviewSession(D0.AddDays(1), true, 5),
            new ReviewSession(D0.AddDays(4), true, 1)
        })!;
        state.Stage.Should().Be(0);
        state.IntervalDays.Should().Be(1);
        MemoryStrength.OverdueDays(state, D0.AddDays(5)).Should().Be(0);
    }

    [Fact]
    public void Retention_FadesOverTime_AndIsFullRightAfterExcellentReview()
    {
        var state = MemoryStrength.Evaluate(new[] { new ReviewSession(D0, false, 5) })!;
        MemoryStrength.Retention(state, D0).Should().Be(100);
        var atDue = MemoryStrength.Retention(state, state.DueDate);
        atDue.Should().BeApproximately(80, 0.5);
        MemoryStrength.Retention(state, D0.AddDays(30)).Should().BeLessThan(atDue);
        MemoryStrength.Level(100).Should().Be("strong");
        MemoryStrength.Level(10).Should().Be("critical");
    }

    [Fact]
    public void Evaluate_Empty_ReturnsNull() =>
        MemoryStrength.Evaluate(Array.Empty<ReviewSession>()).Should().BeNull();
}

public class QuranIndexTests
{
    [Fact]
    public void Index_Has114Surahs_And6214Verses_MadaniCount()
    {
        QuranIndex.Surahs.Should().HaveCount(114);
        QuranIndex.Surahs.Sum(s => s.VerseCount).Should().Be(6214); // العدّ المدني الأول (قالون)
    }

    [Theory]
    [InlineData("الملك", 67)]
    [InlineData("سورة البقرة", 2)]
    [InlineData("ال عمران", 3)]
    public void Find_NormalizesNames(string name, int number) =>
        QuranIndex.Find(name)!.Number.Should().Be(number);

    [Fact]
    public void Range_ExpandsMultiSurahRecords()
    {
        QuranIndex.Range("الناس", "الإخلاص").Select(s => s.Number).Should().Equal(112, 113, 114);
        QuranIndex.Range("غير موجودة", null).Should().BeEmpty();
    }

    [Fact]
    public void CoversJuz_SpansLongSurahs_AndEveryJuzHasASurah()
    {
        QuranIndex.CoversJuz(QuranIndex.Surahs[1], 2).Should().BeTrue("البقرة تغطي الجزء الثاني");
        QuranIndex.CoversJuz(QuranIndex.Surahs[76], 30).Should().BeFalse("المرسلات تنتهي في الجزء 29");
        Enumerable.Range(1, 30).Should().OnlyContain(j => QuranIndex.Surahs.Any(s => QuranIndex.CoversJuz(s, j)));
    }
}

public class AtRiskScoreTests
{
    private static readonly DateTime D0 = new(2026, 3, 1);

    [Fact]
    public void RegularStudent_HasLowRisk()
    {
        var att = Enumerable.Range(0, 10).Select(i => (D0.AddDays(i), AttendanceStatus.Present)).ToList();
        var (score, reasons) = AnalyticsController.RiskScore(att, 2, 30, 8);
        score.Should().BeLessThan(35);
        reasons.Should().BeEmpty();
    }

    [Fact]
    public void FrequentAbsence_AndStalledHifz_IsHighRisk()
    {
        var att = Enumerable.Range(0, 10)
            .Select(i => (D0.AddDays(i), i >= 4 ? AttendanceStatus.UnexcusedAbsence : AttendanceStatus.Present)).ToList();
        var (score, reasons) = AnalyticsController.RiskScore(att, 25, 30, 0);
        score.Should().BeGreaterThanOrEqualTo(60);
        reasons.Should().Contain(r => r.Contains("متتالية"));
        reasons.Should().Contain(r => r.Contains("توقف التسميع"));
    }

    [Fact]
    public void NoHifzEver_IsFlagged()
    {
        var (_, reasons) = AnalyticsController.RiskScore(new List<(DateTime, AttendanceStatus)>(), null, 30, 0);
        reasons.Should().Contain("لا توجد أي جلسة تسميع مسجّلة");
    }
}

public class MonthlyParentReportFormatTests
{
    [Fact]
    public void Format_IncludesAllSections()
    {
        var data = new MonthlyReportData(1, "أحمد", "حلقة الفجر", "الشيخ علي", "0912345678", 2026, 10,
            new() { "الملك", "القلم" }, new() { "النبأ" }, 6, 4, 82, 20, 18, 90, 25, 76, "أداء متميز، نوصي بالمراجعة اليومية", 340);
        var text = MonthlyParentReportService.Format(data, "مركز النور");

        text.Should().Contain("مركز النور").And.Contain("أكتوبر 2026").And.Contain("أحمد");
        text.Should().Contain("الملك، القلم").And.Contain("النبأ");
        text.Should().Contain("90٪").And.Contain("76٪");
        text.Should().Contain("أداء متميز").And.Contain("الشيخ علي");
    }

    [Fact]
    public void Format_HandlesEmptyMonth()
    {
        var data = new MonthlyReportData(1, "سارة", null, null, null, 2026, 1,
            new(), new(), 0, 0, 0, 0, 0, null, 0, null, null, 0);
        var text = MonthlyParentReportService.Format(data);
        text.Should().Contain("لم يُرصد حضور").And.Contain("لم تُسجَّل صلوات").And.Contain("يناير");
        text.Should().NotContain("ملاحظة المحفّظ");
    }

    [Fact]
    public void PreviousMonth_WrapsYear() =>
        MonthlyParentReportService.PreviousMonth(new DateTime(2026, 1, 1)).Should().Be((2025, 12));
}
