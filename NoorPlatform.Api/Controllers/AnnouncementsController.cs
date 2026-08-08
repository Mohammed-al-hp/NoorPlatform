using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnnouncementsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public AnnouncementsController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/announcements
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var query = _context.Announcements.Where(a => a.IsActive);

        // ─── إصلاح عالي: فلترة الإعلانات لضمان عدم رؤية المستخدم لإعلانات غير موجهة له ───
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin)
        {
            var isTeacher = User.IsInRole("Teacher");
            var isStudent = User.IsInRole("Student");
            var isParent = User.IsInRole("Parent");

            query = query.Where(a => 
                (isTeacher && (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Teachers)) ||
                (isStudent && (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Students)) ||
                (isParent && (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Parents))
            );
        }

        var ann = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.Title,
                a.Content,
                a.CreatedAt,
                a.IsActive,
                Target = a.Target.ToString(),
                a.Color
            })
            .ToListAsync();
            
        return Ok(ann);
    }

    // POST /api/announcements
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "العنوان والمحتوى مطلوبان" });

        // ─── إصلاح متوسط: التحقق من كود اللون لمنع ثغرات CSS Injection ───
        if (!string.IsNullOrEmpty(request.Color) && !Regex.IsMatch(request.Color, "^#[0-9A-Fa-f]{6}$"))
            return BadRequest(new { message = "تنسيق اللون غير صالح. يجب أن يكون بصيغة Hex، مثال: #10b981" });

        var target = AnnouncementTargetMapper.ResolveAnnouncementTarget(request.Target);

        // المحفّظ: الطلاب، أولياء الأمور، الجميع فقط (بدون المحفظين)
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && target == AnnouncementTarget.Teachers)
            return Forbid();

        var announcement = new Announcement
        {
            Title     = request.Title.Trim(),
            Content   = request.Content.Trim(),
            Target    = target,
            Color     = request.Color ?? "#10b981",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم نشر الإعلان بنجاح",
            announcement.Id,
            announcement.Title,
            announcement.Content,
            Target = announcement.Target.ToString(),
            announcement.Color,
            announcement.CreatedAt
        });
    }

    // DELETE /api/announcements/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ann = await _context.Announcements.FindAsync(id);
        if (ann == null)
            return NotFound(new { message = "الإعلان غير موجود" });

        // حذف ناعم — نضع IsActive = false بدلاً من الحذف الفعلي
        ann.IsActive = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإعلان" });
    }
}

// DTO آمن
public class CreateAnnouncementRequest
{
    public string Title   { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Target  { get; set; } = "All"; // All | Students | Parents
    public string? Color  { get; set; }
}

static class AnnouncementTargetMapper
{
    public static AnnouncementTarget ResolveAnnouncementTarget(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AnnouncementTarget.All;

        var t = raw.Trim();
        if (Enum.TryParse<AnnouncementTarget>(t, true, out var parsed))
            return parsed;

        return t switch
        {
            "الجميع" => AnnouncementTarget.All,
            "الطلاب" or "الطلاب فقط" => AnnouncementTarget.Students,
            "أولياء الأمور" => AnnouncementTarget.Parents,
            _ => AnnouncementTarget.All
        };
    }
}
