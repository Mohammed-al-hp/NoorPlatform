import '../../core/network/api_response_helper.dart';

class HifzRecordModel {
  final int id;
  final int? studentId;
  final String date;
  final String surahName;
  final String? toSurahName;
  final String verses;
  final int verseCount;
  final String type;
  final String evaluation;
  final String? notes;
  final String? revisionMode;

  const HifzRecordModel({
    required this.id,
    this.studentId,
    required this.date,
    required this.surahName,
    this.toSurahName,
    required this.verses,
    required this.verseCount,
    required this.type,
    required this.evaluation,
    this.notes,
    this.revisionMode,
  });

  bool get isMemorization =>
      type == 'Memorization' || type == '0' || type.toLowerCase() == 'memorization';

  factory HifzRecordModel.fromJson(Map<String, dynamic> json) =>
      HifzRecordModel(
        id: toInt(json['id']),
        studentId: json['studentId'] == null ? null : toInt(json['studentId']),
        date: toStr(json['date']),
        surahName: toStr(json['surahName']),
        toSurahName: json['toSurahName']?.toString(),
        verses: toStr(json['verses']),
        verseCount: toInt(json['verseCount']),
        type: toStr(json['type']),
        evaluation: toStr(json['evaluation']),
        notes: json['notes']?.toString(),
        revisionMode: json['revisionMode']?.toString(),
      );
}

class MyHifzResponse {
  final int hifzProgress;
  final List<HifzRecordModel> records;

  const MyHifzResponse({
    required this.hifzProgress,
    required this.records,
  });

  factory MyHifzResponse.fromJson(Map<String, dynamic> json) {
    final raw = json['records'];
    final list = raw is List
        ? raw
            .whereType<Map>()
            .map((e) => HifzRecordModel.fromJson(Map<String, dynamic>.from(e)))
            .toList()
        : <HifzRecordModel>[];
    return MyHifzResponse(
      hifzProgress: toInt(json['hifzProgress']),
      records: list,
    );
  }
}
