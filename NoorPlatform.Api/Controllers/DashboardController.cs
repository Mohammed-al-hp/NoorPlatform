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
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetStats()
    {
        var isTeacher = User.IsInRole("Teacher");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var studentsQuery = _context.Students.AsQueryable();
        var teachersQuery = _context.Teachers.AsQueryable();
        var circlesQuery = _context.Circles.AsQueryable();
        var attendancesQuery = _context.Attendances.AsQueryable();
        var hifzQuery = _context.HifzRecords.AsQueryable();

        if (isTeacher)
        {
            studentsQuery = studentsQuery.Where(s => s.Circle!.Teacher!.UserId == userId);
            circlesQuery = circlesQuery.Where(c => c.Teacher!.UserId == userId);
            attendancesQuery = attendancesQuery.Where(a => a.Student!.Circle!.Teacher!.UserId == userId);
            hifzQuery = hifzQuery.Where(h => h.Student!.Circle!.Teacher!.UserId == userId);
        }

        var totalStudents = await studentsQuery.CountAsync();
        var totalTeachers = isTeacher ? 1 : await teachersQuery.CountAsync();
        var totalCircles = await circlesQuery.CountAsync();

        var today = DateTime.UtcNow.Date;
        var presentToday = await attendancesQuery
            .CountAsync(a => a.Date.Date == today && a.Status == AttendanceStatus.Present);
        var totalToday = await attendancesQuery
            .CountAsync(a => a.Date.Date == today);

        var attendancePercent = totalToday > 0
            ? (int)Math.Round((double)presentToday / totalToday * 100)
            : 0;

        // ── بيانات الرسم البياني الأسبوعي ──
        var weekStart = today.AddDays(-6);
        var weeklyRaw = await attendancesQuery
            .Where(a => a.Date.Date >= weekStart)
            .GroupBy(a => a.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Present = g.Count(a => a.Status == AttendanceStatus.Present),
                Total = g.Count()
            })
            .OrderBy(g => g.Date)
            .ToListAsync();

        string[] dayNames = { "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
        var weeklyAttendance = Enumerable.Range(0, 7).Select(i =>
        {
            var d = weekStart.AddDays(i);
            var rec = weeklyRaw.FirstOrDefault(r => r.Date == d);
            return new
            {
                dayName = dayNames[(int)d.DayOfWeek],
                date = d.ToString("yyyy-MM-dd"),
                percentage = rec != null && rec.Total > 0
                    ? (int)Math.Round((double)rec.Present / rec.Total * 100)
                    : 0
            };
        });

        // ── توزيع المستويات (Donut Chart) ──
        var levels = await studentsQuery
            .GroupBy(s => s.Level)
            .Select(g => new { level = g.Key, count = g.Count() })
            .ToListAsync();

        var levelDistribution = new
        {
            advanced = levels.FirstOrDefault(l => l.level == "متقدم")?.count ?? 0,
            intermediate = levels.FirstOrDefault(l => l.level == "متوسط")?.count ?? 0,
            beginner = levels.FirstOrDefault(l => l.level == "مبتدئ")?.count ?? 0
        };

        // ── آخر 5 جلسات تسميع (جدول النشاط) ──
        var recentHifz = await hifzQuery
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Student).ThenInclude(s => s.Circle)
            .OrderByDescending(r => r.Date)
            .Take(5)
            .Select(r => new
            {
                studentName = r.Student.User.FullName,
                circleName = r.Student.Circle != null ? r.Student.Circle.Name : "—",
                surahName = r.SurahName,
                verses = r.Verses,
                evaluation = r.Evaluation,
                progress = 0 // يُحسب أدناه
            })
            .ToListAsync();

        return Ok(new
        {
            students = totalStudents,
            teachers = totalTeachers,
            circles = totalCircles,
            attendanceToday = $"{attendancePercent}%",
            weeklyAttendance,
            levelDistribution,
            recentHifz
        });
    }

    // GET /api/dashboard/student-summary
    [HttpGet("student-summary")]
    [Authorize(Roles = "Student")]
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
            points = student.Points,
            badges = student.Badges,
            recentGrades = student.ExamResults
                                    .OrderByDescending(r => r.Id)
                                    .Select(r => new { r.Score, r.MaxScore, r.Feedback })
                                    .Take(5)
        });
    }

    // GET /api/dashboard/parent-summary
    [HttpGet("parent-summary")]
    [Authorize(Roles = "Parent")]
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
            points = c.Points,
            badges = c.Badges,
            lastNote = c.HifzRecords
                            .OrderByDescending(r => r.Date)
                            .FirstOrDefault()?.Notes ?? "لا توجد ملاحظات"
        });

        // جلب الفواتير المتأخرة وغير المدفوعة
        var overduePayments = await _context.Payments
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.ParentId == parent.Id && p.Status != "Paid")
            .Select(p => new
            {
                p.Id,
                studentName = p.Student.User.FullName,
                p.Amount,
                p.Description,
                p.DueDate,
                p.Status,
                isOverdue = p.DueDate < DateTime.UtcNow && p.Status != "Paid"
            })
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();

        // تحديث حالة الفواتير المتأخرة تلقائياً
        var overduePending = await _context.Payments
            .Where(p => p.ParentId == parent.Id && p.Status == "Pending" && p.DueDate < DateTime.UtcNow)
            .ToListAsync();
        foreach (var op in overduePending)
        {
            op.Status = "Overdue";
        }
        if (overduePending.Any()) await _context.SaveChangesAsync();

        return Ok(new
        {
            children = childrenData,
            alerts = overduePayments.Where(p => p.isOverdue).Select(p => new
            {
                message = $"⚠️ فاتورة متأخرة: {p.Description} للطالب {p.studentName} بمبلغ {p.Amount} ريال",
                p.Id,
                p.Amount
            })
        });
    }

    // GET /api/dashboard/activities
    [HttpGet("activities")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetActivityFeed()
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.ActivityFeeds.AsQueryable();

        // Admin sees all, Teacher sees their own activities or activities related to their students
        if (role == "Teacher")
        {
            query = query.Where(a => a.UserId == userId);
            // We could also join with students in their circles, but for simplicity we show their own logged activities.
        }

        var activities = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .Select(a => new
            {
                a.Id,
                a.UserName,
                a.ActivityType,
                a.Description,
                a.CreatedAt,
                a.Icon,
                a.Color
            })
            .ToListAsync();

        return Ok(activities);
    }

    // GET /api/dashboard/leaderboard
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var students = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Include(s => s.Attendances)
            .Include(s => s.HifzRecords)
            .OrderByDescending(s => s.Points)
            .Take(10)
            .ToListAsync();

        var leaderboard = students.Select((s, index) => new
        {
            rank = index + 1,
            studentId = s.Id,
            fullName = s.User.FullName,
            circleName = s.Circle?.Name ?? "بدون حلقة",
            points = s.Points,
            badges = s.Badges,
            attendanceRate = s.Attendances.Any()
                ? Math.Round((double)s.Attendances.Count(a => a.Status == AttendanceStatus.Present) / s.Attendances.Count * 100, 1)
                : 0,
            hifzProgress = CalculateHifzProgress(s.HifzRecords)
        });

        return Ok(leaderboard);
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