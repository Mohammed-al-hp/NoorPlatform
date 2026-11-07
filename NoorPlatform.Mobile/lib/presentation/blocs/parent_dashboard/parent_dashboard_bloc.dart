import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../../domain/repositories/parent_repository.dart';
import 'parent_dashboard_event.dart';
import 'parent_dashboard_state.dart';

class ParentDashboardBloc extends Bloc<ParentDashboardEvent, ParentDashboardState> {
  final ParentRepository parentRepository;

  ParentDashboardBloc({required this.parentRepository}) : super(ParentDashboardInitial()) {
    on<LoadParentDashboard>(_onLoadDashboard);
  }

  Future<void> _onLoadDashboard(
    LoadParentDashboard event,
    Emitter<ParentDashboardState> emit,
  ) async {
    emit(ParentDashboardLoading());

    final result = await parentRepository.getParentSummary();

    result.fold(
      (failure) => emit(ParentDashboardError(failure.message)),
      (summary) => emit(ParentDashboardLoaded(summary)),
    );
  }
}
