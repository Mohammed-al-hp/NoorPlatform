using FluentAssertions;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using Xunit;

namespace NoorPlatform.Tests.Unit;

/// <summary>«المحفوظ» بالترتيب العكسي: أبعد موضع في الحفظ الجديد يعني إتمام كل ما بعده في المصحف.</summary>
public class HifzProgressCalculatorTests
{
    private static HifzRecord Mem(string surah, string verses, string evaluation = "ممتاز", string? to = null) =>
        new() { Type = RecordType.Memorization, SurahName = surah, ToSurahName = to, Verses = verses, Evaluation = evaluation };

    [Fact]
    public void FirstSessionMidQuran_CountsAllSurahsAfterIt()
    {
        // أول جلسة مسجلة: النازعات 12–18 ← محفوظه: من الناس حتى عبس (80–114) + 18 آية من النازعات
        var pos = HifzProgressCalculator.CurrentPosition(new[] { Mem("النازعات", "12-18") })!;
        pos.SurahName.Should().Be("النازعات");
        pos.LastVerse.Should().Be(18);
        pos.SurahCompleted.Should().BeFalse();
        pos.CurrentSurah!.Name.Should().Be("النازعات");

        // آيات السور 80–114 (عبس إلى الناس) بالعدّ المدني
        HifzProgressCalculator.MemorizedVerses(new[] { Mem("النازعات", "12-18") }).Should().Be(NoorPlatform.Core.Services.QuranIndex.Surahs.Where(s => s.Number >= 80).Sum(s => s.VerseCount) + 18);
    }

    [Fact]
    public void FurthestPoint_IsLowestSurahNumber_ThenHighestVerse()
    {
        var records = new[] { Mem("الناس", "1-6"), Mem("عبس", "1-42"), Mem("النازعات", "1-10"), Mem("النازعات", "11-20") };
        var pos = HifzProgressCalculator.CurrentPosition(records)!;
        pos.SurahNumber.Should().Be(79);
        pos.LastVerse.Should().Be(20);
    }

    [Fact]
    public void CompletedSurah_PointsToTheNextOneTowardAlFatiha()
    {
        var pos = HifzProgressCalculator.CurrentPosition(new[] { Mem("النبأ", "1-40") })!;
        pos.SurahCompleted.Should().BeTrue();
        pos.CurrentSurah!.Name.Should().Be("المرسلات");
        pos.Describe().Should().Contain("المرسلات");
    }

    [Fact]
    public void FailedSessionsAndRevision_DoNotMoveThePosition()
    {
        var records = new[]
        {
            Mem("النبأ", "1-20"),
            Mem("النازعات", "1-10", "ضعيف ويحتاج مراجعة"),
            new HifzRecord { Type = RecordType.Revision, SurahName = "البقرة", Verses = "1-5", Evaluation = "ممتاز" }
        };
        HifzProgressCalculator.CurrentPosition(records)!.SurahName.Should().Be("النبأ");
    }

    [Fact]
    public void NoMemorization_IsZero()
    {
        HifzProgressCalculator.Calculate(Array.Empty<HifzRecord>()).Should().Be(0);
        HifzProgressCalculator.CurrentPosition(Array.Empty<HifzRecord>()).Should().BeNull();
    }

    [Fact]
    public void FullQuran_Is100()
    {
        HifzProgressCalculator.Calculate(new[] { Mem("الفاتحة", "1-7") }).Should().Be(100);
        HifzProgressCalculator.CurrentPosition(new[] { Mem("الفاتحة", "1-7") })!.Describe().Should().Be("أتمّ حفظ القرآن الكريم");
    }
}
