namespace NoorPlatform.Core.Entities;

public enum MessageRecipientType
{
    Teacher,    // موجهة لمحفّظ محدد
    Admin       // موجهة للإدارة
}

public class Message
{
    public int Id { get; set; }

    public int SenderUserId { get; set; }
    public User SenderUser { get; set; } = null!;

    public MessageRecipientType RecipientType { get; set; }

    // فقط لو RecipientType == Teacher
    public int? RecipientTeacherId { get; set; }
    public Teacher? RecipientTeacher { get; set; }

    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;

    // للردود (Thread بسيط)
    public int? ParentMessageId { get; set; }
    public Message? ParentMessage { get; set; }
}