using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CirclesController : ControllerBase
{
    private readonly NoorDbContext _context;

    public CirclesController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/circles
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetCircles()
    {
        var isTeacher = User.IsInRole("Teacher");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.Circles
            .Include(c => c.Teacher!).ThenInclude(t => t!.User)
            .Include(c => c.Students)
            .AsQueryable();

        if (isTeacher)
        {
            query = query.Where(c => c.Teacher != null && c.Teacher.UserId == userId);
        }

        var circles = await query
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Time,
                c.Location,
                c.Icon,
                TeacherId   = c.TeacherId,
                TeacherName = c.Teacher != null ? c.Teacher.User.FullName : "لم يحدد",
                StudentCount = c.Students.Count
            })
            .ToListAsync();

        return Ok(circles);
    }

    // GET /api/circles/{id}
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetById(int id)
    {
        var circle = await _context.Circles
            .Include(c => c.Teacher!).ThenInclude(t => t!.User)
            .Include(c => c.Students).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        // ─── إصلاح حرج: التحقق من أن المحفظ يملك الحلقة (منع ثغرة IDOR) ───
        var isTeacher = User.IsInRole("Teacher");
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        
        if (isTeacher && circle.Teacher?.UserId != currentUserId)
            return Forbid();

        return Ok(new
        {
            circle.Id,
            circle.Name,
            circle.Time,
            circle.Location,
            circle.Icon,
            TeacherName = circle.Teacher?.User.FullName ?? "لم يحدد",
            Students = circle.Students.Select(s => new
            {
                s.Id,
                FullName = s.User.FullName,
                s.Level
            })
        });
    }

    // POST /api/circles — Admin فقط
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCircleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "اسم الحلقة مطلوب" });

        // التحقق من وجود المحفظ إذا تم تحديده
        if (request.TeacherId.HasValue)
        {
            var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId);
            if (!teacherExists)
                return NotFound(new { message = "المحفظ غير موجود" });
        }

        var circle = new Circle
        {
            Name      = request.Name.Trim(),
            Time      = request.Time?.Trim()     ?? string.Empty,
            Location  = request.Location?.Trim() ?? string.Empty,
            Icon      = request.Icon             ?? "⭕",
            TeacherId = request.TeacherId
        };

        _context.Circles.Add(circle);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم إنشاء الحلقة بنجاح", circleId = circle.Id });
    }

    // PUT /api/circles/{id} — Admin فقط
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCircleRequest request)
    {
        var circle = await _context.Circles.FindAsync(id);
        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            circle.Name = request.Name.Trim();

        if (request.Time     != null) circle.Time     = request.Time.Trim();
        if (request.Location != null) circle.Location = request.Location.Trim();
        if (request.Icon     != null) circle.Icon     = request.Icon;

        // ─── إصلاح: إتاحة إزالة المحفظ بشكل صريح ───
        if (request.RemoveTeacher)
        {
            circle.TeacherId = null;
        }
        else if (request.TeacherId.HasValue)
        {
            var teacherExists = await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId);
            if (!teacherExists)
                return NotFound(new { message = "المحفظ غير موجود" });
            circle.TeacherId = request.TeacherId.Value;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الحلقة" });
    }

    // DELETE /api/circles/{id} — Admin فقط
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var circle = await _context.Circles
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        if (circle.Students.Any())
            return BadRequest(new
            {
                message = $"لا يمكن حذف الحلقة لأنها تحتوي على {circle.Students.Count} طالب. يرجى نقل الطلاب أولاً."
            });

        _context.Circles.Remove(circle);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف الحلقة" });
    }
}

public class CreateCircleRequest
{
    public string  Name       { get; set; } = string.Empty;
    public string? Time       { get; set; }
    public string? Location   { get; set; }
    public string? Icon       { get; set; }
    public int?    TeacherId  { get; set; }
}

public class UpdateCircleRequest
{
    public string? Name      { get; set; }
    public string? Time      { get; set; }
    public string? Location  { get; set; }
    public string? Icon      { get; set; }
    public int?    TeacherId { get; set; }
    
    // حقل جديد لتمكين إزالة المحفظ بشكل صريح
    public bool RemoveTeacher { get; set; }
}
