using System.Security.Claims;
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

        var (dayStart, dayEnd) = ParseAttendanceDayRange(date);

        var students = await _context.Students
            .Where(s => s.CircleId == circleId)
            .Include(s => s.User)
            .Include(s => s.Attendances.Where(a => a.Date >= dayStart && a.Date < dayEnd))
            .ToListAsync();

        var result = students.Select(s => new
        {
            studentId = s.Id,
            fullName = s.User.FullName,
            status = s.Attendances.FirstOrDefault()?.Status.ToString() ?? "NotRecorded"
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
            .Select(a => new { a.Date, Status = a.Status.ToString() })
            .ToListAsync();

        return Ok(records);
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> MarkAttendance(
        [FromQuery] int? studentId,
        [FromQuery] string? status,
        [FromQuery] string? date,
        [FromBody] MarkAttendanceRequest? body)
    {
        var sid = studentId ?? body?.StudentId;
        var sText = status ?? body?.Status;
        var dateText = date ?? body?.Date;

        if (sid == null || string.IsNullOrEmpty(sText))
            return BadRequest(new { message = "studentId و status مطلوبان" });

        if (!Enum.TryParse<AttendanceStatus>(sText, true, out var parsedStatus))
            return BadRequest(new { message = $"قيمة status غير صالحة: {sText}" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, sid.Value))
            return Forbid();

        var (dayStart, dayEnd) = ParseAttendanceDayRange(dateText);

        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.StudentId == sid && a.Date >= dayStart && a.Date < dayEnd);

        if (record != null)
        {
            record.Status = parsedStatus;
        }
        else
        {
            record = new Attendance
            {
                StudentId = sid.Value,
                Date = dayStart,
                Status = parsedStatus
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
            Status = record.Status.ToString()
        });
    }

    [HttpGet("summary")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var records = await _context.Attendances
            .Where(a => a.Date >= since)
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

    /// <summary>
    /// يحوّل تاريخ الاستعلام (yyyy-MM-dd) إلى نطاق يوم كامل بدون انزياح UTC.
    /// </summary>
    private static (DateTime Start, DateTime End) ParseAttendanceDayRange(string? date)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            var start = today.ToDateTime(TimeOnly.MinValue);
            return (start, start.AddDays(1));
        }

        if (DateOnly.TryParse(date, out var day))
        {
            var start = day.ToDateTime(TimeOnly.MinValue);
            return (start, start.AddDays(1));
        }

        var fallback = DateOnly.FromDateTime(DateTime.Now).ToDateTime(TimeOnly.MinValue);
        return (fallback, fallback.AddDays(1));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> BulkMarkAttendance([FromBody] BulkMarkAttendanceRequest request)
    {
        var (dayStart, dayEnd) = ParseAttendanceDayRange(request.Date);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var userName = User.Identity?.Name ?? "User";

        foreach (var item in request.Records)
        {
            if (!Enum.TryParse<AttendanceStatus>(item.Status, true, out var parsedStatus))
                continue;

            if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, item.StudentId))
                continue;

            var record = await _context.Attendances
                .FirstOrDefaultAsync(a => a.StudentId == item.StudentId && a.Date >= dayStart && a.Date < dayEnd);

            if (record != null)
            {
                record.Status = parsedStatus;
            }
            else
            {
                record = new Attendance
                {
                    StudentId = item.StudentId,
                    Date = dayStart,
                    Status = parsedStatus
                };
                _context.Attendances.Add(record);

                var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == item.StudentId);
                if (student != null && parsedStatus == AttendanceStatus.Present)
                {
                    student.Points += 10;
                }
            }
        }

        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = userId,
            UserName = userName,
            ActivityType = "Attendance",
            Description = $"تم تسجيل الحضور لمجموعة من الطلاب بتاريخ {dayStart:yyyy-MM-dd}",
            Icon = "✅",
            Color = "green"
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حفظ سجل الحضور بنجاح" });
    }
}

public class MarkAttendanceRequest
{
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present";
    public string? Date { get; set; }
}

public class BulkMarkAttendanceRequest
{
    public string? Date { get; set; }
    public List<MarkAttendanceRequest> Records { get; set; } = new();
}
