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
[Route("api/oral-exams")]
[Authorize]
public class OralExamsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public OralExamsController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/oral-exams?studentId=&from=&to=
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? studentId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var query = _context.OralExamSessions
            .AsNoTracking()
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Student).ThenInclude(st => st.Circle).ThenInclude(c => c!.Teacher)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher)
            .AsQueryable();

        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher)
        {
            var userId = AuthorizationHelpers.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            query = query.Where(s =>
                (s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.UserId == userId) ||
                (s.Student.Circle != null && s.Student.Circle.Teacher != null && s.Student.Circle.Teacher.UserId == userId));
        }

        if (studentId.HasValue)
            query = query.Where(s => s.StudentId == studentId.Value);

        if (from.HasValue)
            query = query.Where(s => s.Date >= from.Value.Date);

        if (to.HasValue)
        {
            var toExclusive = to.Value.Date.AddDays(1);
            query = query.Where(s => s.Date < toExclusive);
        }

        var sessions = await query
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.StudentId,
                StudentName = s.Student.User.FullName,
                s.CircleId,
                CircleName = s.Circle != null ? s.Circle.Name : null,
                s.Date,
                Kind = s.Kind.ToString(),
                s.ScopeLabel,
                s.Notes,
                s.OverallPercent,
                s.OverallGrade,
                s.IsConsideredMemorized,
                QuestionsCount = s.Questions.Count,
                s.RecordedByUserId,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(sessions);
    }

    // GET /api/oral-exams/{id}
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher,Student,Parent")]
    public async Task<IActionResult> GetById(int id)
    {
        var session = await _context.OralExamSessions
            .AsNoTracking()
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Circle)
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null)
            return NotFound(new { message = "جلسة الاختبار الشفوي غير موجودة" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, session.StudentId))
            return Forbid();

        return Ok(MapSessionDetail(session));
    }

    // GET /api/oral-exams/student/{studentId}
    [HttpGet("student/{studentId}")]
    [Authorize(Roles = "Admin,Teacher,Student,Parent")]
    public async Task<IActionResult> GetByStudent(int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var sessions = await _context.OralExamSessions
            .AsNoTracking()
            .Include(s => s.Questions)
            .Include(s => s.Circle)
            .Where(s => s.StudentId == studentId)
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.Id)
            .ToListAsync();

        return Ok(sessions.Select(MapSessionDetail));
    }

    // POST /api/oral-exams
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateOralExamRequest request)
    {
        if (request.Questions == null || request.Questions.Count == 0)
            return BadRequest(new { message = "يجب إضافة سؤال واحد على الأقل" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        var studentExists = await _context.Students.AnyAsync(s => s.Id == request.StudentId);
        if (!studentExists)
            return NotFound(new { message = "الطالب غير موجود" });

        if (!Enum.TryParse<OralExamKind>(request.Kind, true, out var kind))
            return BadRequest(new { message = "نوع الاختبار غير صالح. استخدم FullRecitation أو AthmanSampling" });

        if (request.CircleId.HasValue)
        {
            var circleExists = await _context.Circles.AnyAsync(c => c.Id == request.CircleId.Value);
            if (!circleExists)
                return BadRequest(new { message = "الحلقة غير موجودة" });

            if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, request.CircleId.Value))
                return Forbid();
        }

        var settings = await _context.PlatformSettings.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync();
        var maxOpenings = request.MaxOpeningsBeforeFail
            ?? settings?.OralMaxOpeningsBeforeFail
            ?? 3;
        var hesitationPenalty = settings?.OralHesitationPenalty ?? 2;
        var alertPenalty = settings?.OralAlertPenalty ?? 5;
        var openingPenalty = settings?.OralOpeningPenalty ?? 15;

        var questionInputs = request.Questions.Select(q => (
            Hesitation: Math.Max(0, q.HesitationCount),
            Alerts: Math.Max(0, q.AlertCount),
            Openings: Math.Max(0, q.OpeningCount),
            ManualScore: q.ScorePercent
        )).ToList();

        var (overallPercent, overallGrade, isMemorized, _) = PedagogicalGrading.AggregateSession(
            questionInputs,
            maxOpenings,
            hesitationPenalty,
            alertPenalty,
            openingPenalty);

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var session = new OralExamSession
        {
            StudentId = request.StudentId,
            CircleId = request.CircleId,
            Date = request.Date?.Date ?? DateTime.UtcNow.Date,
            Kind = kind,
            ScopeLabel = request.ScopeLabel?.Trim() ?? string.Empty,
            Notes = request.Notes,
            MaxOpeningsBeforeFail = maxOpenings,
            OverallPercent = overallPercent,
            OverallGrade = overallGrade,
            IsConsideredMemorized = isMemorized,
            RecordedByUserId = userId.Value,
            CreatedAt = DateTime.UtcNow
        };

        var order = 0;
        foreach (var q in request.Questions)
        {
            var score = q.ScorePercent
                ?? PedagogicalGrading.ScoreQuestion(
                    q.HesitationCount, q.AlertCount, q.OpeningCount,
                    hesitationPenalty, alertPenalty, openingPenalty);

            var impression = string.IsNullOrWhiteSpace(q.Impression)
                ? PedagogicalGrading.ImpressionFromPercent(score)
                : q.Impression.Trim();

            session.Questions.Add(new OralExamQuestion
            {
                OrderIndex = order++,
                Label = q.Label?.Trim() ?? string.Empty,
                HesitationCount = Math.Max(0, q.HesitationCount),
                AlertCount = Math.Max(0, q.AlertCount),
                OpeningCount = Math.Max(0, q.OpeningCount),
                ScorePercent = Math.Round(score, 1),
                Impression = impression,
                IsPassed = score >= 50
            });
        }

        _context.OralExamSessions.Add(session);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم تسجيل الاختبار الشفوي بنجاح",
            sessionId = session.Id,
            session.OverallPercent,
            session.OverallGrade,
            session.IsConsideredMemorized
        });
    }

    // DELETE /api/oral-exams/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Delete(int id)
    {
        var session = await _context.OralExamSessions
            .Include(s => s.Student).ThenInclude(st => st.Circle).ThenInclude(c => c!.Teacher)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher)
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (session == null)
            return NotFound(new { message = "جلسة الاختبار الشفوي غير موجودة" });

        if (!User.IsInRole("Admin"))
        {
            var userId = AuthorizationHelpers.GetUserId(User);
            if (userId == null)
                return Unauthorized();

            var owns =
                session.RecordedByUserId == userId.Value ||
                (session.Circle?.Teacher != null && session.Circle.Teacher.UserId == userId.Value) ||
                (session.Student.Circle?.Teacher != null && session.Student.Circle.Teacher.UserId == userId.Value);

            if (!owns)
                return StatusCode(403, new { message = "لا يمكنك حذف جلسة لا تملكها" });
        }

        _context.OralExamQuestions.RemoveRange(session.Questions);
        _context.OralExamSessions.Remove(session);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم حذف جلسة الاختبار الشفوي" });
    }

    private static object MapSessionDetail(OralExamSession s) => new
    {
        s.Id,
        s.StudentId,
        StudentName = s.Student?.User?.FullName,
        s.CircleId,
        CircleName = s.Circle?.Name,
        s.Date,
        Kind = s.Kind.ToString(),
        s.ScopeLabel,
        s.Notes,
        s.MaxOpeningsBeforeFail,
        s.OverallPercent,
        s.OverallGrade,
        s.IsConsideredMemorized,
        s.RecordedByUserId,
        s.CreatedAt,
        Questions = s.Questions
            .OrderBy(q => q.OrderIndex)
            .Select(q => new
            {
                q.Id,
                q.OrderIndex,
                q.Label,
                q.HesitationCount,
                q.AlertCount,
                q.OpeningCount,
                q.ScorePercent,
                q.Impression,
                q.IsPassed
            })
    };
}

// ─────────────────────────────────────────────────
// Request Models
// ─────────────────────────────────────────────────

public class CreateOralExamRequest
{
    public int StudentId { get; set; }
    public int? CircleId { get; set; }
    public DateTime? Date { get; set; }
    public string Kind { get; set; } = nameof(OralExamKind.AthmanSampling);
    public string ScopeLabel { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public int? MaxOpeningsBeforeFail { get; set; }
    public List<CreateOralExamQuestionRequest> Questions { get; set; } = new();
}

public class CreateOralExamQuestionRequest
{
    public string Label { get; set; } = string.Empty;
    public int HesitationCount { get; set; }
    public int AlertCount { get; set; }
    public int OpeningCount { get; set; }
    public double? ScorePercent { get; set; }
    public string? Impression { get; set; }
}
