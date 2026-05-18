using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly NoorDbContext _context;

    public DashboardController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/dashboard/stats
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalStudents = await _context.Students.CountAsync();
        var totalTeachers = await _context.Teachers.CountAsync();
        var totalCircles = await _context.Circles.CountAsync();

        var today = DateTime.UtcNow.Date;
        var presentToday = await _context.Attendances
            .CountAsync(a => a.Date.Date == today && a.Status == AttendanceStatus.Present);
        var totalToday = await _context.Attendances
            .CountAsync(a => a.Date.Date == today);

        var attendancePercent = totalToday > 0
            ? (int)Math.Round((double)presentToday / totalToday * 100)
            : 0;

        return Ok(new
        {
            students = totalStudents,
            teachers = totalTeachers,
            circles = totalCircles,
            attendanceToday = $"{attendancePercent}%"
        });
    }

    // GET /api/dashboard/student-summary
    [HttpGet("student-summary")]
    public async Task<IActionResult> GetStudentSummary()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var student = await _context.Students
            .Include(s => s.HifzRecords)
            .Include(s => s.Attendances)
            .Include(s => s.ExamResults)
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });

        var attendancePercent = student.Attendances.Any()
            ? (double)student.Attendances.Count(a => a.Status == AttendanceStatus.Present)
              / student.Attendances.Count * 100
            : 0;

        // ✅ إصلاح 1: حساب التقدم من VerseCount الفعلي
        var hifzProgress = CalculateHifzProgress(student.HifzRecords);

        var lastRecord = student.HifzRecords
            .OrderByDescending(r => r.Date)
            .FirstOrDefault();

        return Ok(new
        {
            fullName = User.Identity?.Name,
            hifzProgress,
            attendancePercentage = Math.Round(attendancePercent, 1),
            lastEvaluation = lastRecord?.Evaluation ?? "لا يوجد",
            lastSurah = lastRecord != null ? $"{lastRecord.SurahName} ({lastRecord.Verses})" : "—",
            recentGrades = student.ExamResults
                                    .OrderByDescending(r => r.Id)
                                    .Select(r => new { r.Score, r.MaxScore, r.Feedback })
                                    .Take(5)
        });
    }

    // GET /api/dashboard/parent-summary
    [HttpGet("parent-summary")]
    public async Task<IActionResult> GetParentSummary()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var parent = await _context.Parents
            .Include(p => p.Children).ThenInclude(c => c.User)
            .Include(p => p.Children).ThenInclude(c => c.HifzRecords)
            .Include(p => p.Children).ThenInclude(c => c.Attendances)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (parent == null)
            return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

        var childrenData = parent.Children.Select(c => new
        {
            c.Id,
            fullName = c.User.FullName,
            // ✅ إصلاح 1: حساب التقدم من VerseCount الفعلي
            progress = CalculateHifzProgress(c.HifzRecords),
            attendance = c.Attendances.Any()
                            ? Math.Round((double)c.Attendances.Count(a => a.Status == AttendanceStatus.Present)
                              / c.Attendances.Count * 100, 1)
                            : 0.0,
            lastNote = c.HifzRecords
                            .OrderByDescending(r => r.Date)
                            .FirstOrDefault()?.Notes ?? "لا توجد ملاحظات"
        });

        return Ok(childrenData);
    }

    // ─────────────────────────────────────────────────
    // Helper: حساب تقدم الحفظ الحقيقي من VerseCount
    // القرآن الكريم = 6236 آية
    // ─────────────────────────────────────────────────
    private static int CalculateHifzProgress(IEnumerable<HifzRecord> records)
    {
        // ✅ إصلاح 1: نجمع الآيات الفعلية من VerseCount بدلاً من ضرب * 10
        var totalVerses = records
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0
                        ? r.VerseCount
                        : HifzRecord.ParseVerseCount(r.Verses)); // fallback للسجلات القديمة

        var percent = Math.Min((int)Math.Round((double)totalVerses / 6236 * 100), 100);
        return percent;
    }
}