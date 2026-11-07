import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../../data/models/pedagogical_models.dart';

class EvaluationsCubit extends Cubit<EvaluationsState> {
  final PortalRepository repository;

  EvaluationsCubit(this.repository) : super(const EvaluationsState.initial());

  Future<void> load({int? studentId}) async {
    emit(state.copyWith(status: EvalStatus.loading, clearError: true));
    final config = await repository.getPortalConfig();
    final visible = config.fold((_) => true, (c) => c.evaluationsVisible);
    if (!visible) {
      emit(state.copyWith(
        status: EvalStatus.hidden,
        evaluations: const [],
        visible: false,
      ));
      return;
    }
    final result = await repository.getMyEvaluations(studentId: studentId);
    result.fold(
      (f) => emit(state.copyWith(status: EvalStatus.error, error: f.message)),
      (list) => emit(state.copyWith(
        status: EvalStatus.loaded,
        evaluations: list,
        visible: true,
      )),
    );
  }
}

enum EvalStatus { initial, loading, loaded, error, hidden }

class EvaluationsState {
  final EvalStatus status;
  final List<EvaluationModel> evaluations;
  final bool visible;
  final String? error;

  const EvaluationsState({
    required this.status,
    this.evaluations = const [],
    this.visible = true,
    this.error,
  });

  const EvaluationsState.initial() : this(status: EvalStatus.initial);

  EvaluationsState copyWith({
    EvalStatus? status,
    List<EvaluationModel>? evaluations,
    bool? visible,
    String? error,
    bool clearError = false,
  }) {
    return EvaluationsState(
      status: status ?? this.status,
      evaluations: evaluations ?? this.evaluations,
      visible: visible ?? this.visible,
      error: clearError ? null : (error ?? this.error),
    );
  }
}
