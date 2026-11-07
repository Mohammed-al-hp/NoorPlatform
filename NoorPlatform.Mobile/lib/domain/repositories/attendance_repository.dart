import 'package:dartz/dartz.dart';
import '../../data/models/attendance_models.dart';
import '../../../core/errors/failures.dart';

abstract class AttendanceRepository {
  Future<Either<Failure, AttendanceModel>> getMyAttendance({int? days});
  Future<Either<Failure, AttendanceModel>> getStudentAttendance(int studentId);
}
