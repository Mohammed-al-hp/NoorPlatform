using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly AccountProvisioningService _accounts;

    public StudentsController(NoorDbContext context, AccountProvisioningService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    // GET /api/students
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAll()
    {
        var isTeacher = User.IsInRole("Teacher");
        var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        var query = _context.Students.AsQueryable();

        if (isTeacher)
        {
            query = query.Where(s => s.Circle!.Teacher!.UserId == userId);
        }

        var result = await query.Select(s => new
        {
            s.Id,
            FullName = s.User.FullName,
            s.User.Email,
            s.ParentPhone,
            s.CircleId,
            CircleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
            s.Level,
            Attendance = s.Attendances.Any()
                ? (int)Math.Round(
                    (double)s.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                    / s.Attendances.Count * 100)
                : 0,
            Progress = (int)Math.Min(100, Math.Round(
                (double)s.HifzRecords
                    .Where(r => r.Type == RecordType.Memorization)
                    .Sum(r => r.VerseCount > 0 ? r.VerseCount : 0) / 6236.0 * 100))
        }).ToListAsync();

        return Ok(result);
    }

    // GET /api/students/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, id))
            return Forbid();

        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Include(s => s.Parent).ThenInclude(p => p!.User)
            .Include(s => s.Attendances)
            .Include(s => s.HifzRecords)
            .Include(s => s.ExamResults)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        return Ok(new
        {
            student.Id,
            student.User.FullName,
            student.User.Email,
            student.Level,
            CircleName = student.Circle?.Name ?? "بدون حلقة",
            ParentName = student.Parent?.User?.FullName ?? "—",
            Attendance = student.Attendances.Any()
                ? (int)Math.Round(
                    (double)student.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                    / student.Attendances.Count * 100)
                : 0,
            // ✅ إصلاح 1: استخدام VerseCount الفعلي
            Progress = CalculateHifzProgress(student.HifzRecords),
            RecentHifz = student.HifzRecords
                                .OrderByDescending(r => r.Date)
                                .Take(5)
                                .Select(r => new { r.SurahName, r.Verses, r.VerseCount, r.Type, r.Evaluation, r.Date })
        });
    }

    // POST /api/students
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "الاسم ورقم الهاتف مطلوبان" });

        var (user, tempPassword, err) = await _accounts.CreateUserAsync(
            request.Phone, request.FullName, UserRole.Student);

        if (err != null)
            return BadRequest(new { message = err });

        Parent? parent = null;
        if (!string.IsNullOrWhiteSpace(request.ParentPhone))
        {
            var (p, _, pErr) = await _accounts.EnsureParentAsync(
                request.ParentName ?? "ولي أمر",
                request.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
        }
        else if (request.ParentId.HasValue)
        {
            parent = await _context.Parents.FindAsync(request.ParentId.Value);
        }

        var student = new Student
        {
            UserId = user.Id,
            Level = request.Level ?? "مبتدئ",
            CircleId = request.CircleId,
            ParentId = parent?.Id ?? request.ParentId,
            ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone ?? string.Empty)
        };

        _context.Students.Add(student);

        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "User",
            ActivityType = "Student",
            Description = $"تمت إضافة الطالب الجديد {request.FullName}",
            Icon = "🎓",
            Color = "blue"
        });

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إضافة الطالب بنجاح",
            studentId = student.Id,
            userId = user.Id,
            credentials = new AccountCredentialsDto(
                request.FullName,
                user.UserName!,
                AccountProvisioningService.ToDisplayPhone(user.UserName!),
                tempPassword,
                UserRole.Student.ToString(),
                true)
        });
    }

    // PUT /api/students/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStudentRequest request)
    {
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        if (!string.IsNullOrEmpty(request.FullName))
            student.User.FullName = request.FullName;

        if (!string.IsNullOrEmpty(request.Level))
            student.Level = request.Level;

        if (request.CircleId.HasValue)
            student.CircleId = request.CircleId;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث بيانات الطالب" });
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        student.IsDeleted = true;
        
        _context.ActivityFeeds.Add(new ActivityFeed {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "أرشفة طالب",
            Description = $"تم أرشفة الطالب {student.User.FullName}",
            Icon = "📦",
            Color = "text-gray-500"
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم أرشفة الطالب بنجاح" });
    }

    [HttpGet("archived")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetArchivedStudents()
    {
        var students = await _context.Students
            .IgnoreQueryFilters()
            .Where(s => s.IsDeleted)
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Select(s => new
            {
                s.Id,
                s.User.FullName,
                s.User.UserName,
                s.Level,
                CircleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
                s.ParentPhone,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreStudent(int id)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود في الأرشيف" });

        student.IsDeleted = false;

        _context.ActivityFeeds.Add(new ActivityFeed {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "استعادة طالب",
            Description = $"تمت استعادة الطالب {student.User.FullName} من الأرشيف",
            Icon = "🔄",
            Color = "text-green-500"
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم استعادة الطالب بنجاح" });
    }

    // ─────────────────────────────────────────────────
    // Helper: حساب نسبة تقدم الحفظ الحقيقية
    // ─────────────────────────────────────────────────
    private static int CalculateHifzProgress(IEnumerable<HifzRecord> records)
    {
        // ✅ إصلاح 1: نجمع الآيات الفعلية
        var totalVerses = records
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0
                        ? r.VerseCount
                        : HifzRecord.ParseVerseCount(r.Verses));

        return Math.Min((int)Math.Round((double)totalVerses / 6236 * 100), 100);
    }
}

// Request Models
public class CreateStudentRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
    public int? ParentId { get; set; }
}

public class UpdateStudentRequest
{
    public string? FullName { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
}
