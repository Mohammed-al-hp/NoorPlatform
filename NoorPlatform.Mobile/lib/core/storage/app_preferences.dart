import 'package:flutter/material.dart';
import 'package:hive_flutter/hive_flutter.dart';

/// Persistent non-sensitive prefs (theme, read inbox ids, reminders).
class AppPreferences {
  static const boxName = 'app_prefs';
  static const _themeKey = 'theme_mode';
  static const _readIdsKey = 'read_inbox_ids';
  static const _prayerReminderEnabledKey = 'prayer_reminder_enabled';
  static const _prayerReminderHourKey = 'prayer_reminder_hour';
  static const _prayerReminderMinuteKey = 'prayer_reminder_minute';

  Future<Box> _box() => Hive.openBox(boxName);

  Future<ThemeMode> getThemeMode() async {
    final raw = (await _box()).get(_themeKey, defaultValue: 'system') as String;
    switch (raw) {
      case 'light':
        return ThemeMode.light;
      case 'dark':
        return ThemeMode.dark;
      default:
        return ThemeMode.system;
    }
  }

  Future<void> setThemeMode(ThemeMode mode) async {
    final value = switch (mode) {
      ThemeMode.light => 'light',
      ThemeMode.dark => 'dark',
      ThemeMode.system => 'system',
    };
    await (await _box()).put(_themeKey, value);
  }

  Future<Set<String>> getReadInboxIds() async {
    final raw = (await _box()).get(_readIdsKey);
    if (raw is List) {
      return raw.map((e) => e.toString()).toSet();
    }
    return {};
  }

  Future<void> markInboxIdsRead(Iterable<String> ids) async {
    final box = await _box();
    final current = await getReadInboxIds();
    current.addAll(ids);
    await box.put(_readIdsKey, current.toList());
  }

  Future<void> markAllInboxIdsRead(Iterable<String> allIds) async {
    await (await _box()).put(_readIdsKey, allIds.toList());
  }

  Future<bool> isPrayerReminderEnabled() async {
    return (await _box()).get(_prayerReminderEnabledKey, defaultValue: false)
        as bool;
  }

  Future<void> setPrayerReminderEnabled(bool value) async {
    await (await _box()).put(_prayerReminderEnabledKey, value);
  }

  Future<int> getPrayerReminderHour() async {
    return (await _box()).get(_prayerReminderHourKey, defaultValue: 20) as int;
  }

  Future<int> getPrayerReminderMinute() async {
    return (await _box()).get(_prayerReminderMinuteKey, defaultValue: 0) as int;
  }

  Future<void> setPrayerReminderTime({
    required int hour,
    required int minute,
  }) async {
    final box = await _box();
    await box.put(_prayerReminderHourKey, hour);
    await box.put(_prayerReminderMinuteKey, minute);
  }

  // ─── تذكير «هل قرأت وردك اليوم؟» داخل التطبيق ───
  static const _wirdDoneKey = 'wird_done_date';
  static const _wirdSnoozeKey = 'wird_snooze_until';

  /// هل يجب إظهار بطاقة الورد الآن؟ (لم يؤكد قراءته اليوم ولم يؤجّل)
  Future<bool> shouldShowWirdReminder(String todayYmd) async {
    final box = await _box();
    if (box.get(_wirdDoneKey) == todayYmd) return false;
    final snooze = box.get(_wirdSnoozeKey, defaultValue: 0) as int;
    return DateTime.now().millisecondsSinceEpoch >= snooze;
  }

  Future<void> markWirdDone(String todayYmd) async {
    await (await _box()).put(_wirdDoneKey, todayYmd);
  }

  Future<void> snoozeWird(Duration duration) async {
    await (await _box()).put(
      _wirdSnoozeKey,
      DateTime.now().add(duration).millisecondsSinceEpoch,
    );
  }
}
