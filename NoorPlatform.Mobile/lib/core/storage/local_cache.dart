import 'dart:convert';
import 'package:hive_flutter/hive_flutter.dart';

/// TTL-aware Hive cache manager for offline-first data access.
class LocalCache {
  /// Initialize Hive for Flutter.
  static Future<void> init() async {
    await Hive.initFlutter();
  }

  /// Get cached data with TTL validation.
  /// Returns null if cache is expired or missing.
  static Future<Map<String, dynamic>?> get(
    String boxName,
    String key, {
    int ttlMinutes = 5,
  }) async {
    final box = await Hive.openBox(boxName);
    final raw = box.get(key);
    if (raw == null) return null;

    try {
      final cached = jsonDecode(raw as String) as Map<String, dynamic>;
      final timestamp = cached['_cached_at'] as int?;
      if (timestamp == null) return null;

      final cachedAt = DateTime.fromMillisecondsSinceEpoch(timestamp);
      final now = DateTime.now();
      if (now.difference(cachedAt).inMinutes > ttlMinutes) {
        // Cache expired — remove it
        await box.delete(key);
        return null;
      }

      return cached['data'] as Map<String, dynamic>?;
    } catch (_) {
      await box.delete(key);
      return null;
    }
  }

  /// Get cached list data with TTL validation.
  static Future<List<dynamic>?> getList(
    String boxName,
    String key, {
    int ttlMinutes = 5,
  }) async {
    final box = await Hive.openBox(boxName);
    final raw = box.get(key);
    if (raw == null) return null;

    try {
      final cached = jsonDecode(raw as String) as Map<String, dynamic>;
      final timestamp = cached['_cached_at'] as int?;
      if (timestamp == null) return null;

      final cachedAt = DateTime.fromMillisecondsSinceEpoch(timestamp);
      if (DateTime.now().difference(cachedAt).inMinutes > ttlMinutes) {
        await box.delete(key);
        return null;
      }

      return cached['data'] as List<dynamic>?;
    } catch (_) {
      await box.delete(key);
      return null;
    }
  }

  /// Store data with automatic timestamp for TTL tracking.
  static Future<void> put(
    String boxName,
    String key,
    dynamic data,
  ) async {
    final box = await Hive.openBox(boxName);
    final wrapper = {
      '_cached_at': DateTime.now().millisecondsSinceEpoch,
      'data': data,
    };
    await box.put(key, jsonEncode(wrapper));
  }

  /// Remove a specific cache entry.
  static Future<void> remove(String boxName, String key) async {
    final box = await Hive.openBox(boxName);
    await box.delete(key);
  }

  /// Clear an entire cache box.
  static Future<void> clearBox(String boxName) async {
    final box = await Hive.openBox(boxName);
    await box.clear();
  }

  /// Clear all cache boxes (used on logout).
  static Future<void> clearAll() async {
    final boxNames = [
      'student_cache',
      'parent_cache',
      'hifz_cache',
      'attendance_cache',
      'library_cache',
      'payments_cache',
      'user_box',
    ];
    for (final name in boxNames) {
      try {
        final box = await Hive.openBox(name);
        await box.clear();
      } catch (_) {
        // Box may not exist yet, ignore
      }
    }
  }
}
