import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../core/utils/formatters.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../../data/models/pedagogical_models.dart';

/// اختيار الطالب لصلاة واحدة قبل الإرسال.
class PrayerChoice {
  final bool prayed;
  final bool inMosque;
  final bool onTime;

  const PrayerChoice({
    required this.prayed,
    this.inMosque = false,
    this.onTime = true,
  });

  PrayerChoice copyWith({bool? prayed, bool? inMosque, bool? onTime}) =>
      PrayerChoice(
        prayed: prayed ?? this.prayed,
        inMosque: inMosque ?? this.inMosque,
        onTime: onTime ?? this.onTime,
      );
}

class PrayerCubit extends Cubit<PrayerState> {
  final PortalRepository repository;

  PrayerCubit(this.repository) : super(PrayerState.initial());

  Future<void> load({String? date}) async {
    final day = date ?? state.date;
    emit(state.copyWith(
      status: PrayerStatus.loading,
      date: day,
      choices: const {},
      clearError: true,
    ));
    final logsResult = await repository.getMyPrayerLogs();
    final timesResult = await repository.getPrayerTimes(date: day);
    logsResult.fold(
      (f) => emit(state.copyWith(status: PrayerStatus.error, error: f.message)),
      (logs) => emit(state.copyWith(
        status: PrayerStatus.loaded,
        logs: logs,
        times: timesResult.fold((_) => null, (t) => t),
        clearTimes: timesResult.isLeft(),
      )),
    );
  }

  void choose(String prayer, PrayerChoice choice) {
    final updated = Map<String, PrayerChoice>.from(state.choices);
    // لم يصلّها ⇒ لا مسجد ولا وقت
    updated[prayer] = choice.prayed
        ? choice
        : const PrayerChoice(prayed: false, inMosque: false, onTime: false);
    emit(state.copyWith(choices: updated, clearMessage: true, clearError: true));
  }

  Future<void> submit({String? studentNote}) async {
    if (state.choices.isEmpty) {
      emit(state.copyWith(error: 'حدّد حالة صلاة واحدة على الأقل (صلّيتها / لم أصلّها)'));
      return;
    }
    emit(state.copyWith(submitting: true, clearError: true, clearMessage: true));
    final prayers = state.choices.entries
        .map((e) => PrayerEntryModel(
              prayer: e.key,
              prayed: e.value.prayed,
              inMosque: e.value.inMosque,
              onTime: e.value.onTime,
            ))
        .toList();
    final result = await repository.submitMyPrayer(
      date: state.date,
      prayers: prayers,
      studentNote: studentNote,
    );
    await result.fold(
      (f) async => emit(state.copyWith(submitting: false, error: f.message)),
      (message) async {
        emit(state.copyWith(
          submitting: false,
          successMessage: message ?? 'تم تسجيل الصلاة بنجاح',
        ));
        await load();
      },
    );
  }
}

enum PrayerStatus { initial, loading, loaded, error }

class PrayerState {
  final PrayerStatus status;
  final String date;
  final List<PrayerLogModel> logs;
  final PrayerTimesModel? times;
  final Map<String, PrayerChoice> choices;
  final String? error;
  final bool submitting;
  final String? successMessage;

  const PrayerState({
    required this.status,
    required this.date,
    this.logs = const [],
    this.times,
    this.choices = const {},
    this.error,
    this.submitting = false,
    this.successMessage,
  });

  factory PrayerState.initial() =>
      PrayerState(status: PrayerStatus.initial, date: Formatters.ymd());

  /// سجل اليوم المحدد (إن وُجد).
  PrayerLogModel? get dayLog {
    for (final l in logs) {
      if (l.date.startsWith(date)) return l;
    }
    return null;
  }

  PrayerState copyWith({
    PrayerStatus? status,
    String? date,
    List<PrayerLogModel>? logs,
    PrayerTimesModel? times,
    Map<String, PrayerChoice>? choices,
    String? error,
    bool? submitting,
    String? successMessage,
    bool clearError = false,
    bool clearMessage = false,
    bool clearTimes = false,
  }) {
    return PrayerState(
      status: status ?? this.status,
      date: date ?? this.date,
      logs: logs ?? this.logs,
      times: clearTimes ? null : (times ?? this.times),
      choices: choices ?? this.choices,
      error: clearError ? null : (error ?? this.error),
      submitting: submitting ?? this.submitting,
      successMessage:
          clearMessage ? null : (successMessage ?? this.successMessage),
    );
  }
}
