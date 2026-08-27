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

    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetCircles([FromQuery] bool? extrasOnly)
    {
        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.Circles
            .Include(c => c.Teacher!).ThenInclude(t => t!.User)
            .Include(c => c.Students)
            .Include(c => c.Enrollments)
            .AsQueryable();

        if (isTeacher)
            query = query.Where(c => c.Teacher != null && c.Teacher.UserId == userId);

        if (extrasOnly == true)
            query = query.Where(c => c.IsExtra);
        else if (extrasOnly == false)
            query = query.Where(c => !c.IsExtra);

        var circles = await query
            .OrderBy(c => c.IsExtra)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Time,
                c.Location,
                c.Icon,
                c.IsExtra,
                c.SessionDate,
                c.ParentCircleId,
                TeacherId = c.TeacherId,
                TeacherName = c.Teacher != null ? c.Teacher.User.FullName : "لم يحدد",
                StudentCount = c.IsExtra ? c.Enrollments.Count : c.Students.Count
            })
            .ToListAsync();

        return Ok(circles);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetById(int id)
    {
        var circle = await _context.Circles
            .Include(c => c.Teacher!).ThenInclude(t => t!.User)
            .Include(c => c.Students).ThenInclude(s => s.User)
            .Include(c => c.Enrollments).ThenInclude(e => e.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (isTeacher && circle.Teacher?.UserId != currentUserId)
            return Forbid();

        var students = circle.IsExtra
            ? circle.Enrollments.Select(e => new { e.Student.Id, FullName = e.Student.User.FullName, e.Student.Level })
            : circle.Students.Select(s => new { s.Id, FullName = s.User.FullName, s.Level });

        return Ok(new
        {
            circle.Id,
            circle.Name,
            circle.Time,
            circle.Location,
            circle.Icon,
            circle.IsExtra,
            circle.SessionDate,
            circle.ParentCircleId,
            TeacherName = circle.Teacher?.User.FullName ?? "لم يحدد",
            TeacherId = circle.TeacherId,
            Students = students
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateCircleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "اسم الحلقة مطلوب" });

        // المحفظ يمكنه إنشاء حلقات إضافية فقط ضمن حلقاته
        var isTeacherOnly = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacherOnly && !request.IsExtra)
            return Forbid();

        int? teacherId = request.TeacherId;
        if (isTeacherOnly)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            teacherId = await _context.Teachers.Where(t => t.UserId == userId).Select(t => (int?)t.Id).FirstOrDefaultAsync();
            if (teacherId == null)
                return BadRequest(new { message = "ملف المحفظ غير موجود" });
        }
        else if (request.TeacherId.HasValue)
        {
            if (!await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId))
                return NotFound(new { message = "المحفظ غير موجود" });
        }

        if (request.ParentCircleId.HasValue &&
            !await _context.Circles.AnyAsync(c => c.Id == request.ParentCircleId && !c.IsExtra))
            return BadRequest(new { message = "الحلقة الرسمية الأم غير موجودة" });

        var circle = new Circle
        {
            Name = request.Name.Trim(),
            Time = request.Time?.Trim() ?? string.Empty,
            Location = request.Location?.Trim() ?? string.Empty,
            Icon = request.Icon ?? (request.IsExtra ? "➕" : "⭕"),
            TeacherId = teacherId,
            IsExtra = request.IsExtra,
            SessionDate = request.SessionDate,
            ParentCircleId = request.ParentCircleId
        };

        _context.Circles.Add(circle);
        await _context.SaveChangesAsync();

        return Ok(new { message = request.IsExtra ? "تم إنشاء الحلقة الإضافية" : "تم إنشاء الحلقة بنجاح", circleId = circle.Id });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCircleRequest request)
    {
        var circle = await _context.Circles.FindAsync(id);
        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        if (!string.IsNullOrWhiteSpace(request.Name))
            circle.Name = request.Name.Trim();

        if (request.Time != null) circle.Time = request.Time.Trim();
        if (request.Location != null) circle.Location = request.Location.Trim();
        if (request.Icon != null) circle.Icon = request.Icon;
        if (request.IsExtra.HasValue) circle.IsExtra = request.IsExtra.Value;
        if (request.SessionDate.HasValue) circle.SessionDate = request.SessionDate;
        if (request.ClearSessionDate) circle.SessionDate = null;
        if (request.ParentCircleId.HasValue) circle.ParentCircleId = request.ParentCircleId;
        if (request.ClearParentCircle) circle.ParentCircleId = null;

        if (request.RemoveTeacher)
            circle.TeacherId = null;
        else if (request.TeacherId.HasValue)
        {
            if (!await _context.Teachers.AnyAsync(t => t.Id == request.TeacherId))
                return NotFound(new { message = "المحفظ غير موجود" });
            circle.TeacherId = request.TeacherId.Value;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الحلقة" });
    }

    [HttpPost("{id}/enrollments")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> EnrollStudents(int id, [FromBody] EnrollStudentsRequest request)
    {
        var circle = await _context.Circles.FirstOrDefaultAsync(c => c.Id == id);
        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });
        if (!circle.IsExtra)
            return BadRequest(new { message = "التسجيل اليدوي للحلقات الإضافية فقط — الطلاب الرسميون يُربطون بالحلقات الرسمية من ملف الطالب" });

        if (!await NoorPlatform.Api.Security.AuthorizationHelpers.CanAccessCircleAsync(_context, User, id))
            return Forbid();

        var ids = (request.StudentIds ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
            return BadRequest(new { message = "اختر طالباً واحداً على الأقل" });

        var existing = await _context.CircleEnrollments
            .Where(e => e.CircleId == id && ids.Contains(e.StudentId))
            .Select(e => e.StudentId)
            .ToListAsync();

        var toAdd = ids.Except(existing).ToList();
        foreach (var sid in toAdd)
        {
            if (!await _context.Students.AnyAsync(s => s.Id == sid))
                continue;
            _context.CircleEnrollments.Add(new CircleEnrollment { CircleId = id, StudentId = sid });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"تم تسجيل {toAdd.Count} طالب في الحلقة الإضافية", added = toAdd.Count });
    }

    [HttpDelete("{id}/enrollments/{studentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Unenroll(int id, int studentId)
    {
        if (!await NoorPlatform.Api.Security.AuthorizationHelpers.CanAccessCircleAsync(_context, User, id))
            return Forbid();

        var enrollment = await _context.CircleEnrollments
            .FirstOrDefaultAsync(e => e.CircleId == id && e.StudentId == studentId);
        if (enrollment == null)
            return NotFound(new { message = "الطالب غير مسجّل في هذه الحلقة" });

        _context.CircleEnrollments.Remove(enrollment);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم إلغاء تسجيل الطالب من الحلقة الإضافية" });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var circle = await _context.Circles
            .Include(c => c.Students)
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        if (circle.Students.Any())
            return BadRequest(new
            {
                message = $"لا يمكن حذف الحلقة لأنها تحتوي على {circle.Students.Count} طالب. يرجى نقل الطلاب أولاً."
            });

        _context.CircleEnrollments.RemoveRange(circle.Enrollments);
        _context.Circles.Remove(circle);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف الحلقة" });
    }
}

public class CreateCircleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Time { get; set; }
    public string? Location { get; set; }
    public string? Icon { get; set; }
    public int? TeacherId { get; set; }
    public bool IsExtra { get; set; }
    public DateTime? SessionDate { get; set; }
    public int? ParentCircleId { get; set; }
}

public class UpdateCircleRequest
{
    public string? Name { get; set; }
    public string? Time { get; set; }
    public string? Location { get; set; }
    public string? Icon { get; set; }
    public int? TeacherId { get; set; }
    public bool RemoveTeacher { get; set; }
    public bool? IsExtra { get; set; }
    public DateTime? SessionDate { get; set; }
    public bool ClearSessionDate { get; set; }
    public int? ParentCircleId { get; set; }
    public bool ClearParentCircle { get; set; }
}

public class EnrollStudentsRequest
{
    public List<int>? StudentIds { get; set; }
}
