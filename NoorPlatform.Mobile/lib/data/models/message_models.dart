import '../../core/network/api_response_helper.dart';

class SentMessageModel {
  final int id;
  final String content;
  final String createdAt;
  final String recipientType;
  final String recipientName;

  const SentMessageModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.recipientType,
    required this.recipientName,
  });

  factory SentMessageModel.fromJson(Map<String, dynamic> json) =>
      SentMessageModel(
        id: toInt(json['id']),
        content: toStr(json['content']),
        createdAt: toStr(json['createdAt']),
        recipientType: toStr(json['recipientType']),
        recipientName: toStr(json['recipientName']),
      );
}

class MessageRecipientModel {
  final int id;
  final String fullName;

  const MessageRecipientModel({required this.id, required this.fullName});

  factory MessageRecipientModel.fromJson(Map<String, dynamic> json) =>
      MessageRecipientModel(
        id: toInt(json['id']),
        fullName: toStr(json['fullName']),
      );
}

class SendMessageRequest {
  final String recipientType; // Admin | Teacher
  final int? recipientTeacherId;
  final String content;

  const SendMessageRequest({
    required this.recipientType,
    required this.content,
    this.recipientTeacherId,
  });

  Map<String, dynamic> toJson() => {
        'recipientType': recipientType,
        'content': content,
        if (recipientTeacherId != null)
          'recipientTeacherId': recipientTeacherId,
      };
}
