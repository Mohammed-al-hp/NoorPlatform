import 'dart:async';
import 'package:dio/dio.dart';
import '../storage/secure_storage.dart';

/// Global auth event stream for triggering logout/redirect from interceptor.
class AuthEventBus {
  static final AuthEventBus _instance = AuthEventBus._();
  factory AuthEventBus() => _instance;
  AuthEventBus._();

  final _controller = StreamController<AuthEvent>.broadcast();
  Stream<AuthEvent> get stream => _controller.stream;
  void emit(AuthEvent event) => _controller.add(event);
  void dispose() => _controller.close();
}

enum AuthEvent { tokenExpired, forbidden, forceLogout, mustChangePassword }

/// Dio interceptor: JWT attach + global 401/403 handling.
///
/// Note: ApiClient uses validateStatus < 500 so 401 arrives as onResponse,
/// not onError. We handle both paths.
class ApiInterceptor extends Interceptor {
  final SecureStorage _secureStorage;
  bool _handling401 = false;

  ApiInterceptor(this._secureStorage);

  @override
  void onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    try {
      final token = await _secureStorage.getToken();
      if (token != null && token.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    } catch (_) {}

    options.headers['Content-Type'] ??= 'application/json';
    options.headers['Accept'] = 'application/json';
    handler.next(options);
  }

  @override
  void onResponse(Response response, ResponseInterceptorHandler handler) async {
    final code = response.statusCode;
    final path = response.requestOptions.path;

    if (code == 401 && !path.contains('/auth/login')) {
      await _emitUnauthorized();
      handler.reject(
        DioException(
          requestOptions: response.requestOptions,
          response: response,
          type: DioExceptionType.badResponse,
          message: 'انتهت صلاحية الجلسة',
        ),
      );
      return;
    }

    if (code == 403) {
      await _emitForbidden(response.data);
    }

    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) async {
    final statusCode = err.response?.statusCode;
    final path = err.requestOptions.path;

    if (statusCode == 401 && !path.contains('/auth/login')) {
      await _emitUnauthorized();
    } else if (statusCode == 403) {
      await _emitForbidden(err.response?.data);
    }

    handler.next(err);
  }

  /// الخادم يفرض تغيير كلمة المرور المؤقتة (code = MUST_CHANGE_PASSWORD):
  /// نحفظ الحالة ليحوّل الموجّه المستخدم إلى شاشة تغيير كلمة المرور.
  Future<void> _emitForbidden(dynamic data) async {
    if (data is Map && data['code'] == 'MUST_CHANGE_PASSWORD') {
      await _secureStorage.setMustChangePassword(true);
      AuthEventBus().emit(AuthEvent.mustChangePassword);
      return;
    }
    AuthEventBus().emit(AuthEvent.forbidden);
  }

  Future<void> _emitUnauthorized() async {
    if (_handling401) return;
    _handling401 = true;
    try {
      await _secureStorage.deleteToken();
      AuthEventBus().emit(AuthEvent.tokenExpired);
    } finally {
      _handling401 = false;
    }
  }
}
