import '../../core/network/api_response_helper.dart';

class AnnouncementModel {
  final int id;
  final String title;
  final String content;
  final String? createdAt;
  final bool isActive;
  final String target;
  final String? color;

  const AnnouncementModel({
    required this.id,
    required this.title,
    required this.content,
    this.createdAt,
    required this.isActive,
    required this.target,
    this.color,
  });

  factory AnnouncementModel.fromJson(Map<String, dynamic> json) =>
      AnnouncementModel(
        id: toInt(json['id']),
        title: toStr(json['title']),
        content: toStr(json['content']),
        createdAt: json['createdAt']?.toString(),
        isActive: toBool(json['isActive'] ?? true),
        target: toStr(json['target']),
        color: json['color']?.toString(),
      );
}
