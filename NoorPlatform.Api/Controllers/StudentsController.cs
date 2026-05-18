using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly UserManager<User> _userManager;

    public StudentsController(NoorDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // GET /api/students
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var students = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Include(s => s.Attendances)
            .Include(s => s.HifzRecords)
            .ToListAsync();

        var result = students.Select(s => new
        {
            s.Id,
            s.User.FullName,
            s.User.Email,
            CircleName = s.Circle?.Name ?? "بدون حلقة",
            s.Level,
            Attendance = s.Attendances.Any()
                ? (int)Math.Round(
                    (double)s.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                    / s.Attendances.Count * 100)
                : 0,
            // ✅ إصلاح 1: استخدام VerseCount الفعلي
            Progress = CalculateHifzProgress(s.HifzRecords)
        });

        return Ok(result);
    }

    // GET /api/students/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
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
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return BadRequest(new { message = "هذا البريد الإلكتروني مستخدم بالفعل" });

        var user = new User
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName,
            Role = UserRole.Student,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { message = string.Join("، ", createResult.Errors.Select(e => e.Description)) });

        var student = new Student
        {
            UserId = user.Id,
            Level = request.Level ?? "مبتدئ",
            CircleId = request.CircleId,
            ParentId = request.ParentId
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إضافة الطالب بنجاح",
            studentId = student.Id,
            userId = user.Id
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

        await _userManager.DeleteAsync(student.User);
        return Ok(new { message = "تم حذف الطالب" });
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
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = "Noor@1234";
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