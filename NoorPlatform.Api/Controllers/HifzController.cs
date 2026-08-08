using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using Microsoft.AspNetCore.Authorization;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HifzController : ControllerBase
{
    private readonly NoorDbContext _context;

    public HifzController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/hifz/student/{studentId}
    [HttpGet("student/{studentId}")]
    public async Task<IActionResult> GetStudentRecords(int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var records = await _context.HifzRecords
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                r.Date,
                r.SurahName,
                r.ToSurahName,
                r.Verses,
                r.VerseCount,
                r.StartVerseText,
                r.EndVerseText,
                r.RevisionMode,
                Type = r.Type.ToString(),
                r.Evaluation,
                r.Notes,
                r.SessionDetailsJson
            })
            .ToListAsync();
        return Ok(records);
    }

    /// <summary>
    /// سجل تسميع ومراجعة الطالب الحالي بالكامل — الهوية من الـ token فقط (لا يقبل studentId).
    /// </summary>
    [HttpGet("my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyHifz()
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId == null)
            return Unauthorized();

        var student = await _context.Students.AsNoTracking()
            .Where(s => s.UserId == userId.Value)
            .Select(s => new { s.Id, s.Level })
            .FirstOrDefaultAsync();

        if (student == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });

        var entityRecords = await _context.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == student.Id)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        var hifzProgress = HifzProgressCalculator.Calculate(entityRecords);

        var memorizationSessions = entityRecords.Count(r => r.Type == RecordType.Memorization);
        var revisionSessions = entityRecords.Count(r => r.Type == RecordType.Revision);
        var memorizedVerses = entityRecords
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses));

        var records = entityRecords.Select(r => new
        {
            id = r.Id,
            date = r.Date.ToString("yyyy-MM-dd"),
            surahName = r.SurahName,
            toSurahName = r.ToSurahName,
            verses = r.Verses,
            verseCount = r.VerseCount,
            type = r.Type.ToString(),
            evaluation = r.Evaluation,
            notes = r.Notes,
            revisionMode = r.RevisionMode
        }).ToList();

        return Ok(new
        {
            level = student.Level,
            hifzProgress,
            summary = new
            {
                memorizationSessions,
                revisionSessions,
                memorizedVerses,
                totalSessions = entityRecords.Count
            },
            records
        });
    }

    // GET /api/hifz/recent?count=10
    [HttpGet("recent")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 10)
    {
        count = Math.Clamp(count, 1, 50);
        var query = _context.HifzRecords
            .Include(r => r.Student).ThenInclude(s => s.User)
            .AsQueryable();

        // ─── إصلاح: تقييد المحفّظ برؤية سجلات طلاب حلقته فقط ───
        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher)
        {
            var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
            query = query.Where(r => r.Student.Circle != null
                                   && r.Student.Circle.Teacher != null
                                   && r.Student.Circle.Teacher.UserId == userId);
        }

        var records = await query
            .OrderByDescending(r => r.Date)
            .Take(count)
                .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student.User.FullName,
                r.Date,
                r.SurahName,
                r.ToSurahName,
                r.Verses,
                r.VerseCount,
                r.StartVerseText,
                r.EndVerseText,
                r.RevisionMode,
                Type = r.Type.ToString(),
                r.Evaluation,
                r.Notes,
                r.SessionDetailsJson
            })
            .ToListAsync();
        return Ok(records);
    }

    // POST /api/hifz
    [HttpPost]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AddRecord([FromBody] AddHifzRecordRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, request.StudentId))
            return Forbid();

        var studentExists = await _context.Students.AnyAsync(s => s.Id == request.StudentId);
        if (!studentExists)
            return NotFound(new { message = "الطالب غير موجود" });

        if (!Enum.TryParse<RecordType>(request.Type, true, out var recordType))
            return BadRequest(new { message = $"نوع السجل غير صالح: {request.Type}. القيم المقبولة: Memorization, Revision" });

        var verses = request.Verses?.Trim() ?? string.Empty;

        // ✅ إصلاح 1: حساب VerseCount تلقائياً من نص الآيات
        var verseCount = HifzRecord.ParseVerseCount(verses);

        var record = new HifzRecord
        {
            StudentId = request.StudentId,
            SurahName = request.SurahName?.Trim() ?? string.Empty,
            ToSurahName = request.ToSurahName?.Trim(),
            Verses = verses,
            VerseCount = verseCount,
            StartVerseText = request.StartVerseText?.Trim() ?? string.Empty,
            EndVerseText = request.EndVerseText?.Trim() ?? string.Empty,
            RevisionMode = request.RevisionMode?.Trim(),
            SessionDetailsJson = request.SessionDetailsJson,
            Type = recordType,
            Evaluation = request.Evaluation?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty,
            Date = request.Date ?? DateTime.UtcNow
        };

        _context.HifzRecords.Add(record);
        
        // Gamification & ActivityFeed
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student != null)
        {
            if (recordType == RecordType.Memorization) student.Points += 50;
            else student.Points += 20;

            _context.ActivityFeeds.Add(new ActivityFeed {
                UserId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!),
                UserName = User.Identity?.Name ?? "User",
                ActivityType = "Hifz",
                Description = $"أكمل الطالب {student.User.FullName} تسميع {record.SurahName} ({record.Verses})",
                Icon = "📖",
                Color = "green"
            });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم حفظ جلسة التسميع بنجاح",
            record.Id,
            record.StudentId,
            record.SurahName,
            record.Verses,
            record.VerseCount,
            Type = record.Type.ToString(),
            record.Evaluation,
            record.Date
        });
    }

    // DELETE /api/hifz/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _context.HifzRecords.FindAsync(id);
        if (record == null)
            return NotFound(new { message = "السجل غير موجود" });

        // ─── إصلاح أمني: التحقق من ملكية المعلم للطالب قبل السماح بالحذف ───
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, record.StudentId))
            return Forbid();

        _context.HifzRecords.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف السجل" });
    }
}

public class AddHifzRecordRequest
{
    public int StudentId { get; set; }
    public string SurahName { get; set; } = string.Empty;
    public string? ToSurahName { get; set; }
    public string Verses { get; set; } = string.Empty;  // مثال: "1-10"
    public string? StartVerseText { get; set; }
    public string? EndVerseText { get; set; }
    public string? RevisionMode { get; set; }
    public string? SessionDetailsJson { get; set; }
    public string Type { get; set; } = "Memorization";
    public string Evaluation { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
}
