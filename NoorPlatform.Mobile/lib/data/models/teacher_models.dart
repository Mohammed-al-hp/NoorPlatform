import '../../core/network/api_response_helper.dart';
import 'monthly_hifz_rating_model.dart';

export 'monthly_hifz_rating_model.dart';

/// نماذج واجهة المحفّظ — مطابقة لأشكال JSON الفعلية في NoorPlatform.Api.

Map<String, dynamic> _map(dynamic v) =>
    v is Map ? Map<String, dynamic>.from(v) : <String, dynamic>{};

List<Map<String, dynamic>> _list(dynamic v) => ApiResponseHelper.asMapList(v);

/// GET /api/dashboard/stats
class TeacherStatsModel {
  final int students;
  final int studentsThisMonth;
  final int circles;
  final String attendanceToday;
  final int attendanceLast30;
  final double hifzVelocity;
  final List<WeeklyAttendanceDay> weeklyAttendance;
  final List<RecentHifzModel> recentHifz;
  final int levelAdvanced;
  final int levelIntermediate;
  final int levelBeginner;

  const TeacherStatsModel({
    required this.students,
    required this.studentsThisMonth,
    required this.circles,
    required this.attendanceToday,
    required this.attendanceLast30,
    required this.hifzVelocity,
    required this.weeklyAttendance,
    required this.recentHifz,
    required this.levelAdvanced,
    required this.levelIntermediate,
    required this.levelBeginner,
  });

  factory TeacherStatsModel.fromJson(Map<String, dynamic> j) {
    final levels = _map(j['levelDistribution']);
    return TeacherStatsModel(
      students: toInt(j['students']),
      studentsThisMonth: toInt(j['studentsThisMonth']),
      circles: toInt(j['circles']),
      attendanceToday: toStr(j['attendanceToday'], '0%'),
      attendanceLast30: toInt(j['attendanceLast30']),
      hifzVelocity: toDouble(j['hifzVelocity']),
      weeklyAttendance:
          _list(j['weeklyAttendance']).map(WeeklyAttendanceDay.fromJson).toList(),
      recentHifz: _list(j['recentHifz']).map(RecentHifzModel.fromJson).toList(),
      levelAdvanced: toInt(levels['advanced']),
      levelIntermediate: toInt(levels['intermediate']),
      levelBeginner: toInt(levels['beginner']),
    );
  }
}

class WeeklyAttendanceDay {
  final String dayName;
  final String date;
  final int percentage;
  final int total;

  const WeeklyAttendanceDay({
    required this.dayName,
    required this.date,
    required this.percentage,
    required this.total,
  });

  factory WeeklyAttendanceDay.fromJson(Map<String, dynamic> j) =>
      WeeklyAttendanceDay(
        dayName: toStr(j['dayName']),
        date: toStr(j['date']),
        percentage: toInt(j['percentage']),
        total: toInt(j['total']),
      );
}

/// عنصر من /api/hifz/recent أو recentHifz في الإحصائيات.
class RecentHifzModel {
  final int id;
  final int studentId;
  final String studentName;
  final String circleName;
  final String surahName;
  final String verses;
  final String type;
  final String evaluation;
  final String date;

  const RecentHifzModel({
    required this.id,
    required this.studentId,
    required this.studentName,
    required this.circleName,
    required this.surahName,
    required this.verses,
    required this.type,
    required this.evaluation,
    required this.date,
  });

  factory RecentHifzModel.fromJson(Map<String, dynamic> j) => RecentHifzModel(
        id: toInt(j['id']),
        studentId: toInt(j['studentId']),
        studentName: toStr(j['studentName']),
        circleName: toStr(j['circleName']),
        surahName: toStr(j['surahName']),
        verses: toStr(j['verses']),
        type: toStr(j['type'], 'Memorization'),
        evaluation: toStr(j['evaluation']),
        date: toStr(j['date']),
      );
}

/// GET /api/dashboard/activities
class ActivityModel {
  final int id;
  final String userName;
  final String description;
  final String icon;
  final String createdAt;

  const ActivityModel({
    required this.id,
    required this.userName,
    required this.description,
    required this.icon,
    required this.createdAt,
  });

