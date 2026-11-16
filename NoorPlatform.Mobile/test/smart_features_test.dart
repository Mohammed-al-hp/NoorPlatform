import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:noor_mobile/core/offline/offline_queue.dart';
import 'package:noor_mobile/core/offline/read_cache.dart';
import 'package:noor_mobile/core/offline/sync_service.dart';
import 'package:noor_mobile/data/models/smart_models.dart';
import 'package:noor_mobile/presentation/widgets/mastery_widgets.dart';

void main() {
  group('OfflineQueue', () {
    late MemoryOfflineStore store;
    late OfflineQueue queue;

    setUp(() {
      store = MemoryOfflineStore();
      queue = OfflineQueue(store);
    });

    Future<void> addHifz(String surah) => queue.enqueue(
          kind: PendingKind.hifz,
          path: '/api/hifz',
          body: {'surahName': surah, 'clientRequestId': surah},
          label: surah,
          ownerUserId: 7,
        );

    test('enqueue persists and updates pending count', () async {
      await addHifz('الملك');
      await addHifz('القلم');
      expect(queue.pendingCount.value, 2);
      expect(store.data.length, 2);

      // طابور جديد على نفس التخزين يستعيد العمليات (بعد إغلاق التطبيق)
      final reopened = OfflineQueue(store);
      await reopened.load();
      expect(reopened.items.map((o) => o.label), ['الملك', 'القلم']);
    });

    test('flush sends in order and removes successes', () async {
      await addHifz('الملك');
      await addHifz('القلم');
      final sent = <String>[];
      final report = await queue.flush((op) async {
        sent.add(op.label);
        return const SendResult(SendOutcome.success);
      }, currentUserId: 7);
      expect(sent, ['الملك', 'القلم']);
      expect(report.sent, 2);
      expect(queue.pendingCount.value, 0);
      expect(store.data, isEmpty);
    });

    test('flush stops at first connectivity failure and keeps order', () async {
      await addHifz('الملك');
      await addHifz('القلم');
      final sent = <String>[];
      await queue.flush((op) async {
        sent.add(op.label);
        return const SendResult(SendOutcome.retryLater, 'لا يوجد اتصال');
      }, currentUserId: 7);
      expect(sent, ['الملك'], reason: 'لا تُرسل العملية اللاحقة قبل السابقة');
      expect(queue.pendingCount.value, 2);
      expect(queue.items.first.attempts, 1);
    });

    test('rejected operations are kept but not counted as pending', () async {
      await addHifz('الملك');
      await queue.flush((_) async => const SendResult(SendOutcome.rejected, 'بيانات غير صحيحة'), currentUserId: 7);
      expect(queue.pendingCount.value, 0);
      expect(queue.items.single.rejected, isTrue);
      expect(queue.items.single.lastError, 'بيانات غير صحيحة');
    });

    test('operations of another user are not sent with the current token', () async {
      await addHifz('الملك');
      var calls = 0;
      await queue.flush((_) async {
        calls++;
        return const SendResult(SendOutcome.success);
      }, currentUserId: 99);
      expect(calls, 0);
      expect(queue.pendingCount.value, 1);
    });

    test('newer attendance for the same circle and day replaces the older one', () async {
      Future<void> att(String status) => queue.enqueue(
            kind: PendingKind.attendanceBulk,
            path: '/api/attendance/bulk',
            body: {
              'date': '2026-11-16',
              'circleId': 3,
              'records': [
                {'studentId': 1, 'status': status}
              ]
            },
            label: 'حضور',
          );
      await att('Present');
      await att('UnexcusedAbsence');
      expect(queue.pendingCount.value, 1);
      expect((queue.items.single.body['records'] as List).first['status'], 'UnexcusedAbsence');
    });
  });

  group('SyncService.classifyStatus', () {
    test('2xx success, 4xx rejected, auth/server errors retried', () {
      expect(SyncService.classifyStatus(200, '').outcome, SendOutcome.success);
      expect(SyncService.classifyStatus(400, 'x').outcome, SendOutcome.rejected);
      expect(SyncService.classifyStatus(403, 'x').outcome, SendOutcome.rejected);
      expect(SyncService.classifyStatus(401, 'x').outcome, SendOutcome.retryLater);
      expect(SyncService.classifyStatus(429, 'x').outcome, SendOutcome.retryLater);
      expect(SyncService.classifyStatus(503, 'x').outcome, SendOutcome.retryLater);
    });
  });

  test('ReadCache.keyFor is stable regardless of query order', () {
    expect(ReadCache.keyFor('/api/x', {'b': 2, 'a': 1}), ReadCache.keyFor('/api/x', {'a': 1, 'b': 2}));
    expect(ReadCache.keyFor('/api/x', null), '/api/x');
  });

  group('heatColor', () {
    test('full mastery is the brand emerald and fades with retention', () {
      expect(heatColor(100), const Color(0xFF1A5C3A));
      expect(heatColor(0), const Color(0xFFF6EBD0));
      final strong = heatColor(90).computeLuminance();
      final weak = heatColor(30).computeLuminance();
      expect(weak, greaterThan(strong), reason: 'اللون يبهت كلما قلّ الثبات');
      expect(heatTextColor(90), Colors.white);
    });
  });

  group('smart models', () {
    test('HeatmapModel parses surahs and juz', () {
      final m = HeatmapModel.fromJson({
        'memorizedCount': 1,
        'averageRetention': 72.5,
        'surahs': [
          {'number': 67, 'name': 'الملك', 'juz': 29, 'memorized': true, 'retention': 72.5, 'level': 'fading', 'daysSinceReview': 9, 'dueDate': '2000-01-01T00:00:00'},
          {'number': 68, 'name': 'القلم', 'juz': 29, 'memorized': false, 'level': 'none'},
        ],
        'juz': [
          {'juz': 29, 'surahCount': 11, 'memorizedCount': 1, 'retention': 72.5}
        ],
      });
      expect(m.surahs.first.isDue, isTrue);
      expect(m.surahs.last.memorized, isFalse);
      expect(m.dueCount, 1);
      expect(m.juz.single.memorizedCount, 1);
    });

    test('ReviewScheduleModel and ServerNotification parse', () {
      final r = ReviewScheduleModel.fromJson({
        'totalMemorized': 3,
        'dueCount': 1,
        'averageRetention': 80,
        'due': [
          {'number': 78, 'name': 'النبأ', 'juz': 30, 'retention': 40, 'overdueDays': 12}
        ],
        'upcoming': [],
      });
      expect(r.due.single.memorized, isTrue);
      expect(r.due.single.overdueDays, 12);

      final n = ServerNotification.fromJson({
        'id': 5, 'kind': 'Absence', 'title': 'تنبيه غياب', 'body': '...', 'isRead': false,
        'createdAt': '2026-11-16T08:00:00', 'studentId': 2,
      });
      expect(n.kind, 'Absence');
      expect(n.studentId, 2);
    });
  });
}
