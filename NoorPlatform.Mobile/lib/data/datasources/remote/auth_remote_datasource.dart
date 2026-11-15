import 'package:dio/dio.dart';
import '../../../config/api_config.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/api_response_helper.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/auth_models.dart';

abstract class AuthRemoteDataSource {
  Future<AuthResponseModel> login(LoginRequestModel request);
  Future<void> changePassword(ChangePasswordRequestModel request);
  Future<(String, int)> requestPasswordOtp(String phone);
  Future<void> verifyPasswordOtp(String phone, String code);
  Future<String> resetPasswordWithOtp(String phone, String code, String newPassword);
}

class AuthRemoteDataSourceImpl implements AuthRemoteDataSource {
  final ApiClient apiClient;

  AuthRemoteDataSourceImpl(this.apiClient);

  @override
  Future<AuthResponseModel> login(LoginRequestModel request) async {
    try {
      final response = await apiClient.post(
        ApiConfig.login,
        data: request.toJson(),
      );

      if (response.statusCode == 200) {
        return AuthResponseModel.fromJson(
          Map<String, dynamic>.from(response.data as Map),
        );
      }
      throw ServerException(
        message: ApiResponseHelper.extractMessage(
          response.data,
          'فشل تسجيل الدخول',
        ),
        statusCode: response.statusCode,
      );
    } on DioException catch (e) {
      if (e.response?.statusCode == 401 || e.response?.statusCode == 400) {
        throw ServerException(
          message: ApiResponseHelper.extractMessage(
            e.response?.data,
            'بيانات الدخول غير صحيحة',
          ),
          statusCode: e.response?.statusCode,
        );
      }
      throw mapDioErrorToException(e, fallbackMessage: 'فشل تسجيل الدخول');
    }
  }

  Future<Map<String, dynamic>> _postPublic(String path, Map<String, dynamic> body, String fallback) async {
    try {
      final res = await apiClient.post(path, data: body);
      ApiResponseHelper.ensureSuccess(res, fallback: fallback);
      return res.data is Map ? Map<String, dynamic>.from(res.data as Map) : <String, dynamic>{};
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: fallback);
    }
  }

  @override
  Future<(String, int)> requestPasswordOtp(String phone) async {
    final data = await _postPublic(ApiConfig.forgotPassword, {'phone': phone}, 'تعذر إرسال رمز التحقق');
    return (
      toStr(data['message'], 'تم إرسال رمز التحقق'),
      data['resendAfterSeconds'] == null ? 60 : toInt(data['resendAfterSeconds']),
    );
  }

  @override
  Future<void> verifyPasswordOtp(String phone, String code) async {
    await _postPublic(ApiConfig.verifyOtp, {'phone': phone, 'code': code}, 'رمز التحقق غير صحيح');
  }

  @override
  Future<String> resetPasswordWithOtp(String phone, String code, String newPassword) async {
    final data = await _postPublic(
      ApiConfig.resetPassword,
      {'phone': phone, 'code': code, 'newPassword': newPassword},
      'تعذر تعيين كلمة المرور',
    );
    return toStr(data['message'], 'تم تعيين كلمة المرور الجديدة');
  }

  @override
  Future<void> changePassword(ChangePasswordRequestModel request) async {
    try {
      final response = await apiClient.post(
        ApiConfig.changePassword,
        data: request.toJson(),
      );
      ApiResponseHelper.ensureSuccess(
        response,
        fallback: 'فشل تغيير كلمة المرور',
      );
    } on DioException catch (e) {
      throw mapDioErrorToException(
        e,
        fallbackMessage: 'فشل تغيير كلمة المرور',
      );
    }
  }
}