  factory ActivityModel.fromJson(Map<String, dynamic> j) => ActivityModel(
        id: toInt(j['id']),
        userName: toStr(j['userName']),
        description: toStr(j['description']),
        icon: toStr(j['icon']),
        createdAt: toStr(j['createdAt']),
      );
}

/// GET /api/dashboard/leaderboard
class LeaderboardEntryModel {
  final int rank;
  final int studentId;
  final String fullName;
  final String circleName;
  final int points;
  final double attendanceRate;
  final int hifzProgress;

  const LeaderboardEntryModel({
    required this.rank,
    required this.studentId,
    required this.fullName,
    required this.circleName,
    required this.points,
    required this.attendanceRate,
    required this.hifzProgress,
  });

  factory LeaderboardEntryModel.fromJson(Map<String, dynamic> j) =>
      LeaderboardEntryModel(
        rank: toInt(j['rank']),
        studentId: toInt(j['studentId']),
        fullName: toStr(j['fullName']),
        circleName: toStr(j['circleName']),
        points: toInt(j['points']),
        attendanceRate: toDouble(j['attendanceRate']),
        hifzProgress: toInt(j['hifzProgress']),
      );
}

/// GET /api/circles (للمحفّظ: حلقاته فقط)
class CircleModel {
  final int id;
  final String name;
  final String time;
  final String location;
  final bool isExtra;
  final int studentCount;

  const CircleModel({
    required this.id,
    required this.name,
    required this.time,
    required this.location,
    required this.isExtra,
    required this.studentCount,
  });

  factory CircleModel.fromJson(Map<String, dynamic> j) => CircleModel(
        id: toInt(j['id']),
        name: toStr(j['name']),
        time: toStr(j['time']),
        location: toStr(j['location']),
        isExtra: toBool(j['isExtra']),
        studentCount: toInt(j['studentCount']),
      );
}

/// طالب داخل GET /api/circles/{id}
class CircleStudentModel {
  final int id;
  final String fullName;
  final String level;

  const CircleStudentModel({
    required this.id,
    required this.fullName,
    required this.level,
  });

  factory CircleStudentModel.fromJson(Map<String, dynamic> j) =>
      CircleStudentModel(
        id: toInt(j['id']),
        fullName: toStr(j['fullName']),
        level: toStr(j['level']),
      );
}

/// GET /api/attendance/circle/{id}?date=
class AttendanceRosterItem {
  final int studentId;
  final String fullName;
  final String status; // Present | Late | ExcusedAbsence | UnexcusedAbsence | NotRecorded
  final String? note;

  const AttendanceRosterItem({
    required this.studentId,
    required this.fullName,
    required this.status,
    this.note,
  });

  factory AttendanceRosterItem.fromJson(Map<String, dynamic> j) =>
      AttendanceRosterItem(
        studentId: toInt(j['studentId']),
        fullName: toStr(j['fullName']),
        status: toStr(j['status'], 'NotRecorded'),
        note: j['note']?.toString(),
      );
}

/// GET /api/students (نطاق المحفّظ)
class TeacherStudentModel {
  final int id;
  final String fullName;
  final String parentName;
  final String parentPhone;
  final int? circleId;
  final String circleName;
  final String level;
  final int attendance;
  final double progress;

  const TeacherStudentModel({
    required this.id,
    required this.fullName,
    required this.parentName,
    required this.parentPhone,
    required this.circleId,
    required this.circleName,
    required this.level,
    required this.attendance,
    required this.progress,
  });

  factory TeacherStudentModel.fromJson(Map<String, dynamic> j) =>
      TeacherStudentModel(
        id: toInt(j['id']),
        fullName: toStr(j['fullName']),
        parentName: toStr(j['parentName']),
        parentPhone: toStr(j['parentPhone']),
        circleId: j['circleId'] == null ? null : toInt(j['circleId']),
        circleName: toStr(j['circleName'], 'بدون حلقة'),
        level: toStr(j['level']),
        attendance: toInt(j['attendance']),
        progress: toDouble(j['progress']),
      );
}

/// GET /api/students/{id}
class StudentDetailsModel {
  final int id;
  final String fullName;
  final String level;
  final String teacherName;
  final String circleName;
  final int? circleId;
  final String parentName;
  final String parentPhone;
  final String? guardianRelationship;
  final String? dateOfBirth;
  final String? registrationDate;
  final String? studentPhone;
  final String? residence;
  final int attendance;
  final double progress;

