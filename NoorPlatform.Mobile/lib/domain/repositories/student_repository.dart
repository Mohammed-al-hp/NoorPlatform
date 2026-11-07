import 'package:dartz/dartz.dart';
import '../../data/models/student_summary_model.dart';
import '../../../core/errors/failures.dart';

abstract class StudentRepository {
  Future<Either<Failure, StudentSummaryModel>> getStudentSummary();
}
