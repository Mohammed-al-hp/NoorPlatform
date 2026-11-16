using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

/// <summary>
/// تحليلات المشرف: مؤشر الإنذار المبكر للانقطاع، ومقارنة أداء الحلقات والمحفّظين.
/// تُحسب من بيانات آخر N يوماً (30 افتراضياً) في الذاكرة — حجم مركز تحفيظ واحد صغير.
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "Admin")]
public class AnalyticsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public AnalyticsController(NoorDbContext context) => _context = context;

    private sealed record Snapshot(
        List<StudentRow> Students,
        List<AttRow> Attendance,
        List<HifzRow> Hifz,
        Dictionary<int, DateTime> LastHifzEver,
        DateTime From,
        DateTime Today);

    private sealed record StudentRow(int Id, string Name, int? CircleId, string? CircleName, int? TeacherId, string? TeacherName, string? Phone, DateTime RegistrationDate);
    private sealed record AttRow(int StudentId, DateTime Date, AttendanceStatus Status);
    private sealed record HifzRow(int StudentId, DateTime Date, RecordType Type, string Evaluation, int VerseCount);

    private async Task<Snapshot> LoadAsync(int days)
    {
        var today = LibyaTime.Today;
        var from = today.AddDays(-Math.Clamp(days, 7, 180) + 1);

        var students = await _context.Students.AsNoTracking()
            .Where(s => s.User.IsActive)
            .Select(s => new StudentRow(s.Id, s.User.FullName, s.CircleId,
                s.Circle != null ? s.Circle.Name : null,
                s.Circle != null ? s.Circle.TeacherId : null,
                s.Circle != null && s.Circle.Teacher != null ? s.Circle.Teacher.User.FullName : null,
                s.ParentPhone, s.RegistrationDate))
            .ToListAsync();

        var att = await _context.Attendances.AsNoTracking()
            .Where(a => a.Date >= from)
            .Select(a => new AttRow(a.StudentId, a.Date, a.Status))
            .ToListAsync();

        var hifz = await _context.HifzRecords.AsNoTracking()
            .Where(h => h.Date >= from)
            .Select(h => new HifzRow(h.StudentId, h.Date, h.Type, h.Evaluation, h.VerseCount))
            .ToListAsync();

        var lastEver = await _context.HifzRecords.AsNoTracking()
            .GroupBy(h => h.StudentId)
            .Select(g => new { g.Key, Last = g.Max(h => h.Date) })
            .ToDictionaryAsync(x => x.Key, x => x.Last);

        return new Snapshot(students, att, hifz, lastEver, from, today);
    }

    private static bool IsAbsent(AttendanceStatus s) => s is AttendanceStatus.UnexcusedAbsence or AttendanceStatus.ExcusedAbsence;

    /// <summary>درجة خطر الانقطاع (0–100) وأسبابها لطالب واحد — منطق مستقل قابل للاختبار.</summary>
    public static (int Score, List<string> Reasons) RiskScore(
        IReadOnlyList<(DateTime Date, AttendanceStatus Status)> attendance,
        int? daysSinceLastHifz, int windowDays, int sessionsInWindow)
    {
        var reasons = new List<string>();
        double score = 0;

        // يوم واحد لكل تاريخ: حاضر إن حضر أي حلقة في ذلك اليوم
        var days = attendance.GroupBy(a => a.Date.Date).OrderBy(g => g.Key)
            .Select(g => (Date: g.Key, Absent: g.All(a => IsAbsent(a.Status)),
                          Unexcused: g.All(a => a.Status == AttendanceStatus.UnexcusedAbsence)))
            .ToList();
        var absences = days.Count(d => d.Absent);
        if (days.Count >= 3)
        {
            var rate = (double)absences / days.Count;
            score += rate * 45;
            if (rate >= 0.3) reasons.Add($"غياب {absences} من {days.Count} يوماً ({Math.Round(rate * 100)}٪)");
        }

        var streak = 0;
        for (var i = days.Count - 1; i >= 0 && days[i].Unexcused; i--) streak++;
        if (streak > 0)
        {
            score += Math.Min(streak, 4) / 4.0 * 20;
            if (streak >= 2) reasons.Add($"غياب بدون عذر {streak} أيام متتالية");
        }

        var stalled = daysSinceLastHifz ?? windowDays;
        score += Math.Min(stalled, windowDays) / (double)windowDays * 35;
        if (daysSinceLastHifz == null) reasons.Add("لا توجد أي جلسة تسميع مسجّلة");
        else if (daysSinceLastHifz >= 14) reasons.Add($"توقف التسميع منذ {daysSinceLastHifz} يوماً");
        else if (sessionsInWindow <= 2) reasons.Add($"جلسات قليلة ({sessionsInWindow}) خلال آخر {windowDays} يوماً");

        return ((int)Math.Round(Math.Clamp(score, 0, 100)), reasons);
    }

    // GET /api/analytics/at-risk?days=30&minScore=35
    [HttpGet("at-risk")]
    public async Task<IActionResult> AtRisk([FromQuery] int days = 30, [FromQuery] int minScore = 35)
    {
        var snap = await LoadAsync(days);
        var window = (int)(snap.Today - snap.From).TotalDays + 1;
        var attBy = snap.Attendance.ToLookup(a => a.StudentId);
        var hifzBy = snap.Hifz.ToLookup(h => h.StudentId);

        var items = snap.Students
            // الطالب المسجّل حديثاً (أقل من أسبوع) لا يُقيَّم بعد
            .Where(s => (snap.Today - s.RegistrationDate.Date).TotalDays >= 7)
            .Select(s =>
            {
                var att = attBy[s.Id].Select(a => (a.Date, a.Status)).ToList();
                int? since = snap.LastHifzEver.TryGetValue(s.Id, out var last) ? (int)(snap.Today - last.Date).TotalDays : null;
                var sessions = hifzBy[s.Id].Count();
                var (score, reasons) = RiskScore(att, since, window, sessions);
                var attDays = att.GroupBy(a => a.Date.Date).ToList();
                return new
                {
                    studentId = s.Id,
                    name = s.Name,
                    circleName = s.CircleName,
                    teacherName = s.TeacherName,
                    parentPhone = s.Phone,
                    score,
                    level = score >= 60 ? "high" : score >= 35 ? "medium" : "low",
                    reasons,
                    absences = attDays.Count(g => g.All(a => IsAbsent(a.Status))),
                    attendanceDays = attDays.Count,
                    daysSinceLastHifz = since,
                    sessions
                };
            })
            .Where(x => x.score >= Math.Clamp(minScore, 0, 100))
            .OrderByDescending(x => x.score)
            .ToList();

        return Ok(new
        {
            windowDays = window,
            total = snap.Students.Count,
            high = items.Count(i => i.level == "high"),
            medium = items.Count(i => i.level == "medium"),
            items
        });
    }

    // GET /api/analytics/performance?days=30 — مقارنة الحلقات والمحفّظين
    [HttpGet("performance")]
    public async Task<IActionResult> Performance([FromQuery] int days = 30)
    {
        var snap = await LoadAsync(days);
        var window = (int)(snap.Today - snap.From).TotalDays + 1;

        object Metrics(IEnumerable<StudentRow> group)
        {
            var ids = group.Select(s => s.Id).ToHashSet();
            var att = snap.Attendance.Where(a => ids.Contains(a.StudentId)).ToList();
            var hifz = snap.Hifz.Where(h => ids.Contains(h.StudentId)).ToList();
            var present = att.Count(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late);
            var passed = hifz.Count(h => MemoryStrength.Quality(h.Evaluation) >= 3);
            var recordedDays = att.Select(a => a.Date.Date).Distinct().Count();
            return new
            {
                students = ids.Count,
                attendanceRate = att.Count == 0 ? (double?)null : Math.Round(100.0 * present / att.Count, 1),
                sessions = hifz.Count,
                sessionsPerStudent = ids.Count == 0 ? 0 : Math.Round((double)hifz.Count / ids.Count, 1),
                successRate = hifz.Count == 0 ? (double?)null : Math.Round(100.0 * passed / hifz.Count, 1),
                newVerses = hifz.Where(h => h.Type == RecordType.Memorization && MemoryStrength.Quality(h.Evaluation) >= 3).Sum(h => h.VerseCount),
                avgQuality = hifz.Count == 0 ? (double?)null : Math.Round(hifz.Average(h => MemoryStrength.Quality(h.Evaluation)), 2),
                activeStudents = hifz.Select(h => h.StudentId).Distinct().Count(),
                // الالتزام: عدد الأيام التي رُصد فيها الحضور خلال الفترة
                recordedDays
            };
        }

        var circles = snap.Students.Where(s => s.CircleId.HasValue)
            .GroupBy(s => new { s.CircleId, s.CircleName, s.TeacherName })
            .Select(g => new { circleId = g.Key.CircleId, name = g.Key.CircleName, teacherName = g.Key.TeacherName, metrics = Metrics(g) })
            .OrderBy(c => c.name)
            .ToList();

        var teachers = snap.Students.Where(s => s.TeacherId.HasValue)
            .GroupBy(s => new { s.TeacherId, s.TeacherName })
            .Select(g => new { teacherId = g.Key.TeacherId, name = g.Key.TeacherName, circles = g.Select(s => s.CircleId).Distinct().Count(), metrics = Metrics(g) })
            .OrderBy(t => t.name)
            .ToList();

        return Ok(new { windowDays = window, from = snap.From, to = snap.Today, circles, teachers });
    }
}
