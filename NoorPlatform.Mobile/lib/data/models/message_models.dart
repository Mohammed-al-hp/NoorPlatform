import '../../core/network/api_response_helper.dart';

/// محادثة ولي الأمر (GET /api/messages/threads): رسالة بدأها ولي الأمر أو بدأها المحفّظ/الإدارة معه.
class SentMessageModel {
  final int id;
  final String content;
  final String createdAt;
  final String recipientType;
  /// الطرف الآخر في المحادثة (المستلم إن بدأها ولي الأمر، أو المرسل إن بدأها المركز).
  final String recipientName;
  final bool isRead;
  /// out = بدأها ولي الأمر، in = بدأها المحفّظ/الإدارة.
  final String direction;
  final bool hasUnread;
  final List<MessageReplyModel> replies;

  const SentMessageModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.recipientType,
    required this.recipientName,
    this.isRead = false,
    this.direction = 'out',
    this.hasUnread = false,
    this.replies = const [],
  });

  bool get isIncoming => direction == 'in';

  factory SentMessageModel.fromJson(Map<String, dynamic> json) =>
      SentMessageModel(
        id: toInt(json['id']),
        content: toStr(json['content']),
        createdAt: toStr(json['createdAt']),
        recipientType: toStr(json['recipientType']),
        recipientName: toStr(json['counterpartName'] ?? json['recipientName']),
        isRead: toBool(json['isRead']),
        direction: toStr(json['direction'] ?? 'out'),
        hasUnread: toBool(json['hasUnread']),
        replies: ApiResponseHelper.asMapList(json['replies'])
            .map(MessageReplyModel.fromJson)
            .toList(),
      );
}

/// رد داخل المحادثة (من ولي الأمر أو من المحفّظ/الإدارة).
class MessageReplyModel {
  final int id;
  final String content;
  final String createdAt;
  final String senderName;
  /// لولي الأمر: الرد منه هو. للمحفّظ: الرد من المركز (غير ولي الأمر).
  final bool isMine;

  const MessageReplyModel({
    required this.id,
    required this.content,
    required this.createdAt,
    required this.senderName,
    this.isMine = false,
  });

  factory MessageReplyModel.fromJson(Map<String, dynamic> json) => MessageReplyModel(
        id: toInt(json['id']),
        content: toStr(json['content']),
        createdAt: toStr(json['createdAt']),
        senderName: toStr(json['senderName']),
        isMine: json.containsKey('isMine')
            ? toBool(json['isMine'])
            : (json.containsKey('fromParent') && !toBool(json['fromParent'])),
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
