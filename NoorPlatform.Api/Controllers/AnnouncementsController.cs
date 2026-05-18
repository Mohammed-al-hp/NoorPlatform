using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using Microsoft.AspNetCore.Authorization;

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
        var ann = await _context.Announcements
            .Where(a => a.IsActive)
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

        // تحويل النص إلى Enum
        if (!Enum.TryParse<AnnouncementTarget>(request.Target, true, out var target))
            target = AnnouncementTarget.All;

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
    public string Target  { get; set; } = "All"; // All | Teachers | Students | Parents
    public string? Color  { get; set; }
}
