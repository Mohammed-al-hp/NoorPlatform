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

  // ── نسيت كلمة المرور (OTP عبر واتساب) ──
  /// يعيد رسالة الخادم الموحّدة وعدد ثواني انتظار إعادة الإرسال.
  Future<Either<Failure, (String message, int resendAfterSeconds)>> requestPasswordOtp(String phone);
  Future<Either<Failure, Unit>> verifyPasswordOtp(String phone, String code);
  Future<Either<Failure, String>> resetPasswordWithOtp(String phone, String code, String newPassword);
  Future<bool> mustChangePassword();
  Future<void> clearMustChangePassword();
}
