import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../core/network/network_info.dart';
import '../../../core/storage/local_cache.dart';
import '../../../config/constants.dart';
import '../datasources/remote/attendance_remote_datasource.dart';
import '../models/attendance_models.dart';
import '../../../domain/repositories/attendance_repository.dart';

class AttendanceRepositoryImpl implements AttendanceRepository {
  final AttendanceRemoteDataSource remoteDataSource;
  final NetworkInfo networkInfo;

  AttendanceRepositoryImpl({
    required this.remoteDataSource,
    required this.networkInfo,
  });

  @override
  Future<Either<Failure, AttendanceModel>> getMyAttendance({int? days}) async {
    final cacheKey = 'my_attendance_${days ?? "default"}';
    const boxName = AppConstants.hiveAttendanceCache;

    if (await networkInfo.isConnected) {
      try {
        final remoteData = await remoteDataSource.getMyAttendance(days: days);
        await LocalCache.put(boxName, cacheKey, remoteData.toJson());
        return Right(remoteData);
      } on ServerException catch (e) {
        return Left(ServerFailure(e.message));
      } catch (_) {
        return const Left(ServerFailure('حدث خطأ أثناء تحميل سجل الحضور'));
      }
    }

    final cachedJson = await LocalCache.get(
      boxName,
      cacheKey,
      ttlMinutes: AppConstants.cacheTtlMedium,
    );
    if (cachedJson != null) {
      return Right(AttendanceModel.fromJson(cachedJson));
    }
    return const Left(
      NetworkFailure('لا يوجد اتصال بالإنترنت ولا توجد بيانات محفوظة مسبقاً'),
    );
  }

  @override
  Future<Either<Failure, AttendanceModel>> getStudentAttendance(
    int studentId,
  ) async {
    if (!await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }
    try {
      final data = await remoteDataSource.getStudentAttendance(studentId);
      return Right(data);
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    } catch (_) {
      return const Left(ServerFailure('حدث خطأ أثناء تحميل سجل الحضور'));
    }
  }
}
