import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';
import 'package:flutter_timezone/flutter_timezone.dart';
import 'package:timezone/data/latest_all.dart' as tzdata;
import 'package:timezone/timezone.dart' as tz;

import '../storage/app_preferences.dart';

/// Schedules a daily local reminder for the student prayer log.
///
/// No-op on web (browser cannot reliably schedule daily local alarms).
/// Windows is unsupported on flutter_local_notifications ^18.
class PrayerReminderService {
  static const notificationId = 1001;
  static const channelId = 'prayer_daily';
  static const channelName = 'تذكير الصلاة';

  final FlutterLocalNotificationsPlugin _plugin =
      FlutterLocalNotificationsPlugin();
  final AppPreferences preferences;

  bool _initialized = false;

  PrayerReminderService(this.preferences);

  bool get isSupported =>
      !kIsWeb &&
      (defaultTargetPlatform == TargetPlatform.android ||
          defaultTargetPlatform == TargetPlatform.iOS);

  Future<void> init() async {
    if (!isSupported || _initialized) return;

    tzdata.initializeTimeZones();
    try {
      final name = await FlutterTimezone.getLocalTimezone();
      tz.setLocalLocation(tz.getLocation(name));
    } catch (_) {
      tz.setLocalLocation(tz.getLocation('UTC'));
    }

    const android = AndroidInitializationSettings('@mipmap/ic_launcher');
    const ios = DarwinInitializationSettings(
      requestAlertPermission: true,
      requestBadgePermission: false,
      requestSoundPermission: true,
    );

    await _plugin.initialize(
      const InitializationSettings(
        android: android,
        iOS: ios,
      ),
    );

    final androidPlugin = _plugin.resolvePlatformSpecificImplementation<
        AndroidFlutterLocalNotificationsPlugin>();
    await androidPlugin?.requestNotificationsPermission();

    _initialized = true;
  }

  Future<void> syncFromPreferences() async {
    if (!isSupported) return;
    await init();
    final enabled = await preferences.isPrayerReminderEnabled();
    if (!enabled) {
      await cancel();
      return;
    }
    final hour = await preferences.getPrayerReminderHour();
    final minute = await preferences.getPrayerReminderMinute();
    await scheduleDaily(hour: hour, minute: minute);
  }

  Future<void> scheduleDaily({required int hour, required int minute}) async {
    if (!isSupported) return;
    await init();

    final androidDetails = AndroidNotificationDetails(
      channelId,
      channelName,
      channelDescription: 'تذكير يومي بتسجيل صلاة المسجد',
      importance: Importance.high,
      priority: Priority.high,
      icon: '@mipmap/ic_launcher',
    );
    const iosDetails = DarwinNotificationDetails();

    final details = NotificationDetails(
      android: androidDetails,
      iOS: iosDetails,
    );

    final when = _nextInstanceOf(hour, minute);
    await _plugin.zonedSchedule(
      notificationId,
      'تذكير سجل الصلاة',
      'سجّل صلاة اليوم قبل إقفال السجل — بارك الله فيك',
      when,
      details,
      androidScheduleMode: AndroidScheduleMode.inexactAllowWhileIdle,
      uiLocalNotificationDateInterpretation:
          UILocalNotificationDateInterpretation.absoluteTime,
      matchDateTimeComponents: DateTimeComponents.time,
    );
  }

  Future<void> cancel() async {
    if (!isSupported) return;
    await init();
    await _plugin.cancel(notificationId);
  }

  tz.TZDateTime _nextInstanceOf(int hour, int minute) {
    final now = tz.TZDateTime.now(tz.local);
    var scheduled = tz.TZDateTime(
      tz.local,
      now.year,
      now.month,
      now.day,
      hour,
      minute,
    );
    if (!scheduled.isAfter(now)) {
      scheduled = scheduled.add(const Duration(days: 1));
    }
    return scheduled;
  }
}
