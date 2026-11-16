// Custom exceptions for the data layer.

class ServerException implements Exception {
  final String message;
  final int? statusCode;

  /// الطلب لم يصل للخادم (انقطاع/مهلة) — يمكن حفظ العملية محلياً وإعادة إرسالها لاحقاً.
  final bool isConnectivity;

  const ServerException({required this.message, this.statusCode, this.isConnectivity = false});

  @override
  String toString() => 'ServerException($statusCode): $message';
}

class CacheException implements Exception {
  final String message;
  const CacheException({this.message = 'خطأ في الذاكرة المؤقتة'});
}

class AuthException implements Exception {
  final String message;
  const AuthException({this.message = 'خطأ في المصادقة'});
}

class NetworkException implements Exception {
  final String message;
  const NetworkException({this.message = 'لا يوجد اتصال بالإنترنت'});
}
