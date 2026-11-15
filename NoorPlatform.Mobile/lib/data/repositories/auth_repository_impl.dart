import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../core/network/network_info.dart';
import '../../../core/storage/local_cache.dart';
import '../../../core/storage/secure_storage.dart';
import '../datasources/remote/auth_remote_datasource.dart';
import '../models/auth_models.dart';
import '../../../domain/repositories/auth_repository.dart';

class AuthRepositoryImpl implements AuthRepository {
  final AuthRemoteDataSource remoteDataSource;
  final SecureStorage secureStorage;
  final NetworkInfo networkInfo;

  AuthRepositoryImpl({
    required this.remoteDataSource,
    required this.secureStorage,
    required this.networkInfo,
  });

  @override
  Future<Either<Failure, LoginResult>> login(LoginRequestModel request) async {
    if (!await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }

    try {
      final response = await remoteDataSource.login(request);
      await secureStorage.saveToken(response.token);
      await secureStorage.saveUserJson(response.user.toJson());
      await secureStorage.setMustChangePassword(response.mustChangePassword);

      return Right(
        LoginResult(
          user: response.user,
          mustChangePassword: response.mustChangePassword,
        ),
      );
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      return Left(NetworkFailure(e.message));
    } catch (e) {
      return Left(ServerFailure('الخطأ: $e'));
    }
  }

  @override
  Future<Either<Failure, Unit>> changePassword(
    ChangePasswordRequestModel request,
  ) async {
    if (!await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }
    try {
      await remoteDataSource.changePassword(request);
      await secureStorage.setMustChangePassword(false);
      return const Right(unit);
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      return Left(NetworkFailure(e.message));
    } catch (e) {
      return Left(ServerFailure('تعذر تغيير كلمة المرور'));
    }
  }

  Future<Either<Failure, T>> _run<T>(Future<T> Function() action) async {
    if (!await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }
    try {
      return Right(await action());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      return Left(NetworkFailure(e.message));
    } catch (_) {
      return const Left(ServerFailure('حدث خطأ غير متوقع'));
    }
  }

  @override
  Future<Either<Failure, (String, int)>> requestPasswordOtp(String phone) =>
      _run(() => remoteDataSource.requestPasswordOtp(phone));

  @override
  Future<Either<Failure, Unit>> verifyPasswordOtp(String phone, String code) => _run(() async {
        await remoteDataSource.verifyPasswordOtp(phone, code);
        return unit;
      });

  @override
  Future<Either<Failure, String>> resetPasswordWithOtp(
          String phone, String code, String newPassword) =>
      _run(() => remoteDataSource.resetPasswordWithOtp(phone, code, newPassword));

  @override
  Future<void> logout() async {
    await secureStorage.clearAll();
    await LocalCache.clearAll();
  }

  @override
  Future<bool> mustChangePassword() => secureStorage.getMustChangePassword();

  @override
  Future<void> clearMustChangePassword() =>
      secureStorage.setMustChangePassword(false);
}
