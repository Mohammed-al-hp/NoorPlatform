import 'package:dartz/dartz.dart';
import '../../data/models/parent_models.dart';
import '../../../core/errors/failures.dart';

abstract class ParentRepository {
  Future<Either<Failure, ParentSummaryModel>> getParentSummary();
}
