import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../core/network/network_info.dart';
import '../../../core/storage/local_cache.dart';
import '../../../config/constants.dart';
import '../datasources/remote/parent_remote_datasource.dart';
import '../models/parent_models.dart';
import '../../../domain/repositories/parent_repository.dart';

class ParentRepositoryImpl implements ParentRepository {
  final ParentRemoteDataSource remoteDataSource;
  final NetworkInfo networkInfo;

  ParentRepositoryImpl({
    required this.remoteDataSource,
    required this.networkInfo,
  });

  @override
  Future<Either<Failure, ParentSummaryModel>> getParentSummary() async {
    const cacheKey = 'summary';
    const boxName = AppConstants.hiveParentCache;

    if (await networkInfo.isConnected) {
      try {
        final remoteData = await remoteDataSource.getParentSummary();
        
        await LocalCache.put(boxName, cacheKey, remoteData.toJson());
        
        return Right(remoteData);
      } on ServerException catch (e) {
        return Left(ServerFailure(e.message));
      } catch (e) {
        return const Left(ServerFailure('حدث خطأ أثناء تحميل بيانات ولي الأمر'));
      }
    } else {
      final cachedJson = await LocalCache.get(
        boxName,
        cacheKey,
        ttlMinutes: AppConstants.cacheTtlMedium,
      );

      if (cachedJson != null) {
        return Right(ParentSummaryModel.fromJson(cachedJson));
      } else {
        return const Left(NetworkFailure('لا يوجد اتصال بالإنترنت'));
      }
    }
  }
}
