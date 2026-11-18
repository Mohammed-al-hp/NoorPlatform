import 'package:dartz/dartz.dart';
import '../../core/errors/failures.dart';
import '../../data/models/announcement_models.dart';
import '../../data/models/hifz_models.dart';
import '../../data/models/library_models.dart';
import '../../data/models/message_models.dart';
import '../../data/models/oral_exam_models.dart';
import '../../data/models/pedagogical_models.dart';
import '../../data/models/parent_models.dart';

abstract class PortalRepository {
  Future<Either<Failure, PortalConfigModel>> getPortalConfig();
  Future<Either<Failure, List<EvaluationModel>>> getMyEvaluations({
    int? studentId,
  });
  Future<Either<Failure, List<PrayerLogModel>>> getMyPrayerLogs();
  /// يعيد رسالة الخادم (مثل «تم تسجيل صلاة الظهر»).
  Future<Either<Failure, String?>> submitMyPrayer({
    required String date,
    required List<PrayerEntryModel> prayers,
    String? studentNote,
  });
  Future<Either<Failure, PrayerTimesModel>> getPrayerTimes({String? date});
  Future<Either<Failure, List<PrayerLogModel>>> getPrayerByStudent(
    int studentId,
  );
  Future<Either<Failure, List<ParentHomeFeedbackModel>>> getParentHome(
    int studentId,
  );
  Future<Either<Failure, Unit>> submitParentHome({
    required int studentId,
    required String weekStartDate,
    required int rating,
    String? notes,
  });
  Future<Either<Failure, List<MatnRecordModel>>> getMatn(int studentId);
  Future<Either<Failure, List<MonthlyTargetModel>>> getMonthlyTargets(
    int studentId,
  );
  Future<Either<Failure, MyHifzResponse>> getMyHifz();
  Future<Either<Failure, List<HifzRecordModel>>> getStudentHifz(int studentId);
  Future<Either<Failure, List<SentMessageModel>>> getSentMessages();
  Future<Either<Failure, List<MessageRecipientModel>>> getMessageRecipients();
  Future<Either<Failure, Unit>> sendMessage(SendMessageRequest request);
  Future<Either<Failure, Unit>> replyToThread(int threadId, String content);
  Future<Either<Failure, List<PaymentModel>>> getParentPayments();
  Future<Either<Failure, List<LibraryItemModel>>> getLibrary({
    String? search,
    String? category,
  });
  Future<Either<Failure, String>> downloadLibraryFile(int id, String title);
  Future<Either<Failure, String>> downloadCertificate(int studentId);
  Future<Either<Failure, String>> downloadMonthlyReport(int studentId, int year, int month);
  Future<Either<Failure, List<AnnouncementModel>>> getAnnouncements();
  Future<Either<Failure, List<OralExamModel>>> getOralExams(int studentId);
}