  const StudentDetailsModel({
    required this.id,
    required this.fullName,
    required this.level,
    required this.teacherName,
    required this.circleName,
    required this.circleId,
    required this.parentName,
    required this.parentPhone,
    required this.guardianRelationship,
    required this.dateOfBirth,
    required this.registrationDate,
    required this.studentPhone,
    required this.residence,
    required this.attendance,
    required this.progress,
  });

  factory StudentDetailsModel.fromJson(Map<String, dynamic> j) =>
      StudentDetailsModel(
        id: toInt(j['id']),
        fullName: toStr(j['fullName']),
        level: toStr(j['level']),
        teacherName: toStr(j['teacherName'], '—'),
        circleName: toStr(j['circleName'], 'بدون حلقة'),
        circleId: j['circleId'] == null ? null : toInt(j['circleId']),
        parentName: toStr(j['parentName'], '—'),
        parentPhone: toStr(j['parentPhone']),
        guardianRelationship: j['guardianRelationship']?.toString(),
        dateOfBirth: j['dateOfBirth']?.toString(),
        registrationDate: j['registrationDate']?.toString(),
        studentPhone: j['studentPhone']?.toString(),
        residence: j['residence']?.toString(),
        attendance: toInt(j['attendance']),
        progress: toDouble(j['progress']),
      );
}

/// GET /api/parents/lookup
class ParentLookupModel {
  final int id;
  final String fullName;
  final String phone;

  const ParentLookupModel({
    required this.id,
    required this.fullName,
    required this.phone,
  });

  factory ParentLookupModel.fromJson(Map<String, dynamic> j) =>
      ParentLookupModel(
        id: toInt(j['id']),
        fullName: toStr(j['fullName']),
        phone: toStr(j['phone']),
      );
}

/// GET /api/waiting-list (المحفّظ يرى الأرقام المعروضة فقط)
class WaitingEntryModel {
  final int id;
  final String fullName;
  final String displayPhone;
  final String parentName;
  final String displayParentPhone;
  final int? age;
  final String requestedLevel;
  final String preferredTime;
  final String notes;
  final String status;

  const WaitingEntryModel({
    required this.id,
    required this.fullName,
    required this.displayPhone,
    required this.parentName,
    required this.displayParentPhone,
    required this.age,
    required this.requestedLevel,
    required this.preferredTime,
    required this.notes,
    required this.status,
  });

  factory WaitingEntryModel.fromJson(Map<String, dynamic> j) =>
      WaitingEntryModel(
        id: toInt(j['id']),
        fullName: toStr(j['fullName']),
        displayPhone: toStr(j['displayPhone']),
        parentName: toStr(j['parentName']),
        displayParentPhone: toStr(j['displayParentPhone']),
        age: j['age'] == null ? null : toInt(j['age']),
        requestedLevel: toStr(j['requestedLevel'], 'مبتدئ'),
        preferredTime: toStr(j['preferredTime']),
        notes: toStr(j['notes']),
        status: toStr(j['status'], 'Pending'),
      );
}

/// GET /api/hifz/last-position/{id}?mode=
class LastPositionModel {
  final bool hasPrevious;
  final String? lastSurahName;
  final int lastVerse;
  final String? surahName;
  final int nextVerse;
  final String? lastSessionDate;

  const LastPositionModel({
    required this.hasPrevious,
    this.lastSurahName,
    this.lastVerse = 0,
    this.surahName,
    this.nextVerse = 1,
    this.lastSessionDate,
  });

  factory LastPositionModel.fromJson(Map<String, dynamic> j) =>
      LastPositionModel(
        hasPrevious: toBool(j['hasPrevious']),
        lastSurahName: j['lastSurahName']?.toString(),
        lastVerse: toInt(j['lastVerse']),
        surahName: j['surahName']?.toString(),
        nextVerse: j['nextVerse'] == null ? 1 : toInt(j['nextVerse']),
        lastSessionDate: j['lastSessionDate']?.toString(),
      );
}

/// GET /api/matns
class MatnOptionModel {
  final int id;
  final String name;

  const MatnOptionModel({required this.id, required this.name});

