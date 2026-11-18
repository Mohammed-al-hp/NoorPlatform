import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:hive_flutter/hive_flutter.dart';
import '../../config/constants.dart';

/// Secure wrapper around flutter_secure_storage for JWT & user caching.
///
/// على الويب يعتمد flutter_secure_storage على واجهة التشفير في المتصفح (crypto.subtle)،
/// وهي غير متاحة إلا في سياق آمن (HTTPS أو localhost). عند فتح التطبيق عبر
/// عنوان IP بـ http من هاتف على الشبكة المحلية يفشل الحفظ، فننتقل إلى صندوق Hive عادي.
/// على الويب لا يختلف مستوى الحماية فعلياً: المكتبة نفسها تحفظ مفتاحها في localStorage.
class SecureStorage {
  final FlutterSecureStorage _storage;

  static const _webFallbackBox = 'web_session';
  bool _useWebFallback = false;

  SecureStorage()
      : _storage = const FlutterSecureStorage(
          aOptions: AndroidOptions(encryptedSharedPreferences: true),
          iOptions: IOSOptions(
            accessibility: KeychainAccessibility.first_unlock,
          ),
        );

  Future<Box> _fallbackBox() => Hive.openBox(_webFallbackBox);

  /// ينفّذ العملية عبر flutter_secure_storage، وعلى الويب ينتقل إلى Hive إن فشلت.
  Future<T> _guard<T>(
    Future<T> Function() secure,
    Future<T> Function(Box box) fallback,
  ) async {
    if (kIsWeb && _useWebFallback) return fallback(await _fallbackBox());
    try {
      return await secure();
    } catch (e) {
      if (!kIsWeb) rethrow;
      debugPrint('SecureStorage: web crypto unavailable, using Hive fallback ($e)');
      _useWebFallback = true;
      return fallback(await _fallbackBox());
    }
  }

  Future<String?> _read(String key) => _guard(
        () => _storage.read(key: key),
        (box) async => box.get(key) as String?,
      );

  Future<void> _write(String key, String value) => _guard(
        () => _storage.write(key: key, value: value),
        (box) => box.put(key, value),
      );

  Future<void> _delete(String key) => _guard(
        () => _storage.delete(key: key),
        (box) => box.delete(key),
      );

  Future<void> _deleteAll() => _guard(
        () => _storage.deleteAll(),
        (box) async {
          await box.clear();
        },
      );

  Future<void> saveToken(String token) => _write(AppConstants.tokenKey, token);

  Future<String?> getToken() => _read(AppConstants.tokenKey);

  Future<void> deleteToken() => _delete(AppConstants.tokenKey);

  Future<void> saveUserJson(Map<String, dynamic> userMap) =>
      _write(AppConstants.userKey, jsonEncode(userMap));

  Future<Map<String, dynamic>?> getUserJson() async {
    final raw = await _read(AppConstants.userKey);
    if (raw == null) return null;
    try {
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }

  Future<void> setMustChangePassword(bool value) =>
      _write(AppConstants.mustChangePasswordKey, value ? '1' : '0');

  Future<bool> getMustChangePassword() async {
    final v = await _read(AppConstants.mustChangePasswordKey);
    return v == '1';
  }

  Future<void> saveCachedStudentId(int studentId) =>
      _write(AppConstants.cachedStudentIdKey, studentId.toString());

  Future<int?> getCachedStudentId() async {
    final v = await _read(AppConstants.cachedStudentIdKey);
    if (v == null) return null;
    return int.tryParse(v);
  }

  /// يمسح بيانات الجلسة مع الإبقاء على مفتاح تشفير طابور العمل دون اتصال،
  /// حتى لا تضيع سجلات لم تُرفع بعد إذا سجّل المحفّظ الخروج قبل المزامنة.
  Future<void> clearAll() async {
    final offlineKey = await _read(_offlineKeyName);
    await _deleteAll();
    if (offlineKey != null) {
      await _write(_offlineKeyName, offlineKey);
    }
  }

  static const String _offlineKeyName = 'noor_offline_queue_key';

  /// مفتاح AES (32 بايت، base64) لتشفير صندوق Hive الخاص بالعمليات المؤجلة.
  Future<String?> readOfflineKey() => _read(_offlineKeyName);
  Future<void> writeOfflineKey(String value) => _write(_offlineKeyName, value);

  Future<bool> hasToken() async {
    final token = await getToken();
    return token != null && token.isNotEmpty;
  }
}
