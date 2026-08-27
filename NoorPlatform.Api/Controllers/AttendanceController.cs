using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly NoorDbContext _context;

    public AttendanceController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/attendance/circle/{circleId}?date=2026-05-16
    [HttpGet("circle/{circleId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetByCircle(int circleId, [FromQuery] string? date)
    {
        if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, circleId))
            return Forbid();

        var circle = await _context.Circles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == circleId);
        if (circle == null)
            return NotFound(new { message = "الحلقة غير موجودة" });

        var (dayStart, dayEnd) = ParseAttendanceDayRange(date);

        List<Student> students;
        if (circle.IsExtra)
        {
            students = await _context.Students
                .Where(s => s.ExtraEnrollments.Any(e => e.CircleId == circleId))
                .Include(s => s.User)
                .Include(s => s.Attendances.Where(a =>
                    a.Date >= dayStart && a.Date < dayEnd && a.CircleId == circleId))
                .ToListAsync();
        }
        else
        {
            students = await _context.Students
                .Where(s => s.CircleId == circleId)
                .Include(s => s.User)
                .Include(s => s.Attendances.Where(a =>
                    a.Date >= dayStart &&
                    a.Date < dayEnd &&
                    (a.CircleId == circleId || a.CircleId == null)))
                .ToListAsync();
        }

        var result = students.Select(s =>
        {
            var att = s.Attendances.FirstOrDefault();
            return new
            {
                studentId = s.Id,
                fullName = s.User.FullName,
                status = att?.Status.ToString() ?? "NotRecorded",
                note = att?.Note,
                circleId = att?.CircleId ?? circleId
            };
        });

        return Ok(result);
    }

    [HttpGet("student/{studentId}")]
    public async Task<IActionResult> GetByStudent(int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var since = DateTime.UtcNow.AddDays(-30).Date;
        var records = await _context.Attendances
            .Where(a => a.StudentId == studentId && a.Date >= since)
            .OrderByDescending(a => a.Date)
            .Select(a => new { a.Date, Status = a.Status.ToString(), a.CircleId, a.Note })
            .ToListAsync();

        return Ok(records);
    }

    /// <summary>
    /// سجل حضور الطالب الحالي بالكامل — الهوية من الـ token فقط (لا يقبل studentId من الطلب).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyAttendance([FromQuery] int? days)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var studentId = await _context.Students.AsNoTracking()
            .Where(s => s.UserId == userId.Value)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (studentId == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });

        // الاستعلام مقيّد دائمًا بـ StudentId المستخرج من التوكن — لا يوجد معامل ID قابل للتلاعب
        var query = _context.Attendances.AsNoTracking()
            .Where(a => a.StudentId == studentId.Value);

        if (days.HasValue)
        {
            var clamped = Math.Clamp(days.Value, 1, 3650);
            var since = DateTime.UtcNow.Date.AddDays(-clamped);
            query = query.Where(a => a.Date >= since);
        }

        var records = await query
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.Id)
            .Select(a => new
            {
                date = a.Date.ToString("yyyy-MM-dd"),
                status = a.Status.ToString(),
                note = a.Note,
                circleId = a.CircleId
            })
            .ToListAsync();

        var present = records.Count(r => r.status == nameof(AttendanceStatus.Present));
        var late = records.Count(r => r.status == nameof(AttendanceStatus.Late));
        var excused = records.Count(r => r.status == nameof(AttendanceStatus.ExcusedAbsence));
        var unexcused = records.Count(r => r.status == nameof(AttendanceStatus.UnexcusedAbsence));
        var total = records.Count;
        var attendanceRate = total > 0
            ? Math.Round((double)(present + late) / total * 100, 1)
            : 0.0;

        return Ok(new
        {
            summary = new
            {
                present,
                late,
                excusedAbsence = excused,
                unexcusedAbsence = unexcused,
                total,
                attendanceRate
            },
            records
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> MarkAttendance(
        [FromQuery] int? studentId,
        [FromQuery] string? status,
        [FromQuery] string? date,
        [FromQuery] int? circleId,
        [FromBody] MarkAttendanceRequest? body)
    {
        var sid = studentId ?? body?.StudentId;
        var sText = status ?? body?.Status;
        var dateText = date ?? body?.Date;
        var cid = circleId ?? body?.CircleId;

        if (sid == null || string.IsNullOrEmpty(sText))
            return BadRequest(new { message = "studentId و status مطلوبان" });

        if (!Enum.TryParse<AttendanceStatus>(sText, true, out var parsedStatus))
            return BadRequest(new { message = $"قيمة status غير صالحة: {sText}" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, sid.Value))
            return Forbid();

        if (cid.HasValue)
        {
            if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, cid.Value))
                return Forbid();

            // للمحفظ: السماح إن كان الطالب في الحلقة الرسمية أو مسجلاً في حلقة إضافية يملكها
            if (User.IsInRole("Teacher") && !User.IsInRole("Admin"))
            {
                var userId = AuthorizationHelpers.GetUserId(User)!.Value;
                var allowed = await IsStudentAllowedForTeacherCircleAsync(sid.Value, cid.Value, userId);
                if (!allowed)
                    return StatusCode(403, new { message = "لا يمكنك تسجيل حضور هذا الطالب في هذه الحلقة" });
            }
        }

        var (dayStart, dayEnd) = ParseAttendanceDayRange(dateText);

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a =>
                a.StudentId == sid &&
                a.Date >= dayStart &&
                a.Date < dayEnd &&
                a.CircleId == cid);

        if (record != null)
        {
            record.Status = parsedStatus;
            record.CircleId = cid;
        }
        else
        {
            record = new Attendance
            {
                StudentId = sid.Value,
                Date = dayStart,
                Status = parsedStatus,
                CircleId = cid
            };
            _context.Attendances.Add(record);

            var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == sid.Value);
            if (student != null)
            {
                if (parsedStatus == AttendanceStatus.Present) student.Points += 10;

                _context.ActivityFeeds.Add(new ActivityFeed
                {
                    UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
                    UserName = User.Identity?.Name ?? "User",
                    ActivityType = "Attendance",
                    Description = $"تم تسجيل حضور الطالب {student.User.FullName}",
                    Icon = "✅",
                    Color = "green"
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "تم تسجيل الحضور",
            record.StudentId,
            record.Date,
            Status = record.Status.ToString(),
            record.CircleId
        });
    }

    [HttpGet("summary")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days).Date;

        // ─── إصلاح: تصفية الحضور حسب حلقات المعلم ───
        var query = _context.Attendances.Where(a => a.Date >= since);

        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            query = query.Where(a => a.Student.Circle != null
                                  && a.Student.Circle.Teacher != null
                                  && a.Student.Circle.Teacher.UserId == userId);
        }

        var records = await query
            .GroupBy(a => a.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Absent = g.Count(a => a.Status == AttendanceStatus.ExcusedAbsence || a.Status == AttendanceStatus.UnexcusedAbsence),
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
                Total = g.Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        return Ok(records);
    }

    private static (DateTime Start, DateTime End) ParseAttendanceDayRange(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            // ─── إصلاح: استخدام UtcNow بدل Now لتوافق بيئة Docker (UTC) ───
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var start = today.ToDateTime(TimeOnly.MinValue);
            return (start, start.AddDays(1));
        }

        if (DateOnly.TryParse(date, out var day))
        {
            var start = day.ToDateTime(TimeOnly.MinValue);
            return (start, start.AddDays(1));
        }

        var fallback = DateOnly.FromDateTime(DateTime.UtcNow).ToDateTime(TimeOnly.MinValue);
        return (fallback, fallback.AddDays(1));
    }

    // ════════════════════════════════════════════════════════
    // دالة الحفظ الجماعي الجديدة التي تعتمد على زر الحفظ اليدوي
    // ════════════════════════════════════════════════════════
    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SaveBulk([FromBody] BulkAttendanceRequest request)
    {
        if (request.Records == null || !request.Records.Any())
            return BadRequest(new { message = "لا توجد سجلات للحفظ" });

        if (!DateOnly.TryParse(request.Date, out var date))
            return BadRequest(new { message = "تاريخ غير صالح" });

        var studentIds = request.Records.Select(r => r.StudentId).Distinct().ToList();
        var circleId = request.CircleId;

        var isTeacherOnly = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (isTeacherOnly)
        {
            if (circleId.HasValue)
            {
                if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, circleId.Value))
                    return Forbid();

                var forbidden = new List<int>();
                foreach (var sid in studentIds)
                {
                    if (!await IsStudentAllowedForTeacherCircleAsync(sid, circleId.Value, userId))
                        forbidden.Add(sid);
                }

                if (forbidden.Any())
                    return StatusCode(403, new { message = "لا يمكنك تسجيل حضور طلاب خارج حلقتك أو غير مسجّلين في الحلقة الإضافية" });
            }
            else
            {
                // بدون CircleId: الحلقة الرسمية للمعلم أو مسجّل في أي حلقة إضافية يملكها
                var allowed = await _context.Students
                    .Where(s => studentIds.Contains(s.Id) &&
                        (
                            (s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.UserId == userId) ||
                            s.ExtraEnrollments.Any(e =>
                                e.Circle.Teacher != null && e.Circle.Teacher.UserId == userId)
                        ))
                    .Select(s => s.Id)
                    .ToListAsync();

                var forbidden = studentIds.Except(allowed).ToList();
                if (forbidden.Any())
                    return StatusCode(403, new { message = "لا يمكنك تسجيل حضور طلاب خارج حلقتك" });
            }
        }
        else if (circleId.HasValue)
        {
            var circleExists = await _context.Circles.AnyAsync(c => c.Id == circleId.Value);
            if (!circleExists)
                return BadRequest(new { message = "الحلقة غير موجودة" });
        }

        var targetDate = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var existing = await _context.Attendances
            .Where(a =>
                a.Date.Date == targetDate.Date &&
                studentIds.Contains(a.StudentId) &&
                a.CircleId == circleId)
            .ToListAsync();

        foreach (var record in request.Records)
        {
            // تم تحسين التحويل ليدعم الحالات الأربعة: حاضر، متأخر، غائب بإذن، غائب بدون إذن
            if (!Enum.TryParse<AttendanceStatus>(record.Status, true, out var status))
                continue;

            var att = existing.FirstOrDefault(a => a.StudentId == record.StudentId && a.CircleId == circleId);
            if (att != null)
            {
                att.Status = status;
                att.Date = targetDate;
                att.Note = record.Note;
                att.CircleId = circleId;
            }
            else
            {
                _context.Attendances.Add(new Attendance
                {
                    StudentId = record.StudentId,
                    Status = status,
                    Date = targetDate,
                    Note = record.Note,
                    CircleId = circleId
                });

                // إضافة نقاط للحاضرين الجدد
                if (status == AttendanceStatus.Present)
                {
                    var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == record.StudentId);
                    if (student != null) student.Points += 10;
                }
            }
        }

        // إضافة سجل في الـ ActivityFeed
        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = userId,
            UserName = User.Identity?.Name ?? "User",
            ActivityType = "Attendance",
            Description = $"تم حفظ حضور جماعي لـ {request.Records.Count} طالب بتاريخ {date:yyyy-MM-dd}",
            Icon = "📝",
            Color = "blue"
        });

        await _context.SaveChangesAsync();

        return Ok(new { message = $"تم حفظ {request.Records.Count} سجل حضور بنجاح" });
    }

    /// <summary>
    /// يسمح للمعلم برصد طالب إذا كان في حلقته الرسمية أو مسجّلاً في حلقته الإضافية المحددة.
    /// </summary>
    private async Task<bool> IsStudentAllowedForTeacherCircleAsync(int studentId, int circleId, int teacherUserId)
    {
        var circle = await _context.Circles.AsNoTracking()
            .Include(c => c.Teacher)
            .FirstOrDefaultAsync(c => c.Id == circleId);

        if (circle?.Teacher == null || circle.Teacher.UserId != teacherUserId)
            return false;

        if (circle.IsExtra)
        {
            var enrolled = await _context.CircleEnrollments
                .AnyAsync(e => e.CircleId == circleId && e.StudentId == studentId);
            if (enrolled)
                return true;

            // أيضاً: طالب حلقته الرسمية تحت نفس المعلم
            return await _context.Students.AnyAsync(s =>
                s.Id == studentId &&
                s.Circle != null &&
                s.Circle.Teacher != null &&
                s.Circle.Teacher.UserId == teacherUserId);
        }

        return await _context.Students.AnyAsync(s =>
            s.Id == studentId && s.CircleId == circleId);
    }
}

// ════════════════════════════════════════════════════════
// Request Models
// ════════════════════════════════════════════════════════

public class MarkAttendanceRequest
{
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present";
    public string? Date { get; set; }
    public int? CircleId { get; set; }
}

public class BulkAttendanceRequest
{
    public string Date { get; set; } = string.Empty;
    public int? CircleId { get; set; }
    public List<AttendanceRecord> Records { get; set; } = new();
}

public class AttendanceRecord
{
    public int StudentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}
