import 'dart:convert';

import 'package:hive_flutter/hive_flutter.dart';

/// نسخة محلية من استجابات القراءة (JSON خام) للرجوع إليها عند انقطاع الاتصال.
abstract class ReadCache {
  static const String boxName = 'teacher_offline_cache';

  Future<void> put(String key, dynamic data);
  Future<dynamic> get(String key);

  static String keyFor(String path, Map<String, dynamic>? query) {
    if (query == null || query.isEmpty) return path;
    final keys = query.keys.toList()..sort();
    return '$path?${keys.map((k) => '$k=${query[k]}').join('&')}';
  }
}

class HiveReadCache implements ReadCache {
  // صندوق غير مُنمَّط: LocalCache.clearAll يفتحه بنفس الطريقة عند تسجيل الخروج
  Box? _box;

  Future<Box> _open() async => _box ??= await Hive.openBox(ReadCache.boxName);

  @override
  Future<void> put(String key, dynamic data) async {
    try {
      await (await _open()).put(key, jsonEncode(data));
    } catch (_) {/* التخزين المؤقت لا يُفشل الطلب */}
  }

  @override
  Future<dynamic> get(String key) async {
    try {
      final raw = (await _open()).get(key);
      return raw is String ? jsonDecode(raw) : null;
    } catch (_) {
      return null;
    }
  }
}
