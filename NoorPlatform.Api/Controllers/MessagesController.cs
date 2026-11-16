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

    public MessagesController(NoorDbContext context)
    {
        _context = context;
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

    // GET /api/messages/inbox — الرسائل الواردة (للمحفّظ أو الأدمن)
    [HttpGet("inbox")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetInbox()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin");

        IQueryable<Message> query;

        if (isAdmin)
        {
            query = _context.Messages.Where(m => m.RecipientType == MessageRecipientType.Admin);
        }
        else
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (teacher == null)
                return NotFound(new { message = "لم يُعثر على بيانات المحفّظ" });

            query = _context.Messages.Where(m =>
                m.RecipientType == MessageRecipientType.Teacher &&
                m.RecipientTeacherId == teacher.Id);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                m.IsRead,
                SenderName = m.SenderUser.FullName,
                Replies = _context.Messages
                    .Where(r => r.ParentMessageId == m.Id && r.RecipientType == MessageRecipientType.Parent)
                    .OrderBy(r => r.CreatedAt)
                    .Select(r => new { r.Id, r.Content, r.CreatedAt, SenderName = r.SenderUser.FullName })
                    .ToList()
            })
            .ToListAsync();

        return Ok(messages);
    }

    // PATCH /api/messages/{id}/read — تعليم كمقروءة
    [HttpPatch("{id}/read")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var message = await _context.Messages.FindAsync(id);
        if (message == null)
            return NotFound();

        // ─── تحقق أمني: التأكد أن الرسالة فعليًا موجهة لهذا المستخدم ───
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isAdmin = User.IsInRole("Admin");

        if (isAdmin)
        {
            if (message.RecipientType != MessageRecipientType.Admin)
                return Forbid();
        }
        else
        {
            var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
            if (teacher == null || message.RecipientTeacherId != teacher.Id)
                return Forbid();
        }

        message.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم التعليم كمقروءة" });
    }

    // POST /api/messages/{id}/reply — رد المحفّظ/الإدارة على رسالة ولي أمر
    [HttpPost("{id}/reply")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Reply(int id, [FromBody] ReplyMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "نص الرد مطلوب" });
        if (request.Content.Trim().Length > MaxContentLength)
            return BadRequest(new { message = "الرد طويل جداً (الحد 2000 حرف)" });

        var original = await _context.Messages.FindAsync(id);
        if (original == null)
            return NotFound(new { message = "الرسالة غير موجودة" });

        if (!await IsAddressedToCurrentUserAsync(original))
            return Forbid();

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        _context.Messages.Add(new Message
        {
            SenderUserId = userId,
            RecipientType = MessageRecipientType.Parent,
            RecipientUserId = original.SenderUserId,
            ParentMessageId = original.Id,
            Content = request.Content.Trim()
        });
        original.IsRead = true;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم إرسال الرد إلى ولي الأمر" });
    }

    private const int MaxContentLength = 2000;

    /// <summary>الرسالة موجهة للمستخدم الحالي: للإدارة إن كان مشرفاً، أو لملف المحفّظ الحالي.</summary>
    private async Task<bool> IsAddressedToCurrentUserAsync(Message message)
    {
        if (User.IsInRole("Admin"))
            return message.RecipientType == MessageRecipientType.Admin;
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var teacher = await _context.Teachers.FirstOrDefaultAsync(t => t.UserId == userId);
        return teacher != null && message.RecipientType == MessageRecipientType.Teacher && message.RecipientTeacherId == teacher.Id;
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
