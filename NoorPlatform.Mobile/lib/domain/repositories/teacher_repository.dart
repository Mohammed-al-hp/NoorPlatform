import 'package:dartz/dartz.dart';
import '../../core/errors/failures.dart';
import '../../data/models/teacher_models.dart';

/// عمليات دور المحفّظ — كل نتيجة Either&lt;Failure, T&gt; مثل بقية المستودعات.
abstract class TeacherRepository {
  Future<Either<Failure, TeacherStatsModel>> getStats();
  Future<Either<Failure, List<ActivityModel>>> getActivities();
  Future<Either<Failure, List<LeaderboardEntryModel>>> getLeaderboard();

  Future<Either<Failure, List<CircleModel>>> getCircles();
  Future<Either<Failure, List<CircleStudentModel>>> getCircleStudents(int circleId);
  Future<Either<Failure, List<AttendanceRosterItem>>> getAttendanceRoster(
      int circleId, String date);
  Future<Either<Failure, String>> saveBulkAttendance({
    required String date,
    required int circleId,
    required List<Map<String, dynamic>> records,
  });
  Future<Either<Failure, String>> sendBulkAbsence(int circleId);
  Future<Either<Failure, String>> sendAbsenceNotification(int studentId);

  Future<Either<Failure, List<TeacherStudentModel>>> getStudents();
  Future<Either<Failure, StudentDetailsModel>> getStudent(int id);
  Future<Either<Failure, List<Map<String, dynamic>>>> getStudentAttendance(int id);
  Future<Either<Failure, CreateStudentResult>> createStudent(Map<String, dynamic> body);
  Future<Either<Failure, String>> updateStudent(int id, Map<String, dynamic> body);
  Future<Either<Failure, List<ParentLookupModel>>> getParentsLookup();

  Future<Either<Failure, List<WaitingEntryModel>>> getWaitingList();
  Future<Either<Failure, String>> saveWaitingEntry(int? id, Map<String, dynamic> body);
  Future<Either<Failure, String>> deleteWaitingEntry(int id);

  Future<Either<Failure, LastPositionModel>> getLastPosition(int studentId,
      {bool sequential = false});
  Future<Either<Failure, String>> addHifzRecord(Map<String, dynamic> body);
  Future<Either<Failure, String>> deleteHifzRecord(int id);
  Future<Either<Failure, String>> updateHifzRecord(int id, Map<String, dynamic> body);
  Future<Either<Failure, List<RecentHifzModel>>> getRecentHifz({int count = 20});
  Future<Either<Failure, String>> sendHifzPraise(Map<String, dynamic> body);
  Future<Either<Failure, String>> createOralExam(Map<String, dynamic> body);
  Future<Either<Failure, GradingDefaultsModel>> getGradingDefaults();
  Future<Either<Failure, MonthlyRatingsReportModel>> getMonthlyRatings({
    required int year,
    required int month,
    int? circleId,
  });
  Future<Either<Failure, MonthlyHifzRatingModel>> getStudentMonthlyRating(int studentId,
      {int? year, int? month});

  Future<Either<Failure, List<MatnOptionModel>>> getMatns();
  Future<Either<Failure, String>> addMatnRecord(Map<String, dynamic> body);
  Future<Either<Failure, String>> deleteMatnRecord(int id);
  Future<Either<Failure, String>> upsertMonthlyTarget(Map<String, dynamic> body);
  Future<Either<Failure, String>> syncMonthlyTargetFromOral(Map<String, dynamic> body);

  Future<Either<Failure, List<EvaluationPeriodModel>>> getPeriods();
  Future<Either<Failure, String>> createPeriod(Map<String, dynamic> body);
  Future<Either<Failure, List<PeriodEvaluationDraft>>> getPeriodEvaluations(int periodId);
  Future<Either<Failure, PeriodEvaluationDraft>> autoDraftEvaluation(
      int periodId, int studentId, String studentName);
  Future<Either<Failure, String>> savePeriodEvaluation(
      int periodId, Map<String, dynamic> body);

  Future<Either<Failure, List<DressRecordModel>>> getDress(String date);
  Future<Either<Failure, String>> saveDressBulk(
      String date, List<Map<String, dynamic>> records);
  Future<Either<Failure, String>> overridePrayer(int id, Map<String, dynamic> body);

  Future<Either<Failure, List<InboxMessageModel>>> getInbox();
  Future<Either<Failure, Unit>> markMessageRead(int id);
  Future<Either<Failure, String>> replyToMessage(int id, String content);
  Future<Either<Failure, List<ParentContactModel>>> getParentRecipients();
  Future<Either<Failure, String>> sendToParent(int parentUserId, String content);

  Future<Either<Failure, String>> createAnnouncement({
    required String title,
    required String content,
    required String target,
  });
  Future<Either<Failure, String>> uploadLibraryFile({
    required String title,
    String? description,
    required String category,
    int? circleId,
    required String fileName,
    String? filePath,
    List<int>? bytes,
  });
  Future<Either<Failure, String>> deleteLibraryItem(int id);
}
