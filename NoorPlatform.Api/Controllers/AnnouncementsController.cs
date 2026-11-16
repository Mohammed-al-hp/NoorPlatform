using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;
using NoorPlatform.Api.Security;

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

        // ─── فلترة الإعلانات حسب الفئة المستهدفة ونطاق المحفّظ الناشر ───
        // إعلان الإدارة (TeacherId = null) يصل للجميع حسب الفئة، وإعلان المحفّظ لطلاب حلقاته وأولياء أمورهم فقط
        var isAdmin = User.IsInRole("Admin");
        var userId = AuthorizationHelpers.GetUserId(User) ?? -1;
        if (!isAdmin)
        {
            if (User.IsInRole("Teacher"))
            {
                query = query.Where(a =>
                    (a.TeacherId == null && (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Teachers)) ||
                    (a.Teacher != null && a.Teacher.UserId == userId));
            }
            else if (User.IsInRole("Student"))
            {
                query = query.Where(a =>
                    (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Students) &&
                    (a.TeacherId == null || _context.Students.Any(s => s.UserId == userId &&
                        ((s.Circle != null && s.Circle.TeacherId == a.TeacherId) ||
                         s.ExtraEnrollments.Any(e => e.Circle.TeacherId == a.TeacherId)))));
            }
            else if (User.IsInRole("Parent"))
            {
                query = query.Where(a =>
                    (a.Target == AnnouncementTarget.All || a.Target == AnnouncementTarget.Parents) &&
                    (a.TeacherId == null || _context.Students.Any(s => s.Parent != null && s.Parent.UserId == userId &&
                        ((s.Circle != null && s.Circle.TeacherId == a.TeacherId) ||
                         s.ExtraEnrollments.Any(e => e.Circle.TeacherId == a.TeacherId)))));
            }
            else
            {
                query = query.Where(_ => false);
            }
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
                a.Color,
                a.TeacherId,
                AuthorName = a.Teacher != null ? a.Teacher.User.FullName : "إدارة المركز",
                CanManage = isAdmin || (a.Teacher != null && a.Teacher.UserId == userId)
            })
            .ToListAsync();

        return Ok(ann);
    }

    // POST /api/announcements
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateAnnouncementRequest request)
    {
        var error = Validate(request);
        if (error != null)
            return BadRequest(new { message = error });

        var target = AnnouncementTargetMapper.ResolveAnnouncementTarget(request.Target);

        // المحفّظ: الطلاب، أولياء الأمور، الجميع فقط (بدون المحفظين) — وفي نطاق حلقاته
        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && target == AnnouncementTarget.Teachers)
            return Forbid();

        int? teacherId = null;
        if (!isAdmin)
        {
            var userId = AuthorizationHelpers.GetUserId(User) ?? -1;
            teacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            if (teacherId == null)
                return BadRequest(new { message = "ملف المحفّظ غير موجود" });
        }

        var announcement = new Announcement
        {
            Title     = request.Title.Trim(),
            Content   = request.Content.Trim(),
            Target    = target,
            Color     = request.Color ?? "#10b981",
            IsActive  = true,
            CreatedAt = DateTime.UtcNow,
            TeacherId = teacherId
        };

        _context.Announcements.Add(announcement);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = teacherId == null ? "تم نشر الإعلان بنجاح" : "تم نشر الإعلان لطلاب حلقاتك وأولياء أمورهم",
            announcement.Id,
            announcement.Title,
            announcement.Content,
            Target = announcement.Target.ToString(),
            announcement.Color,
            announcement.CreatedAt
        });
    }

    // PUT /api/announcements/{id} — تعديل الإعلان (المشرف أو المحفّظ الناشر)
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateAnnouncementRequest request)
    {
        var ann = await _context.Announcements.Include(a => a.Teacher)
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
        if (ann == null)
            return NotFound(new { message = "الإعلان غير موجود" });
        if (!CanManage(ann))
            return StatusCode(403, new { message = "يمكنك تعديل إعلاناتك فقط" });

        var error = Validate(request);
        if (error != null)
            return BadRequest(new { message = error });

        var target = AnnouncementTargetMapper.ResolveAnnouncementTarget(request.Target);
        if (!User.IsInRole("Admin") && target == AnnouncementTarget.Teachers)
            return Forbid();

        ann.Title = request.Title.Trim();
        ann.Content = request.Content.Trim();
        ann.Target = target;
        if (!string.IsNullOrEmpty(request.Color)) ann.Color = request.Color;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعديل الإعلان" });
    }

    // DELETE /api/announcements/{id} — المشرف أو المحفّظ الناشر
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Delete(int id)
    {
        var ann = await _context.Announcements.Include(a => a.Teacher).FirstOrDefaultAsync(a => a.Id == id);
        if (ann == null)
            return NotFound(new { message = "الإعلان غير موجود" });
        if (!CanManage(ann))
            return StatusCode(403, new { message = "يمكنك حذف إعلاناتك فقط" });

        // حذف ناعم — نضع IsActive = false بدلاً من الحذف الفعلي
        ann.IsActive = false;
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإعلان" });
    }

    private bool CanManage(Announcement ann) =>
        User.IsInRole("Admin") ||
        (ann.Teacher != null && ann.Teacher.UserId == AuthorizationHelpers.GetUserId(User));

    private static string? Validate(CreateAnnouncementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
            return "العنوان والمحتوى مطلوبان";
        if (request.Title.Trim().Length > 150)
            return "العنوان طويل جداً (الحد 150 حرفاً)";
        if (request.Content.Trim().Length > 4000)
            return "المحتوى طويل جداً (الحد 4000 حرف)";
        // التحقق من كود اللون لمنع ثغرات CSS Injection
        if (!string.IsNullOrEmpty(request.Color) && !Regex.IsMatch(request.Color, "^#[0-9A-Fa-f]{6}$"))
            return "تنسيق اللون غير صالح. يجب أن يكون بصيغة Hex، مثال: #10b981";
        return null;
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
