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
public class ExamsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public ExamsController(NoorDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────
    // GET /api/exams
    // جميع الاختبارات مع عدد المشاركين ومتوسط الدرجات
    // إصلاح: حماية من القسمة على صفر في حساب النسبة المئوية
    // ─────────────────────────────────────────────────
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAll()
    {
        var exams = await _context.Exams
            .Include(e => e.Results)
            .OrderByDescending(e => e.Date)
            .Select(e => new
            {
                e.Id,
                e.Title,
                e.Date,
                e.Description,
                ParticipantsCount = e.Results.Count,
                // إصلاح: حماية من القسمة على صفر - تجاهل النتائج ذات MaxScore = 0
                AverageScore = e.Results.Any(r => r.MaxScore > 0)
                    ? Math.Round(e.Results.Where(r => r.MaxScore > 0).Average(r => r.Score / r.MaxScore * 100), 1)
                    : 0
            })
            .ToListAsync();

        return Ok(exams);
    }

    // ─────────────────────────────────────────────────
    // GET /api/exams/{id}
    // تفاصيل اختبار معين مع نتائج الطلاب
    // ─────────────────────────────────────────────────
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetById(int id)
    {
        var exam = await _context.Exams
            .Include(e => e.Results).ThenInclude(r => r.Student).ThenInclude(s => s.User)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (exam == null)
            return NotFound(new { message = "الاختبار غير موجود" });

        return Ok(new
        {
            exam.Id,
            exam.Title,
            exam.Date,
            exam.Description,
            Results = exam.Results.Select(r => new
            {
                r.Id,
                StudentName = r.Student.User.FullName,
                r.Score,
                r.MaxScore,
                // إصلاح: حماية من القسمة على صفر في تفاصيل الاختبار
                Percentage = r.MaxScore > 0 ? Math.Round(r.Score / r.MaxScore * 100, 1) : 0,
                r.Feedback
            })
        });
    }

    // ─────────────────────────────────────────────────
    // POST /api/exams
    // إنشاء اختبار جديد (Admin أو Teacher)
    // ─────────────────────────────────────────────────
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Create([FromBody] CreateExamRequest request)
    {
        var exam = new Exam
        {
            Title = request.Title,
            Date = request.Date,
            Description = request.Description ?? string.Empty
        };

        _context.Exams.Add(exam);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم إنشاء الاختبار بنجاح", examId = exam.Id });
    }

    // ─────────────────────────────────────────────────
    // POST /api/exams/{id}/results
    // تسجيل نتائج الطلاب في اختبار
    // إصلاحات: التحقق من وجود الطالب، منع التكرار، فحص ملكية المحفظ
    // ─────────────────────────────────────────────────
    [HttpPost("{id}/results")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AddResults(int id, [FromBody] List<AddExamResultRequest> results)
    {
        var exam = await _context.Exams.FindAsync(id);
        if (exam == null)
            return NotFound(new { message = "الاختبار غير موجود" });

        // التحقق الأساسي: الدرجة الكاملة يجب أن تكون أكبر من صفر
        foreach (var r in results)
        {
            if (r.MaxScore <= 0)
                return BadRequest(new { message = "الدرجة الكاملة يجب أن تكون أكبر من صفر" });
        }

        var requestedStudentIds = results.Select(r => r.StudentId).Distinct().ToList();

        // ─── إصلاح حرج 1: التحقق من أن جميع الطلاب موجودون فعلاً في قاعدة البيانات ───
        var existingStudents = await _context.Students
            .Include(s => s.User)
            .Where(s => requestedStudentIds.Contains(s.Id))
            .ToListAsync();

        var existingStudentIds = existingStudents.Select(s => s.Id).ToHashSet();
        var missingIds = requestedStudentIds.Where(sid => !existingStudentIds.Contains(sid)).ToList();
        if (missingIds.Any())
            return BadRequest(new { message = $"الطلاب التالية أرقامهم غير موجودة: {string.Join(", ", missingIds)}" });

        // ─── إصلاح عالي: فحص ملكية المحفظ (Ownership) - المحفظ لا يرصد درجات لطلاب خارج حلقاته ───
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentRole = User.FindFirstValue(ClaimTypes.Role);

        if (currentRole == "Teacher")
        {
            // جلب أرقام طلاب حلقات هذا المحفظ
            var teacherStudentIds = await _context.Teachers
                .Where(t => t.UserId == int.Parse(currentUserId!))
                .SelectMany(t => t.Circles)
                .SelectMany(c => c.Students)
                .Select(s => s.Id)
                .ToListAsync();

            var teacherStudentIdsSet = teacherStudentIds.ToHashSet();
            var unauthorizedIds = requestedStudentIds.Where(sid => !teacherStudentIdsSet.Contains(sid)).ToList();
            if (unauthorizedIds.Any())
                return StatusCode(403, new { message = $"لا يمكنك رصد درجات لطلاب خارج حلقاتك. الطلاب: {string.Join(", ", unauthorizedIds)}" });
        }

        // ─── إصلاح حرج 2: منع تكرار النتيجة لنفس الطالب في نفس الاختبار ───
        var alreadyRecorded = await _context.ExamResults
            .Where(er => er.ExamId == id && requestedStudentIds.Contains(er.StudentId))
            .Select(er => er.StudentId)
            .ToListAsync();

        if (alreadyRecorded.Any())
        {
            var duplicateNames = existingStudents
                .Where(s => alreadyRecorded.Contains(s.Id))
                .Select(s => s.User.FullName);
            return Conflict(new { message = $"توجد نتائج مسجلة مسبقاً لهؤلاء الطلاب: {string.Join("، ", duplicateNames)}" });
        }

        var examResults = results.Select(r => new ExamResult
        {
            ExamId = id,
            StudentId = r.StudentId,
            Score = r.Score,
            MaxScore = r.MaxScore,
            Feedback = r.Feedback ?? string.Empty
        }).ToList();

        _context.ExamResults.AddRange(examResults);
        
        // Gamification & ActivityFeed
        var userId = int.Parse(currentUserId!);
        var userName = User.Identity?.Name ?? "User";

        foreach (var r in examResults)
        {
            var student = existingStudents.FirstOrDefault(s => s.Id == r.StudentId);
            if (student != null)
            {
                // إصلاح: حماية من القسمة على صفر في حساب النسبة
                var percentage = r.MaxScore > 0 ? (r.Score / r.MaxScore) * 100 : 0;
                if (percentage >= 90) student.Points += 100;
                else if (percentage >= 80) student.Points += 50;

                _context.ActivityFeeds.Add(new ActivityFeed {
                    UserId = userId,
                    UserName = userName,
                    ActivityType = "Exam",
                    Description = $"تم رصد درجة {r.Score} للطالب {student.User.FullName} في اختبار {exam.Title}",
                    Icon = "📝",
                    Color = "purple"
                });
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = $"تم تسجيل {examResults.Count} نتيجة بنجاح" });
    }

    // ─────────────────────────────────────────────────
    // DELETE /api/exams/{id}
    // حذف اختبار (Admin فقط)
    // ─────────────────────────────────────────────────
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var exam = await _context.Exams.FindAsync(id);
        if (exam == null)
            return NotFound(new { message = "الاختبار غير موجود" });

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم حذف الاختبار" });
    }
}

// ─────────────────────────────────────────────────
// Request Models
// ─────────────────────────────────────────────────
public class CreateExamRequest
{
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Description { get; set; }
}

public class AddExamResultRequest
{
    public int StudentId { get; set; }
    public double Score { get; set; }
    public double MaxScore { get; set; } = 100;
    public string? Feedback { get; set; }
}
