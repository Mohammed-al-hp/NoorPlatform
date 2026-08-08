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

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var parent = await _context.Parents
            .Include(p => p.Children).ThenInclude(c => c.Circle)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (parent == null)
            return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

        if (!Enum.TryParse<MessageRecipientType>(request.RecipientType, true, out var recipientType))
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
            .Include(m => m.RecipientTeacher).ThenInclude(t => t!.User)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                RecipientType = m.RecipientType.ToString(),
                RecipientName = m.RecipientType == MessageRecipientType.Admin
                    ? "إدارة المركز"
                    : (m.RecipientTeacher != null ? m.RecipientTeacher.User.FullName : "—")
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
            .Include(m => m.SenderUser)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.CreatedAt,
                m.IsRead,
                SenderName = m.SenderUser.FullName
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