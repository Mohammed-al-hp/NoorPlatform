using FluentAssertions;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using Xunit;

namespace NoorPlatform.Tests.Unit;

public class PrayerTimesTests
{
    [Theory]
    [InlineData(2026, 1, 15)]
    [InlineData(2026, 6, 21)]
    [InlineData(2026, 11, 16)]
    public void Misrata_Times_AreOrdered_AndPlausible(int y, int m, int d)
    {
        var day = new DateTime(y, m, d);
        var t = PrayerTimes.ForDate(day);

        t[PrayerName.Fajr].Should().BeBefore(t[PrayerName.Dhuhr]);
        t[PrayerName.Dhuhr].Should().BeBefore(t[PrayerName.Asr]);
        t[PrayerName.Asr].Should().BeBefore(t[PrayerName.Maghrib]);
        t[PrayerName.Maghrib].Should().BeBefore(t[PrayerName.Isha]);

        t[PrayerName.Fajr].Hour.Should().BeInRange(3, 6);
        // مصراتة (خط طول 15.09°) بتوقيت UTC+2: الزوال قرابة الواحدة ظهراً على مدار السنة
        t[PrayerName.Dhuhr].TimeOfDay.Should().BeGreaterThan(new TimeSpan(12, 40, 0))
            .And.BeLessThan(new TimeSpan(13, 20, 0));
        t[PrayerName.Maghrib].Hour.Should().BeInRange(17, 20);
        t[PrayerName.Isha].Hour.Should().BeInRange(18, 22);
        t.Values.Should().OnlyContain(v => v.Date == day.Date);
    }

    [Fact]
    public void SummerSolstice_MatchesSolarGeometry()
    {
        // مصراتة 32.4° شمالاً: الزوال ≈ 13:01 (خط الطول 15.09° + معادلة الزمن)،
        // وطول النهار في الانقلاب الصيفي ≈ 14 ساعة و17 دقيقة ⇒ الغروب ≈ 20:10
        var t = PrayerTimes.ForDate(new DateTime(2026, 6, 21));
        t[PrayerName.Dhuhr].TimeOfDay.Should().BeCloseTo(new TimeSpan(13, 2, 0), TimeSpan.FromMinutes(3));
        t[PrayerName.Maghrib].TimeOfDay.Should().BeCloseTo(new TimeSpan(20, 10, 0), TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void HasStarted_AtNoon_AllowsFajrAndDhuhrOnly()
    {
        var day = new DateTime(2026, 11, 16);
        var dhuhr = PrayerTimes.ForDate(day)[PrayerName.Dhuhr];
        var now = dhuhr.AddMinutes(10);

        PrayerTimes.HasStarted(PrayerName.Fajr, day, now).Should().BeTrue();
        PrayerTimes.HasStarted(PrayerName.Dhuhr, day, now).Should().BeTrue();
        PrayerTimes.HasStarted(PrayerName.Asr, day, now).Should().BeFalse();
        PrayerTimes.HasStarted(PrayerName.Maghrib, day, now).Should().BeFalse();
        PrayerTimes.HasStarted(PrayerName.Isha, day, now).Should().BeFalse();
    }

    [Fact]
    public void HasStarted_PastDayAlwaysTrue_FutureDayAlwaysFalse()
    {
        var now = new DateTime(2026, 11, 16, 5, 0, 0);
        PrayerTimes.HasStarted(PrayerName.Isha, now.Date.AddDays(-1), now).Should().BeTrue();
        PrayerTimes.HasStarted(PrayerName.Fajr, now.Date.AddDays(1), now).Should().BeFalse();
    }

    [Fact]
    public void RecalculateTotals_NoJamaahCredit_WithoutMosque_AndNotPrayedHasNoFlags()
    {
        var log = new PrayerDailyLog();
        log.Entries.Add(new PrayerEntry { Prayer = PrayerName.Fajr, Prayed = true, InMosque = true, OnTime = true });
        log.Entries.Add(new PrayerEntry { Prayer = PrayerName.Dhuhr, Prayed = true, InMosque = false, OnTime = true });
        log.Entries.Add(new PrayerEntry { Prayer = PrayerName.Asr, Prayed = true, InMosque = false, OnTime = false });
        log.Entries.Add(new PrayerEntry { Prayer = PrayerName.Maghrib, Prayed = false, InMosque = true, OnTime = true });

        log.RecalculateTotals();

        log.PrayedCount.Should().Be(3);
        log.MosquePrayerCount.Should().Be(1, "صلاة الجماعة تُحتسب فقط لما أُشّر عليه «في المسجد» وصُلّي فعلاً");
        log.OnTimeCount.Should().Be(2);
        log.PrayedInMosque.Should().BeTrue();
        log.OnTime.Should().BeFalse();
    }

    [Fact]
    public void DetailedAdvisory_ScoresPerPrayer_AndKeepsLegacyFormula()
    {
        PedagogicalGrading.PrayerAdvisoryFromDetailedLogs(new[]
        {
            (true, true, 5, (int?)5, (int?)5)
        }).Should().Be(100);

        PedagogicalGrading.PrayerAdvisoryFromDetailedLogs(new[]
        {
            (false, false, 0, (int?)5, (int?)0)
        }).Should().Be(40, "صلّى الخمس خارج المسجد وقضاءً: رصيد الأداء فقط");

        // سجل قديم بالنموذج الإجمالي
        PedagogicalGrading.PrayerAdvisoryFromDetailedLogs(new[]
        {
            (true, true, 5, (int?)null, (int?)null)
        }).Should().Be(100);
    }
}
