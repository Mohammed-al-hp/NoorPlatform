import 'package:flutter/foundation.dart';
import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../config/api_config.dart';
import '../../../core/network/network_info.dart';
import '../../../core/offline/offline_queue.dart';
import '../../../core/storage/secure_storage.dart';
import '../../../domain/repositories/teacher_repository.dart';
import '../datasources/remote/teacher_remote_datasource.dart';
import '../models/teacher_models.dart';

class TeacherRepositoryImpl implements TeacherRepository {
  final TeacherRemoteDataSource remote;
  final NetworkInfo networkInfo;

  /// طابور العمل دون اتصال (الحضور والتسميع) — اختياري حتى تبقى الاختبارات بسيطة.
  final OfflineQueue? offlineQueue;
  final SecureStorage? secureStorage;

  TeacherRepositoryImpl({
    required this.remote,
    required this.networkInfo,
    this.offlineQueue,
    this.secureStorage,
  });

  static const String offlineSavedMessage =
      'تم الحفظ على الهاتف دون اتصال — سيُرفع تلقائياً عند عودة الإنترنت';

  /// ينفّذ عملية كتابة؛ إن لم يتوفر اتصال (أو انقطع أثناء الإرسال) تُحفظ في الطابور المحلي وتُعتبر ناجحة.
  Future<Either<Failure, String>> _writeOrQueue({
    required PendingKind kind,
    required String path,
    required Map<String, dynamic> body,
    required String label,
    required Future<String> Function() action,
  }) async {
    final queue = offlineQueue;
    Future<Either<Failure, String>> enqueue() async {
      final owner = (await secureStorage?.getUserJson())?['id'] as int?;
      await queue!.enqueue(kind: kind, path: path, body: body, label: label, ownerUserId: owner);
      return const Right(offlineSavedMessage);
    }

    if (queue != null && !await networkInfo.isConnected) return enqueue();
    try {
      return Right(await action());
    } on ServerException catch (e) {
      if (queue != null && e.isConnectivity) return enqueue();
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      if (queue != null) return enqueue();
      return Left(NetworkFailure(e.message));
    } catch (e) {
      debugPrint('repository error: $e');
      return const Left(ServerFailure('حدث خطأ غير متوقع، حاول مرة أخرى'));
    }
  }

  Future<Either<Failure, T>> _run<T>(Future<T> Function() action) async {
    // مع وجود النسخة المحلية نحاول حتى دون اتصال: القراءات تعود من الذاكرة المحلية
    if (offlineQueue == null && !await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }
    try {
      return Right(await action());
    } on ArchivedStudentException catch (e) {
      return Left(ArchivedStudentFailure(
        e.message,
        canRestore: e.canRestore,
        archivedStudentId: e.archivedStudentId,
        archivedName: e.archivedName,
      ));
    } on ServerException catch (e) {
      if (e.isConnectivity) return Left(NetworkFailure(e.message));
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      return Left(NetworkFailure(e.message));
    } catch (e) {
      debugPrint('repository error: $e');
      return const Left(ServerFailure('حدث خطأ غير متوقع، حاول مرة أخرى'));
    }
  }

  @override
  Future<Either<Failure, TeacherStatsModel>> getStats() => _run(remote.getStats);

  @override
  Future<Either<Failure, List<ActivityModel>>> getActivities() => _run(remote.getActivities);

  @override
  Future<Either<Failure, List<LeaderboardEntryModel>>> getLeaderboard() =>
      _run(remote.getLeaderboard);

  @override
  Future<Either<Failure, List<CircleModel>>> getCircles() => _run(remote.getCircles);

  @override
  Future<Either<Failure, List<CircleStudentModel>>> getCircleStudents(int circleId) =>
      _run(() => remote.getCircleStudents(circleId));

  @override
  Future<Either<Failure, List<AttendanceRosterItem>>> getAttendanceRoster(
          int circleId, String date) =>
      _run(() => remote.getAttendanceRoster(circleId, date));

