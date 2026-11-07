import '../../core/network/api_response_helper.dart';

class PrayerLogModel {
  final int id;
  final String date;
  final bool prayedInMosque;
  final bool onTime;
  final int mosquePrayerCount;
  final bool isLocked;
  final String? studentNote;
  final String? sheikhOverrideNote;
  final String? submittedAt;

  const PrayerLogModel({
    required this.id,
    required this.date,
    required this.prayedInMosque,
    required this.onTime,
    required this.mosquePrayerCount,
    required this.isLocked,
    this.studentNote,
    this.sheikhOverrideNote,
    this.submittedAt,
  });

  factory PrayerLogModel.fromJson(Map<String, dynamic> json) => PrayerLogModel(
        id: toInt(json['id']),
        date: toStr(json['date']),
        prayedInMosque: toBool(json['prayedInMosque']),
        onTime: toBool(json['onTime']),
        mosquePrayerCount: toInt(json['mosquePrayerCount']),
        isLocked: toBool(json['isLocked']),
        studentNote: json['studentNote']?.toString(),
        sheikhOverrideNote: json['sheikhOverrideNote']?.toString(),
        submittedAt: json['submittedAt']?.toString(),
      );
}

class ParentHomeFeedbackModel {
  final int id;
  final int studentId;
  final String weekStartDate;
  final String rating;
  final String ratingLabel;
  final String? notes;
  final String? submittedAt;

  const ParentHomeFeedbackModel({
    required this.id,
    required this.studentId,
    required this.weekStartDate,
    required this.rating,
    required this.ratingLabel,
    this.notes,
    this.submittedAt,
  });

  factory ParentHomeFeedbackModel.fromJson(Map<String, dynamic> json) =>
      ParentHomeFeedbackModel(
        id: toInt(json['id']),
        studentId: toInt(json['studentId']),
        weekStartDate: toStr(json['weekStartDate']),
        rating: toStr(json['rating']),
        ratingLabel: toStr(json['ratingLabel'], toStr(json['rating'])),
        notes: json['notes']?.toString(),
        submittedAt: json['submittedAt']?.toString(),
      );
}

class EvaluationModel {
  final int id;
  final int periodId;
  final String periodName;
  final String? periodStart;
  final String? periodEnd;
  final int studentId;
  final String studentName;
  final double attendanceScore;
  final double hifzScore;
  final double revisionScore;
  final double progressScore;
  final double matnScore;
  final double dressScore;
  final double overallScore;
  final String gradeLabel;
  final String? sheikhNotes;
  final double? prayerAdvisoryScore;
  final double? parentHomeAdvisoryScore;
  final bool includeAdvisoryInOverall;
  final String? evaluatedAt;

  const EvaluationModel({
    required this.id,
    required this.periodId,
    required this.periodName,
    this.periodStart,
    this.periodEnd,
    required this.studentId,
    required this.studentName,
    required this.attendanceScore,
    required this.hifzScore,
    required this.revisionScore,
    required this.progressScore,
    required this.matnScore,
    required this.dressScore,
    required this.overallScore,
    required this.gradeLabel,
    this.sheikhNotes,
    this.prayerAdvisoryScore,
    this.parentHomeAdvisoryScore,
    required this.includeAdvisoryInOverall,
    this.evaluatedAt,
  });

  factory EvaluationModel.fromJson(Map<String, dynamic> json) =>
      EvaluationModel(
        id: toInt(json['id']),
        periodId: toInt(json['periodId']),
        periodName: toStr(json['periodName']),
        periodStart: json['periodStart']?.toString(),
        periodEnd: json['periodEnd']?.toString(),
        studentId: toInt(json['studentId']),
        studentName: toStr(json['studentName']),
        attendanceScore: toDouble(json['attendanceScore']),
        hifzScore: toDouble(json['hifzScore']),
        revisionScore: toDouble(json['revisionScore']),
        progressScore: toDouble(json['progressScore']),
        matnScore: toDouble(json['matnScore']),
        dressScore: toDouble(json['dressScore']),
        overallScore: toDouble(json['overallScore']),
        gradeLabel: toStr(json['gradeLabel']),
        sheikhNotes: json['sheikhNotes']?.toString(),
        prayerAdvisoryScore: json['prayerAdvisoryScore'] == null
            ? null
            : toDouble(json['prayerAdvisoryScore']),
        parentHomeAdvisoryScore: json['parentHomeAdvisoryScore'] == null
            ? null
            : toDouble(json['parentHomeAdvisoryScore']),
        includeAdvisoryInOverall: toBool(json['includeAdvisoryInOverall']),
        evaluatedAt: json['evaluatedAt']?.toString(),
      );
}

class MatnRecordModel {
  final int id;
  final int studentId;
  final String date;
  final String matnName;
  final String portion;
  final String type;
  final String evaluation;
  final String? notes;

  const MatnRecordModel({
    required this.id,
    required this.studentId,
    required this.date,
    required this.matnName,
    required this.portion,
    required this.type,
    required this.evaluation,
    this.notes,
  });

  factory MatnRecordModel.fromJson(Map<String, dynamic> json) =>
      MatnRecordModel(
        id: toInt(json['id']),
        studentId: toInt(json['studentId']),
        date: toStr(json['date']),
        matnName: toStr(json['matnName']),
        portion: toStr(json['portion']),
        type: toStr(json['type']),
        evaluation: toStr(json['evaluation']),
        notes: json['notes']?.toString(),
      );
}

class MonthlyTargetModel {
  final int id;
  final int studentId;
  final int year;
  final int month;
  final int targetAthmanCount;
  final int achievedAthmanCount;
  final double progressScoreOutOf10;
  final bool isSpecialMode;
  final String? specialModeNote;
  final String? notes;

  const MonthlyTargetModel({
    required this.id,
    required this.studentId,
    required this.year,
    required this.month,
    required this.targetAthmanCount,
    required this.achievedAthmanCount,
    required this.progressScoreOutOf10,
    required this.isSpecialMode,
    this.specialModeNote,
    this.notes,
  });

  factory MonthlyTargetModel.fromJson(Map<String, dynamic> json) =>
      MonthlyTargetModel(
        id: toInt(json['id']),
        studentId: toInt(json['studentId']),
        year: toInt(json['year']),
        month: toInt(json['month']),
        targetAthmanCount: toInt(json['targetAthmanCount']),
        achievedAthmanCount: toInt(json['achievedAthmanCount']),
        progressScoreOutOf10: toDouble(json['progressScoreOutOf10']),
        isSpecialMode: toBool(json['isSpecialMode']),
        specialModeNote: json['specialModeNote']?.toString(),
        notes: json['notes']?.toString(),
      );
}

class PortalConfigModel {
  final bool evaluationsVisible;

  const PortalConfigModel({required this.evaluationsVisible});

  factory PortalConfigModel.fromJson(Map<String, dynamic> json) =>
      PortalConfigModel(
        evaluationsVisible: toBool(json['evaluationsVisible'] ?? true),
      );
}
