using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using System.Security.Claims;
using NoorPlatform.Api.Services;

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
    // ─── إصلاح عالي: استخدام Task.WhenAll لتنفيذ الاستعلامات بشكل متوازٍ ───
    // ملاحظة: EF Core DbContext غير آمن للعمليات المتزامنة (Not Thread-Safe). 
    // لتنفيذ استعلامات متوازية، يجب إنشاء Scopes منفصلة لتجنب InvalidOperationException
    [HttpGet("stats")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetStats([FromServices] IServiceScopeFactory scopeFactory)
    {
        var isTeacher = User.IsInRole("Teacher");
        var isAdmin = User.IsInRole("Admin");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var today = DateTime.UtcNow.Date;
        var weekStart = today.AddDays(-6);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        // إنشاء مهام متوازية باستخدام Contexts منفصلة لكل مهمة
        var studentsTask = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            IQueryable<Student> q = db.Students.AsNoTracking();
            if (isTeacher && !isAdmin)
            {
                var circleIds = db.Circles
                    .Where(c => c.Teacher != null && c.Teacher.UserId == userId)
                    .Select(c => c.Id);
                q = q.Where(s => s.CircleId != null && circleIds.Contains(s.CircleId.Value));
            }
            return await q.CountAsync();
        });

        var teachersTask = Task.Run(async () =>
        {
            if (isTeacher && !isAdmin) return 1;
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            return await db.Teachers.CountAsync();
        });

        var circlesTask = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            var q = db.Circles.AsNoTracking();
            if (isTeacher && !isAdmin) q = q.Where(c => c.Teacher!.UserId == userId);
            return await q.CountAsync();
        });

        var attendancesTask = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            var q = db.Attendances.AsNoTracking();
            if (isTeacher && !User.IsInRole("Admin")) q = q.Where(a => a.Student!.Circle!.Teacher!.UserId == userId);
            
            var presentToday = await q.CountAsync(a => a.Date.Date == today && a.Status == AttendanceStatus.Present);
            var totalToday = await q.CountAsync(a => a.Date.Date == today);
            
            var rawAttendances = await q.Where(a => a.Date >= weekStart).Select(a => new { a.Date, a.Status }).ToListAsync();
            
            var weeklyRaw = rawAttendances
                .GroupBy(a => a.Date.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Present = g.Count(a => a.Status == AttendanceStatus.Present),
                    Late = g.Count(a => a.Status == AttendanceStatus.Late),
                    Total = g.Count()
                })
                .ToList();
            
            return (presentToday, totalToday, weeklyRaw);
        });

        var hifzTask = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            var q = db.HifzRecords.AsNoTracking();
            if (isTeacher && !isAdmin) q = q.Where(h => h.Student!.Circle!.Teacher!.UserId == userId);

            // ─── تحسين: الاعتماد على VerseCount الفعلي من قاعدة البيانات وتبسيط الحساب في الاستعلام ───
            var recent = await q.OrderByDescending(r => r.Date).Take(5)
                .Select(r => new {
                    studentName = r.Student.User.FullName,
                    circleName = r.Student.Circle != null ? r.Student.Circle.Name : "—",
                    surahName = r.SurahName,
                    verses = r.Verses,
                    evaluation = r.Evaluation,
                    progress = 0
                }).ToListAsync();
            
            var weeklyVerses = await q.Where(r => r.Type == RecordType.Memorization && r.Date >= weekStart)
                .SumAsync(r => r.VerseCount); // Use pre-calculated VerseCount efficiently

            return (recent, weeklyVerses);
        });

        var financialsTask = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NoorDbContext>();
            var q = db.Payments.AsNoTracking();
            if (isTeacher && !isAdmin) q = q.Where(p => p.Student!.Circle!.Teacher!.UserId == userId);

            var rev = await q.Where(p => p.Status == PaymentStatus.Paid && p.PaidDate >= startOfMonth).SumAsync(p => p.Amount);
            var due = await q.Where(p => p.Status != PaymentStatus.Paid && p.DueDate < today).SumAsync(p => p.Amount);
            
            return (rev, due);
        });

        // انتظار جميع المهام بشكل متوازٍ
        await Task.WhenAll(studentsTask, teachersTask, circlesTask, attendancesTask, hifzTask, financialsTask);

        var totalStudents = studentsTask.Result;
        var totalTeachers = teachersTask.Result;
        var totalCircles = circlesTask.Result;
        var (presentToday, totalToday, weeklyRaw) = attendancesTask.Result;
        var (recentHifz, weeklyVerses) = hifzTask.Result;
        var (monthlyRevenue, totalOverdue) = financialsTask.Result;

        var attendancePercent = totalToday > 0 ? (int)Math.Round((double)presentToday / totalToday * 100) : 0;

        string[] dayNames = { "الأحد", "الإثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };
        var weeklyAttendance = Enumerable.Range(0, 7).Select(i =>
        {
            var d = weekStart.AddDays(i).Date;
            var rec = weeklyRaw.FirstOrDefault(r => r.Date.Date == d);
            var attended = rec == null ? 0 : rec.Present + rec.Late;
            return new
            {
                dayName = dayNames[(int)d.DayOfWeek],
                date = d.ToString("yyyy-MM-dd"),
                percentage = rec != null && rec.Total > 0
                    ? (int)Math.Round((double)attended / rec.Total * 100)
                    : 0,
                present = rec?.Present ?? 0,
                late = rec?.Late ?? 0,
                total = rec?.Total ?? 0
            };
        }).ToList();

        // ── توزيع المستويات (بشكل تسلسلي خفيف لأنها قد لا تكون ثقيلة) ──
        var levelsQ = _context.Students.AsNoTracking().AsQueryable();
        if (isTeacher && !isAdmin) levelsQ = levelsQ.Where(s => s.Circle!.Teacher!.UserId == userId);
        
        var levels = await levelsQ.GroupBy(s => s.Level).Select(g => new { level = g.Key, count = g.Count() }).ToListAsync();
        var levelDistribution = new {
            advanced = levels.FirstOrDefault(l => l.level == "متقدم")?.count ?? 0,
            intermediate = levels.FirstOrDefault(l => l.level == "متوسط")?.count ?? 0,
            beginner = levels.FirstOrDefault(l => l.level == "مبتدئ")?.count ?? 0
        };

        var totalStudentsCount = totalStudents > 0 ? totalStudents : 1;
        var hifzVelocity = Math.Round((double)weeklyVerses / totalStudentsCount, 1);

        var bestCircleRaw = await _context.Circles.AsNoTracking()
            .Select(c => new
            {
                c.Name,
                TeacherName = c.Teacher != null ? c.Teacher.User.FullName : "—",
                AttendanceRate = c.Students.SelectMany(s => s.Attendances).Any()
                    ? (double)c.Students.SelectMany(s => s.Attendances).Count(a => a.Status == AttendanceStatus.Present) / c.Students.SelectMany(s => s.Attendances).Count() * 100
                    : 0
            })
            .OrderByDescending(c => c.AttendanceRate)
            .FirstOrDefaultAsync();

        var bestCircle = bestCircleRaw != null ? new {
            name = bestCircleRaw.Name,
            teacher = bestCircleRaw.TeacherName,
            attendanceRate = Math.Round(bestCircleRaw.AttendanceRate, 1)
        } : null;

        return Ok(new
        {
            students = totalStudents,
            teachers = totalTeachers,
            circles = totalCircles,
            attendanceToday = $"{attendancePercent}%",
            weeklyAttendance,
            levelDistribution,
            recentHifz,
            financials = new { monthlyRevenue, totalOverdue },
            hifzVelocity,
            bestCircle
        });
    }

    // GET /api/dashboard/student-summary
    [HttpGet("student-summary")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetStudentSummary()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ─── إصلاح متوسط: تحديد عدد السجلات المجلوبة في الذاكرة لمنع اختناق الذاكرة مع مرور الوقت ───
        var student = await _context.Students.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Circle!).ThenInclude(c => c.Teacher!).ThenInclude(t => t.User)
            .Include(s => s.HifzRecords.OrderByDescending(h => h.Date).Take(10))
            .Include(s => s.Attendances.OrderByDescending(a => a.Date).Take(10))
            .Include(s => s.ExamResults.OrderByDescending(e => e.Id).Take(10))
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (student == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });

        var attendancePercent = student.Attendances.Any()
            ? (double)student.Attendances.Count(a => a.Status == AttendanceStatus.Present)
              / student.Attendances.Count * 100
            : 0;

        var hifzProgress = HifzProgressCalculator.Calculate(student.HifzRecords);

        var lastRecord = student.HifzRecords
            .OrderByDescending(r => r.Date)
            .FirstOrDefault();

        const int weeklyGoal = 50;
        var weekStart = DateTime.UtcNow.Date.AddDays(-6);

        // مجموع آيات الحفظ الجديد هذا الأسبوع (استعلام مستقل — لا يعتمد على Take(10))
        var weeklyVerses = await _context.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == student.Id
                        && r.Type == RecordType.Memorization
                        && r.Date >= weekStart)
            .SumAsync(r => r.VerseCount);

        // آخر سورة محفوظة = ورد المراجعة القادم
        var lastMemRecord = await _context.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == student.Id && r.Type == RecordType.Memorization)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        // أفضل 3 طلاب في نفس الحلقة حسب النقاط
        object circleTop3 = Array.Empty<object>();
        if (student.CircleId != null)
        {
            circleTop3 = await _context.Students.AsNoTracking()
                .Where(s => s.CircleId == student.CircleId && !s.IsDeleted)
                .OrderByDescending(s => s.Points)
                .Take(3)
                .Select(s => new
                {
                    studentId = s.Id,
                    fullName = s.User.FullName,
                    points = s.Points,
                    badges = s.Badges,
                    isCurrentUser = s.Id == student.Id
                })
                .ToListAsync();
        }

        return Ok(new
        {
            id = student.Id,
            fullName = student.User?.FullName ?? User.Identity?.Name,
            hifzProgress,
            attendancePercentage = Math.Round(attendancePercent, 1),
            lastEvaluation = lastRecord?.Evaluation ?? "لا يوجد",
            lastSurah = lastRecord != null ? $"{lastRecord.SurahName} ({lastRecord.Verses})" : "—",
            points = student.Points,
            weeklyVerses,
            weeklyGoal,
            circleTop3,
            nextReview = lastMemRecord != null
                ? new
                {
                    date = lastMemRecord.Date.ToString("yyyy-MM-dd"),
                    surah = lastMemRecord.SurahName,
                    toSurah = lastMemRecord.ToSurahName,
                    verses = lastMemRecord.Verses,
                    verseCount = lastMemRecord.VerseCount,
                    evaluation = lastMemRecord.Evaluation
                }
                : null,
            lastMemorization = lastMemRecord != null
                ? new
                {
                    date = lastMemRecord.Date.ToString("yyyy-MM-dd"),
                    surah = lastMemRecord.SurahName,
                    toSurah = lastMemRecord.ToSurahName,
                    verses = lastMemRecord.Verses
                }
                : null,
            teacherName = student.Circle?.Teacher?.User?.FullName ?? "—",
            teacherRating = student.Circle?.Teacher?.AverageRating ?? 0.0,
            circleName = student.Circle?.Name ?? "بدون حلقة",
            badges = student.Badges,
            recentGrades = student.ExamResults
                                    .OrderByDescending(r => r.Id)
                                    .Select(r => new { r.Score, r.MaxScore, r.Feedback })
                                    .Take(5),
            recentHifz = student.HifzRecords
                                    .OrderByDescending(r => r.Date)
                                    .Select(r => new { Date = r.Date.ToString("yyyy-MM-dd"), r.SurahName, r.Verses, r.Evaluation })
                                    .Take(5),
            teacherNotes = student.HifzRecords
                                    .OrderByDescending(r => r.Date)
                                    .Where(r => !string.IsNullOrEmpty(r.Notes))
                                    .Select(r => new { Date = r.Date.ToString("yyyy-MM-dd"), r.Notes, TeacherName = student.Circle?.Teacher?.User?.FullName ?? "إدارة الحلقة" })
                                    .Take(3)
        });
    }

    // GET /api/dashboard/parent-summary
    [HttpGet("parent-summary")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetParentSummary()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var parent = await _context.Parents.AsNoTracking()
            .Include(p => p.Children).ThenInclude(c => c.User)
            .Include(p => p.Children).ThenInclude(c => c.HifzRecords.OrderByDescending(h => h.Date).Take(10))
            .Include(p => p.Children).ThenInclude(c => c.Attendances.OrderByDescending(a => a.Date).Take(10))
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (parent == null)
            return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

        var childrenData = parent.Children.Select(c => new
        {
            c.Id,
            fullName = c.User.FullName,
            progress = HifzProgressCalculator.Calculate(c.HifzRecords),
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
        var overduePayments = await _context.Payments.AsNoTracking()
            .Include(p => p.Student).ThenInclude(s => s.User)
            .Where(p => p.ParentId == parent.Id && p.Status != PaymentStatus.Paid)
            .Select(p => new
            {
                p.Id,
                studentName = p.Student.User.FullName,
                p.Amount,
                p.Description,
                p.DueDate,
                p.Status,
                isOverdue = p.DueDate < DateTime.UtcNow && p.Status != PaymentStatus.Paid
            })
            .OrderByDescending(p => p.DueDate)
            .ToListAsync();

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
        // ─── إصلاح عالي: استبدال User.FindFirstValue بالاعتماد على User.IsInRole لدعم المستخدمين ذوي الأدوار المتعددة بأمان ───
        var isTeacher = User.IsInRole("Teacher");
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var query = _context.ActivityFeeds.AsNoTracking().AsQueryable();

        if (isTeacher && !User.IsInRole("Admin"))
        {
            query = query.Where(a => a.UserId == userId);
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
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetLeaderboard()
    {
        var leaderboardQuery = _context.Students.AsNoTracking().AsQueryable();

        // ─── إصلاح: تقييد المحفّظ برؤية طلاب حلقته فقط ───
        var isTeacherLeaderboard = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacherLeaderboard)
        {
            var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
            leaderboardQuery = leaderboardQuery.Where(s => s.Circle != null
                                                         && s.Circle.Teacher != null
                                                         && s.Circle.Teacher.UserId == userId);
        }

        var leaderboardRaw = await leaderboardQuery
            .OrderByDescending(s => s.Points)
            .Take(10)
                    .Select(s => new
            {
                studentId = s.Id,
                fullName = s.User.FullName,
                circleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
                points = s.Points,
                badges = s.Badges,
                attendanceRate = s.Attendances.Any()
                    ? Math.Round((double)s.Attendances.Count(a => a.Status == AttendanceStatus.Present) / s.Attendances.Count * 100, 1)
                    : 0.0,
                HifzRecordsList = s.HifzRecords.Select(r => new { r.Type, r.VerseCount, r.Verses })
            })
            .ToListAsync();

        var leaderboard = leaderboardRaw.Select((s, index) => new
        {
            rank = index + 1,
            s.studentId,
            s.fullName,
            s.circleName,
            s.points,
            s.badges,
            s.attendanceRate,
            hifzProgress = Math.Min((int)Math.Round(
                (double)s.HifzRecordsList
                    .Where(r => r.Type == RecordType.Memorization)
                    .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses))
                / 6236.0 * 100), 100)
        });

        return Ok(leaderboard);
    }

    // ─────────────────────────────────────────────────
    // Helper: حساب تقدم الحفظ الحقيقي من VerseCount
    // القرآن الكريم = 6236 آية
    // ─────────────────────────────────────────────────
}