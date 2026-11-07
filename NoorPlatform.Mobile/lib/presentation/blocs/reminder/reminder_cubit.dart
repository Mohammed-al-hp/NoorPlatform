import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/notifications/prayer_reminder_service.dart';
import '../../../core/storage/app_preferences.dart';

class ReminderState extends Equatable {
  final bool enabled;
  final int hour;
  final int minute;
  final bool supported;
  final bool busy;
  final String? message;

  const ReminderState({
    this.enabled = false,
    this.hour = 20,
    this.minute = 0,
    this.supported = false,
    this.busy = false,
    this.message,
  });

  String get timeLabel {
    final h = hour.toString().padLeft(2, '0');
    final m = minute.toString().padLeft(2, '0');
    return '$h:$m';
  }

  ReminderState copyWith({
    bool? enabled,
    int? hour,
    int? minute,
    bool? supported,
    bool? busy,
    String? message,
    bool clearMessage = false,
  }) =>
      ReminderState(
        enabled: enabled ?? this.enabled,
        hour: hour ?? this.hour,
        minute: minute ?? this.minute,
        supported: supported ?? this.supported,
        busy: busy ?? this.busy,
        message: clearMessage ? null : (message ?? this.message),
      );

  @override
  List<Object?> get props =>
      [enabled, hour, minute, supported, busy, message];
}

class ReminderCubit extends Cubit<ReminderState> {
  final AppPreferences preferences;
  final PrayerReminderService reminderService;

  ReminderCubit({
    required this.preferences,
    required this.reminderService,
  }) : super(const ReminderState());

  Future<void> load() async {
    emit(
      state.copyWith(
        supported: reminderService.isSupported,
        enabled: await preferences.isPrayerReminderEnabled(),
        hour: await preferences.getPrayerReminderHour(),
        minute: await preferences.getPrayerReminderMinute(),
        clearMessage: true,
      ),
    );
  }

  Future<void> setEnabled(bool enabled) async {
    if (!reminderService.isSupported) {
      emit(state.copyWith(
        message: 'التذكير يعمل على Android وiOS فقط (ليس على المتصفح)',
      ));
      return;
    }
    emit(state.copyWith(busy: true, clearMessage: true));
    await preferences.setPrayerReminderEnabled(enabled);
    if (enabled) {
      await reminderService.scheduleDaily(
        hour: state.hour,
        minute: state.minute,
      );
      emit(state.copyWith(
        enabled: true,
        busy: false,
        message: 'تم تفعيل التذكير اليومي الساعة ${state.timeLabel}',
      ));
    } else {
      await reminderService.cancel();
      emit(state.copyWith(
        enabled: false,
        busy: false,
        message: 'تم إيقاف تذكير الصلاة',
      ));
    }
  }

  Future<void> setTime({required int hour, required int minute}) async {
    emit(state.copyWith(busy: true, clearMessage: true));
    await preferences.setPrayerReminderTime(hour: hour, minute: minute);
    emit(state.copyWith(hour: hour, minute: minute));
    if (state.enabled && reminderService.isSupported) {
      await reminderService.scheduleDaily(hour: hour, minute: minute);
      emit(state.copyWith(
        busy: false,
        message: 'تم تحديث وقت التذكير إلى ${hour.toString().padLeft(2, '0')}:${minute.toString().padLeft(2, '0')}',
      ));
    } else {
      emit(state.copyWith(busy: false));
    }
  }
}
