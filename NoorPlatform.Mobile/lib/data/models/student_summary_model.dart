import 'monthly_hifz_rating_model.dart';
// ── Safe type converters ──
int _toInt(dynamic v) {
  if (v == null) return 0;
  if (v is int) return v;
  if (v is double) return v.toInt();
  if (v is String) return int.tryParse(v) ?? 0;
  return 0;
}

double _toDouble(dynamic v) {
  if (v == null) return 0.0;
  if (v is double) return v;
  if (v is int) return v.toDouble();
  if (v is String) return double.tryParse(v) ?? 0.0;
  return 0.0;
}

/// Student summary model — mirrors GET /api/dashboard/student-summary response.
class StudentSummaryModel {
  final int id;
  final String fullName;
  final int hifzProgress;
  final double attendancePercentage;
  final String lastEvaluation;
  final String lastSurah;
  final int points;
  final int weeklyVerses;
  final int weeklyGoal;
  final List<LeaderboardEntry> circleTop3;
  final NextReview? nextReview;
  final LastMemorization? lastMemorization;
  final String teacherName;
  final double teacherRating;
  final String circleName;
  final String? badges;
  final List<RecentGrade> recentGrades;
  final List<RecentHifz> recentHifz;
  final List<TeacherNote> teacherNotes;
  final MonthlyHifzRatingModel? monthlyHifzRating;

  const StudentSummaryModel({
    required this.id,
    required this.fullName,
    required this.hifzProgress,
    required this.attendancePercentage,
    required this.lastEvaluation,
    required this.lastSurah,
    required this.points,
    required this.weeklyVerses,
    required this.weeklyGoal,
    required this.circleTop3,
    this.nextReview,
    this.lastMemorization,
    required this.teacherName,
    required this.teacherRating,
    required this.circleName,
    this.badges,
    required this.recentGrades,
    required this.recentHifz,
    required this.teacherNotes,
    this.monthlyHifzRating,
  });

  List<String> get badgeList =>
      badges?.split(',').where((b) => b.trim().isNotEmpty).toList() ?? [];

