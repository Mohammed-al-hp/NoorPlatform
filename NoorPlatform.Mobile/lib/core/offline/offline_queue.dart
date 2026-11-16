import 'dart:convert';
import 'dart:math';

import 'package:flutter/foundation.dart';

/// نوع العملية المؤجلة.
enum PendingKind { attendanceBulk, hifz }

/// عملية كتبها المحفّظ دون اتصال وتنتظر الرفع إلى الخادم.
class PendingOperation {
  final String id;
  final PendingKind kind;
  final String method;
  final String path;
  final Map<String, dynamic> body;

  /// وصف مختصر بالعربية يظهر في قائمة «بانتظار المزامنة».
  final String label;

  /// المستخدم الذي أنشأ العملية — لا تُرفع إلا بتوكن نفس المستخدم.
  final int? ownerUserId;
  final DateTime createdAt;
  final int attempts;
  final String? lastError;

  /// رفضها الخادم نهائياً (4xx) — تبقى للعرض حتى يحذفها المحفّظ.
  final bool rejected;

  const PendingOperation({
    required this.id,
    required this.kind,
    required this.method,
    required this.path,
    required this.body,
    required this.label,
    required this.createdAt,
    this.ownerUserId,
    this.attempts = 0,
    this.lastError,
    this.rejected = false,
  });

  PendingOperation copyWith({int? attempts, String? lastError, bool? rejected}) => PendingOperation(
        id: id,
        kind: kind,
        method: method,
        path: path,
        body: body,
        label: label,
        createdAt: createdAt,
        ownerUserId: ownerUserId,
        attempts: attempts ?? this.attempts,
        lastError: lastError ?? this.lastError,
        rejected: rejected ?? this.rejected,
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'kind': kind.name,
        'method': method,
        'path': path,
        'body': body,
        'label': label,
        'ownerUserId': ownerUserId,
        'createdAt': createdAt.toIso8601String(),
        'attempts': attempts,
        'lastError': lastError,
        'rejected': rejected,
      };

  factory PendingOperation.fromJson(Map<String, dynamic> j) => PendingOperation(
        id: j['id'] as String,
        kind: PendingKind.values.firstWhere((k) => k.name == j['kind'], orElse: () => PendingKind.hifz),
        method: (j['method'] as String?) ?? 'POST',
        path: j['path'] as String,
        body: Map<String, dynamic>.from(j['body'] as Map),
        label: (j['label'] as String?) ?? '',
        ownerUserId: j['ownerUserId'] as int?,
        createdAt: DateTime.tryParse(j['createdAt'] as String? ?? '') ?? DateTime.now(),
        attempts: (j['attempts'] as int?) ?? 0,
        lastError: j['lastError'] as String?,
        rejected: (j['rejected'] as bool?) ?? false,
      );
}

/// تخزين دائم للعمليات (Hive مشفّر في التطبيق، وذاكرة في الاختبارات).
abstract class OfflineStore {
  Future<List<String>> readAll();
  Future<void> put(String id, String json);
  Future<void> delete(String id);
}

class MemoryOfflineStore implements OfflineStore {
  final Map<String, String> data = {};
  @override
  Future<List<String>> readAll() async => data.values.toList();
  @override
  Future<void> put(String id, String json) async => data[id] = json;
  @override
  Future<void> delete(String id) async => data.remove(id);
}

/// نتيجة إرسال عملية واحدة.
enum SendOutcome { success, retryLater, rejected }

class SendResult {
  final SendOutcome outcome;
  final String? message;
  const SendResult(this.outcome, [this.message]);
}

class FlushReport {
  final int sent;
  final int rejected;
  final int remaining;
  const FlushReport(this.sent, this.rejected, this.remaining);
}

/// طابور العمليات المؤجلة: إضافة، عرض العدد للواجهة، ورفع مرتّب (الأقدم أولاً)
/// يتوقف عند أول انقطاع حتى لا تُرسل عمليات لاحقة قبل سابقتها.
class OfflineQueue {
  final OfflineStore _store;
  final List<PendingOperation> _items = [];
  bool _loaded = false;
  bool _flushing = false;

