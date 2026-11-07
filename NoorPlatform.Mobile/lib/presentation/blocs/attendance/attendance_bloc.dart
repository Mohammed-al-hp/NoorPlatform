import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../../domain/repositories/attendance_repository.dart';
import 'attendance_event.dart';
import 'attendance_state.dart';

class AttendanceBloc extends Bloc<AttendanceEvent, AttendanceState> {
  final AttendanceRepository attendanceRepository;

  AttendanceBloc({required this.attendanceRepository}) : super(AttendanceInitial()) {
    on<LoadAttendance>(_onLoadAttendance);
  }

  Future<void> _onLoadAttendance(
    LoadAttendance event,
    Emitter<AttendanceState> emit,
  ) async {
    emit(AttendanceLoading());

    final result = await attendanceRepository.getMyAttendance(days: event.days);

    result.fold(
      (failure) => emit(AttendanceError(failure.message)),
      (data) => emit(AttendanceLoaded(data)),
    );
  }
}
