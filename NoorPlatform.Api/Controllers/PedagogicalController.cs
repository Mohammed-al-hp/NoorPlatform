using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/pedagogical")]
[Authorize]
public class PedagogicalController : ControllerBase
{
    private readonly NoorDbContext _context;

    public PedagogicalController(NoorDbContext context)
    {
        _context = context;
    }

    // ════════════════════════════════════════════════════════
    // متون (Matn)
    // ════════════════════════════════════════════════════════

    [HttpGet("matn")]
    [Authorize(Roles = "Admin,Teacher,Student,Parent")]
    public async Task<IActionResult> GetMatn([FromQuery] int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var records = await _context.MatnRecords
            .AsNoTracking()
            .Where(m => m.StudentId == studentId)
            .OrderByDescending(m => m.Date)
            .ThenByDescending(m => m.Id)
            .Select(m => new
            {
                m.Id,
                m.StudentId,
                m.Date,
                m.MatnName,
                m.Portion,
                Type = m.Type.ToString(),
                m.Evaluation,
                m.Notes,
                m.RecordedByUserId
            })
            .ToListAsync();

        return Ok(records);
    }

    [HttpPost("matn")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> CreateMatn([FromBody] CreateMatnRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        if (!await _context.Students.AnyAsync(s => s.Id == request.StudentId))
            return NotFound(new { message = "الطالب غير موجود" });

        if (!Enum.TryParse<MatnRecordType>(request.Type, true, out var type))
            return BadRequest(new { message = "نوع المتن غير صالح. استخدم Memorization أو Revision" });

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var record = new MatnRecord
        {
            StudentId = request.StudentId,
            Date = request.Date?.Date ?? DateTime.UtcNow.Date,
            MatnName = request.MatnName?.Trim() ?? string.Empty,
            Portion = request.Portion?.Trim() ?? string.Empty,
            Type = type,
            Evaluation = request.Evaluation?.Trim() ?? string.Empty,
            Notes = request.Notes,
            RecordedByUserId = userId.Value
        };

        _context.MatnRecords.Add(record);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تسجيل المتن بنجاح", id = record.Id });
    }

    [HttpDelete("matn/{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> DeleteMatn(int id)
    {
        var record = await _context.MatnRecords.FirstOrDefaultAsync(m => m.Id == id);
        if (record == null)
            return NotFound(new { message = "سجل المتن غير موجود" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, record.StudentId))
            return Forbid();

        _context.MatnRecords.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف سجل المتن" });
    }

    // ════════════════════════════════════════════════════════
    // أهداف شهرية
    // ════════════════════════════════════════════════════════

    [HttpGet("monthly-targets")]
    [Authorize(Roles = "Admin,Teacher,Student,Parent")]
    public async Task<IActionResult> GetMonthlyTargets(
        [FromQuery] int studentId,
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var query = _context.StudentMonthlyTargets
            .AsNoTracking()
            .Where(t => t.StudentId == studentId);

        if (year.HasValue)
            query = query.Where(t => t.Year == year.Value);
        if (month.HasValue)
            query = query.Where(t => t.Month == month.Value);

        var targets = await query
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Month)
            .Select(t => new
            {
                t.Id,
                t.StudentId,
                t.Year,
                t.Month,
                t.TargetAthmanCount,
                t.AchievedAthmanCount,
                t.ProgressScoreOutOf10,
                t.IsSpecialMode,
                t.SpecialModeNote,
                t.Notes,
                t.SetByUserId,
                t.UpdatedAt
            })
            .ToListAsync();

        return Ok(targets);
    }

    [HttpPut("monthly-targets")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UpsertMonthlyTarget([FromBody] UpsertMonthlyTargetRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        if (request.Month < 1 || request.Month > 12)
            return BadRequest(new { message = "الشهر يجب أن يكون بين 1 و 12" });

        if (!await _context.Students.AnyAsync(s => s.Id == request.StudentId))
            return NotFound(new { message = "الطالب غير موجود" });

        var userId = AuthorizationHelpers.GetUserId(User);
        var existing = await _context.StudentMonthlyTargets
            .FirstOrDefaultAsync(t =>
                t.StudentId == request.StudentId &&
                t.Year == request.Year &&
                t.Month == request.Month);

        if (existing == null)
        {
            var achieved = request.AchievedAthmanCount ?? 0;
            existing = new StudentMonthlyTarget
            {
                StudentId = request.StudentId,
                Year = request.Year,
                Month = request.Month,
                TargetAthmanCount = request.TargetAthmanCount,
                AchievedAthmanCount = achieved,
                ProgressScoreOutOf10 = PedagogicalGrading.ProgressScoreOutOf10(achieved, request.TargetAthmanCount),
                IsSpecialMode = request.IsSpecialMode ?? false,
                SpecialModeNote = request.SpecialModeNote,
                Notes = request.Notes,
                SetByUserId = userId,
                UpdatedAt = DateTime.UtcNow
            };
            _context.StudentMonthlyTargets.Add(existing);
        }
        else
        {
            existing.TargetAthmanCount = request.TargetAthmanCount;
            if (request.AchievedAthmanCount.HasValue)
                existing.AchievedAthmanCount = request.AchievedAthmanCount.Value;

            existing.ProgressScoreOutOf10 = PedagogicalGrading.ProgressScoreOutOf10(
                existing.AchievedAthmanCount, existing.TargetAthmanCount);

            if (request.IsSpecialMode.HasValue)
                existing.IsSpecialMode = request.IsSpecialMode.Value;
            if (request.SpecialModeNote != null)
                existing.SpecialModeNote = request.SpecialModeNote;
            if (request.Notes != null)
                existing.Notes = request.Notes;

            existing.SetByUserId = userId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "تم حفظ الهدف الشهري",
            id = existing.Id,
            existing.TargetAthmanCount,
            existing.AchievedAthmanCount,
            existing.ProgressScoreOutOf10
        });
    }

    // ════════════════════════════════════════════════════════
    // فترات التقييم
    // ════════════════════════════════════════════════════════

    [HttpGet("periods")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetPeriods()
    {
        var periods = await _context.EvaluationPeriods
            .AsNoTracking()
            .OrderByDescending(p => p.StartDate)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.StartDate,
                p.EndDate,
                p.CircleId,
                CircleName = p.Circle != null ? p.Circle.Name : null,
                p.Notes,
                p.CreatedAt,
                EvaluationsCount = p.StudentEvaluations.Count
            })
            .ToListAsync();

        return Ok(periods);
    }

    /// <summary>تقييمات الفترة المسموح عرضها للطالب أو ولي الأمر.</summary>
    [HttpGet("my-evaluations")]
    [Authorize(Roles = "Student,Parent")]
    public async Task<IActionResult> GetMyEvaluations([FromQuery] int? studentId = null)
    {
        var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        if (settings != null && !settings.EvaluationsVisibleToStudentsAndParents)
            return Ok(Array.Empty<object>());

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        List<int> allowedStudentIds;
        if (User.IsInRole("Student"))
        {
            var myId = await _context.Students.AsNoTracking()
                .Where(s => s.UserId == userId.Value)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
            if (myId == null)
                return NotFound(new { message = "لم يُعثر على بيانات الطالب" });
            allowedStudentIds = new List<int> { myId.Value };
        }
        else
        {
            var parent = await _context.Parents.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);
            if (parent == null)
                return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

            allowedStudentIds = await _context.Students.AsNoTracking()
                .Where(s => s.ParentId == parent.Id)
                .Select(s => s.Id)
                .ToListAsync();

            if (studentId.HasValue)
            {
                if (!allowedStudentIds.Contains(studentId.Value))
                    return Forbid();
                allowedStudentIds = new List<int> { studentId.Value };
            }
        }

        var evaluations = await _context.StudentPeriodEvaluations
            .AsNoTracking()
            .Include(e => e.Period)
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Where(e => allowedStudentIds.Contains(e.StudentId))
            .OrderByDescending(e => e.Period.EndDate)
            .ThenByDescending(e => e.EvaluatedAt)
            .Select(e => new
            {
                e.Id,
                e.PeriodId,
                PeriodName = e.Period.Name,
                PeriodStart = e.Period.StartDate,
                PeriodEnd = e.Period.EndDate,
                e.StudentId,
                StudentName = e.Student.User.FullName,
                e.AttendanceScore,
                e.HifzScore,
                e.RevisionScore,
                e.ProgressScore,
                e.MatnScore,
                e.DressScore,
                e.OverallScore,
                e.GradeLabel,
                e.SheikhNotes,
                e.PrayerAdvisoryScore,
                e.ParentHomeAdvisoryScore,
                e.IncludeAdvisoryInOverall,
                e.EvaluatedAt
            })
            .ToListAsync();

        return Ok(evaluations);
    }

