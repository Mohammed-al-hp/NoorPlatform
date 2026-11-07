import 'package:intl/intl.dart';

/// Latin (Western) numerals for dates and stats display.
class Formatters {
  Formatters._();

  static final _date = DateFormat('yyyy-MM-dd', 'en_GB');
  static final _display = DateFormat('dd/MM/yyyy', 'en_GB');
  static final _dateTime = DateFormat('dd/MM/yyyy HH:mm', 'en_GB');

  static String ymd([DateTime? d]) => _date.format(d ?? DateTime.now());

  static String displayDate(String? raw) {
    if (raw == null || raw.isEmpty) return '—';
    final parsed = DateTime.tryParse(raw);
    if (parsed == null) return raw;
    return _display.format(parsed);
  }

  static String displayDateTime(String? raw) {
    if (raw == null || raw.isEmpty) return '—';
    final parsed = DateTime.tryParse(raw);
    if (parsed == null) return raw;
    return _dateTime.format(parsed);
  }

  /// Monday of the week containing [date] (local).
  static DateTime weekStart([DateTime? date]) {
    final d = date ?? DateTime.now();
    final weekday = d.weekday; // Mon=1
    return DateTime(d.year, d.month, d.day)
        .subtract(Duration(days: weekday - 1));
  }

  static String weekStartYmd([DateTime? date]) => ymd(weekStart(date));

  static String number(num n, {int fractionDigits = 0}) {
    if (fractionDigits == 0) return n.round().toString();
    return n.toStringAsFixed(fractionDigits);
  }
}
