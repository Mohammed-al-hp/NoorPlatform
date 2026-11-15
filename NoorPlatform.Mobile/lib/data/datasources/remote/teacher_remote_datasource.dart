import 'package:dio/dio.dart';

import '../../../config/api_config.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/api_response_helper.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/teacher_models.dart';

/// مصدر بيانات دور المحفّظ — نفس الـ endpoints التي تستخدمها واجهة الويب.
/// الخادم يقيّد المحفّظ بطلاب حلقاته فقط في كل استدعاء.
abstract class TeacherRemoteDataSource {
  Future<TeacherStatsModel> getStats();
  Future<List<ActivityModel>> getActivities();
  Future<List<LeaderboardEntryModel>> getLeaderboard();

  Future<List<CircleModel>> getCircles();
  Future<List<CircleStudentModel>> getCircleStudents(int circleId);
  Future<List<AttendanceRosterItem>> getAttendanceRoster(int circleId, String date);
  Future<String> saveBulkAttendance({
    required String date,
    required int circleId,
    required List<Map<String, dynamic>> records,
  });
  Future<String> sendBulkAbsence(int circleId);
  Future<String> sendAbsenceNotification(int studentId);

  Future<List<TeacherStudentModel>> getStudents();
  Future<StudentDetailsModel> getStudent(int id);
  Future<List<Map<String, dynamic>>> getStudentAttendance(int id);
  Future<CreateStudentResult> createStudent(Map<String, dynamic> body);
  Future<String> updateStudent(int id, Map<String, dynamic> body);
  Future<List<ParentLookupModel>> getParentsLookup();

  Future<List<WaitingEntryModel>> getWaitingList();
  Future<String> saveWaitingEntry(int? id, Map<String, dynamic> body);
  Future<String> deleteWaitingEntry(int id);

  Future<LastPositionModel> getLastPosition(int studentId, {bool sequential = false});
  Future<String> addHifzRecord(Map<String, dynamic> body);
  Future<String> deleteHifzRecord(int id);
  Future<List<RecentHifzModel>> getRecentHifz({int count = 20});
  Future<String> sendHifzPraise(Map<String, dynamic> body);
  Future<String> createOralExam(Map<String, dynamic> body);
  Future<GradingDefaultsModel> getGradingDefaults();
  Future<MonthlyRatingsReportModel> getMonthlyRatings({
    required int year,
    required int month,
    int? circleId,
  });
  Future<MonthlyHifzRatingModel> getStudentMonthlyRating(int studentId,
      {int? year, int? month});

  Future<List<MatnOptionModel>> getMatns();
  Future<String> addMatnRecord(Map<String, dynamic> body);
  Future<String> deleteMatnRecord(int id);
  Future<String> upsertMonthlyTarget(Map<String, dynamic> body);
  Future<String> syncMonthlyTargetFromOral(Map<String, dynamic> body);

  Future<List<EvaluationPeriodModel>> getPeriods();
  Future<String> createPeriod(Map<String, dynamic> body);
  Future<List<PeriodEvaluationDraft>> getPeriodEvaluations(int periodId);
  Future<PeriodEvaluationDraft> autoDraftEvaluation(int periodId, int studentId, String studentName);
  Future<String> savePeriodEvaluation(int periodId, Map<String, dynamic> body);

  Future<List<DressRecordModel>> getDress(String date);
  Future<String> saveDressBulk(String date, List<Map<String, dynamic>> records);
  Future<String> overridePrayer(int id, Map<String, dynamic> body);

  Future<List<InboxMessageModel>> getInbox();
  Future<void> markMessageRead(int id);

  Future<String> createAnnouncement({
    required String title,
    required String content,
    required String target,
  });
  Future<String> uploadLibraryFile({
    required String title,
    String? description,
    required String category,
    int? circleId,
    required String fileName,
    String? filePath,
    List<int>? bytes,
  });
  Future<String> deleteLibraryItem(int id);
}

class TeacherRemoteDataSourceImpl implements TeacherRemoteDataSource {
  final ApiClient apiClient;

  TeacherRemoteDataSourceImpl(this.apiClient);

