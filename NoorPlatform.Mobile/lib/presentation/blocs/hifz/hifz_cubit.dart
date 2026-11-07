import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../../data/models/hifz_models.dart';

class HifzCubit extends Cubit<HifzState> {
  final PortalRepository repository;

  HifzCubit(this.repository) : super(const HifzState.initial());

  Future<void> loadMine() async {
    emit(state.copyWith(status: HifzStatus.loading, clearError: true));
    final result = await repository.getMyHifz();
    result.fold(
      (f) => emit(state.copyWith(status: HifzStatus.error, error: f.message)),
      (data) => emit(state.copyWith(
        status: HifzStatus.loaded,
        records: data.records,
        progress: data.hifzProgress,
      )),
    );
  }

  Future<void> loadForStudent(int studentId) async {
    emit(state.copyWith(status: HifzStatus.loading, clearError: true));
    final result = await repository.getStudentHifz(studentId);
    result.fold(
      (f) => emit(state.copyWith(status: HifzStatus.error, error: f.message)),
      (records) => emit(state.copyWith(
        status: HifzStatus.loaded,
        records: records,
      )),
    );
  }
}

enum HifzStatus { initial, loading, loaded, error }

class HifzState {
  final HifzStatus status;
  final List<HifzRecordModel> records;
  final int progress;
  final String? error;

  const HifzState({
    required this.status,
    this.records = const [],
    this.progress = 0,
    this.error,
  });

  const HifzState.initial() : this(status: HifzStatus.initial);

  HifzState copyWith({
    HifzStatus? status,
    List<HifzRecordModel>? records,
    int? progress,
    String? error,
    bool clearError = false,
  }) {
    return HifzState(
      status: status ?? this.status,
      records: records ?? this.records,
      progress: progress ?? this.progress,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