  /// عدد العمليات المنتظرة (غير المرفوضة) — تستمع له شارة المزامنة.
  final ValueNotifier<int> pendingCount = ValueNotifier(0);

  /// هل يجري الرفع الآن؟
  final ValueNotifier<bool> syncing = ValueNotifier(false);

  OfflineQueue(this._store);

  List<PendingOperation> get items => List.unmodifiable(_items);

  Future<void> load() async {
    if (_loaded) return;
    for (final raw in await _store.readAll()) {
      try {
        _items.add(PendingOperation.fromJson(jsonDecode(raw) as Map<String, dynamic>));
      } catch (_) {/* سجل تالف — يُتجاهل */}
    }
    _items.sort((a, b) => a.createdAt.compareTo(b.createdAt));
    _loaded = true;
    _notify();
  }

  static String newId() {
    final r = Random.secure();
    return List.generate(16, (_) => r.nextInt(256).toRadixString(16).padLeft(2, '0')).join();
  }

  Future<PendingOperation> enqueue({
    required PendingKind kind,
    required String path,
    required Map<String, dynamic> body,
    required String label,
    int? ownerUserId,
    String method = 'POST',
    String? id,
  }) async {
    await load();
    final op = PendingOperation(
      id: id ?? newId(),
      kind: kind,
      method: method,
      path: path,
      body: body,
      label: label,
      ownerUserId: ownerUserId,
      createdAt: DateTime.now(),
    );
    // حضور نفس الحلقة واليوم: الأحدث يحلّ محل الأقدم (الخادم يعتمد آخر حالة أصلاً)
    if (kind == PendingKind.attendanceBulk) {
      final dup = _items.where((o) => o.kind == kind && !o.rejected &&
          o.body['date'] == body['date'] && o.body['circleId'] == body['circleId']).toList();
      for (final d in dup) {
        _items.remove(d);
        await _store.delete(d.id);
      }
    }
    _items.add(op);
    await _store.put(op.id, jsonEncode(op.toJson()));
    _notify();
    return op;
  }

  Future<void> remove(String id) async {
    _items.removeWhere((o) => o.id == id);
    await _store.delete(id);
    _notify();
  }

  /// يرفع العمليات بالترتيب. [currentUserId] يمنع رفع عمليات مستخدم آخر بتوكن الحالي.
  Future<FlushReport> flush(Future<SendResult> Function(PendingOperation op) send, {int? currentUserId}) async {
    await load();
    if (_flushing) return FlushReport(0, 0, pendingCount.value);
    _flushing = true;
    syncing.value = true;
    var sent = 0, rejected = 0;
    try {
      for (final op in List<PendingOperation>.from(_items)) {
        if (op.rejected) continue;
        if (op.ownerUserId != null && currentUserId != null && op.ownerUserId != currentUserId) continue;
        final r = await send(op);
        if (r.outcome == SendOutcome.success) {
          await remove(op.id);
          sent++;
        } else if (r.outcome == SendOutcome.rejected) {
          await _replace(op.copyWith(attempts: op.attempts + 1, lastError: r.message, rejected: true));
          rejected++;
        } else {
          await _replace(op.copyWith(attempts: op.attempts + 1, lastError: r.message));
          break; // لا اتصال — نتوقف ونحاول لاحقاً بنفس الترتيب
        }
      }
    } finally {
      _flushing = false;
      syncing.value = false;
      _notify();
    }
    return FlushReport(sent, rejected, pendingCount.value);
  }

  Future<void> _replace(PendingOperation op) async {
    final i = _items.indexWhere((o) => o.id == op.id);
    if (i >= 0) _items[i] = op;
    await _store.put(op.id, jsonEncode(op.toJson()));
  }

  void _notify() => pendingCount.value = _items.where((o) => !o.rejected).length;
}
