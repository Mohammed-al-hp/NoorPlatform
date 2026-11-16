import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../../config/constants.dart';

/// Secure wrapper around flutter_secure_storage for JWT & user caching.
class SecureStorage {
  final FlutterSecureStorage _storage;

  SecureStorage()
      : _storage = const FlutterSecureStorage(
          aOptions: AndroidOptions(encryptedSharedPreferences: true),
          iOptions: IOSOptions(
            accessibility: KeychainAccessibility.first_unlock,
          ),
        );

  Future<void> saveToken(String token) async {
    await _storage.write(key: AppConstants.tokenKey, value: token);
  }

  Future<String?> getToken() async {
    return await _storage.read(key: AppConstants.tokenKey);
  }

  Future<void> deleteToken() async {
    await _storage.delete(key: AppConstants.tokenKey);
  }

  Future<void> saveUserJson(Map<String, dynamic> userMap) async {
    await _storage.write(
      key: AppConstants.userKey,
      value: jsonEncode(userMap),
    );
  }

  Future<Map<String, dynamic>?> getUserJson() async {
    final raw = await _storage.read(key: AppConstants.userKey);
    if (raw == null) return null;
    try {
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }

  Future<void> setMustChangePassword(bool value) async {
    await _storage.write(
      key: AppConstants.mustChangePasswordKey,
      value: value ? '1' : '0',
    );
  }

  Future<bool> getMustChangePassword() async {
    final v = await _storage.read(key: AppConstants.mustChangePasswordKey);
    return v == '1';
  }

  Future<void> saveCachedStudentId(int studentId) async {
    await _storage.write(
      key: AppConstants.cachedStudentIdKey,
      value: studentId.toString(),
    );
  }

  Future<int?> getCachedStudentId() async {
    final v = await _storage.read(key: AppConstants.cachedStudentIdKey);
    if (v == null) return null;
    return int.tryParse(v);
  }

  /// يمسح بيانات الجلسة مع الإبقاء على مفتاح تشفير طابور العمل دون اتصال،
  /// حتى لا تضيع سجلات لم تُرفع بعد إذا سجّل المحفّظ الخروج قبل المزامنة.
  Future<void> clearAll() async {
    final offlineKey = await _storage.read(key: _offlineKeyName);
    await _storage.deleteAll();
    if (offlineKey != null) {
      await _storage.write(key: _offlineKeyName, value: offlineKey);
    }
  }

  static const String _offlineKeyName = 'noor_offline_queue_key';

  /// مفتاح AES (32 بايت، base64) لتشفير صندوق Hive الخاص بالعمليات المؤجلة.
  Future<String?> readOfflineKey() => _storage.read(key: _offlineKeyName);
  Future<void> writeOfflineKey(String value) =>
      _storage.write(key: _offlineKeyName, value: value);

  Future<bool> hasToken() async {
    final token = await getToken();
    return token != null && token.isNotEmpty;
  }
}
