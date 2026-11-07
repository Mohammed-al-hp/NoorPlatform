import 'package:dartz/dartz.dart';
import '../../data/models/auth_models.dart';
import '../../../core/errors/failures.dart';

class LoginResult {
  final UserModel user;
  final bool mustChangePassword;

  const LoginResult({required this.user, required this.mustChangePassword});
}

abstract class AuthRepository {
  Future<Either<Failure, LoginResult>> login(LoginRequestModel request);
  Future<Either<Failure, Unit>> changePassword(
    ChangePasswordRequestModel request,
  );
  Future<void> logout();
  Future<bool> mustChangePassword();
  Future<void> clearMustChangePassword();
}