    [HttpPost("periods")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> CreatePeriod([FromBody] CreateEvaluationPeriodRequest request)
    {
        if (request.EndDate.Date < request.StartDate.Date)
            return BadRequest(new { message = "تاريخ النهاية يجب أن يكون بعد تاريخ البداية" });

        if (request.CircleId.HasValue)
        {
            if (!await _context.Circles.AnyAsync(c => c.Id == request.CircleId.Value))
                return BadRequest(new { message = "الحلقة غير موجودة" });
            if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, request.CircleId.Value))
                return Forbid();
        }

        var period = new EvaluationPeriod
        {
            Name = request.Name?.Trim() ?? string.Empty,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            CircleId = request.CircleId,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _context.EvaluationPeriods.Add(period);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم إنشاء فترة التقييم", id = period.Id });
    }

    [HttpGet("periods/{id}/evaluations")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetPeriodEvaluations(int id)
    {
        var period = await _context.EvaluationPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (period == null)
            return NotFound(new { message = "فترة التقييم غير موجودة" });

        var query = _context.StudentPeriodEvaluations
            .AsNoTracking()
            .Include(e => e.Student).ThenInclude(s => s.User)
            .Where(e => e.PeriodId == id);

        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher)
        {
            var userId = AuthorizationHelpers.GetUserId(User);
            query = query.Where(e =>
                e.Student.Circle != null &&
                e.Student.Circle.Teacher != null &&
                e.Student.Circle.Teacher.UserId == userId);
        }

        var evaluations = await query
            .OrderBy(e => e.Student.User.FullName)
            .Select(e => new
            {
                e.Id,
                e.PeriodId,
                e.StudentId,
                StudentName = e.Student.User.FullName,
                e.AttendanceScore,
                e.HifzScore,
                e.RevisionScore,
                e.ProgressScore,
                e.MatnScore,
                e.DressScore,
                e.OverallScore,
                e.GradeLabel,
                e.SheikhNotes,
                e.PrayerAdvisoryScore,
                e.ParentHomeAdvisoryScore,
                e.IncludeAdvisoryInOverall,
                e.EvaluatedByUserId,
                e.EvaluatedAt
            })
            .ToListAsync();

        return Ok(evaluations);
    }

