using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly NoorPlatform.Api.Services.InAppNotificationService _notifications;

    public MessagesController(NoorDbContext context, NoorPlatform.Api.Services.InAppNotificationService notifications)
    {
        _context = context;
        _notifications = notifications;
    }

    // POST /api/messages — إرسال رسالة (ولي الأمر فقط حاليًا)
    [HttpPost]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "نص الرسالة مطلوب" });
        if (request.Content.Trim().Length > MaxContentLength)
            return BadRequest(new { message = "الرسالة طويلة جداً (الحد 2000 حرف)" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var parent = await _context.Parents
            .Include(p => p.Children).ThenInclude(c => c.Circle)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (parent == null)
            return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

        // ولي الأمر يراسل المحفّظ أو الإدارة فقط (النوع Parent مخصص للردود)
        if (!Enum.TryParse<MessageRecipientType>(request.RecipientType, true, out var recipientType)
            || recipientType is not (MessageRecipientType.Teacher or MessageRecipientType.Admin))
            return BadRequest(new { message = "نوع المستلم غير صالح" });

        Teacher? recipientTeacher = null;
        if (recipientType == MessageRecipientType.Teacher)
        {
            if (!request.RecipientTeacherId.HasValue)
                return BadRequest(new { message = "يجب تحديد المحفّظ المستلم" });

            // ─── تحقق أمني: المحفّظ المستهدف يجب أن يكون محفّظ أحد أبناء ولي الأمر فعليًا ───
            var isValidTeacher = parent.Children.Any(c =>
                c.Circle != null && c.Circle.TeacherId == request.RecipientTeacherId.Value);

            if (!isValidTeacher)
                return Forbid();

            recipientTeacher = await _context.Teachers.FindAsync(request.RecipientTeacherId.Value);
        }

        // ParentMessageId: متابعة محادثة تخص ولي الأمر نفسه فقط (رسالته أو رد موجّه إليه)
        if (request.ParentMessageId.HasValue)
        {
            var ownsThread = await _context.Messages.AnyAsync(m => m.Id == request.ParentMessageId.Value &&
                (m.SenderUserId == userId || m.RecipientUserId == userId));
            if (!ownsThread)
                return BadRequest(new { message = "الرسالة الأصلية غير موجودة" });
        }

        var message = new Message
        {
            SenderUserId = userId,
            RecipientType = recipientType,
            RecipientTeacherId = recipientTeacher?.Id,
            Content = request.Content.Trim(),
            ParentMessageId = request.ParentMessageId
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        // إشعار المستلم: المحفّظ المحدد أو جميع المشرفين
        var recipients = recipientTeacher != null
            ? new List<int> { recipientTeacher.UserId }
            : await _context.Users.AsNoTracking()
                .Where(u => u.Role == UserRole.Admin && u.IsActive).Select(u => u.Id).ToListAsync();
        var senderName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "ولي أمر";
        await _notifications.NotifyUsersAsync(recipients, NoorPlatform.Api.Services.NotificationKinds.Message,
            $"رسالة جديدة من {senderName}", message.Content, "messages");

        return Ok(new { message = "تم إرسال الرسالة بنجاح", messageId = message.Id });
    }

    // GET /api/messages/sent — رسائل ولي الأمر المُرسَلة
    [HttpGet("sent")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetSentMessages()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var messages = await _context.Messages
            .Where(m => m.SenderUserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                m.IsRead,
                RecipientType = m.RecipientType.ToString(),
                RecipientName = m.RecipientType == MessageRecipientType.Admin
                    ? "إدارة المركز"
                    : (m.RecipientTeacher != null ? m.RecipientTeacher.User.FullName : "—"),
                // ردود المحفّظ/الإدارة على هذه الرسالة
                Replies = _context.Messages
                    .Where(r => r.ParentMessageId == m.Id && r.RecipientUserId == userId)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.Content, r.CreatedAt, SenderName = r.SenderUser.FullName })
                    .ToList()
            })
            .ToListAsync();

        return Ok(messages);
    }

    // GET /api/messages/inbox — محادثات المحفّظ/الإدارة: الواردة من أولياء الأمور + التي بدأها هو مع ولي أمر
    [HttpGet("inbox")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetInbox()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin");
        int? teacherId = null;
        if (!isAdmin)
        {
            var teacher = await _context.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
            if (teacher == null)
                return NotFound(new { message = "لم يُعثر على بيانات المحفّظ" });
            teacherId = teacher.Id;
        }

        // صندوق الإدارة مشترك بين المشرفين: ما بدأه أي مشرف يظهر للجميع
        var roots = isAdmin
            ? _context.Messages.Where(m => m.ParentMessageId == null &&
                (m.RecipientType == MessageRecipientType.Admin ||
                 (m.RecipientType == MessageRecipientType.Parent && m.SenderUser.Role == UserRole.Admin)))
            : _context.Messages.Where(m => m.ParentMessageId == null &&
                ((m.RecipientType == MessageRecipientType.Teacher && m.RecipientTeacherId == teacherId) ||
                 (m.RecipientType == MessageRecipientType.Parent && m.SenderUserId == userId)));

        var messages = await roots
            .OrderByDescending(m => _context.Messages.Where(r => r.ParentMessageId == m.Id).Max(r => (DateTime?)r.CreatedAt) ?? m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                // مقروءة = لا توجد في المحادثة رسالة موجهة لي لم تُقرأ
                IsRead = !((m.RecipientType != MessageRecipientType.Parent && !m.IsRead) ||
                    _context.Messages.Any(r => r.ParentMessageId == m.Id && !r.IsRead &&
                        (isAdmin ? r.RecipientType == MessageRecipientType.Admin
                                 : r.RecipientType == MessageRecipientType.Teacher && r.RecipientTeacherId == teacherId))),
                SenderName = m.SenderUser.FullName,
                Direction = m.RecipientType == MessageRecipientType.Parent ? "out" : "in",
                CounterpartName = m.RecipientType == MessageRecipientType.Parent
                    ? (m.RecipientUser != null ? m.RecipientUser.FullName : "ولي الأمر")
                    : m.SenderUser.FullName,
                Replies = _context.Messages
                    .Where(r => r.ParentMessageId == m.Id)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.Content,
                        r.CreatedAt,
                        SenderName = r.SenderUser.FullName,
                        FromParent = r.SenderUser.Role == UserRole.Parent
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(messages);
    }

    // PATCH /api/messages/{id}/read — تعليم المحادثة كمقروءة
    [HttpPatch("{id}/read")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var root = await FindRootAsync(id);
        if (root == null)
            return NotFound();
        if (!await IsStaffParticipantAsync(root))
            return Forbid();

        var addressedToMe = await StaffAddressFilterAsync();
        var unread = await _context.Messages
            .Where(m => (m.Id == root.Id || m.ParentMessageId == root.Id) && !m.IsRead)
            .Where(addressedToMe)
            .ToListAsync();
        foreach (var m in unread) m.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم التعليم كمقروءة" });
    }

    // POST /api/messages/{id}/reply — رد المحفّظ/الإدارة داخل محادثة مع ولي أمر
    [HttpPost("{id}/reply")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "نص الرد مطلوب" });
        if (request.Content.Trim().Length > MaxContentLength)
            return BadRequest(new { message = "الرد طويل جداً (الحد 2000 حرف)" });

        var root = await FindRootAsync(id);
        if (root == null)
            return NotFound(new { message = "الرسالة غير موجودة" });
        if (!await IsStaffParticipantAsync(root))
            return Forbid();

        var parentUserId = root.RecipientType == MessageRecipientType.Parent ? root.RecipientUserId : root.SenderUserId;
        if (parentUserId == null)
            return BadRequest(new { message = "تعذر تحديد ولي الأمر في هذه المحادثة" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _context.Messages.Add(new Message
        {
            SenderUserId = userId,
            RecipientType = MessageRecipientType.Parent,
            RecipientUserId = parentUserId,
            ParentMessageId = root.Id,
            Content = request.Content.Trim()
        });

        // الرد يعني قراءة ما وصل في المحادثة
        var addressedToMe = await StaffAddressFilterAsync();
        var unread = await _context.Messages
            .Where(m => (m.Id == root.Id || m.ParentMessageId == root.Id) && !m.IsRead)
            .Where(addressedToMe)
            .ToListAsync();
        foreach (var m in unread) m.IsRead = true;

        await _context.SaveChangesAsync();
        await _notifications.NotifyUsersAsync(new[] { parentUserId.Value },
            NoorPlatform.Api.Services.NotificationKinds.MessageReply,
            "رد جديد على رسالتك", request.Content.Trim(), "messages");
        return Ok(new { message = "تم إرسال الرد إلى ولي الأمر" });
    }

    // POST /api/messages/to-parent — المحفّظ/الإدارة يبدأ رسالة جديدة لولي أمر
    [HttpPost("to-parent")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendToParent([FromBody] SendToParentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "نص الرسالة مطلوب" });
        if (request.Content.Trim().Length > MaxContentLength)
            return BadRequest(new { message = "الرسالة طويلة جداً (الحد 2000 حرف)" });

        var allowed = await ParentRecipientsQuery().AnyAsync(p => p.UserId == request.ParentUserId);
        if (!allowed)
            return BadRequest(new { message = "ولي الأمر غير موجود أو ليس من أولياء أمور طلابك" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var message = new Message
        {
            SenderUserId = userId,
            RecipientType = MessageRecipientType.Parent,
            RecipientUserId = request.ParentUserId,
            Content = request.Content.Trim()
        };
        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        var senderName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "المركز";
        await _notifications.NotifyUsersAsync(new[] { request.ParentUserId },
            NoorPlatform.Api.Services.NotificationKinds.Message,
            $"رسالة جديدة من {senderName}", message.Content, "messages");
        return Ok(new { message = "تم إرسال الرسالة إلى ولي الأمر", messageId = message.Id });
    }

    // GET /api/messages/parent-recipients — أولياء الأمور الذين يمكن مراسلتهم (كلهم للإدارة، أولياء طلابه للمحفّظ)
    [HttpGet("parent-recipients")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetParentRecipients()
    {
        var parents = await ParentRecipientsQuery()
            .OrderBy(p => p.User.FullName)
            .Select(p => new
            {
                userId = p.UserId,
                fullName = p.User.FullName,
                children = p.Children.Select(c => c.User.FullName).ToList()
            })
            .ToListAsync();
        return Ok(parents);
    }

    // GET /api/messages/threads — محادثات ولي الأمر: ما أرسله وما بدأه المحفّظ/الإدارة معه، مع كل الردود
    [HttpGet("threads")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetParentThreads()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var threads = await _context.Messages
            .Where(m => m.ParentMessageId == null && (m.SenderUserId == userId || m.RecipientUserId == userId))
            .OrderByDescending(m => _context.Messages.Where(r => r.ParentMessageId == m.Id).Max(r => (DateTime?)r.CreatedAt) ?? m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                Direction = m.SenderUserId == userId ? "out" : "in",
                CounterpartName = m.SenderUserId == userId
                    ? (m.RecipientType == MessageRecipientType.Admin
                        ? "إدارة المركز"
                        : (m.RecipientTeacher != null ? m.RecipientTeacher.User.FullName : "—"))
                    : m.SenderUser.FullName,
                HasUnread = (m.RecipientUserId == userId && !m.IsRead) ||
                    _context.Messages.Any(r => r.ParentMessageId == m.Id && r.RecipientUserId == userId && !r.IsRead),
                Replies = _context.Messages
                    .Where(r => r.ParentMessageId == m.Id)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.Content, r.CreatedAt, SenderName = r.SenderUser.FullName, IsMine = r.SenderUserId == userId })
                    .ToList()
            })
            .ToListAsync();

        // فتح المحادثات يعني قراءة ما وصل لولي الأمر
        var unread = await _context.Messages.Where(m => m.RecipientUserId == userId && !m.IsRead).ToListAsync();
        if (unread.Count > 0)
        {
            foreach (var m in unread) m.IsRead = true;
            await _context.SaveChangesAsync();
        }
        return Ok(threads);
    }

    // POST /api/messages/{id}/parent-reply — رد ولي الأمر داخل محادثة (يُوجَّه تلقائياً للطرف الآخر)
    [HttpPost("{id}/parent-reply")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> ParentReply(int id, [FromBody] ReplyMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "نص الرد مطلوب" });
        if (request.Content.Trim().Length > MaxContentLength)
            return BadRequest(new { message = "الرد طويل جداً (الحد 2000 حرف)" });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var root = await FindRootAsync(id);
        if (root == null || (root.SenderUserId != userId && root.RecipientUserId != userId))
            return NotFound(new { message = "الرسالة غير موجودة" });

        // الطرف الآخر: مستلم رسالة ولي الأمر الأصلية، أو مُرسِل الرسالة التي بدأها المحفّظ/الإدارة
        MessageRecipientType recipientType;
        int? recipientTeacherId;
        if (root.SenderUserId == userId)
        {
            recipientType = root.RecipientType;
            recipientTeacherId = root.RecipientTeacherId;
        }
        else
        {
            var teacher = await _context.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == root.SenderUserId);
            recipientType = teacher != null ? MessageRecipientType.Teacher : MessageRecipientType.Admin;
            recipientTeacherId = teacher?.Id;
        }

        var reply = new Message
        {
            SenderUserId = userId,
            RecipientType = recipientType,
            RecipientTeacherId = recipientTeacherId,
            ParentMessageId = root.Id,
            Content = request.Content.Trim()
        };
        _context.Messages.Add(reply);
        await _context.SaveChangesAsync();

        var recipients = recipientTeacherId.HasValue
            ? await _context.Teachers.Where(t => t.Id == recipientTeacherId.Value).Select(t => t.UserId).ToListAsync()
            : await _context.Users.AsNoTracking().Where(u => u.Role == UserRole.Admin && u.IsActive).Select(u => u.Id).ToListAsync();
        var senderName = await _context.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync() ?? "ولي أمر";
        await _notifications.NotifyUsersAsync(recipients, NoorPlatform.Api.Services.NotificationKinds.Message,
            $"رد جديد من {senderName}", reply.Content, "messages");
        return Ok(new { message = "تم إرسال الرد" });
    }

    private const int MaxContentLength = 2000;

    /// <summary>أول رسالة في المحادثة (الردود تُربط دائماً بالرسالة الأصلية).</summary>
    private async Task<Message?> FindRootAsync(int id)
    {
        var msg = await _context.Messages.FindAsync(id);
        if (msg?.ParentMessageId is int rootId)
            msg = await _context.Messages.FindAsync(rootId);
        return msg;
    }

    /// <summary>المحفّظ/المشرف طرف في المحادثة: موجهة إليه، أو بدأها هو (أو أي مشرف لصندوق الإدارة).</summary>
    private async Task<bool> IsStaffParticipantAsync(Message root)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (User.IsInRole("Admin"))
        {
            if (root.RecipientType == MessageRecipientType.Admin) return true;
            return root.RecipientType == MessageRecipientType.Parent &&
                   await _context.Users.AnyAsync(u => u.Id == root.SenderUserId && u.Role == UserRole.Admin);
        }
        var teacher = await _context.Teachers.AsNoTracking().FirstOrDefaultAsync(t => t.UserId == userId);
        if (teacher == null) return false;
        return (root.RecipientType == MessageRecipientType.Teacher && root.RecipientTeacherId == teacher.Id) ||
               (root.RecipientType == MessageRecipientType.Parent && root.SenderUserId == userId);
    }

    /// <summary>شرط «الرسالة موجهة للمستخدم الحالي» (للإدارة أو لملف المحفّظ).</summary>
    private async Task<System.Linq.Expressions.Expression<Func<Message, bool>>> StaffAddressFilterAsync()
    {
        if (User.IsInRole("Admin"))
            return m => m.RecipientType == MessageRecipientType.Admin;
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var teacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync();
        return m => m.RecipientType == MessageRecipientType.Teacher && m.RecipientTeacherId == teacherId;
    }

    /// <summary>أولياء الأمور المسموح مراسلتهم: كلهم للإدارة، وأولياء طلاب حلقاته (الرسمية والإضافية) للمحفّظ.</summary>
    private IQueryable<Parent> ParentRecipientsQuery()
    {
        if (User.IsInRole("Admin"))
            return _context.Parents.Where(p => p.User.IsActive);
        var students = NoorPlatform.Api.Security.StudentQueryHelper.ScopeForUser(_context, User);
        return _context.Parents.Where(p => p.User.IsActive && students.Any(s => s.ParentId == p.Id));
    }

    // GET /api/messages/available-recipients — قائمة محفّظي أبناء ولي الأمر (لملء القائمة المنسدلة بالواجهة)
    [HttpGet("available-recipients")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetAvailableRecipients()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var teachers = await _context.Parents
            .Where(p => p.UserId == userId)
            .SelectMany(p => p.Children)
            .Where(c => c.Circle != null && c.Circle.Teacher != null)
            .Select(c => new { c.Circle!.Teacher!.Id, c.Circle.Teacher.User.FullName })
            .Distinct()
            .ToListAsync();

        return Ok(teachers);
    }
}

public class SendMessageRequest
{
    public string RecipientType { get; set; } = string.Empty; // "Teacher" | "Admin"
    public int? RecipientTeacherId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int? ParentMessageId { get; set; }
}
public class ReplyMessageRequest
{
    public string Content { get; set; } = string.Empty;
}

public class SendToParentRequest
{
    public int ParentUserId { get; set; }
    public string Content { get; set; } = string.Empty;
}
