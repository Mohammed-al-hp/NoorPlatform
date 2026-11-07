import 'package:dio/dio.dart';
import '../../config/api_config.dart';
import '../storage/secure_storage.dart';
import 'api_interceptor.dart';

/// Singleton Dio HTTP client pre-configured with:
/// - Base URL from ApiConfig
/// - JWT Bearer Token interceptor
/// - Global 401/403 error handling
/// - Timeout configuration
/// - Debug logging (in debug mode only)
class ApiClient {
  late final Dio dio;
  final SecureStorage _secureStorage;

  ApiClient(this._secureStorage) {
    dio = Dio(
      BaseOptions(
        baseUrl: ApiConfig.baseUrl,
        connectTimeout: ApiConfig.connectTimeout,
        receiveTimeout: ApiConfig.receiveTimeout,
        sendTimeout: ApiConfig.sendTimeout,
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
        responseType: ResponseType.json,
        validateStatus: (status) => status != null && status < 500,
      ),
    );

    // Add JWT interceptor FIRST (runs before logging)
    dio.interceptors.add(ApiInterceptor(_secureStorage));

    // Add logging interceptor in debug mode
    assert(() {
      dio.interceptors.add(
        LogInterceptor(
          requestHeader: true,
          requestBody: true,
          responseHeader: false,
          responseBody: true,
          error: true,
          logPrint: (obj) => print('🌐 $obj'),
        ),
      );
      return true;
    }());
  }

  // ── Convenience Methods ──

  /// GET request with optional query parameters.
  Future<Response> get(
    String path, {
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return dio.get(path, queryParameters: queryParameters, options: options);
  }

  /// POST request with body data.
  Future<Response> post(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return dio.post(path,
        data: data, queryParameters: queryParameters, options: options);
  }

  /// PUT request with body data.
  Future<Response> put(
    String path, {
    dynamic data,
    Options? options,
  }) {
    return dio.put(path, data: data, options: options);
  }

  /// DELETE request.
  Future<Response> delete(
    String path, {
    Options? options,
  }) {
    return dio.delete(path, options: options);
  }

  /// Download file (for PDF certificates/reports).
  Future<Response> download(
    String path,
    String savePath, {
    Map<String, dynamic>? queryParameters,
    Options? options,
  }) {
    return dio.download(
      path,
      savePath,
      queryParameters: queryParameters,
      options: options ?? Options(responseType: ResponseType.bytes),
    );
  }
}
