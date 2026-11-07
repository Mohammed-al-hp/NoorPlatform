import 'package:equatable/equatable.dart';

abstract class AttendanceEvent extends Equatable {
  const AttendanceEvent();
  @override
  List<Object?> get props => [];
}

/// [days] عدد الأيام المطلوب جلبها. null يعني الاعتماد على افتراضي الـ API.
class LoadAttendance extends AttendanceEvent {
  final int? days;
  const LoadAttendance({this.days});

  @override
  List<Object?> get props => [days];
}