  @override
  Future<Either<Failure, String>> saveBulkAttendance({
    required String date,
    required int circleId,
    required List<Map<String, dynamic>> records,
  }) =>
      _writeOrQueue(
        kind: PendingKind.attendanceBulk,
        path: ApiConfig.attendanceBulk,
        body: {'date': date, 'circleId': circleId, 'records': records},
        label: 'حضور ${records.length} طالب — $date',
        action: () => remote.saveBulkAttendance(date: date, circleId: circleId, records: records),
      );

  @override
  Future<Either<Failure, String>> sendBulkAbsence(int circleId) =>
      _run(() => remote.sendBulkAbsence(circleId));

  @override
  Future<Either<Failure, String>> sendAbsenceNotification(int studentId) =>
      _run(() => remote.sendAbsenceNotification(studentId));

  @override
  Future<Either<Failure, List<TeacherStudentModel>>> getStudents() => _run(remote.getStudents);

  @override
  Future<Either<Failure, StudentDetailsModel>> getStudent(int id) =>
      _run(() => remote.getStudent(id));

  @override
  Future<Either<Failure, List<Map<String, dynamic>>>> getStudentAttendance(int id) =>
      _run(() => remote.getStudentAttendance(id));

  @override
  Future<Either<Failure, CreateStudentResult>> createStudent(Map<String, dynamic> body) =>
      _run(() => remote.createStudent(body));

  @override
  Future<Either<Failure, String>> updateStudent(int id, Map<String, dynamic> body) =>
      _run(() => remote.updateStudent(id, body));

  @override
  Future<Either<Failure, List<ParentLookupModel>>> getParentsLookup() =>
      _run(remote.getParentsLookup);

  @override
  Future<Either<Failure, List<WaitingEntryModel>>> getWaitingList() =>
      _run(remote.getWaitingList);

  @override
  Future<Either<Failure, String>> saveWaitingEntry(int? id, Map<String, dynamic> body) =>
      _run(() => remote.saveWaitingEntry(id, body));

  @override
  Future<Either<Failure, String>> deleteWaitingEntry(int id) =>
      _run(() => remote.deleteWaitingEntry(id));

  @override
  Future<Either<Failure, LastPositionModel>> getLastPosition(int studentId,
          {bool sequential = false}) =>
      _run(() => remote.getLastPosition(studentId, sequential: sequential));

  @override
  Future<Either<Failure, String>> addHifzRecord(Map<String, dynamic> body) {
    // معرّف ثابت للجلسة: إعادة الإرسال (بعد انقطاع أثناء الطلب) لا تكرر الجلسة على الخادم
    final withId = {...body, 'clientRequestId': body['clientRequestId'] ?? OfflineQueue.newId()};
    final kind = withId['type'] == 'Revision' ? 'مراجعة' : 'تسميع';
    final name = withId['studentName'] ?? 'طالب';
    return _writeOrQueue(
      kind: PendingKind.hifz,
      path: ApiConfig.hifz,
      body: withId..remove('studentName'),
      label: '$kind ${withId['surahName'] ?? ''} (${withId['verses'] ?? ''}) — $name',
      action: () => remote.addHifzRecord(withId),
    );
  }

  @override
  Future<Either<Failure, String>> deleteHifzRecord(int id) =>
      _run(() => remote.deleteHifzRecord(id));

  @override
  Future<Either<Failure, String>> updateHifzRecord(int id, Map<String, dynamic> body) =>
      _run(() => remote.updateHifzRecord(id, body));

  @override
  Future<Either<Failure, List<RecentHifzModel>>> getRecentHifz({int count = 20}) =>
      _run(() => remote.getRecentHifz(count: count));

  @override
  Future<Either<Failure, String>> sendHifzPraise(Map<String, dynamic> body) =>
      _run(() => remote.sendHifzPraise(body));

  @override
  Future<Either<Failure, String>> createOralExam(Map<String, dynamic> body) =>
      _run(() => remote.createOralExam(body));

  @override
  Future<Either<Failure, GradingDefaultsModel>> getGradingDefaults() =>
      _run(remote.getGradingDefaults);

