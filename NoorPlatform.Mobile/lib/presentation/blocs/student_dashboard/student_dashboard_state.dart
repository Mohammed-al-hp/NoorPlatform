import 'package:equatable/equatable.dart';
import '../../../../data/models/student_summary_model.dart';

abstract class StudentDashboardState extends Equatable {
  const StudentDashboardState();
  @override
  List<Object> get props => [];
}

class StudentDashboardInitial extends StudentDashboardState {}

class StudentDashboardLoading extends StudentDashboardState {}

class StudentDashboardLoaded extends StudentDashboardState {
  final StudentSummaryModel summary;
  const StudentDashboardLoaded(this.summary);

  @override
  List<Object> get props => [summary];
}

class StudentDashboardError extends StudentDashboardState {
  final String message;
  const StudentDashboardError(this.message);

  @override
  List<Object> get props => [message];
}
