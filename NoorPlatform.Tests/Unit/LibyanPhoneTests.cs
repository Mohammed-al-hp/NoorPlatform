using FluentAssertions;
using NoorPlatform.Api.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

/// <summary>
/// اختبارات وحدة لخدمة LibyanPhone
/// تغطي: IsValid, Normalize, ToDisplay, ForWhatsApp, GetLoginLookupKeys
/// </summary>
public class LibyanPhoneTests
{
    // ─── IsValid ───

    [Theory]
    [InlineData("0912345678", true)]       // محلي صحيح 10 أرقام
    [InlineData("0923456789", true)]
    [InlineData("218912345678", true)]      // دولي صحيح 12 رقم
    [InlineData("091234567", false)]        // 9 أرقام — قصير
    [InlineData("09123456789", false)]      // 11 رقم — طويل
    [InlineData("0812345678", false)]       // لا يبدأ بـ 09
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("abcdefghij", false)]       // نص وليس أرقام
    public void IsValid_VariousInputs_ReturnsExpected(string? phone, bool expected)
    {
        LibyanPhone.IsValid(phone).Should().Be(expected);
    }

    // ─── Normalize ───

    [Theory]
    [InlineData("0912345678", "218912345678")]
    [InlineData("218912345678", "218912345678")]
    [InlineData("+218912345678", "218912345678")]
    [InlineData("00218912345678", "218912345678")]
    public void Normalize_VariousFormats_ReturnsInternational(string input, string expected)
    {
        LibyanPhone.Normalize(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_NullOrEmpty_ReturnsEmpty(string? input, string expected)
    {
        LibyanPhone.Normalize(input).Should().Be(expected);
    }

    // ─── ToDisplay ───

    [Theory]
    [InlineData("218912345678", "0912345678")]
    [InlineData("0912345678", "0912345678")]
    public void ToDisplay_ReturnsLocalFormat(string input, string expected)
    {
        LibyanPhone.ToDisplay(input).Should().Be(expected);
    }

    // ─── ForWhatsApp ───

    [Fact]
    public void ForWhatsApp_ReturnsNormalized()
    {
        LibyanPhone.ForWhatsApp("0912345678").Should().Be("218912345678");
    }

    [Fact]
    public void ForWhatsApp_Null_ReturnsEmpty()
    {
        LibyanPhone.ForWhatsApp(null).Should().Be(string.Empty);
    }

    // ─── GetLoginLookupKeys ───

    [Fact]
    public void GetLoginLookupKeys_LocalNumber_ReturnsAllVariants()
    {
        var keys = LibyanPhone.GetLoginLookupKeys("0912345678");
        keys.Should().Contain("0912345678");    // الرقم المحلي الأصلي
        keys.Should().Contain("218912345678");  // الرقم الدولي المُطبّع
    }

    [Fact]
    public void GetLoginLookupKeys_InternationalNumber_ReturnsAllVariants()
    {
        var keys = LibyanPhone.GetLoginLookupKeys("218912345678");
        keys.Should().Contain("218912345678");
        keys.Should().Contain("0912345678");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetLoginLookupKeys_Empty_ReturnsEmptyList(string? input)
    {
        LibyanPhone.GetLoginLookupKeys(input).Should().BeEmpty();
    }
}
