import 'package:equatable/equatable.dart';

abstract class ParentDashboardEvent extends Equatable {
  const ParentDashboardEvent();
  @override
  List<Object> get props => [];
}

class LoadParentDashboard extends ParentDashboardEvent {}
