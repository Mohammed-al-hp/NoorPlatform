import 'dart:typed_data';

import 'package:dio/dio.dart';

import '../../../config/api_config.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/api_response_helper.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../../core/utils/file_open_helper.dart';
import '../../models/announcement_models.dart';
import '../../models/hifz_models.dart';
import '../../models/library_models.dart';
import '../../models/message_models.dart';
import '../../models/oral_exam_models.dart';
import '../../models/pedagogical_models.dart';
import '../../models/parent_models.dart';

/// Consolidated remote datasource for Student/Parent portal features.
abstract class PortalRemoteDataSource {
  Future<PortalConfigModel> getPortalConfig();
  Future<List<EvaluationModel>> getMyEvaluations({int? studentId});
  Future<List<PrayerLogModel>> getMyPrayerLogs();
  Future<String?> submitMyPrayer({
    required String date,
    required List<PrayerEntryModel> prayers,
    String? studentNote,
  });
  Future<PrayerTimesModel> getPrayerTimes({String? date});
  Future<List<PrayerLogModel>> getPrayerByStudent(int studentId);
  Future<List<ParentHomeFeedbackModel>> getParentHome(int studentId);
  Future<void> submitParentHome({
    required int studentId,
    required String weekStartDate,
    required int rating,
    String? notes,
  });
  Future<List<MatnRecordModel>> getMatn(int studentId);
  Future<List<MonthlyTargetModel>> getMonthlyTargets(int studentId);
  Future<MyHifzResponse> getMyHifz();
  Future<List<HifzRecordModel>> getStudentHifz(int studentId);
  Future<List<SentMessageModel>> getSentMessages();
  Future<List<MessageRecipientModel>> getMessageRecipients();
  Future<void> sendMessage(SendMessageRequest request);
  Future<List<PaymentModel>> getParentPayments();
  Future<List<LibraryItemModel>> getLibrary({String? search, String? category});
  Future<String> downloadLibraryFile(int id, String title);
  Future<String> downloadCertificate(int studentId);
  Future<String> downloadMonthlyReport(int studentId, int year, int month);
  Future<List<AnnouncementModel>> getAnnouncements();
  Future<List<OralExamModel>> getOralExams(int studentId);
}

class PortalRemoteDataSourceImpl implements PortalRemoteDataSource {
  final ApiClient apiClient;

  PortalRemoteDataSourceImpl(this.apiClient);

