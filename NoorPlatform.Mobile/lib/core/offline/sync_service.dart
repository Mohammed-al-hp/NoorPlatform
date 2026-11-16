import 'dart:async';
import 'dart:convert';

import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import 'package:hive_flutter/hive_flutter.dart';

import '../network/api_client.dart';
import '../network/network_info.dart';
import '../storage/secure_storage.dart';
import 'offline_queue.dart';

/// صندوق Hive مشفّر بـ AES-256؛ المفتاح محفوظ في flutter_secure_storage (Keystore/Keychain).
class HiveOfflineStore implements OfflineStore {
  static const String boxName = 'noor_offline_queue';
  final SecureStorage _secure;
  Box<String>? _box;

  HiveOfflineStore(this._secure);

  Future<Box<String>> _open() async {
    if (_box != null) return _box!;
    var key = await _secure.readOfflineKey();
    if (key == null) {
      key = base64Encode(Hive.generateSecureKey());
      await _secure.writeOfflineKey(key);
    }
    _box = await Hive.openBox<String>(boxName, encryptionCipher: HiveAesCipher(base64Decode(key)));
    return _box!;
  }

  @override
  Future<List<String>> readAll() async => (await _open()).values.toList();
  @override
  Future<void> put(String id, String json) async => (await _open()).put(id, json);
  @override
  Future<void> delete(String id) async => (await _open()).delete(id);
}

/// يرفع طابور العمليات تلقائياً عند عودة الاتصال، وعند فتح التطبيق، وكل دقيقة ما دام هناك معلّق.
class SyncService {
  final OfflineQueue queue;
  final ApiClient api;
  final NetworkInfo network;
  final SecureStorage secure;

  StreamSubscription? _sub;
  Timer? _timer;

  /// آخر رسالة مزامنة لعرضها للمستخدم (مثلاً «تمت مزامنة 3 سجلات»).
  final ValueNotifier<String?> lastMessage = ValueNotifier(null);

  SyncService({required this.queue, required this.api, required this.network, required this.secure});

  Future<void> start() async {
    await queue.load();
    _sub ??= network.onConnectivityChanged.listen((results) {
      if (!results.every((r) => r.name == 'none')) {
        // مهلة قصيرة حتى تستقر الشبكة
        Future.delayed(const Duration(seconds: 2), syncNow);
      }
    });
    _timer ??= Timer.periodic(const Duration(minutes: 1), (_) {
      if (queue.pendingCount.value > 0) syncNow();
    });
    unawaited(syncNow());
  }

  Future<int?> _currentUserId() async {
    final u = await secure.getUserJson();
    return u?['id'] as int?;
  }

  Future<FlushReport?> syncNow() async {
    if (queue.pendingCount.value == 0) return null;
    if (!await network.isConnected) return null;
    final token = await secure.getToken();
    if (token == null || token.isEmpty) return null;

    final report = await queue.flush(_send, currentUserId: await _currentUserId());
    if (report.sent > 0 || report.rejected > 0) {
      final parts = <String>[];
      if (report.sent > 0) parts.add('تمت مزامنة ${report.sent} سجل');
      if (report.rejected > 0) parts.add('رفض الخادم ${report.rejected} سجل');
      lastMessage.value = parts.join('، ');
    }
    return report;
  }

  /// يصنّف نتيجة الإرسال: 2xx نجاح، 4xx رفض نهائي (عدا 401/408/429)، وغير ذلك إعادة لاحقاً.
  Future<SendResult> _send(PendingOperation op) async {
    try {
      final res = await api.dio.request(op.path, data: op.body, options: Options(method: op.method));
      final code = res.statusCode ?? 0;
      if (code >= 200 && code < 300) return const SendResult(SendOutcome.success);
      // تغيير كلمة المرور المؤقتة مطلوب: ليست رفضاً للعملية نفسها — تُعاد بعد التغيير
      if (code == 403 && res.data is Map && (res.data as Map)['code'] == 'MUST_CHANGE_PASSWORD') {
        return const SendResult(SendOutcome.retryLater, 'يجب تغيير كلمة المرور أولاً');
      }
      final msg = res.data is Map && (res.data as Map)['message'] is String
          ? (res.data as Map)['message'] as String
          : 'رفض الخادم العملية';
      return classifyStatus(code, msg);
    } on DioException catch (e) {
      final code = e.response?.statusCode;
      if (code != null) return classifyStatus(code, 'تعذر رفع العملية');
      return const SendResult(SendOutcome.retryLater, 'لا يوجد اتصال');
    } catch (_) {
      return const SendResult(SendOutcome.retryLater, 'خطأ غير متوقع');
    }
  }

  @visibleForTesting
  static SendResult classifyStatus(int code, String message) {
    if (code >= 200 && code < 300) return const SendResult(SendOutcome.success);
    if (code == 401 || code == 408 || code == 429 || code >= 500) {
      return SendResult(SendOutcome.retryLater, message);
    }
    return SendResult(SendOutcome.rejected, message);
  }

  void dispose() {
    _sub?.cancel();
    _timer?.cancel();
  }
}
