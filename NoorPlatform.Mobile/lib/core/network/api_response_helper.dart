import 'package:dio/dio.dart';
import '../errors/exceptions.dart';

/// Shared helpers for remote datasources.
class ApiResponseHelper {
  ApiResponseHelper._();

  static String extractMessage(dynamic data, String fallback) {
    if (data is Map && data['message'] is String) {
      return data['message'] as String;
    }
    if (data is String && data.isNotEmpty && data.length <= 200 && !data.trimLeft().startsWith('<')) {
      return data;
    }
    return fallback;
  }

  static void ensureSuccess(Response response, {String fallback = 'حدث خطأ'}) {
    final code = response.statusCode ?? 0;
    if (code >= 200 && code < 300) return;

    throw ServerException(
      message: extractMessage(response.data, fallback),
      statusCode: code,
    );
  }

  static List<Map<String, dynamic>> asMapList(dynamic data) {
    if (data is! List) return const [];
    return data
        .whereType<Map>()
        .map((e) => Map<String, dynamic>.from(e))
        .toList();
  }
}

int toInt(dynamic v) {
  if (v == null) return 0;
  if (v is int) return v;
  if (v is double) return v.toInt();
  if (v is String) return int.tryParse(v) ?? 0;
  return 0;
}

double toDouble(dynamic v) {
  if (v == null) return 0.0;
  if (v is double) return v;
  if (v is int) return v.toDouble();
  if (v is String) return double.tryParse(v) ?? 0.0;
  return 0.0;
}

bool toBool(dynamic v) {
  if (v is bool) return v;
  if (v is num) return v != 0;
  if (v is String) {
    final s = v.toLowerCase();
    return s == 'true' || s == '1';
  }
  return false;
}

String toStr(dynamic v, [String fallback = '']) =>
    v?.toString() ?? fallback;
