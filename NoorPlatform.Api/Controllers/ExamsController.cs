using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    // ─────────────────────────────────────────────────
    [HttpGet]
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
                AverageScore = e.Results.Any()
                    ? Math.Round(e.Results.Average(r => r.Score / r.MaxScore * 100), 1)
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
                Percentage = Math.Round(r.Score / r.MaxScore * 100, 1),
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
    // ─────────────────────────────────────────────────
    [HttpPost("{id}/results")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AddResults(int id, [FromBody] List<AddExamResultRequest> results)
    {
        var exam = await _context.Exams.FindAsync(id);
        if (exam == null)
            return NotFound(new { message = "الاختبار غير موجود" });

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
        var studentIds = examResults.Select(r => r.StudentId).Distinct().ToList();
        var students = await _context.Students.Include(s => s.User).Where(s => studentIds.Contains(s.Id)).ToListAsync();
        
        var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
        var userName = User.Identity?.Name ?? "User";

        foreach (var r in examResults)
        {
            var student = students.FirstOrDefault(s => s.Id == r.StudentId);
            if (student != null)
            {
                var percentage = (r.Score / r.MaxScore) * 100;
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
