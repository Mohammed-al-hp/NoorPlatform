using FluentAssertions;
using NoorPlatform.Core.Entities;
using Xunit;

namespace NoorPlatform.Tests.Unit;

/// <summary>
/// اختبارات وحدة لدالة HifzRecord.ParseVerseCount
/// تغطي جميع أنماط الإدخال: نطاق "1-10"، رقم مفرد "20"، نص "كاملة"، قيمة فارغة/null
/// </summary>
public class HifzRecordParseVerseCountTests
{
    [Theory]
    [InlineData("1-10", 10)]
    [InlineData("1-1", 1)]
    [InlineData("5-20", 16)]
    [InlineData("100-286", 187)]
    public void ParseVerseCount_Range_ReturnsCorrectCount(string input, int expected)
    {
        HifzRecord.ParseVerseCount(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(" 1 - 10 ", 10)]   // مسافات زائدة
    [InlineData("  3-7  ", 5)]
    public void ParseVerseCount_Range_TrimsWhitespace(string input, int expected)
    {
        HifzRecord.ParseVerseCount(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("20", 20)]
    [InlineData("1", 1)]
    [InlineData("286", 286)]
    public void ParseVerseCount_SingleNumber_ReturnsThatNumber(string input, int expected)
    {
        HifzRecord.ParseVerseCount(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("كاملة")]
    [InlineData("مراجعة تسلسلية")]
    [InlineData("abc")]
    public void ParseVerseCount_TextLabel_ReturnsZero(string input)
    {
        HifzRecord.ParseVerseCount(input).Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseVerseCount_NullOrEmpty_ReturnsZero(string? input)
    {
        HifzRecord.ParseVerseCount(input!).Should().Be(0);
    }

    [Fact]
    public void ParseVerseCount_ReversedRange_ReturnsZero()
    {
        // "10-1" حيث from > to — لا يمكن حسابه
        HifzRecord.ParseVerseCount("10-1").Should().Be(0);
    }

    [Fact]
    public void ParseVerseCount_ZeroSingle_ReturnsZero()
    {
        HifzRecord.ParseVerseCount("0").Should().Be(0);
    }

    [Fact]
    public void ParseVerseCount_NegativeNumber_ReturnsZero()
    {
        HifzRecord.ParseVerseCount("-5").Should().Be(0);
    }
}