    [HttpPost("periods/{id}/evaluations")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> UpsertPeriodEvaluation(int id, [FromBody] UpsertPeriodEvaluationRequest request)
    {
        var period = await _context.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == id);
        if (period == null)
            return NotFound(new { message = "فترة التقييم غير موجودة" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        if (!await _context.Students.AnyAsync(s => s.Id == request.StudentId))
            return NotFound(new { message = "الطالب غير موجود" });

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        var overall = ComputeOverallScore(
            request.AttendanceScore,
            request.HifzScore,
            request.RevisionScore,
            request.ProgressScore,
            request.MatnScore,
            request.DressScore,
            request.PrayerAdvisoryScore,
            request.ParentHomeAdvisoryScore,
            request.IncludeAdvisoryInOverall,
            settings);

        var existing = await _context.StudentPeriodEvaluations
            .FirstOrDefaultAsync(e => e.PeriodId == id && e.StudentId == request.StudentId);

        if (existing == null)
        {
            existing = new StudentPeriodEvaluation
            {
                PeriodId = id,
                StudentId = request.StudentId
            };
            _context.StudentPeriodEvaluations.Add(existing);
        }

        existing.AttendanceScore = request.AttendanceScore;
        existing.HifzScore = request.HifzScore;
        existing.RevisionScore = request.RevisionScore;
        existing.ProgressScore = request.ProgressScore;
        existing.MatnScore = request.MatnScore;
        existing.DressScore = request.DressScore;
        existing.SheikhNotes = request.SheikhNotes;
        existing.PrayerAdvisoryScore = request.PrayerAdvisoryScore;
        existing.ParentHomeAdvisoryScore = request.ParentHomeAdvisoryScore;
        existing.IncludeAdvisoryInOverall = request.IncludeAdvisoryInOverall;
        existing.OverallScore = overall;
        existing.GradeLabel = PedagogicalGrading.ImpressionFromPercent(overall);
        existing.EvaluatedByUserId = userId.Value;
        existing.EvaluatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حفظ تقييم الفترة",
            id = existing.Id,
            existing.OverallScore,
            existing.GradeLabel
        });
    }

