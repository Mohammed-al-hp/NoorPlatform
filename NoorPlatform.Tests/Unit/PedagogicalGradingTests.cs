using FluentAssertions;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

/// <summary>اختبارات حساب درجات الاختبار الشفوي والتقدم الشهري وتحويل التقييم.</summary>
public class PedagogicalGradingTests
{
    // ───── ScoreQuestion ─────
    [Theory]
    [InlineData(0, 0, 0, 100)]
    [InlineData(2, 1, 1, 96)]   // 100 - 2*0.5 - 1*1 - 1*2
    [InlineData(1000, 0, 0, 0)] // لا تنزل عن الصفر
    public void ScoreQuestion_AppliesPenalties(int hesitation, int alerts, int openings, double expected)
    {
        PedagogicalGrading.ScoreQuestion(hesitation, alerts, openings).Should().Be(expected);
    }

    [Fact]
    public void ScoreQuestion_IgnoresNegativeInputs()
    {
        PedagogicalGrading.ScoreQuestion(-5, -5, -5).Should().Be(100);
    }

    // ───── AggregateSession ─────
    [Fact]
    public void AggregateSession_Empty_IsNotMemorized()
    {
        var r = PedagogicalGrading.AggregateSession(Array.Empty<(int, int, int, double?)>());
        r.IsMemorized.Should().BeFalse();
        r.OverallPercent.Should().Be(0);
    }

    [Fact]
    public void AggregateSession_CleanQuestions_Pass()
    {
        var qs = new (int, int, int, double?)[] { (0, 0, 0, null), (0, 0, 0, null) };
        var r = PedagogicalGrading.AggregateSession(qs);
        r.OverallPercent.Should().Be(100);
        r.Grade.Should().Be("ممتاز");
        r.IsMemorized.Should().BeTrue();
        r.FailReason.Should().BeEmpty();
    }

    [Fact]
    public void AggregateSession_TooManyOpenings_CancelsQuestionAndExcludesIt()
    {
        var qs = new (int, int, int, double?)[] { (0, 0, 2, null), (0, 0, 0, null) };
        var r = PedagogicalGrading.AggregateSession(qs, maxOpeningsPerQuestion: 1);
        r.TotalOpenings.Should().Be(2);
        r.OverallPercent.Should().Be(100); // السؤال الملغى لا يدخل في المتوسط
        r.IsMemorized.Should().BeTrue();
    }

    [Fact]
    public void AggregateSession_AllCancelled_Fails()
    {
        var qs = new (int, int, int, double?)[] { (0, 0, 2, null), (0, 0, 3, null) };
        PedagogicalGrading.AggregateSession(qs, maxOpeningsPerQuestion: 1).IsMemorized.Should().BeFalse();
    }

    [Fact]
    public void AggregateSession_BelowPassThreshold_Fails()
    {
        var qs = new (int, int, int, double?)[] { (100, 0, 0, null) }; // 100 - 50 = 50
        PedagogicalGrading.AggregateSession(qs, passThreshold: 50).IsMemorized.Should().BeTrue();
        var r = PedagogicalGrading.AggregateSession(qs, passThreshold: 60);
        r.IsMemorized.Should().BeFalse();
        r.FailReason.Should().Contain("أقل من حد النجاح");
    }

    [Fact]
    public void AggregateSession_ManualScore_OverridesCalculation()
    {
        var qs = new (int, int, int, double?)[] { (50, 50, 0, 90) };
        PedagogicalGrading.AggregateSession(qs).OverallPercent.Should().Be(90);
    }

    // ───── ProgressScoreOutOf10 ─────
    [Theory]
    [InlineData(8, 8, 10)]
    [InlineData(6, 8, 10)]
    [InlineData(5, 8, 9)]
    [InlineData(4, 8, 8)]
    [InlineData(3, 8, 6)]
    [InlineData(2, 8, 4)]
    [InlineData(1, 8, 2)]
    [InlineData(0, 8, 0)]
    public void ProgressScore_UsesDefaultThresholds(int achieved, int target, double expected)
    {
        PedagogicalGrading.ProgressScoreOutOf10(achieved, target).Should().Be(expected);
    }

    [Fact]
    public void ProgressScore_ZeroThresholds_FallBackToDefaults()
    {
        // قاعدة بيانات قديمة فيها عتبات = 0 يجب ألا تعطي 10 للجميع
        PedagogicalGrading.ProgressScoreOutOf10(1, 8, 0, 0, 0, 0, 0).Should().Be(2);
        PedagogicalGrading.ProgressScoreOutOf10(6, 8, 0, 0, 0, 0, 0).Should().Be(10);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 0, 10)]
    public void ProgressScore_ZeroTarget(int achieved, int target, double expected)
    {
        PedagogicalGrading.ProgressScoreOutOf10(achieved, target).Should().Be(expected);
    }

    // ───── MapEvaluationToPercent ─────
    [Theory]
    [InlineData("ممتاز", 100)]
    [InlineData("جيد جداً", 85)]
    [InlineData("جيد", 70)]
    [InlineData("مقبول", 50)]
    [InlineData("ضعيف", 30)]
    [InlineData("Excellent", 100)]
    public void MapEvaluation_KnownValues(string input, double expected)
    {
        PedagogicalGrading.MapEvaluationToPercent(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("غير معروف")]
    public void MapEvaluation_UnknownOrEmpty_ReturnsNull(string? input)
    {
        PedagogicalGrading.MapEvaluationToPercent(input).Should().BeNull();
    }
}

/// <summary>اختبارات تنسيق أرقام الهواتف الليبية (بما فيها الأرقام العربية الهندية).</summary>
public class LibyanPhoneNormalizationTests
{
    [Theory]
    [InlineData("0912345678", "218912345678")]
    [InlineData("+218 91 234 5678", "218912345678")]
    [InlineData("00218912345678", "218912345678")]
    [InlineData("٠٩١٢٣٤٥٦٧٨", "218912345678")] // أرقام عربية هندية
    public void Normalize_ReturnsInternationalForm(string input, string expected)
    {
        LibyanPhone.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("218912345678", true)]
    [InlineData("٠٩١٢٣٤٥٦٧٨", true)]
    [InlineData("0812345678", false)]
    [InlineData("091234", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_AcceptsOnlyLibyanMobiles(string? input, bool expected)
    {
        LibyanPhone.IsValid(input).Should().Be(expected);
    }

    [Fact]
    public void ToDisplay_ConvertsBackToLocalForm()
    {
        LibyanPhone.ToDisplay("218912345678").Should().Be("0912345678");
    }
}
