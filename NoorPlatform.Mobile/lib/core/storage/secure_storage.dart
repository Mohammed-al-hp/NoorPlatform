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

  Future<void> clearAll() async {
    await _storage.deleteAll();
  }

  Future<bool> hasToken() async {
    final token = await getToken();
    return token != null && token.isNotEmpty;
  }
}
