using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

    // ─────────────────────────────────────────────────
    // GET /api/attendance/circle/{circleId}?date=2026-05-16
    // ─────────────────────────────────────────────────
    [HttpGet("circle/{circleId}")]
    public async Task<IActionResult> GetByCircle(int circleId, [FromQuery] DateTime? date)
    {
        var targetDate = (date ?? DateTime.UtcNow).Date;

        var students = await _context.Students
            .Where(s => s.CircleId == circleId)
            .Include(s => s.User)
            .Include(s => s.Attendances.Where(a => a.Date.Date == targetDate))
            .ToListAsync();

        var result = students.Select(s => new
        {
            StudentId = s.Id,
            s.User.FullName,
            Status = s.Attendances.FirstOrDefault()?.Status.ToString() ?? "NotRecorded"
        });

        return Ok(result);
    }

    // ─────────────────────────────────────────────────
    // GET /api/attendance/student/{studentId}
    // سجل حضور طالب معين (آخر 30 يوم)
    // ─────────────────────────────────────────────────
    [HttpGet("student/{studentId}")]
    public async Task<IActionResult> GetByStudent(int studentId)
    {
        var since = DateTime.UtcNow.AddDays(-30).Date;
        var records = await _context.Attendances
            .Where(a => a.StudentId == studentId && a.Date.Date >= since)
            .OrderByDescending(a => a.Date)
            .Select(a => new { a.Date, Status = a.Status.ToString() })
            .ToListAsync();

        return Ok(records);
    }

    // ─────────────────────────────────────────────────
    // POST /api/attendance?studentId=1&status=Present
    // يدعم query string للتوافق مع الكود الحالي في Frontend
    // ─────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> MarkAttendance(
        [FromQuery] int? studentId,
        [FromQuery] string? status,
        [FromBody] MarkAttendanceRequest? body)
    {
        // يقبل الطلب من query string أو من body
        var sid = studentId ?? body?.StudentId;
        var sText = status ?? body?.Status;

        if (sid == null || string.IsNullOrEmpty(sText))
            return BadRequest(new { message = "studentId و status مطلوبان" });

        if (!Enum.TryParse<AttendanceStatus>(sText, true, out var parsedStatus))
            return BadRequest(new { message = $"قيمة status غير صالحة: {sText}" });

        var today = DateTime.UtcNow.Date;
        var record = await _context.Attendances
            .FirstOrDefaultAsync(a => a.StudentId == sid && a.Date.Date == today);

        if (record != null)
        {
            record.Status = parsedStatus;
        }
        else
        {
            record = new Attendance
            {
                StudentId = sid.Value,
                Date = today,
                Status = parsedStatus
            };
            _context.Attendances.Add(record);
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

    // ─────────────────────────────────────────────────
    // GET /api/attendance/summary?days=7
    // ملخص الحضور للأيام الأخيرة (للرسم البياني)
    // ─────────────────────────────────────────────────
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] int days = 7)
    {
        var since = DateTime.UtcNow.AddDays(-days).Date;
        var records = await _context.Attendances
            .Where(a => a.Date.Date >= since)
            .GroupBy(a => a.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
                Late = g.Count(a => a.Status == AttendanceStatus.Late),
                Total = g.Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        return Ok(records);
    }
}

public class MarkAttendanceRequest
{
    public int StudentId { get; set; }
    public string Status { get; set; } = "Present";
}