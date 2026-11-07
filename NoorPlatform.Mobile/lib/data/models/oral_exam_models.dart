import '../../core/network/api_response_helper.dart';

class OralExamModel {
  final int id;
  final int studentId;
  final String? studentName;
  final String? circleName;
  final String date;
  final String kind;
  final String scopeLabel;
  final String? notes;
  final double overallPercent;
  final String overallGrade;
  final bool isConsideredMemorized;
  final List<OralExamQuestionModel> questions;

  const OralExamModel({
    required this.id,
    required this.studentId,
    this.studentName,
    this.circleName,
    required this.date,
    required this.kind,
    required this.scopeLabel,
    this.notes,
    required this.overallPercent,
    required this.overallGrade,
    required this.isConsideredMemorized,
    required this.questions,
  });

  factory OralExamModel.fromJson(Map<String, dynamic> json) {
    final qs = json['questions'];
    return OralExamModel(
      id: toInt(json['id']),
      studentId: toInt(json['studentId']),
      studentName: json['studentName']?.toString(),
      circleName: json['circleName']?.toString(),
      date: toStr(json['date']),
      kind: toStr(json['kind']),
      scopeLabel: toStr(json['scopeLabel']),
      notes: json['notes']?.toString(),
      overallPercent: toDouble(json['overallPercent']),
      overallGrade: toStr(json['overallGrade']),
      isConsideredMemorized: toBool(json['isConsideredMemorized']),
      questions: qs is List
          ? qs
              .whereType<Map>()
              .map((e) =>
                  OralExamQuestionModel.fromJson(Map<String, dynamic>.from(e)))
              .toList()
          : const [],
    );
  }
}

class OralExamQuestionModel {
  final int id;
  final int orderIndex;
  final String label;
  final double scorePercent;
  final String? impression;
  final bool isPassed;

  const OralExamQuestionModel({
    required this.id,
    required this.orderIndex,
    required this.label,
    required this.scorePercent,
    this.impression,
    required this.isPassed,
  });

  factory OralExamQuestionModel.fromJson(Map<String, dynamic> json) =>
      OralExamQuestionModel(
        id: toInt(json['id']),
        orderIndex: toInt(json['orderIndex']),
        label: toStr(json['label']),
        scorePercent: toDouble(json['scorePercent']),
        impression: json['impression']?.toString(),
        isPassed: toBool(json['isPassed']),
      );
}
