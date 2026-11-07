import 'package:dio/dio.dart';
import '../errors/exceptions.dart';

/// يحوّل DioException إلى ServerException برسالة عربية مناسبة لكل statusCode.
/// يُستخدَم في كل RemoteDataSource بدل تكرار نفس منطق try/catch في كل ملف.
///
/// الفرق المهم عن المعالجة السابقة: 401 كان يُترجَم لرسالة عامة "فشل الاتصال
/// بالخادم" في Parent/StudentRemoteDataSource رغم أن السبب الفعلي هو انتهاء
/// الجلسة (والـ ApiInterceptor يتعامل مع التوجيه فعلياً عبر AuthEventBus،
/// لكن رسالة الخطأ المعروضة في نفس اللحظة كانت مضلّلة قبل وصول إعادة التوجيه).
ServerException mapDioErrorToException(DioException e, {String? fallbackMessage}) {
  final statusCode = e.response?.statusCode;

  String extractServerMessage(String defaultMsg) {
    final data = e.response?.data;
    if (data is Map<String, dynamic> && data['message'] is String) {
      return data['message'] as String;
    }
    if (data is String && data.isNotEmpty) {
      return data;
    }
    return defaultMsg;
  }

  switch (statusCode) {
    case 401:
      // الـ ApiInterceptor سيتعامل مع إعادة التوجيه عبر AuthEventBus،
      // هذه الرسالة تُعرض فقط للحظة القصيرة قبل اكتمال إعادة التوجيه.
      return const ServerException(
        message: 'انتهت صلاحية الجلسة، جارٍ إعادة تسجيل الدخول...',
        statusCode: 401,
      );
    case 403:
      return ServerException(
        message: extractServerMessage('ليس لديك صلاحية الوصول لهذه البيانات'),
        statusCode: 403,
      );
    case 404:
      return ServerException(
        message: extractServerMessage('البيانات المطلوبة غير موجودة'),
        statusCode: 404,
      );
    case 400:
      return ServerException(
        message: extractServerMessage('بيانات غير صحيحة'),
        statusCode: 400,
      );
    case 503:
      return const ServerException(
        message: 'الخدمة غير متاحة حالياً، حاول مرة أخرى لاحقاً',
        statusCode: 503,
      );
    default:
      if (e.type == DioExceptionType.connectionTimeout ||
          e.type == DioExceptionType.receiveTimeout ||
          e.type == DioExceptionType.sendTimeout) {
        return const ServerException(
          message: 'انتهت مهلة الاتصال بالخادم، تحقق من جودة الشبكة',
        );
      }
      if (e.type == DioExceptionType.connectionError) {
        return const ServerException(
          message: 'تعذّر الاتصال بالخادم، تحقق من اتصالك بالإنترنت',
        );
      }
      return ServerException(
        message: extractServerMessage(fallbackMessage ?? 'حدث خطأ غير متوقع'),
        statusCode: statusCode,
      );
  }
}