    [HttpPost("periods/{id}/auto-draft/{studentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AutoDraftEvaluation(int id, int studentId)
    {
        var period = await _context.EvaluationPeriods.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
        if (period == null)
            return NotFound(new { message = "فترة التقييم غير موجودة" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        if (!await _context.Students.AnyAsync(s => s.Id == studentId))
            return NotFound(new { message = "الطالب غير موجود" });

        var start = period.StartDate.Date;
        var endExclusive = period.EndDate.Date.AddDays(1);
        var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();

        // Attendance: % present+late (يشمل حلقات إضافية مرتبطة بنفس الطالب)
        var attendanceRecords = await _context.Attendances.AsNoTracking()
            .Where(a => a.StudentId == studentId && a.Date >= start && a.Date < endExclusive)
            .ToListAsync();
        var attendanceScore = attendanceRecords.Count == 0
            ? 0.0
            : Math.Round(
                attendanceRecords.Count(a =>
                    a.Status == AttendanceStatus.Present || a.Status == AttendanceStatus.Late)
                / (double)attendanceRecords.Count * 100, 1);

        // Hifz: متوسط الاختبارات الشفوية + سجلات الحفظ اليومي (Memorization)
        var oralPercents = await _context.OralExamSessions.AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Date >= start && s.Date < endExclusive)
            .Select(s => s.OverallPercent)
            .ToListAsync();

        var dailyMemEvals = await _context.HifzRecords.AsNoTracking()
            .Where(h => h.StudentId == studentId &&
                        h.Date >= start && h.Date < endExclusive &&
                        h.Type == RecordType.Memorization)
            .Select(h => h.Evaluation)
            .ToListAsync();
        var dailyMemMapped = dailyMemEvals
            .Select(PedagogicalGrading.MapEvaluationToPercent)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        var hifzParts = new List<double>();
        if (oralPercents.Count > 0) hifzParts.Add(oralPercents.Average());
        if (dailyMemMapped.Count > 0) hifzParts.Add(dailyMemMapped.Average());
        var hifzScore = hifzParts.Count == 0 ? 0.0 : Math.Round(hifzParts.Average(), 1);

        // Revision: سجلات المراجعة اليومية
        var dailyRevEvals = await _context.HifzRecords.AsNoTracking()
            .Where(h => h.StudentId == studentId &&
                        h.Date >= start && h.Date < endExclusive &&
                        h.Type == RecordType.Revision)
            .Select(h => h.Evaluation)
            .ToListAsync();
        var dailyRevMapped = dailyRevEvals
            .Select(PedagogicalGrading.MapEvaluationToPercent)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        var revisionScore = dailyRevMapped.Count == 0 ? 0.0 : Math.Round(dailyRevMapped.Average(), 1);

        // Progress: average of ProgressScoreOutOf10 * 10 for overlapping months
        var monthKeys = EnumerateYearMonths(start, period.EndDate.Date).ToList();
        var targets = await _context.StudentMonthlyTargets.AsNoTracking()
            .Where(t => t.StudentId == studentId)
            .ToListAsync();
        var overlappingScores = targets
            .Where(t => monthKeys.Any(k => k.Year == t.Year && k.Month == t.Month))
            .Select(t => t.ProgressScoreOutOf10 * 10)
            .ToList();
        var progressScore = overlappingScores.Count == 0
            ? 0.0
            : Math.Round(overlappingScores.Average(), 1);

        // Matn
        var matnEvals = await _context.MatnRecords.AsNoTracking()
            .Where(m => m.StudentId == studentId && m.Date >= start && m.Date < endExclusive)
            .Select(m => m.Evaluation)
            .ToListAsync();
        var matnMapped = matnEvals
            .Select(PedagogicalGrading.MapEvaluationToPercent)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        var matnScore = matnMapped.Count == 0 ? 0.0 : Math.Round(matnMapped.Average(), 1);

        // Dress
        var dressScores = await _context.DressRecords.AsNoTracking()
            .Where(d => d.StudentId == studentId && d.Date >= start && d.Date < endExclusive)
            .Select(d => d.ScoreOutOf10)
            .ToListAsync();
        var dressScore = dressScores.Count == 0
            ? 0.0
            : Math.Round(dressScores.Average() * 10, 1);

        // Prayer advisory: مسجد + وقت + عدد
        var prayerLogs = await _context.PrayerDailyLogs.AsNoTracking()
            .Where(p => p.StudentId == studentId && p.Date >= start && p.Date < endExclusive)
            .Select(p => new { p.PrayedInMosque, p.OnTime, p.MosquePrayerCount })
            .ToListAsync();
        double? prayerAdvisory = PedagogicalGrading.PrayerAdvisoryFromLogs(
            prayerLogs.Select(p => (p.PrayedInMosque, p.OnTime, p.MosquePrayerCount)));

        // Parent home advisory
        var homeRatings = await _context.ParentHomeFeedbacks.AsNoTracking()
            .Where(f => f.StudentId == studentId &&
                        f.WeekStartDate >= start &&
                        f.WeekStartDate < endExclusive)
            .Select(f => f.Rating)
            .ToListAsync();
        double? parentHomeAdvisory = homeRatings.Count == 0
            ? null
            : Math.Round(homeRatings.Average(r => (int)r * 20.0), 1);

        var draftOverall = ComputeOverallScore(
            attendanceScore, hifzScore, revisionScore, progressScore, matnScore, dressScore,
            prayerAdvisory, parentHomeAdvisory, includeAdvisory: false, settings);

        return Ok(new
        {
            periodId = id,
            studentId,
            attendanceScore,
            hifzScore,
            revisionScore,
            progressScore,
            matnScore,
            dressScore,
            prayerAdvisoryScore = prayerAdvisory,
            parentHomeAdvisoryScore = parentHomeAdvisory,
            includeAdvisoryInOverall = false,
            overallScore = draftOverall,
            gradeLabel = PedagogicalGrading.ImpressionFromPercent(draftOverall),
            sources = new
            {
                oralSessions = oralPercents.Count,
                dailyMemorization = dailyMemMapped.Count,
                dailyRevision = dailyRevMapped.Count,
                monthlyTargets = overlappingScores.Count,
                matnRecords = matnMapped.Count,
                dressDays = dressScores.Count,
                prayerDays = prayerLogs.Count,
                parentWeeks = homeRatings.Count,
                attendanceDays = attendanceRecords.Count
            },
            isDraft = true,
            message = "مسودة محسوبة من قاعدة البيانات — لم تُحفظ"
        });
    }

    /// <summary>
    /// يحسب الإنجاز الشهري تلقائياً من أسئلة الاختبار الشفوي الناجحة (أثمان)
    /// مع إبقاء إمكانية تجاوز الشيخ عبر UpsertMonthlyTarget.
    /// </summary>
    [HttpPost("monthly-targets/sync-from-oral")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SyncMonthlyTargetFromOral([FromBody] SyncMonthlyTargetRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        if (request.Month < 1 || request.Month > 12)
            return BadRequest(new { message = "الشهر يجب أن يكون بين 1 و 12" });

        if (!await _context.Students.AnyAsync(s => s.Id == request.StudentId))
            return NotFound(new { message = "الطالب غير موجود" });

        var monthStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var passedQuestions = await _context.OralExamQuestions.AsNoTracking()
            .Where(q => q.Session.StudentId == request.StudentId &&
                        q.Session.Date >= monthStart &&
                        q.Session.Date < monthEnd &&
                        q.IsPassed)
            .CountAsync();

        var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
        var defaultTarget = settings?.DefaultMonthlyAthmanTarget ?? 8;
        var userId = AuthorizationHelpers.GetUserId(User);

        var existing = await _context.StudentMonthlyTargets
            .FirstOrDefaultAsync(t =>
                t.StudentId == request.StudentId &&
                t.Year == request.Year &&
                t.Month == request.Month);

        if (existing == null)
        {
            existing = new StudentMonthlyTarget
            {
                StudentId = request.StudentId,
                Year = request.Year,
                Month = request.Month,
                TargetAthmanCount = request.TargetAthmanCount ?? defaultTarget,
                AchievedAthmanCount = passedQuestions,
                ProgressScoreOutOf10 = PedagogicalGrading.ProgressScoreOutOf10(
                    passedQuestions, request.TargetAthmanCount ?? defaultTarget),
                IsSpecialMode = request.IsSpecialMode ?? false,
                SpecialModeNote = request.SpecialModeNote,
                Notes = request.Notes ?? "مزامنة تلقائية من الاختبارات الشفوية",
                SetByUserId = userId,
                UpdatedAt = DateTime.UtcNow
            };
            _context.StudentMonthlyTargets.Add(existing);
        }
        else
        {
            if (request.TargetAthmanCount.HasValue)
                existing.TargetAthmanCount = request.TargetAthmanCount.Value;
            existing.AchievedAthmanCount = passedQuestions;
            existing.ProgressScoreOutOf10 = PedagogicalGrading.ProgressScoreOutOf10(
                existing.AchievedAthmanCount, existing.TargetAthmanCount);
            if (request.IsSpecialMode.HasValue)
                existing.IsSpecialMode = request.IsSpecialMode.Value;
            if (request.SpecialModeNote != null)
                existing.SpecialModeNote = request.SpecialModeNote;
            if (request.Notes != null)
                existing.Notes = request.Notes;
            existing.SetByUserId = userId;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تمت مزامنة الإنجاز من الاختبارات الشفوية",
            id = existing.Id,
            achievedFromOral = passedQuestions,
            existing.TargetAthmanCount,
            existing.AchievedAthmanCount,
            existing.ProgressScoreOutOf10
        });
    }

    // ════════════════════════════════════════════════════════
    // اللباس
    // ════════════════════════════════════════════════════════

    [HttpGet("dress")]
    [Authorize(Roles = "Admin,Teacher,Student,Parent")]
    public async Task<IActionResult> GetDress([FromQuery] int? studentId, [FromQuery] string? date)
    {
        var query = _context.DressRecords.AsNoTracking().AsQueryable();

        if (studentId.HasValue)
        {
            if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId.Value))
                return Forbid();
            query = query.Where(d => d.StudentId == studentId.Value);
        }
        else if (!User.IsInRole("Admin") && !User.IsInRole("Teacher"))
        {
            return BadRequest(new { message = "studentId مطلوب" });
        }
        else if (User.IsInRole("Teacher") && !User.IsInRole("Admin"))
        {
            var userId = AuthorizationHelpers.GetUserId(User);
            query = query.Where(d =>
                d.Student.Circle != null &&
                d.Student.Circle.Teacher != null &&
                d.Student.Circle.Teacher.UserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var day))
        {
            var dayStart = day.ToDateTime(TimeOnly.MinValue);
            var dayEnd = dayStart.AddDays(1);
            query = query.Where(d => d.Date >= dayStart && d.Date < dayEnd);
        }

        var records = await query
            .OrderByDescending(d => d.Date)
            .Select(d => new
            {
                d.Id,
                d.StudentId,
                StudentName = d.Student.User.FullName,
                Date = d.Date.ToString("yyyy-MM-dd"),
                d.IsCompliant,
                d.ScoreOutOf10,
                d.Note,
                d.RecordedByUserId
            })
            .ToListAsync();

        return Ok(records);
    }

    [HttpPost("dress/bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SaveDressBulk([FromBody] BulkDressRequest request)
    {
        if (request.Records == null || request.Records.Count == 0)
            return BadRequest(new { message = "لا توجد سجلات للحفظ" });

        if (!DateOnly.TryParse(request.Date, out var day))
            return BadRequest(new { message = "تاريخ غير صالح" });

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var studentIds = request.Records.Select(r => r.StudentId).Distinct().ToList();
        foreach (var sid in studentIds)
        {
            if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, sid))
                return StatusCode(403, new { message = $"لا يمكنك تسجيل لباس لطالب خارج صلاحيتك ({sid})" });
        }

        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var existing = await _context.DressRecords
            .Where(d => studentIds.Contains(d.StudentId) && d.Date >= dayStart && d.Date < dayEnd)
            .ToListAsync();

        foreach (var rec in request.Records)
        {
            var score = rec.ScoreOutOf10 ?? (rec.IsCompliant ? 10.0 : 0.0);
            var row = existing.FirstOrDefault(d => d.StudentId == rec.StudentId);
            if (row != null)
            {
                row.IsCompliant = rec.IsCompliant;
                row.ScoreOutOf10 = score;
                row.Note = rec.Note;
                row.RecordedByUserId = userId.Value;
            }
            else
            {
                _context.DressRecords.Add(new DressRecord
                {
                    StudentId = rec.StudentId,
                    Date = dayStart,
                    IsCompliant = rec.IsCompliant,
                    ScoreOutOf10 = score,
                    Note = rec.Note,
                    RecordedByUserId = userId.Value
                });
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"تم حفظ {request.Records.Count} سجل لباس" });
    }

    // ════════════════════════════════════════════════════════
    // الصلاة
    // ════════════════════════════════════════════════════════

    [HttpGet("prayer/my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyPrayerLogs()
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

        var logs = await _context.PrayerDailyLogs.AsNoTracking()
            .Where(p => p.StudentId == studentId.Value)
            .OrderByDescending(p => p.Date)
            .Select(p => new
            {
                p.Id,
                Date = p.Date.ToString("yyyy-MM-dd"),
                p.PrayedInMosque,
                p.OnTime,
                p.MosquePrayerCount,
                p.IsLocked,
                p.StudentNote,
                p.SheikhOverrideNote,
                p.SubmittedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    [HttpPost("prayer/my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> SubmitMyPrayerLog([FromBody] StudentPrayerLogRequest request)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var studentId = await _context.Students
            .Where(s => s.UserId == userId.Value)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync();

        if (studentId == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });

        if (!DateOnly.TryParse(request.Date, out var day))
            return BadRequest(new { message = "صيغة التاريخ يجب أن تكون yyyy-MM-dd" });

        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var existing = await _context.PrayerDailyLogs
            .FirstOrDefaultAsync(p => p.StudentId == studentId.Value && p.Date >= dayStart && p.Date < dayEnd);

        if (existing != null && existing.IsLocked)
            return BadRequest(new { message = "لا يمكن تعديل سجل الصلاة بعد الإرسال — تواصل مع الشيخ للتعديل" });

        if (existing != null)
        {
            existing.PrayedInMosque = request.PrayedInMosque;
            existing.OnTime = request.OnTime;
            existing.MosquePrayerCount = request.MosquePrayerCount ?? existing.MosquePrayerCount;
            existing.StudentNote = request.StudentNote;
            existing.IsLocked = true;
            existing.SubmittedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new PrayerDailyLog
            {
                StudentId = studentId.Value,
                Date = dayStart,
                PrayedInMosque = request.PrayedInMosque,
                OnTime = request.OnTime,
                MosquePrayerCount = Math.Clamp(request.MosquePrayerCount ?? 0, 0, 5),
                StudentNote = request.StudentNote,
                IsLocked = true,
                SubmittedAt = DateTime.UtcNow
            };
            _context.PrayerDailyLogs.Add(existing);
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تسجيل الصلاة", id = existing.Id });
    }

    [HttpPut("prayer/{id}/override")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> OverridePrayerLog(int id, [FromBody] OverridePrayerLogRequest request)
    {
        var log = await _context.PrayerDailyLogs.FirstOrDefaultAsync(p => p.Id == id);
        if (log == null)
            return NotFound(new { message = "سجل الصلاة غير موجود" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, log.StudentId))
            return Forbid();

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        log.IsLocked = false;

        if (request.PrayedInMosque.HasValue)
            log.PrayedInMosque = request.PrayedInMosque.Value;
        if (request.OnTime.HasValue)
            log.OnTime = request.OnTime.Value;
        if (request.MosquePrayerCount.HasValue)
            log.MosquePrayerCount = Math.Clamp(request.MosquePrayerCount.Value, 0, 5);
        if (request.SheikhOverrideNote != null)
            log.SheikhOverrideNote = request.SheikhOverrideNote;

        log.OverriddenByUserId = userId.Value;
        log.OverriddenAt = DateTime.UtcNow;
        log.IsLocked = true;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعديل سجل الصلاة بواسطة الشيخ", id = log.Id });
    }

    [HttpGet("prayer")]
    [Authorize(Roles = "Admin,Teacher,Parent")]
    public async Task<IActionResult> GetPrayerByStudent([FromQuery] int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var logs = await _context.PrayerDailyLogs.AsNoTracking()
            .Where(p => p.StudentId == studentId)
            .OrderByDescending(p => p.Date)
            .Select(p => new
            {
                p.Id,
                p.StudentId,
                Date = p.Date.ToString("yyyy-MM-dd"),
                p.PrayedInMosque,
                p.OnTime,
                p.MosquePrayerCount,
                p.IsLocked,
                p.StudentNote,
                p.SheikhOverrideNote,
                p.OverriddenByUserId,
                p.OverriddenAt,
                p.SubmittedAt
            })
            .ToListAsync();

        return Ok(logs);
    }

    // ════════════════════════════════════════════════════════
    // ملاحظات ولي الأمر المنزلية
    // ════════════════════════════════════════════════════════

    [HttpGet("parent-home")]
    [Authorize(Roles = "Admin,Teacher,Parent,Student")]
    public async Task<IActionResult> GetParentHomeFeedback([FromQuery] int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var feedbacks = await _context.ParentHomeFeedbacks.AsNoTracking()
            .Where(f => f.StudentId == studentId)
            .OrderByDescending(f => f.WeekStartDate)
            .Select(f => new
            {
                f.Id,
                f.StudentId,
                f.ParentId,
                WeekStartDate = f.WeekStartDate.ToString("yyyy-MM-dd"),
                Rating = f.Rating.ToString(),
                RatingLabel = PedagogicalGrading.RatingLabel(f.Rating),
                f.Notes,
                f.SubmittedAt
            })
            .ToListAsync();

        return Ok(feedbacks);
    }

    [HttpPost("parent-home")]
    [Authorize(Roles = "Admin,Parent")]
    public async Task<IActionResult> UpsertParentHomeFeedback([FromBody] ParentHomeFeedbackRequest request)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        int parentId;
        if (User.IsInRole("Admin") && !User.IsInRole("Parent"))
        {
            var student = await _context.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.StudentId);
            if (student == null)
                return NotFound(new { message = "الطالب غير موجود" });
            if (student.ParentId == null)
                return BadRequest(new { message = "الطالب غير مرتبط بولي أمر" });
            parentId = student.ParentId.Value;
        }
        else
        {
            var parent = await _context.Parents.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId.Value);
            if (parent == null)
                return NotFound(new { message = "لم يُعثر على بيانات ولي الأمر" });

            var owns = await _context.Students.AnyAsync(s =>
                s.Id == request.StudentId && s.ParentId == parent.Id);
            if (!owns)
                return StatusCode(403, new { message = "لا يمكنك إضافة ملاحظة لطالب ليس من أبنائك" });

            parentId = parent.Id;
        }

        if (!TryParseHomeRating(request.Rating, out var rating))
            return BadRequest(new { message = "التقييم غير صالح. استخدم Excellent|VeryGood|Good|Acceptable|Weak أو رقماً من 1 إلى 5" });

        var weekStart = request.WeekStartDate.Date;

        var existing = await _context.ParentHomeFeedbacks
            .FirstOrDefaultAsync(f => f.StudentId == request.StudentId && f.WeekStartDate == weekStart);

        if (existing == null)
        {
            existing = new ParentHomeFeedback
            {
                StudentId = request.StudentId,
                ParentId = parentId,
                WeekStartDate = weekStart,
                Rating = rating,
                Notes = request.Notes,
                SubmittedAt = DateTime.UtcNow
            };
            _context.ParentHomeFeedbacks.Add(existing);
        }
        else
        {
            existing.ParentId = parentId;
            existing.Rating = rating;
            existing.Notes = request.Notes;
            existing.SubmittedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return Ok(new
        {
            message = "تم حفظ ملاحظة المتابعة المنزلية",
            id = existing.Id,
            rating = existing.Rating.ToString(),
            ratingLabel = PedagogicalGrading.RatingLabel(existing.Rating)
        });
    }

    // ════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════

    private static double ComputeOverallScore(
        double attendance,
        double hifz,
        double revision,
        double progress,
        double matn,
        double dress,
        double? prayerAdvisory,
        double? parentHomeAdvisory,
        bool includeAdvisory,
        PlatformSettings? settings)
    {
        var wAtt = settings?.WeightAttendance ?? 1;
        var wHifz = settings?.WeightHifz ?? 1;
        var wRev = settings?.WeightRevision ?? 1;
        var wProg = settings?.WeightProgress ?? 1;
        var wMatn = settings?.WeightMatn ?? 1;
        var wDress = settings?.WeightDress ?? 1;

        return PedagogicalGrading.ComputeWeightedOverall(
            new[]
            {
                (attendance, wAtt),
                (hifz, wHifz),
                (revision, wRev),
                (progress, wProg),
                (matn, wMatn),
                (dress, wDress)
            },
            prayerAdvisory,
            parentHomeAdvisory,
            includeAdvisory);
    }

    private static IEnumerable<(int Year, int Month)> EnumerateYearMonths(DateTime start, DateTime end)
    {
        var cursor = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        while (cursor <= last)
        {
            yield return (cursor.Year, cursor.Month);
            cursor = cursor.AddMonths(1);
        }
    }

    private static bool TryParseHomeRating(object? rating, out HomePracticeRating parsed)
    {
        parsed = HomePracticeRating.Good;
        if (rating == null)
            return false;

        if (rating is System.Text.Json.JsonElement je)
        {
            if (je.ValueKind == System.Text.Json.JsonValueKind.Number && je.TryGetInt32(out var n))
            {
                if (Enum.IsDefined(typeof(HomePracticeRating), n))
                {
                    parsed = (HomePracticeRating)n;
                    return true;
                }
                return false;
            }

            if (je.ValueKind == System.Text.Json.JsonValueKind.String)
                rating = je.GetString();
            else
                return false;
        }

        if (rating is int i)
        {
            if (Enum.IsDefined(typeof(HomePracticeRating), i))
            {
                parsed = (HomePracticeRating)i;
                return true;
            }
            return false;
        }

        var text = rating?.ToString()?.Trim();
        if (string.IsNullOrEmpty(text))
            return false;

        if (int.TryParse(text, out var asInt) && Enum.IsDefined(typeof(HomePracticeRating), asInt))
        {
            parsed = (HomePracticeRating)asInt;
            return true;
        }

        return Enum.TryParse(text, true, out parsed);
    }
}

// ─────────────────────────────────────────────────
// Request Models
// ─────────────────────────────────────────────────

public class CreateMatnRequest
{
    public int StudentId { get; set; }
    public DateTime? Date { get; set; }
    public string MatnName { get; set; } = string.Empty;
    public string Portion { get; set; } = string.Empty;
    public string Type { get; set; } = nameof(MatnRecordType.Memorization);
    public string Evaluation { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class UpsertMonthlyTargetRequest
{
    public int StudentId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int TargetAthmanCount { get; set; }
    public int? AchievedAthmanCount { get; set; }
    public bool? IsSpecialMode { get; set; }
    public string? SpecialModeNote { get; set; }
    public string? Notes { get; set; }
}

public class CreateEvaluationPeriodRequest
{
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int? CircleId { get; set; }
    public string? Notes { get; set; }
}

public class UpsertPeriodEvaluationRequest
{
    public int StudentId { get; set; }
    public double AttendanceScore { get; set; }
    public double HifzScore { get; set; }
    public double RevisionScore { get; set; }
    public double ProgressScore { get; set; }
    public double MatnScore { get; set; }
    public double DressScore { get; set; }
    public string? SheikhNotes { get; set; }
    public double? PrayerAdvisoryScore { get; set; }
    public double? ParentHomeAdvisoryScore { get; set; }
    public bool IncludeAdvisoryInOverall { get; set; }
}

public class SyncMonthlyTargetRequest
{
    public int StudentId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int? TargetAthmanCount { get; set; }
    public bool? IsSpecialMode { get; set; }
    public string? SpecialModeNote { get; set; }
    public string? Notes { get; set; }
}

public class BulkDressRequest
{
    public string Date { get; set; } = string.Empty;
    public List<DressRecordItem> Records { get; set; } = new();
}

public class DressRecordItem
{
    public int StudentId { get; set; }
    public bool IsCompliant { get; set; } = true;
    public double? ScoreOutOf10 { get; set; }
    public string? Note { get; set; }
}

public class StudentPrayerLogRequest
{
    public string Date { get; set; } = string.Empty;
    public bool PrayedInMosque { get; set; }
    public bool OnTime { get; set; }
    public int? MosquePrayerCount { get; set; }
    public string? StudentNote { get; set; }
}

public class OverridePrayerLogRequest
{
    public bool? PrayedInMosque { get; set; }
    public bool? OnTime { get; set; }
    public int? MosquePrayerCount { get; set; }
    public string? SheikhOverrideNote { get; set; }
}

public class ParentHomeFeedbackRequest
{
    public int StudentId { get; set; }
    public DateTime WeekStartDate { get; set; }
    /// <summary>Excellent|VeryGood|Good|Acceptable|Weak أو رقم 1–5</summary>
    public object Rating { get; set; } = "Good";
    public string? Notes { get; set; }
}
