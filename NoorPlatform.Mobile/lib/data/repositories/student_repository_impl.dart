import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../core/network/network_info.dart';
import '../../../core/storage/local_cache.dart';
import '../../../core/storage/secure_storage.dart';
import '../../../config/constants.dart';
import '../datasources/remote/student_remote_datasource.dart';
import '../models/student_summary_model.dart';
import '../../../domain/repositories/student_repository.dart';

class StudentRepositoryImpl implements StudentRepository {
  final StudentRemoteDataSource remoteDataSource;
  final NetworkInfo networkInfo;
  final SecureStorage secureStorage;

  StudentRepositoryImpl({
    required this.remoteDataSource,
    required this.networkInfo,
    required this.secureStorage,
  });

  @override
  Future<Either<Failure, StudentSummaryModel>> getStudentSummary() async {
    const cacheKey = 'summary';
    const boxName = AppConstants.hiveStudentCache;

    if (await networkInfo.isConnected) {
      try {
        final remoteData = await remoteDataSource.getStudentSummary();
        await LocalCache.put(boxName, cacheKey, remoteData.toJson());
        if (remoteData.id > 0) {
          await secureStorage.saveCachedStudentId(remoteData.id);
        }
        return Right(remoteData);
      } on ServerException catch (e) {
        return Left(ServerFailure(e.message));
      } catch (_) {
        return const Left(ServerFailure('حدث خطأ أثناء تحميل بيانات الطالب'));
      }
    }

    final cachedJson = await LocalCache.get(
      boxName,
      cacheKey,
      ttlMinutes: AppConstants.cacheTtlMedium,
    );
    if (cachedJson != null) {
      return Right(StudentSummaryModel.fromJson(cachedJson));
    }
    return const Left(
      NetworkFailure('لا يوجد اتصال بالإنترنت ولا توجد بيانات محفوظة مسبقاً'),
    );
  }
}