  Future<T> _guard<T>(Future<T> Function() run, String fallback) async {
    try {
      return await run();
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: fallback);
    }
  }

  @override
  Future<PortalConfigModel> getPortalConfig() => _guard(() async {
        final res = await apiClient.get(ApiConfig.portalConfig);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل الإعدادات');
        return PortalConfigModel.fromJson(
          Map<String, dynamic>.from(res.data as Map),
        );
      }, 'فشل تحميل الإعدادات');

  @override
  Future<List<EvaluationModel>> getMyEvaluations({int? studentId}) =>
      _guard(() async {
        final res = await apiClient.get(
          ApiConfig.myEvaluations,
          queryParameters: studentId != null ? {'studentId': studentId} : null,
        );
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل التقييمات');
        return ApiResponseHelper.asMapList(res.data)
            .map(EvaluationModel.fromJson)
            .toList();
      }, 'فشل تحميل التقييمات');

  @override
  Future<List<PrayerLogModel>> getMyPrayerLogs() => _guard(() async {
        final res = await apiClient.get(ApiConfig.myPrayer);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل سجل الصلاة');
        return ApiResponseHelper.asMapList(res.data)
            .map(PrayerLogModel.fromJson)
            .toList();
      }, 'فشل تحميل سجل الصلاة');

  @override
  Future<String?> submitMyPrayer({
    required String date,
    required List<PrayerEntryModel> prayers,
    String? studentNote,
  }) =>
      _guard(() async {
        final res = await apiClient.post(
          ApiConfig.myPrayer,
          data: {
            'date': date,
            'prayers': prayers.map((p) => p.toJson()).toList(),
            if (studentNote != null) 'studentNote': studentNote,
          },
        );
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تسجيل الصلاة');
        final data = res.data;
        return data is Map ? data['message']?.toString() : null;
      }, 'فشل تسجيل الصلاة');

  @override
  Future<PrayerTimesModel> getPrayerTimes({String? date}) => _guard(() async {
        final res = await apiClient.get(
          ApiConfig.prayerTimes,
          queryParameters: date == null ? null : {'date': date},
        );
        ApiResponseHelper.ensureSuccess(res, fallback: 'تعذر تحميل مواقيت الصلاة');
        return PrayerTimesModel.fromJson(
          Map<String, dynamic>.from(res.data as Map),
        );
      }, 'تعذر تحميل مواقيت الصلاة');

  @override
  Future<List<PrayerLogModel>> getPrayerByStudent(int studentId) =>
      _guard(() async {
        final res = await apiClient.get(ApiConfig.prayerByStudent(studentId));
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل سجل الصلاة');
        return ApiResponseHelper.asMapList(res.data)
            .map(PrayerLogModel.fromJson)
            .toList();
      }, 'فشل تحميل سجل الصلاة');

  @override
  Future<List<ParentHomeFeedbackModel>> getParentHome(int studentId) =>
      _guard(() async {
        final res = await apiClient.get(ApiConfig.parentHome(studentId));
        ApiResponseHelper.ensureSuccess(
          res,
          fallback: 'فشل تحميل المتابعة المنزلية',
        );
        return ApiResponseHelper.asMapList(res.data)
            .map(ParentHomeFeedbackModel.fromJson)
            .toList();
      }, 'فشل تحميل المتابعة المنزلية');

  @override
  Future<void> submitParentHome({
    required int studentId,
    required String weekStartDate,
    required int rating,
    String? notes,
  }) =>
      _guard(() async {
        final res = await apiClient.post(
          ApiConfig.parentHomeSubmit,
          data: {
            'studentId': studentId,
            'weekStartDate': weekStartDate,
            'rating': rating,
            if (notes != null) 'notes': notes,
          },
        );
        ApiResponseHelper.ensureSuccess(
          res,
          fallback: 'فشل حفظ المتابعة المنزلية',
        );
      }, 'فشل حفظ المتابعة المنزلية');

  @override
  Future<List<MatnRecordModel>> getMatn(int studentId) => _guard(() async {
        final res = await apiClient.get(ApiConfig.matn(studentId));
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل سجل المتون');
        return ApiResponseHelper.asMapList(res.data)
            .map(MatnRecordModel.fromJson)
            .toList();
      }, 'فشل تحميل سجل المتون');

  @override
  Future<List<MonthlyTargetModel>> getMonthlyTargets(int studentId) =>
      _guard(() async {
        final res = await apiClient.get(ApiConfig.monthlyTargets(studentId));
        ApiResponseHelper.ensureSuccess(
          res,
          fallback: 'فشل تحميل الأهداف الشهرية',
        );
        return ApiResponseHelper.asMapList(res.data)
            .map(MonthlyTargetModel.fromJson)
            .toList();
      }, 'فشل تحميل الأهداف الشهرية');

  @override
  Future<MyHifzResponse> getMyHifz() => _guard(() async {
        final res = await apiClient.get(ApiConfig.myHifz);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل سجل الحفظ');
        return MyHifzResponse.fromJson(
          Map<String, dynamic>.from(res.data as Map),
        );
      }, 'فشل تحميل سجل الحفظ');

  @override
  Future<List<HifzRecordModel>> getStudentHifz(int studentId) =>
      _guard(() async {
        final res = await apiClient.get(ApiConfig.studentHifz(studentId));
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل سجل الحفظ');
        return ApiResponseHelper.asMapList(res.data)
            .map(HifzRecordModel.fromJson)
            .toList();
      }, 'فشل تحميل سجل الحفظ');

  @override
  Future<List<SentMessageModel>> getSentMessages() => _guard(() async {
        final res = await apiClient.get(ApiConfig.messagesSent);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل الرسائل');
        return ApiResponseHelper.asMapList(res.data)
            .map(SentMessageModel.fromJson)
            .toList();
      }, 'فشل تحميل الرسائل');

  @override
  Future<List<MessageRecipientModel>> getMessageRecipients() =>
      _guard(() async {
        final res = await apiClient.get(ApiConfig.messagesRecipients);
        ApiResponseHelper.ensureSuccess(
          res,
          fallback: 'فشل تحميل المستلمين',
        );
        return ApiResponseHelper.asMapList(res.data)
            .map(MessageRecipientModel.fromJson)
            .toList();
      }, 'فشل تحميل المستلمين');

  @override
  Future<void> sendMessage(SendMessageRequest request) => _guard(() async {
        final res = await apiClient.post(
          ApiConfig.messages,
          data: request.toJson(),
        );
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل إرسال الرسالة');
      }, 'فشل إرسال الرسالة');

  @override
  Future<List<PaymentModel>> getParentPayments() => _guard(() async {
        final res = await apiClient.get(ApiConfig.parentPayments);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل الرسوم');
        return ApiResponseHelper.asMapList(res.data)
            .map(PaymentModel.fromJson)
            .toList();
      }, 'فشل تحميل الرسوم');

  @override
  Future<List<LibraryItemModel>> getLibrary({
    String? search,
    String? category,
  }) =>
      _guard(() async {
        final res = await apiClient.get(
          ApiConfig.library,
          queryParameters: {
            if (search != null && search.isNotEmpty) 'search': search,
            if (category != null && category.isNotEmpty) 'category': category,
          },
        );
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل المكتبة');
        return ApiResponseHelper.asMapList(res.data)
            .map(LibraryItemModel.fromJson)
            .toList();
      }, 'فشل تحميل المكتبة');

  Future<String> _downloadPdf(String apiPath, String filename, String fallback) =>
      _guard(() async {
        final res = await apiClient.get(
          apiPath,
          options: Options(
            responseType: ResponseType.bytes,
            validateStatus: (s) => s != null && s >= 200 && s < 300,
          ),
        );
        if (res.statusCode == null ||
            res.statusCode! < 200 ||
            res.statusCode! >= 300) {
          throw mapDioErrorToException(
            DioException(
              requestOptions: res.requestOptions,
              response: res,
              type: DioExceptionType.badResponse,
            ),
            fallbackMessage: fallback,
          );
        }
        final raw = res.data;
        final bytes = raw is Uint8List
            ? raw
            : Uint8List.fromList(List<int>.from(raw as List));
        return FileOpenHelper.savePdf(bytes, filename);
      }, fallback);

  @override
  Future<String> downloadLibraryFile(int id, String title) {
    final safe = title.replaceAll(RegExp(r'[^\w\u0600-\u06FF\- ]'), '_');
    return _downloadPdf(
      ApiConfig.libraryFile(id),
      '$safe-$id.pdf',
      'فشل تنزيل الملف',
    );
  }

  @override
  Future<String> downloadCertificate(int studentId) => _downloadPdf(
        ApiConfig.certificate(studentId),
        'certificate-$studentId.pdf',
        'فشل تنزيل الشهادة',
      );

  @override
  Future<String> downloadMonthlyReport(int studentId, int year, int month) => _downloadPdf(
        '${ApiConfig.monthlyReport(studentId)}?year=$year&month=$month',
        'monthly-report-$studentId-$year-${month.toString().padLeft(2, '0')}.pdf',
        'فشل تنزيل التقرير الشهري',
      );

  @override
  Future<List<AnnouncementModel>> getAnnouncements() => _guard(() async {
        final res = await apiClient.get(ApiConfig.announcements);
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل الإعلانات');
        return ApiResponseHelper.asMapList(res.data)
            .map(AnnouncementModel.fromJson)
            .toList();
      }, 'فشل تحميل الإعلانات');

  @override
  Future<List<OralExamModel>> getOralExams(int studentId) => _guard(() async {
        final res = await apiClient.get(ApiConfig.oralExamsByStudent(studentId));
        ApiResponseHelper.ensureSuccess(res, fallback: 'فشل تحميل الاختبارات');
        return ApiResponseHelper.asMapList(res.data)
            .map(OralExamModel.fromJson)
            .toList();
      }, 'فشل تحميل الاختبارات');
}
