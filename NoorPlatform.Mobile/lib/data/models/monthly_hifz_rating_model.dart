import '../../core/network/api_response_helper.dart';

/// التقييم الشهري بحسب جلسات الحفظ الجديد الناجحة (من 10) — مشترك بين الطالب وولي الأمر والمحفّظ.
class MonthlyHifzRatingModel {
  final int year;
  final int month;
  final int? ageYears;
  final String ageBand;
  final int target;
  final int successfulSessions;
  final int totalMemorizationSessions;
  final double score;
  final String? warning;

  const MonthlyHifzRatingModel({
    required this.year,
    required this.month,
    required this.ageYears,
    required this.ageBand,
    required this.target,
    required this.successfulSessions,
    required this.totalMemorizationSessions,
    required this.score,
    required this.warning,
  });

  static MonthlyHifzRatingModel? tryParse(dynamic v) =>
      v is Map ? MonthlyHifzRatingModel.fromJson(Map<String, dynamic>.from(v)) : null;

  factory MonthlyHifzRatingModel.fromJson(Map<String, dynamic> j) => MonthlyHifzRatingModel(
        year: toInt(j['year']),
        month: toInt(j['month']),
        ageYears: j['ageYears'] == null ? null : toInt(j['ageYears']),
        ageBand: toStr(j['ageBand'], '5–7'),
        target: toInt(j['target']),
        successfulSessions: toInt(j['successfulSessions']),
        totalMemorizationSessions: toInt(j['totalMemorizationSessions']),
        score: toDouble(j['score']),
        warning: j['warning']?.toString(),
      );

  Map<String, dynamic> toJson() => {
        'year': year,
        'month': month,
        'ageYears': ageYears,
        'ageBand': ageBand,
        'target': target,
        'successfulSessions': successfulSessions,
        'totalMemorizationSessions': totalMemorizationSessions,
        'score': score,
        'warning': warning,
      };
}