  factory MatnOptionModel.fromJson(Map<String, dynamic> j) =>
      MatnOptionModel(id: toInt(j['id']), name: toStr(j['name']));
}

/// GET /api/pedagogical/grading-defaults
class GradingDefaultsModel {
  final int oralMaxOpeningsBeforeFail;
  final int defaultMonthlyAthmanTarget;

  const GradingDefaultsModel({
    required this.oralMaxOpeningsBeforeFail,
    required this.defaultMonthlyAthmanTarget,
  });

  factory GradingDefaultsModel.fromJson(Map<String, dynamic> j) =>
      GradingDefaultsModel(
        oralMaxOpeningsBeforeFail: toInt(j['oralMaxOpeningsBeforeFail']),
        defaultMonthlyAthmanTarget: toInt(j['defaultMonthlyAthmanTarget']),
      );
}

/// GET /api/pedagogical/periods
class EvaluationPeriodModel {
  final int id;
  final String name;
  final String startDate;
  final String endDate;
  final int? circleId;
  final String? circleName;
  final String? notes;
  final int evaluationsCount;

  const EvaluationPeriodModel({
    required this.id,
    required this.name,
    required this.startDate,
    required this.endDate,
    required this.circleId,
    required this.circleName,
    required this.notes,
    required this.evaluationsCount,
  });

  factory EvaluationPeriodModel.fromJson(Map<String, dynamic> j) =>
      EvaluationPeriodModel(
        id: toInt(j['id']),
        name: toStr(j['name']),
        startDate: toStr(j['startDate']),
        endDate: toStr(j['endDate']),
        circleId: j['circleId'] == null ? null : toInt(j['circleId']),
        circleName: j['circleName']?.toString(),
        notes: j['notes']?.toString(),
        evaluationsCount: toInt(j['evaluationsCount']),
      );
}

/// تقييم فترة لطالب (GET evaluations / POST auto-draft) — قيم الأبعاد من 100.
class PeriodEvaluationDraft {
  final int studentId;
  final String studentName;
  double attendanceScore;
  double hifzScore;
  double revisionScore;
  double progressScore;
  double matnScore;
  double dressScore;
  double? prayerAdvisoryScore;
  double? parentHomeAdvisoryScore;
  bool includeAdvisoryInOverall;
  String? sheikhNotes;
  final double overallScore;
  final String gradeLabel;
  final bool saved;

  PeriodEvaluationDraft({
    required this.studentId,
    required this.studentName,
    required this.attendanceScore,
    required this.hifzScore,
    required this.revisionScore,
    required this.progressScore,
    required this.matnScore,
    required this.dressScore,
    required this.prayerAdvisoryScore,
    required this.parentHomeAdvisoryScore,
    required this.includeAdvisoryInOverall,
    required this.sheikhNotes,
    required this.overallScore,
    required this.gradeLabel,
    required this.saved,
  });

  factory PeriodEvaluationDraft.fromJson(Map<String, dynamic> j,
          {bool saved = true, String? studentName}) =>
      PeriodEvaluationDraft(
        studentId: toInt(j['studentId']),
        studentName: studentName ?? toStr(j['studentName']),
        attendanceScore: toDouble(j['attendanceScore']),
        hifzScore: toDouble(j['hifzScore']),
        revisionScore: toDouble(j['revisionScore']),
        progressScore: toDouble(j['progressScore']),
        matnScore: toDouble(j['matnScore']),
        dressScore: toDouble(j['dressScore']),
        prayerAdvisoryScore: j['prayerAdvisoryScore'] == null
            ? null
            : toDouble(j['prayerAdvisoryScore']),
        parentHomeAdvisoryScore: j['parentHomeAdvisoryScore'] == null
            ? null
            : toDouble(j['parentHomeAdvisoryScore']),
        includeAdvisoryInOverall: toBool(j['includeAdvisoryInOverall']),
        sheikhNotes: j['sheikhNotes']?.toString(),
        overallScore: toDouble(j['overallScore']),
        gradeLabel: toStr(j['gradeLabel']),
        saved: saved,
      );

