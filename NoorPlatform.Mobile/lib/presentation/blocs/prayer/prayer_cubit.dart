import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../../data/models/pedagogical_models.dart';

class PrayerCubit extends Cubit<PrayerState> {
  final PortalRepository repository;

  PrayerCubit(this.repository) : super(const PrayerState.initial());

  Future<void> load() async {
    emit(state.copyWith(status: PrayerStatus.loading, clearError: true));
    final result = await repository.getMyPrayerLogs();
    result.fold(
      (f) => emit(state.copyWith(status: PrayerStatus.error, error: f.message)),
      (logs) => emit(state.copyWith(status: PrayerStatus.loaded, logs: logs)),
    );
  }

  Future<void> submit({
    required String date,
    required bool prayedInMosque,
    required bool onTime,
    required int mosquePrayerCount,
    String? studentNote,
  }) async {
    emit(state.copyWith(submitting: true, clearError: true, clearMessage: true));
    final result = await repository.submitMyPrayer(
      date: date,
      prayedInMosque: prayedInMosque,
      onTime: onTime,
      mosquePrayerCount: mosquePrayerCount,
      studentNote: studentNote,
    );
    result.fold(
      (f) => emit(state.copyWith(submitting: false, error: f.message)),
      (_) async {
        emit(state.copyWith(
          submitting: false,
          successMessage: 'تم تسجيل الصلاة بنجاح',
        ));
        await load();
      },
    );
  }
}

enum PrayerStatus { initial, loading, loaded, error }

class PrayerState {
  final PrayerStatus status;
  final List<PrayerLogModel> logs;
  final String? error;
  final bool submitting;
  final String? successMessage;

  const PrayerState({
    required this.status,
    this.logs = const [],
    this.error,
    this.submitting = false,
    this.successMessage,
  });

  const PrayerState.initial() : this(status: PrayerStatus.initial);

  PrayerState copyWith({
    PrayerStatus? status,
    List<PrayerLogModel>? logs,
    String? error,
    bool? submitting,
    String? successMessage,
    bool clearError = false,
    bool clearMessage = false,
  }) {
    return PrayerState(
      status: status ?? this.status,
      logs: logs ?? this.logs,
      error: clearError ? null : (error ?? this.error),
      submitting: submitting ?? this.submitting,
      successMessage:
          clearMessage ? null : (successMessage ?? this.successMessage),
    );
  }
}
