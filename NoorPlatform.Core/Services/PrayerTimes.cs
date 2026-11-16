using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

/// <summary>
/// حساب مواقيت الأذان فلكياً لمدينة مصراتة (ليبيا) بتوقيت ليبيا (UTC+2، بلا توقيت صيفي).
/// الطريقة: الهيئة المصرية العامة للمساحة (الفجر 19.5°، العشاء 17.5°)، والعصر بالمذهب الجمهوري
/// (ظل المثل) — وهي المعتمدة غالباً في ليبيا. الدقة ±1–2 دقيقة وهي كافية لمنع تسجيل صلاة لم يدخل وقتها.
/// </summary>
public static class PrayerTimes
{
    public const double MisrataLatitude = 32.3754;
    public const double MisrataLongitude = 15.0925;
    public const double LibyaUtcOffset = 2;

    private const double FajrAngle = 19.5;
    private const double IshaAngle = 17.5;
    private const double SunsetAngle = 0.833;
    /// <summary>دقائق احتياط تضاف للمغرب (المعتاد في التقاويم المحلية).</summary>
    private const double MaghribSafetyMinutes = 1;

    public static readonly IReadOnlyDictionary<PrayerName, string> ArabicNames = new Dictionary<PrayerName, string>
    {
        [PrayerName.Fajr] = "الفجر",
        [PrayerName.Dhuhr] = "الظهر",
        [PrayerName.Asr] = "العصر",
        [PrayerName.Maghrib] = "المغرب",
        [PrayerName.Isha] = "العشاء"
    };

    /// <summary>مواقيت يوم معيّن (بتوقيت ليبيا المحلي) لمدينة مصراتة.</summary>
    public static IReadOnlyDictionary<PrayerName, DateTime> ForDate(DateTime libyaDate) =>
        ForDate(libyaDate, MisrataLatitude, MisrataLongitude, LibyaUtcOffset);

    public static IReadOnlyDictionary<PrayerName, DateTime> ForDate(DateTime date, double lat, double lng, double tz)
    {
        var day = date.Date;
        var jd = JulianDay(day.Year, day.Month, day.Day) - lng / (15.0 * 24.0);

        // موضع الشمس عند الظهيرة التقريبية
        var (decl, eqt) = SunPosition(jd + 0.5);
        var dhuhr = 12 + tz - lng / 15.0 - eqt;

        double HourAngle(double angle) =>
            RadToDeg(Math.Acos((-Math.Sin(DegToRad(angle)) - Math.Sin(DegToRad(decl)) * Math.Sin(DegToRad(lat)))
                                / (Math.Cos(DegToRad(decl)) * Math.Cos(DegToRad(lat))))) / 15.0;

        double AsrHourAngle(double shadowFactor)
        {
            var angle = -RadToDeg(Math.Atan(1.0 / (shadowFactor + Math.Tan(DegToRad(Math.Abs(lat - decl))))));
            return HourAngle(angle);
        }

        var fajr = dhuhr - HourAngle(FajrAngle);
        var asr = dhuhr + AsrHourAngle(1);
        var maghrib = dhuhr + HourAngle(SunsetAngle) + MaghribSafetyMinutes / 60.0;
        var isha = dhuhr + HourAngle(IshaAngle);

        DateTime At(double hours) => day.AddMinutes(Math.Ceiling(hours * 60));

        return new Dictionary<PrayerName, DateTime>
        {
            [PrayerName.Fajr] = At(fajr),
            [PrayerName.Dhuhr] = At(dhuhr + 1.0 / 60), // دقيقة بعد الزوال
            [PrayerName.Asr] = At(asr),
            [PrayerName.Maghrib] = At(maghrib),
            [PrayerName.Isha] = At(isha)
        };
    }

    /// <summary>هل دخل وقت أذان هذه الصلاة في اليوم المحدد عند اللحظة <paramref name="libyaNow"/>؟</summary>
    public static bool HasStarted(PrayerName prayer, DateTime libyaDate, DateTime libyaNow)
    {
        if (libyaDate.Date < libyaNow.Date) return true;   // يوم سابق: كل الصلوات دخل وقتها
        if (libyaDate.Date > libyaNow.Date) return false;  // يوم قادم
        return libyaNow >= ForDate(libyaDate)[prayer];
    }

    private static double JulianDay(int year, int month, int day)
    {
        if (month <= 2) { year -= 1; month += 12; }
        var a = Math.Floor(year / 100.0);
        var b = 2 - a + Math.Floor(a / 4.0);
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    /// <summary>الميل الشمسي (درجات) ومعادلة الزمن (ساعات).</summary>
    private static (double Declination, double EquationOfTime) SunPosition(double jd)
    {
        var d = jd - 2451545.0;
        var g = FixAngle(357.529 + 0.98560028 * d);
        var q = FixAngle(280.459 + 0.98564736 * d);
        var l = FixAngle(q + 1.915 * Math.Sin(DegToRad(g)) + 0.020 * Math.Sin(DegToRad(2 * g)));
        var e = 23.439 - 0.00000036 * d;

        var ra = RadToDeg(Math.Atan2(Math.Cos(DegToRad(e)) * Math.Sin(DegToRad(l)), Math.Cos(DegToRad(l)))) / 15.0;
        var eqt = q / 15.0 - FixHour(ra);
        if (eqt > 12) eqt -= 24;
        if (eqt < -12) eqt += 24;
        var decl = RadToDeg(Math.Asin(Math.Sin(DegToRad(e)) * Math.Sin(DegToRad(l))));
        return (decl, eqt);
    }

    private static double DegToRad(double d) => d * Math.PI / 180.0;
    private static double RadToDeg(double r) => r * 180.0 / Math.PI;
    private static double FixAngle(double a) { a %= 360; return a < 0 ? a + 360 : a; }
    private static double FixHour(double h) { h %= 24; return h < 0 ? h + 24 : h; }
}