  factory StudentSummaryModel.fromJson(Map<String, dynamic> json) {
    return StudentSummaryModel(
      id: _toInt(json['id']),
      fullName: json['fullName']?.toString() ?? '',
      hifzProgress: _toInt(json['hifzProgress']),
      attendancePercentage: _toDouble(json['attendancePercentage']),
      lastEvaluation: json['lastEvaluation']?.toString() ?? 'لا يوجد',
      lastSurah: json['lastSurah']?.toString() ?? '—',
      points: _toInt(json['points']),
      weeklyVerses: _toInt(json['weeklyVerses']),
      weeklyGoal: _toInt(json['weeklyGoal']),
      circleTop3: (json['circleTop3'] as List<dynamic>?)
              ?.map((e) => LeaderboardEntry.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      nextReview: json['nextReview'] != null
          ? NextReview.fromJson(json['nextReview'] as Map<String, dynamic>)
          : null,
      lastMemorization: json['lastMemorization'] != null
          ? LastMemorization.fromJson(json['lastMemorization'] as Map<String, dynamic>)
          : null,
      teacherName: json['teacherName']?.toString() ?? '—',
      teacherRating: _toDouble(json['teacherRating']),
      circleName: json['circleName']?.toString() ?? 'بدون حلقة',
      badges: json['badges']?.toString(),
      recentGrades: (json['recentGrades'] as List<dynamic>?)
              ?.map((e) => RecentGrade.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      recentHifz: (json['recentHifz'] as List<dynamic>?)
              ?.map((e) => RecentHifz.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      teacherNotes: (json['teacherNotes'] as List<dynamic>?)
              ?.map((e) => TeacherNote.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      monthlyHifzRating: MonthlyHifzRatingModel.tryParse(json['monthlyHifzRating']),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'hifzProgress': hifzProgress,
        'attendancePercentage': attendancePercentage,
        'lastEvaluation': lastEvaluation,
        'lastSurah': lastSurah,
        'points': points,
        'weeklyVerses': weeklyVerses,
        'weeklyGoal': weeklyGoal,
        'circleTop3': circleTop3.map((e) => e.toJson()).toList(),
        'nextReview': nextReview?.toJson(),
        'lastMemorization': lastMemorization?.toJson(),
        'teacherName': teacherName,
        'teacherRating': teacherRating,
        'circleName': circleName,
        'badges': badges,
        'recentGrades': recentGrades.map((e) => e.toJson()).toList(),
        'recentHifz': recentHifz.map((e) => e.toJson()).toList(),
        'teacherNotes': teacherNotes.map((e) => e.toJson()).toList(),
        'monthlyHifzRating': monthlyHifzRating?.toJson(),
      };
}

class LeaderboardEntry {
  final int studentId;
  final String fullName;
  final int points;
  final String? badges;
  final bool isCurrentUser;

  const LeaderboardEntry({
    required this.studentId,
    required this.fullName,
    required this.points,
    this.badges,
    required this.isCurrentUser,
  });

  factory LeaderboardEntry.fromJson(Map<String, dynamic> json) {
    return LeaderboardEntry(
      studentId: _toInt(json['studentId']),
      fullName: json['fullName']?.toString() ?? '',
      points: _toInt(json['points']),
      badges: json['badges']?.toString(),
      isCurrentUser: json['isCurrentUser'] as bool? ?? false,
    );
  }

  Map<String, dynamic> toJson() => {
        'studentId': studentId,
        'fullName': fullName,
        'points': points,
        'badges': badges,
        'isCurrentUser': isCurrentUser,
      };
}

class NextReview {
  final String date;
  final String surah;
  final String? toSurah;
  final String verses;
  final int verseCount;
  final String evaluation;

  const NextReview({
    required this.date,
    required this.surah,
    this.toSurah,
    required this.verses,
    required this.verseCount,
    required this.evaluation,
  });

  factory NextReview.fromJson(Map<String, dynamic> json) => NextReview(
        date: json['date']?.toString() ?? '',
        surah: json['surah']?.toString() ?? '',
        toSurah: json['toSurah']?.toString(),
        verses: json['verses']?.toString() ?? '',
        verseCount: _toInt(json['verseCount']),
        evaluation: json['evaluation']?.toString() ?? '',
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'surah': surah,
        'toSurah': toSurah,
        'verses': verses,
        'verseCount': verseCount,
        'evaluation': evaluation,
      };
}

class LastMemorization {
  final String date;
  final String surah;
  final String? toSurah;
  final String verses;

  const LastMemorization({
    required this.date,
    required this.surah,
    this.toSurah,
    required this.verses,
  });

  factory LastMemorization.fromJson(Map<String, dynamic> json) =>
      LastMemorization(
        date: json['date']?.toString() ?? '',
        surah: json['surah']?.toString() ?? '',
        toSurah: json['toSurah']?.toString(),
        verses: json['verses']?.toString() ?? '',
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'surah': surah,
        'toSurah': toSurah,
        'verses': verses,
      };
}

class RecentGrade {
  final int score;
  final int maxScore;
  final String? feedback;

  const RecentGrade({
    required this.score,
    required this.maxScore,
    this.feedback,
  });

  double get percentage => maxScore > 0 ? score / maxScore * 100 : 0;

  factory RecentGrade.fromJson(Map<String, dynamic> json) => RecentGrade(
        score: _toInt(json['score']),
        maxScore: _toInt(json['maxScore']),
        feedback: json['feedback']?.toString(),
      );

  Map<String, dynamic> toJson() => {
        'score': score,
        'maxScore': maxScore,
        'feedback': feedback,
      };
}

class RecentHifz {
  final String date;
  final String surahName;
  final String verses;
  final String evaluation;

  const RecentHifz({
    required this.date,
    required this.surahName,
    required this.verses,
    required this.evaluation,
  });

  factory RecentHifz.fromJson(Map<String, dynamic> json) => RecentHifz(
        date: json['date']?.toString() ?? '',
        surahName: json['surahName']?.toString() ?? '',
        verses: json['verses']?.toString() ?? '',
        evaluation: json['evaluation']?.toString() ?? '',
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'surahName': surahName,
        'verses': verses,
        'evaluation': evaluation,
      };
}

class TeacherNote {
  final String date;
  final String notes;
  final String teacherName;

  const TeacherNote({
    required this.date,
    required this.notes,
    required this.teacherName,
  });

  factory TeacherNote.fromJson(Map<String, dynamic> json) => TeacherNote(
        date: json['date']?.toString() ?? '',
        notes: json['notes']?.toString() ?? '',
        teacherName: json['teacherName']?.toString() ?? '',
      );

  Map<String, dynamic> toJson() => {
        'date': date,
        'notes': notes,
        'teacherName': teacherName,
      };
}
