import '../../core/network/api_response_helper.dart';

class SentMessageModel {
  final int id;
  final String content;
  final String createdAt;
  final String recipientType;
  final String recipientName;
  final bool isRead;
  final List<MessageReplyModel> replies;

  const SentMessageModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.recipientType,
    required this.recipientName,
    this.isRead = false,
    this.replies = const [],
  });

  factory SentMessageModel.fromJson(Map<String, dynamic> json) =>
      SentMessageModel(
        id: toInt(json['id']),
        content: toStr(json['content']),
        createdAt: toStr(json['createdAt']),
        recipientType: toStr(json['recipientType']),
        recipientName: toStr(json['recipientName']),
        isRead: toBool(json['isRead']),
        replies: ApiResponseHelper.asMapList(json['replies'])
            .map(MessageReplyModel.fromJson)
            .toList(),
      );
}

/// رد المحفّظ/الإدارة على رسالة ولي الأمر.
class MessageReplyModel {
  final int id;
  final String content;
  final String createdAt;
  final String senderName;

  const MessageReplyModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.senderName,
  });

  factory MessageReplyModel.fromJson(Map<String, dynamic> json) => MessageReplyModel(
        id: toInt(json['id']),
        content: toStr(json['content']),
        createdAt: toStr(json['createdAt']),
        senderName: toStr(json['senderName']),
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