  Map<String, dynamic> toJson() => {
        'studentId': studentId,
        'attendanceScore': attendanceScore,
        'hifzScore': hifzScore,
        'revisionScore': revisionScore,
        'progressScore': progressScore,
        'matnScore': matnScore,
        'dressScore': dressScore,
        'sheikhNotes': sheikhNotes,
        'prayerAdvisoryScore': prayerAdvisoryScore,
        'parentHomeAdvisoryScore': parentHomeAdvisoryScore,
        'includeAdvisoryInOverall': includeAdvisoryInOverall,
      };
}

/// GET /api/pedagogical/dress?date=
class DressRecordModel {
  final int studentId;
  final bool isCompliant;
  final double? scoreOutOf10;
  final String? note;

  const DressRecordModel({
    required this.studentId,
    required this.isCompliant,
    this.scoreOutOf10,
    this.note,
  });

  factory DressRecordModel.fromJson(Map<String, dynamic> j) => DressRecordModel(
        studentId: toInt(j['studentId']),
        isCompliant: toBool(j['isCompliant']),
        scoreOutOf10:
            j['scoreOutOf10'] == null ? null : toDouble(j['scoreOutOf10']),
        note: j['note']?.toString(),
      );
}

/// GET /api/messages/inbox
class InboxMessageModel {
  final int id;
  final String content;
  final String createdAt;
  final bool isRead;
  final String senderName;

  const InboxMessageModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.isRead,
    required this.senderName,
  });

  factory InboxMessageModel.fromJson(Map<String, dynamic> j) =>
      InboxMessageModel(
        id: toInt(j['id']),
        content: toStr(j['content']),
        createdAt: toStr(j['createdAt']),
        isRead: toBool(j['isRead']),
        senderName: toStr(j['senderName']),
      );
}

/// GET /api/hifz/monthly-ratings
class MonthlyRatingsReportModel {
  final int year;
  final int month;
  final List<String> passingEvaluations;
  final List<MonthlyRatingRow> items;

  const MonthlyRatingsReportModel({
    required this.year,
    required this.month,
    required this.passingEvaluations,
    required this.items,
  });

  factory MonthlyRatingsReportModel.fromJson(Map<String, dynamic> j) =>
      MonthlyRatingsReportModel(
        year: toInt(j['year']),
        month: toInt(j['month']),
        passingEvaluations: (j['passingEvaluations'] is List)
            ? (j['passingEvaluations'] as List).map((e) => e.toString()).toList()
            : const [],
        items: _list(j['items']).map(MonthlyRatingRow.fromJson).toList(),
      );
}

class MonthlyRatingRow {
  final int studentId;
  final String fullName;
  final String circleName;
  final MonthlyHifzRatingModel? rating;

  const MonthlyRatingRow({
    required this.studentId,
    required this.fullName,
    required this.circleName,
    required this.rating,
  });

  factory MonthlyRatingRow.fromJson(Map<String, dynamic> j) => MonthlyRatingRow(
        studentId: toInt(j['studentId']),
        fullName: toStr(j['fullName']),
        circleName: toStr(j['circleName']),
        rating: MonthlyHifzRatingModel.tryParse(j['rating']),
      );
}

/// نتيجة إضافة طالب: إما بيانات الحساب أو تنبيه "طالب سابق".
class CreateStudentResult {
  final String message;
  final int? studentId;
  final Map<String, dynamic>? credentials;
  final Map<String, dynamic>? parentCredentials;

  const CreateStudentResult({
    required this.message,
    this.studentId,
    this.credentials,
    this.parentCredentials,
  });

  factory CreateStudentResult.fromJson(Map<String, dynamic> j) =>
      CreateStudentResult(
        message: toStr(j['message'], 'تم الحفظ'),
        studentId: j['studentId'] == null ? null : toInt(j['studentId']),
        credentials: j['credentials'] is Map ? _map(j['credentials']) : null,
        parentCredentials:
            j['parentCredentials'] is Map ? _map(j['parentCredentials']) : null,
      );
}

/// رمز الخطأ 409 حين يكون الطالب المُضاف طالباً سابقاً مؤرشفاً.
class ArchivedStudentException implements Exception {
  final String message;
  final bool canRestore;
  final int archivedStudentId;
  final String archivedName;

  const ArchivedStudentException({
    required this.message,
    required this.canRestore,
    required this.archivedStudentId,
    required this.archivedName,
  });

  @override
  String toString() => message;
}
