using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

/// <summary>
/// مركز الإشعارات داخل التطبيق (جرس الإشعارات). كل مستخدم يرى إشعاراته فقط،
/// والهوية من التوكن دائماً. (إشعارات واتساب الخارجية في NotificationsController.)
/// </summary>
[ApiController]
[Route("api/inbox")]
[Authorize]
public class InboxController : ControllerBase
{
    private readonly NoorDbContext _context;

    public InboxController(NoorDbContext context) => _context = context;

    // GET /api/inbox?unreadOnly=false&take=30&beforeId=
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool unreadOnly = false, [FromQuery] int take = 30, [FromQuery] int? beforeId = null)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();

        var q = _context.UserNotifications.AsNoTracking().Where(n => n.UserId == userId.Value);
        if (unreadOnly) q = q.Where(n => !n.IsRead);
        if (beforeId.HasValue) q = q.Where(n => n.Id < beforeId.Value);

        var items = await q.OrderByDescending(n => n.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new { n.Id, n.Kind, n.Title, n.Body, n.Link, n.StudentId, n.IsRead, n.CreatedAt })
            .ToListAsync();
        var unread = await _context.UserNotifications.CountAsync(n => n.UserId == userId.Value && !n.IsRead);
        return Ok(new { unread, items });
    }

    // GET /api/inbox/unread-count — للسحب الدوري الخفيف لشارة الجرس
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();
        var unread = await _context.UserNotifications.CountAsync(n => n.UserId == userId.Value && !n.IsRead);
        var latestId = await _context.UserNotifications.Where(n => n.UserId == userId.Value)
            .OrderByDescending(n => n.Id).Select(n => (int?)n.Id).FirstOrDefaultAsync();
        return Ok(new { unread, latestId });
    }

    // POST /api/inbox/{id}/read
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        var n = await _context.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n == null) return NotFound(new { message = "الإشعار غير موجود" });
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return Ok(new { message = "تم تعليم الإشعار كمقروء" });
    }

    // POST /api/inbox/read-all
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null) return Unauthorized();
        var unread = await _context.UserNotifications.Where(n => n.UserId == userId.Value && !n.IsRead).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var n in unread) { n.IsRead = true; n.ReadAt = now; }
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعليم جميع الإشعارات كمقروءة", updated = unread.Count });
    }

    // DELETE /api/inbox/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        var n = await _context.UserNotifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        if (n == null) return NotFound(new { message = "الإشعار غير موجود" });
        _context.UserNotifications.Remove(n);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف الإشعار" });
    }
}
