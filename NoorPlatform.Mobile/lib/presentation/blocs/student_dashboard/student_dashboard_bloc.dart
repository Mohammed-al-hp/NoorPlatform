import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../../domain/repositories/student_repository.dart';
import 'student_dashboard_event.dart';
import 'student_dashboard_state.dart';

class StudentDashboardBloc extends Bloc<StudentDashboardEvent, StudentDashboardState> {
  final StudentRepository studentRepository;

  StudentDashboardBloc({required this.studentRepository}) : super(StudentDashboardInitial()) {
    on<LoadStudentDashboard>(_onLoadDashboard);
  }

  Future<void> _onLoadDashboard(
    LoadStudentDashboard event,
    Emitter<StudentDashboardState> emit,
  ) async {
    emit(StudentDashboardLoading());

    final result = await studentRepository.getStudentSummary();

    result.fold(
      (failure) => emit(StudentDashboardError(failure.message)),
      (summary) => emit(StudentDashboardLoaded(summary)),
    );
  }
}
