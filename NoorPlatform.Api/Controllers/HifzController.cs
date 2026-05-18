using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
        var records = await _context.HifzRecords
            .Where(r => r.StudentId == studentId)
            .OrderByDescending(r => r.Date)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                r.Date,
                r.SurahName,
                r.Verses,
                r.VerseCount,
                Type = r.Type.ToString(),
                r.Evaluation,
                r.Notes
            })
            .ToListAsync();
        return Ok(records);
    }

    // GET /api/hifz/recent?count=10
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 10)
    {
        var records = await _context.HifzRecords
            .Include(r => r.Student).ThenInclude(s => s.User)
            .OrderByDescending(r => r.Date)
            .Take(count)
            .Select(r => new
            {
                r.Id,
                r.StudentId,
                StudentName = r.Student.User.FullName,
                r.Date,
                r.SurahName,
                r.Verses,
                r.VerseCount,
                Type = r.Type.ToString(),
                r.Evaluation,
                r.Notes
            })
            .ToListAsync();
        return Ok(records);
    }

    // POST /api/hifz
    [HttpPost]
    public async Task<IActionResult> AddRecord([FromBody] AddHifzRecordRequest request)
    {
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
            Verses = verses,
            VerseCount = verseCount,
            Type = recordType,
            Evaluation = request.Evaluation?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty,
            Date = request.Date ?? DateTime.UtcNow
        };

        _context.HifzRecords.Add(record);
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

        _context.HifzRecords.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف السجل" });
    }
}

public class AddHifzRecordRequest
{
    public int StudentId { get; set; }
    public string SurahName { get; set; } = string.Empty;
    public string Verses { get; set; } = string.Empty;  // مثال: "1-10"
    public string Type { get; set; } = "Memorization";
    public string Evaluation { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
}