  @override
  Future<Either<Failure, MonthlyRatingsReportModel>> getMonthlyRatings({
    required int year,
    required int month,
    int? circleId,
  }) =>
      _run(() => remote.getMonthlyRatings(year: year, month: month, circleId: circleId));

  @override
  Future<Either<Failure, MonthlyHifzRatingModel>> getStudentMonthlyRating(int studentId,
          {int? year, int? month}) =>
      _run(() => remote.getStudentMonthlyRating(studentId, year: year, month: month));

  @override
  Future<Either<Failure, List<MatnOptionModel>>> getMatns() => _run(remote.getMatns);

  @override
  Future<Either<Failure, String>> addMatnRecord(Map<String, dynamic> body) =>
      _run(() => remote.addMatnRecord(body));

  @override
  Future<Either<Failure, String>> deleteMatnRecord(int id) =>
      _run(() => remote.deleteMatnRecord(id));

  @override
  Future<Either<Failure, String>> upsertMonthlyTarget(Map<String, dynamic> body) =>
      _run(() => remote.upsertMonthlyTarget(body));

  @override
  Future<Either<Failure, String>> syncMonthlyTargetFromOral(Map<String, dynamic> body) =>
      _run(() => remote.syncMonthlyTargetFromOral(body));

  @override
  Future<Either<Failure, List<EvaluationPeriodModel>>> getPeriods() => _run(remote.getPeriods);

  @override
  Future<Either<Failure, String>> createPeriod(Map<String, dynamic> body) =>
      _run(() => remote.createPeriod(body));

  @override
  Future<Either<Failure, List<PeriodEvaluationDraft>>> getPeriodEvaluations(int periodId) =>
      _run(() => remote.getPeriodEvaluations(periodId));

  @override
  Future<Either<Failure, PeriodEvaluationDraft>> autoDraftEvaluation(
          int periodId, int studentId, String studentName) =>
      _run(() => remote.autoDraftEvaluation(periodId, studentId, studentName));

  @override
  Future<Either<Failure, String>> savePeriodEvaluation(
          int periodId, Map<String, dynamic> body) =>
      _run(() => remote.savePeriodEvaluation(periodId, body));

  @override
  Future<Either<Failure, List<DressRecordModel>>> getDress(String date) =>
      _run(() => remote.getDress(date));

  @override
  Future<Either<Failure, String>> saveDressBulk(
          String date, List<Map<String, dynamic>> records) =>
      _run(() => remote.saveDressBulk(date, records));

  @override
  Future<Either<Failure, String>> overridePrayer(int id, Map<String, dynamic> body) =>
      _run(() => remote.overridePrayer(id, body));

  @override
  Future<Either<Failure, List<InboxMessageModel>>> getInbox() => _run(remote.getInbox);

  @override
  Future<Either<Failure, Unit>> markMessageRead(int id) => _run(() async {
        await remote.markMessageRead(id);
        return unit;
      });

  @override
  Future<Either<Failure, String>> replyToMessage(int id, String content) =>
      _run(() => remote.replyToMessage(id, content));

  @override
  Future<Either<Failure, List<ParentContactModel>>> getParentRecipients() =>
      _run(remote.getParentRecipients);

  @override
  Future<Either<Failure, String>> sendToParent(int parentUserId, String content) =>
      _run(() => remote.sendToParent(parentUserId, content));

  @override
  Future<Either<Failure, String>> createAnnouncement({
    required String title,
    required String content,
    required String target,
  }) =>
      _run(() => remote.createAnnouncement(title: title, content: content, target: target));

  @override
  Future<Either<Failure, String>> uploadLibraryFile({
    required String title,
    String? description,
    required String category,
    int? circleId,
    required String fileName,
    String? filePath,
    List<int>? bytes,
  }) =>
      _run(() => remote.uploadLibraryFile(
            title: title,
            description: description,
            category: category,
            circleId: circleId,
            fileName: fileName,
            filePath: filePath,
            bytes: bytes,
          ));

  @override
  Future<Either<Failure, String>> deleteLibraryItem(int id) =>
      _run(() => remote.deleteLibraryItem(id));
}
