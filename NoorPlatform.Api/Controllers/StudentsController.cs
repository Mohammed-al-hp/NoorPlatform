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
        var query = StudentQueryHelper.ScopeForUser(_context, User);

        var rawResult = await query.Select(s => new
        {
            s.Id,
            fullName = s.User.FullName,
            s.User.Email,
            s.ParentPhone,
            s.CircleId,
            circleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
            teacherName = s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.User != null ? s.Circle.Teacher.User.FullName : "—",
            s.Level,
            attendance = s.Attendances.Any()
                ? (int)Math.Round(
                    (double)s.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                    / s.Attendances.Count * 100)
                : 0,
            HifzRecordsList = s.HifzRecords.Select(r => new { r.Type, r.VerseCount, r.Verses })
        }).ToListAsync();

        var result = rawResult.Select(s => new
        {
            s.Id,
            s.fullName,
            s.Email,
            s.ParentPhone,
            s.CircleId,
            s.circleName,
            s.teacherName,
            s.Level,
            s.attendance,
            progress = Math.Min(100, (int)Math.Round(
                (double)s.HifzRecordsList
                    .Where(r => r.Type == RecordType.Memorization)
                    .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses)) / 6236.0 * 100))
        });

        return Ok(result);
    }

    [HttpGet("count")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetCount()
    {
        var count = await StudentQueryHelper.ScopeForUser(_context, User).CountAsync();
        return Ok(new { count });
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
            fullName = student.User.FullName,
            student.User.Email,
            student.Level,
            circleName = student.Circle?.Name ?? "بدون حلقة",
            circleId = student.CircleId,
            parentName = !string.IsNullOrWhiteSpace(student.GuardianName)
                ? student.GuardianName
                : student.Parent?.User?.FullName ?? "—",
            parentPhone = student.ParentPhone,
            guardianRelationship = student.GuardianRelationship?.ToString(),
            dateOfBirth = student.DateOfBirth?.ToString("yyyy-MM-dd"),
            registrationDate = student.RegistrationDate.ToString("yyyy-MM-dd"),
            studentPhone = student.StudentPhone,
            residence = student.Residence,
            attendance = student.Attendances.Any()
                ? (int)Math.Round(
                    (double)student.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                    / student.Attendances.Count * 100)
                : 0,
            progress = HifzProgressCalculator.Calculate(student.HifzRecords),
            recentHifz = student.HifzRecords
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
        var validationError = ValidateStudentPayload(request.FullName, request.DateOfBirth, request.GuardianName,
            request.ParentPhone, request.GuardianRelationship, request.RegistrationDate);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        // ─── إصلاح: منع المحفّظ من إضافة طالب لحلقة خارج نطاقه ───
        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher && request.CircleId.HasValue)
        {
            var ownsCircle = await _context.Circles.AnyAsync(c =>
                c.Id == request.CircleId.Value &&
                c.Teacher != null &&
                c.Teacher.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));

            if (!ownsCircle)
                return Forbid();
        }

        var loginPhone = string.IsNullOrWhiteSpace(request.Phone)
            ? request.ParentPhone
            : request.Phone;

        var (user, tempPassword, err) = await _accounts.CreateUserAsync(
            loginPhone, request.FullName.Trim(), UserRole.Student);

        if (err != null)
            return BadRequest(new { message = err });

        Parent? parent = null;
        if (!string.IsNullOrWhiteSpace(request.ParentPhone))
        {
            var (p, _, pErr) = await _accounts.EnsureParentAsync(
                request.GuardianName,
                request.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
        }
        else if (request.ParentId.HasValue)
        {
            parent = await _context.Parents.FindAsync(request.ParentId.Value);
        }

        if (!Enum.TryParse<GuardianRelationship>(request.GuardianRelationship, true, out var relationship))
            return BadRequest(new { message = "صلة القرابة غير صالحة" });

        var student = new Student
        {
            UserId = user.Id,
            Level = request.Level ?? "مبتدئ",
            CircleId = request.CircleId,
            ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone),
            GuardianName = request.GuardianName.Trim(),
            GuardianRelationship = relationship,
            DateOfBirth = request.DateOfBirth,
            RegistrationDate = request.RegistrationDate ?? DateTime.UtcNow.Date,
            StudentPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : AccountProvisioningService.NormalizePhone(request.Phone),
            Residence = string.IsNullOrWhiteSpace(request.Residence) ? null : request.Residence.Trim()
        };

        // ─── إصلاح حرج: استخدام خاصية العلاقة (Navigation) بدل الـ Id الخام
        // لأن parent الجديد لم يُحفظ بعد (Id = 0)، وربط الـ FK مباشرة يسبب فشل الحفظ ───
        if (parent != null)
        {
            student.Parent = parent;
        }
        else if (request.ParentId.HasValue)
        {
            student.ParentId = request.ParentId;
        }

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
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, id))
            return Forbid();

        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var validationError = ValidateStudentPayload(request.FullName, request.DateOfBirth, request.GuardianName,
            request.ParentPhone, request.GuardianRelationship, request.RegistrationDate);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        student.User.FullName = request.FullName!.Trim();
        student.Level = request.Level ?? student.Level;
        student.CircleId = request.CircleId;
        student.ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone!);
        student.GuardianName = request.GuardianName!.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.RegistrationDate = request.RegistrationDate ?? student.RegistrationDate;
        student.Residence = string.IsNullOrWhiteSpace(request.Residence) ? null : request.Residence.Trim();
        student.StudentPhone = string.IsNullOrWhiteSpace(request.StudentPhone)
            ? null
            : AccountProvisioningService.NormalizePhone(request.StudentPhone);

        if (!Enum.TryParse<GuardianRelationship>(request.GuardianRelationship, true, out var relationship))
            return BadRequest(new { message = "صلة القرابة غير صالحة" });
        student.GuardianRelationship = relationship;

        if (!string.IsNullOrWhiteSpace(request.ParentName))
        {
            var (p, _, pErr) = await _accounts.EnsureParentAsync(request.ParentName.Trim(), request.ParentPhone!);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            student.ParentId = p?.Id;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث بيانات الطالب" });
    }

    private static string? ValidateStudentPayload(
        string? fullName,
        DateTime? dateOfBirth,
        string? guardianName,
        string? parentPhone,
        string? guardianRelationship,
        DateTime? registrationDate)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "الاسم الثلاثي مطلوب";
        if (fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3)
            return "يرجى إدخال الاسم الثلاثي كاملاً";
        if (dateOfBirth == null)
            return "تاريخ الميلاد مطلوب";
        if (string.IsNullOrWhiteSpace(guardianName))
            return "اسم ولي الأمر مطلوب";
        if (string.IsNullOrWhiteSpace(parentPhone))
            return "رقم هاتف ولي الأمر مطلوب";
        if (string.IsNullOrWhiteSpace(guardianRelationship))
            return "صلة القرابة مطلوبة";
        if (registrationDate == null)
            return "تاريخ التسجيل مطلوب";
        return null;
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
}

// Request Models
public class CreateStudentRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string GuardianRelationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? Residence { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
    public int? ParentId { get; set; }
}

public class UpdateStudentRequest
{
    public string? FullName { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string GuardianRelationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? StudentPhone { get; set; }
    public string? Residence { get; set; }
    public string? ParentName { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
}
