import 'package:equatable/equatable.dart';
import '../../../../data/models/parent_models.dart';

abstract class ParentDashboardState extends Equatable {
  const ParentDashboardState();
  @override
  List<Object> get props => [];
}

class ParentDashboardInitial extends ParentDashboardState {}

class ParentDashboardLoading extends ParentDashboardState {}

class ParentDashboardLoaded extends ParentDashboardState {
  final ParentSummaryModel summary;
  const ParentDashboardLoaded(this.summary);

  @override
  List<Object> get props => [summary];
}

class ParentDashboardError extends ParentDashboardState {
  final String message;
  const ParentDashboardError(this.message);

  @override
  List<Object> get props => [message];
}
