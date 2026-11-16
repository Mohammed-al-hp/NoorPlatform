// نماذج الميزات الذكية: الإشعارات داخل التطبيق، جدول المراجعة، والخريطة الحرارية.

double? _toDouble(dynamic v) => v == null ? null : (v is num ? v.toDouble() : double.tryParse('$v'));
int _toInt(dynamic v, [int d = 0]) => v is int ? v : (v is num ? v.toInt() : int.tryParse('$v') ?? d);
String _toStr(dynamic v, [String d = '']) => v == null ? d : '$v';

/// إشعار من مركز الإشعارات في الخادم (/api/inbox).
class ServerNotification {
  final int id;
  final String kind; // Hifz | Absence | Announcement | Message | MessageReply | MonthlyReport | System
  final String title;
  final String body;
  final String? link;
  final int? studentId;
  final bool isRead;
  final String createdAt;

  const ServerNotification({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    required this.isRead,
    required this.createdAt,
    this.link,
    this.studentId,
  });

  factory ServerNotification.fromJson(Map<String, dynamic> j) => ServerNotification(
        id: _toInt(j['id']),
        kind: _toStr(j['kind'], 'System'),
        title: _toStr(j['title']),
        body: _toStr(j['body']),
        link: j['link']?.toString(),
        studentId: j['studentId'] == null ? null : _toInt(j['studentId']),
        isRead: j['isRead'] == true,
        createdAt: _toStr(j['createdAt']),
      );
}

/// سورة في جدول المراجعة أو الخريطة الحرارية.
class SurahStrength {
  final int number;
  final String name;
  final int juz;
  final bool memorized;
  final double? retention;
  final String level; // strong | good | fading | weak | critical | none
  final int? daysSinceReview;
  final int overdueDays;
  final String? dueDate;
  final String? lastEvaluation;

  const SurahStrength({
    required this.number,
    required this.name,
    required this.juz,
    required this.memorized,
    required this.level,
    this.retention,
    this.daysSinceReview,
    this.overdueDays = 0,
    this.dueDate,
    this.lastEvaluation,
  });

  bool get isDue {
    if (!memorized || dueDate == null) return false;
    final d = DateTime.tryParse(dueDate!);
    return d != null && !d.isAfter(DateTime.now());
  }

  factory SurahStrength.fromJson(Map<String, dynamic> j) => SurahStrength(
        number: _toInt(j['number']),
        name: _toStr(j['name']),
        juz: _toInt(j['juz'], 1),
        // في جدول المراجعة كل السور محفوظة (لا يوجد حقل memorized)
        memorized: j['memorized'] == null ? true : j['memorized'] == true,
        retention: _toDouble(j['retention']),
        level: _toStr(j['level'], 'none'),
        daysSinceReview: j['daysSinceReview'] == null ? null : _toInt(j['daysSinceReview']),
        overdueDays: _toInt(j['overdueDays']),
        dueDate: j['dueDate']?.toString(),
        lastEvaluation: j['lastEvaluation']?.toString(),
      );
}

class JuzStrength {
  final int juz;
  final int surahCount;
  final int memorizedCount;
  final double? retention;

  const JuzStrength({required this.juz, required this.surahCount, required this.memorizedCount, this.retention});

  factory JuzStrength.fromJson(Map<String, dynamic> j) => JuzStrength(
        juz: _toInt(j['juz']),
        surahCount: _toInt(j['surahCount']),
        memorizedCount: _toInt(j['memorizedCount']),
        retention: _toDouble(j['retention']),
      );
}

class HeatmapModel {
  final int memorizedCount;
  final double averageRetention;
  final List<SurahStrength> surahs;
  final List<JuzStrength> juz;

  const HeatmapModel({required this.memorizedCount, required this.averageRetention, required this.surahs, required this.juz});

  int get dueCount => surahs.where((s) => s.isDue).length;

  factory HeatmapModel.fromJson(Map<String, dynamic> j) => HeatmapModel(
        memorizedCount: _toInt(j['memorizedCount']),
        averageRetention: _toDouble(j['averageRetention']) ?? 0,
        surahs: ((j['surahs'] as List?) ?? const [])
            .map((e) => SurahStrength.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
        juz: ((j['juz'] as List?) ?? const [])
            .map((e) => JuzStrength.fromJson(Map<String, dynamic>.from(e as Map)))
            .toList(),
      );
}

class ReviewScheduleModel {
  final int totalMemorized;
  final int dueCount;
  final double averageRetention;
  final List<SurahStrength> due;
  final List<SurahStrength> upcoming;

  const ReviewScheduleModel({
    required this.totalMemorized,
    required this.dueCount,
    required this.averageRetention,
    required this.due,
    required this.upcoming,
  });

  factory ReviewScheduleModel.fromJson(Map<String, dynamic> j) {
    List<SurahStrength> list(dynamic v) => ((v as List?) ?? const [])
        .map((e) => SurahStrength.fromJson(Map<String, dynamic>.from(e as Map)))
        .toList();
    return ReviewScheduleModel(
      totalMemorized: _toInt(j['totalMemorized']),
      dueCount: _toInt(j['dueCount']),
      averageRetention: _toDouble(j['averageRetention']) ?? 0,
      due: list(j['due']),
      upcoming: list(j['upcoming']),
    );
  }
}