  Future<T> _guard<T>(Future<T> Function() run, String fallback) async {
    try {
      return await run();
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: fallback);
    }
  }

  String _msg(Response res, String fallback) =>
      ApiResponseHelper.extractMessage(res.data, fallback);

  Future<List<T>> _getList<T>(
    String path,
    T Function(Map<String, dynamic>) parse,
    String fallback, {
    Map<String, dynamic>? query,
  }) =>
      _guard(() async {
        final res = await apiClient.get(path, queryParameters: query);
        ApiResponseHelper.ensureSuccess(res, fallback: fallback);
        return ApiResponseHelper.asMapList(res.data).map(parse).toList();
      }, fallback);

  Future<Map<String, dynamic>> _getMap(String path, String fallback,
          {Map<String, dynamic>? query}) =>
      _guard(() async {
        final res = await apiClient.get(path, queryParameters: query);
        ApiResponseHelper.ensureSuccess(res, fallback: fallback);
        return Map<String, dynamic>.from(res.data as Map);
      }, fallback);

  Future<String> _send(
    String method,
    String path,
    String success,
    String fallback, {
    dynamic data,
  }) =>
      _guard(() async {
        final Response res;
        switch (method) {
          case 'PUT':
            res = await apiClient.put(path, data: data);
            break;
          case 'DELETE':
            res = await apiClient.delete(path);
            break;
          case 'PATCH':
            res = await apiClient.dio.patch(path, data: data);
            break;
          default:
            res = await apiClient.post(path, data: data);
        }
        ApiResponseHelper.ensureSuccess(res, fallback: fallback);
        return _msg(res, success);
      }, fallback);

  // ── لوحة المحفّظ ──
  @override
  Future<TeacherStatsModel> getStats() async => TeacherStatsModel.fromJson(
      await _getMap(ApiConfig.dashboardStats, 'فشل تحميل الإحصائيات'));

  @override
  Future<List<ActivityModel>> getActivities() => _getList(
      ApiConfig.dashboardActivities, ActivityModel.fromJson, 'فشل تحميل النشاطات');

  @override
  Future<List<LeaderboardEntryModel>> getLeaderboard() => _getList(
      ApiConfig.dashboardLeaderboard,
      LeaderboardEntryModel.fromJson,
      'فشل تحميل لوحة الشرف');

  // ── الحلقات والحضور ──
  @override
  Future<List<CircleModel>> getCircles() =>
      _getList(ApiConfig.circles, CircleModel.fromJson, 'فشل تحميل الحلقات');

  @override
  Future<List<CircleStudentModel>> getCircleStudents(int circleId) async {
    final data = await _getMap(ApiConfig.circle(circleId), 'فشل تحميل طلاب الحلقة');
    return ApiResponseHelper.asMapList(data['students'])
        .map(CircleStudentModel.fromJson)
        .toList();
  }

  @override
  Future<List<AttendanceRosterItem>> getAttendanceRoster(int circleId, String date) =>
      _getList(
        ApiConfig.attendanceByCircle(circleId),
        AttendanceRosterItem.fromJson,
        'فشل تحميل سجل الحضور',
        query: {'date': date},
      );

  @override
  Future<String> saveBulkAttendance({
    required String date,
    required int circleId,
    required List<Map<String, dynamic>> records,
  }) =>
      _send('POST', ApiConfig.attendanceBulk, 'تم حفظ سجل الحضور بنجاح',
          'فشل حفظ الحضور',
          data: {'date': date, 'circleId': circleId, 'records': records});

  @override
  Future<String> sendBulkAbsence(int circleId) => _send(
      'POST', ApiConfig.notifyBulkAbsence, 'تم إرسال الإشعارات', 'تعذر إرسال إشعارات الغياب',
      data: {'circleId': circleId});

  @override
  Future<String> sendAbsenceNotification(int studentId) => _send(
      'POST', ApiConfig.notifyAbsence, 'تم إرسال إشعار الغياب', 'تعذر إرسال إشعار الغياب',
      data: {'studentId': studentId});

  // ── الطلاب ──
  @override
  Future<List<TeacherStudentModel>> getStudents() =>
      _getList(ApiConfig.students, TeacherStudentModel.fromJson, 'فشل تحميل الطلاب');

  @override
  Future<StudentDetailsModel> getStudent(int id) async => StudentDetailsModel.fromJson(
      await _getMap(ApiConfig.student(id), 'فشل تحميل بيانات الطالب'));

  @override
  Future<List<Map<String, dynamic>>> getStudentAttendance(int id) =>
      _getList(ApiConfig.studentAttendance(id), (m) => m, 'فشل تحميل الحضور');

  @override
  Future<CreateStudentResult> createStudent(Map<String, dynamic> body) =>
      _guard(() async {
        final res = await apiClient.post(ApiConfig.students, data: body);
        final data = res.data;
        // 409: الطالب سابق مؤرشف — الاستعادة من صلاحيات الإدارة
        if (res.statusCode == 409 && data is Map && data['code'] == 'ARCHIVED_STUDENT_FOUND') {
          final s = data['archivedStudent'] is Map
              ? Map<String, dynamic>.from(data['archivedStudent'] as Map)
              : <String, dynamic>{};
          throw ArchivedStudentException(
            message: toStr(data['message'], 'هذا طالب سابق مؤرشف'),
            canRestore: toBool(data['canRestore']),
            archivedStudentId: toInt(s['id']),
            archivedName: toStr(s['fullName']),
          );
        }
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل إضافة الطالب');
        return CreateStudentResult.fromJson(Map<String, dynamic>.from(data as Map));
      }, 'فشل إضافة الطالب');

  @override
  Future<String> updateStudent(int id, Map<String, dynamic> body) => _send(
      'PUT', ApiConfig.student(id), 'تم تحديث بيانات الطالب', 'فشل تحديث الطالب',
      data: body);

  @override
  Future<List<ParentLookupModel>> getParentsLookup() => _getList(
      ApiConfig.parentsLookup, ParentLookupModel.fromJson, 'فشل تحميل أولياء الأمور');

  // ── قائمة الانتظار ──
  @override
  Future<List<WaitingEntryModel>> getWaitingList() => _getList(
      ApiConfig.waitingList, WaitingEntryModel.fromJson, 'فشل تحميل قائمة الانتظار');

  @override
  Future<String> saveWaitingEntry(int? id, Map<String, dynamic> body) => id == null
      ? _send('POST', ApiConfig.waitingList, 'تمت الإضافة لقائمة الانتظار', 'فشل الحفظ',
          data: body)
      : _send('PUT', ApiConfig.waitingEntry(id), 'تم التحديث', 'فشل الحفظ', data: body);

  @override
  Future<String> deleteWaitingEntry(int id) =>
      _send('DELETE', ApiConfig.waitingEntry(id), 'تم الحذف', 'فشل الحذف');

  // ── الحفظ والمراجعة ──
  @override
  Future<LastPositionModel> getLastPosition(int studentId, {bool sequential = false}) async =>
      LastPositionModel.fromJson(await _getMap(
        ApiConfig.hifzLastPosition(studentId),
        'تعذر تحميل آخر موضع',
        query: sequential ? {'mode': 'Sequential'} : null,
      ));

  @override
  Future<String> addHifzRecord(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.hifz, 'تم حفظ الجلسة بنجاح', 'فشل حفظ الجلسة',
      data: body);

  @override
  Future<String> deleteHifzRecord(int id) =>
      _send('DELETE', ApiConfig.hifzRecord(id), 'تم حذف السجل', 'فشل حذف السجل');

  @override
  Future<List<RecentHifzModel>> getRecentHifz({int count = 20}) => _getList(
        ApiConfig.hifzRecent,
        RecentHifzModel.fromJson,
        'فشل تحميل آخر الجلسات',
        query: {'count': count},
      );

  @override
  Future<String> sendHifzPraise(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.notifyHifzPraise, 'تم إرسال رسالة المديح لولي الأمر',
      'تعذر إرسال الرسالة',
      data: body);

  @override
  Future<String> createOralExam(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.oralExams, 'تم حفظ اختبار الأسئلة بنجاح', 'فشل حفظ الاختبار',
      data: body);

  @override
  Future<GradingDefaultsModel> getGradingDefaults() async => GradingDefaultsModel.fromJson(
      await _getMap(ApiConfig.gradingDefaults, 'تعذر تحميل الإعدادات'));

  @override
  Future<MonthlyRatingsReportModel> getMonthlyRatings({
    required int year,
    required int month,
    int? circleId,
  }) async =>
      MonthlyRatingsReportModel.fromJson(await _getMap(
        ApiConfig.monthlyRatings,
        'فشل تحميل التقييم الشهري',
        query: {
          'year': year,
          'month': month,
          if (circleId != null) 'circleId': circleId,
        },
      ));

  @override
  Future<MonthlyHifzRatingModel> getStudentMonthlyRating(int studentId,
          {int? year, int? month}) async =>
      MonthlyHifzRatingModel.fromJson(await _getMap(
        ApiConfig.studentMonthlyRating(studentId),
        'فشل تحميل التقييم الشهري',
        query: {
          if (year != null) 'year': year,
          if (month != null) 'month': month,
        },
      ));

  // ── التقييم التربوي ──
  @override
  Future<List<MatnOptionModel>> getMatns() =>
      _getList(ApiConfig.matns, MatnOptionModel.fromJson, 'فشل تحميل المتون');

  @override
  Future<String> addMatnRecord(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.matnRecords, 'تم حفظ سجل المتن', 'فشل حفظ سجل المتن',
      data: body);

  @override
  Future<String> deleteMatnRecord(int id) =>
      _send('DELETE', ApiConfig.matnRecord(id), 'تم الحذف', 'فشل الحذف');

  @override
  Future<String> upsertMonthlyTarget(Map<String, dynamic> body) => _send(
      'PUT', ApiConfig.monthlyTargetsBase, 'تم حفظ الهدف الشهري', 'فشل حفظ الهدف',
      data: body);

  @override
  Future<String> syncMonthlyTargetFromOral(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.monthlyTargetsSync, 'تمت المزامنة من الاختبارات', 'فشل المزامنة',
      data: body);

  @override
  Future<List<EvaluationPeriodModel>> getPeriods() => _getList(
      ApiConfig.periods, EvaluationPeriodModel.fromJson, 'فشل تحميل فترات التقييم');

  @override
  Future<String> createPeriod(Map<String, dynamic> body) => _send(
      'POST', ApiConfig.periods, 'تم إنشاء فترة التقييم', 'فشل إنشاء الفترة',
      data: body);

  @override
  Future<List<PeriodEvaluationDraft>> getPeriodEvaluations(int periodId) => _getList(
        ApiConfig.periodEvaluations(periodId),
        (m) => PeriodEvaluationDraft.fromJson(m),
        'فشل تحميل التقييمات',
      );

  @override
  Future<PeriodEvaluationDraft> autoDraftEvaluation(
          int periodId, int studentId, String studentName) =>
      _guard(() async {
        final res = await apiClient.post(ApiConfig.periodAutoDraft(periodId, studentId));
        ApiResponseHelper.ensureSuccess(res, fallback: 'تعذر حساب المسودة');
        return PeriodEvaluationDraft.fromJson(
          {'studentId': studentId, ...Map<String, dynamic>.from(res.data as Map)},
          saved: false,
          studentName: studentName,
        );
      }, 'تعذر حساب المسودة');

  @override
  Future<String> savePeriodEvaluation(int periodId, Map<String, dynamic> body) => _send(
      'POST', ApiConfig.periodEvaluations(periodId), 'تم حفظ التقييم', 'فشل حفظ التقييم',
      data: body);

  @override
  Future<List<DressRecordModel>> getDress(String date) => _getList(
        ApiConfig.dress,
        DressRecordModel.fromJson,
        'فشل تحميل سجل اللباس',
        query: {'date': date},
      );

  @override
  Future<String> saveDressBulk(String date, List<Map<String, dynamic>> records) => _send(
      'POST', ApiConfig.dressBulk, 'تم حفظ سجل اللباس', 'فشل حفظ سجل اللباس',
      data: {'date': date, 'records': records});

  @override
  Future<String> overridePrayer(int id, Map<String, dynamic> body) => _send(
      'PUT', ApiConfig.prayerOverride(id), 'تم تعديل سجل الصلاة', 'فشل تعديل السجل',
      data: body);

  // ── الرسائل ──
  @override
  Future<List<InboxMessageModel>> getInbox() =>
      _getList(ApiConfig.messagesInbox, InboxMessageModel.fromJson, 'فشل تحميل الرسائل');

  @override
  Future<void> markMessageRead(int id) =>
      _send('PATCH', ApiConfig.messageRead(id), 'تم', 'تعذر تحديث الرسالة');

  // ── الإعلانات والمكتبة ──
  @override
  Future<String> createAnnouncement({
    required String title,
    required String content,
    required String target,
  }) =>
      _send('POST', ApiConfig.announcements, 'تم نشر الإعلان', 'فشل نشر الإعلان',
          data: {'title': title, 'content': content, 'target': target});

  @override
  Future<String> uploadLibraryFile({
    required String title,
    String? description,
    required String category,
    int? circleId,
    required String fileName,
    String? filePath,
    List<int>? bytes,
  }) =>
      _guard(() async {
        final MultipartFile file = bytes != null
            ? MultipartFile.fromBytes(bytes,
                filename: fileName, contentType: DioMediaType('application', 'pdf'))
            : await MultipartFile.fromFile(filePath!,
                filename: fileName, contentType: DioMediaType('application', 'pdf'));
        final form = FormData.fromMap({
          'title': title,
          if (description != null && description.isNotEmpty) 'description': description,
          'category': category,
          if (circleId != null) 'circleId': circleId,
          'file': file,
        });
        final res = await apiClient.post(ApiConfig.libraryUpload, data: form);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل رفع الملف');
        return _msg(res, 'تم رفع الملف');
      }, 'فشل رفع الملف');

  @override
  Future<String> deleteLibraryItem(int id) =>
      _send('DELETE', ApiConfig.libraryItem(id), 'تم حذف الملف', 'فشل حذف الملف');
}
