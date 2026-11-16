import 'dart:math' as math;

/// مواقيت الأذان لمدينة مصراتة (ليبيا) — نفس خوارزمية الخادم (PrayerTimes.cs):
/// الهيئة المصرية العامة للمساحة (الفجر 19.5°، العشاء 17.5°)، العصر بظل المثل، توقيت UTC+2.
/// تُستخدم لجدولة تنبيهات الأذان محلياً دون اتصال.
class MisrataPrayerTimes {
  static const double latitude = 32.3754;
  static const double longitude = 15.0925;
  static const double utcOffset = 2;

  static const List<String> keys = ['Fajr', 'Dhuhr', 'Asr', 'Maghrib', 'Isha'];
  static const Map<String, String> names = {
    'Fajr': 'الفجر',
    'Dhuhr': 'الظهر',
    'Asr': 'العصر',
    'Maghrib': 'المغرب',
    'Isha': 'العشاء',
  };

  /// المواقيت كـ (ساعة، دقيقة) بتوقيت ليبيا المحلي لليوم المحدد.
  static Map<String, ({int hour, int minute})> forDate(DateTime date) {
    final jd = _julianDay(date.year, date.month, date.day) - longitude / (15.0 * 24.0);
    final sun = _sunPosition(jd + 0.5);
    final decl = sun.$1;
    final eqt = sun.$2;
    final dhuhr = 12 + utcOffset - longitude / 15.0 - eqt;

    double hourAngle(double angle) =>
        _deg(math.acos((-math.sin(_rad(angle)) - math.sin(_rad(decl)) * math.sin(_rad(latitude))) /
            (math.cos(_rad(decl)) * math.cos(_rad(latitude))))) /
        15.0;

    double asrHourAngle(double factor) {
      final angle = -_deg(math.atan(1.0 / (factor + math.tan(_rad((latitude - decl).abs())))));
      return hourAngle(angle);
    }

    ({int hour, int minute}) at(double hours) {
      final total = (hours * 60).ceil();
      return (hour: total ~/ 60, minute: total % 60);
    }

    return {
      'Fajr': at(dhuhr - hourAngle(19.5)),
      'Dhuhr': at(dhuhr + 1 / 60),
      'Asr': at(dhuhr + asrHourAngle(1)),
      'Maghrib': at(dhuhr + hourAngle(0.833) + 1 / 60),
      'Isha': at(dhuhr + hourAngle(17.5)),
    };
  }

  static double _julianDay(int year, int month, int day) {
    if (month <= 2) {
      year -= 1;
      month += 12;
    }
    final a = (year / 100).floorToDouble();
    final b = 2 - a + (a / 4).floorToDouble();
    return (365.25 * (year + 4716)).floorToDouble() +
        (30.6001 * (month + 1)).floorToDouble() +
        day +
        b -
        1524.5;
  }

  static (double, double) _sunPosition(double jd) {
    final d = jd - 2451545.0;
    final g = _fixAngle(357.529 + 0.98560028 * d);
    final q = _fixAngle(280.459 + 0.98564736 * d);
    final l = _fixAngle(q + 1.915 * math.sin(_rad(g)) + 0.020 * math.sin(_rad(2 * g)));
    final e = 23.439 - 0.00000036 * d;
    final ra = _deg(math.atan2(math.cos(_rad(e)) * math.sin(_rad(l)), math.cos(_rad(l)))) / 15.0;
    var eqt = q / 15.0 - _fixHour(ra);
    if (eqt > 12) eqt -= 24;
    if (eqt < -12) eqt += 24;
    final decl = _deg(math.asin(math.sin(_rad(e)) * math.sin(_rad(l))));
    return (decl, eqt);
  }

  static double _rad(double d) => d * math.pi / 180.0;
  static double _deg(double r) => r * 180.0 / math.pi;
  static double _fixAngle(double a) {
    a = a % 360;
    return a < 0 ? a + 360 : a;
  }

  static double _fixHour(double h) {
    h = h % 24;
    return h < 0 ? h + 24 : h;
  }
}
