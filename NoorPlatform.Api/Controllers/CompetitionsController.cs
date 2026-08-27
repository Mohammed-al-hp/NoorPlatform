using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompetitionsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public CompetitionsController(NoorDbContext context)
    {
        _context = context;
    }

    // ═══════════════════════════════════════════════
    // إدارة المسابقات (CRUD)
    // ═══════════════════════════════════════════════

    // ─── جلب جميع المسابقات ───
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAll()
    {
        var competitions = await _context.Competitions
            .OrderByDescending(c => c.Date)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Date,
                Level = c.Level.ToString(),
                c.Description,
                ParticipantsCount = c.Results.Count
            })
            .ToListAsync();

        return Ok(competitions);
    }

    // ─── جلب مسابقة بالمعرف ───
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetById(int id)
    {
        var competition = await _context.Competitions
            .Include(c => c.Results)
                .ThenInclude(r => r.Student)
                    .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        return Ok(new
        {
            competition.Id,
            competition.Title,
            competition.Date,
            Level = competition.Level.ToString(),
            competition.Description,
            Results = competition.Results
                .OrderByDescending(r => r.HifzScore + r.TajweedScore + r.TafseerScore)
                .Select((r, index) => new
                {
                    Rank = index + 1,
                    r.Id,
                    StudentName = r.Student.User.FullName,
                    r.HifzScore,
                    r.TajweedScore,
                    r.TafseerScore,
                    TotalScore = r.HifzScore + r.TajweedScore + r.TafseerScore,
                    r.Feedback
                })
        });
    }

    public class CreateCompetitionDto
    {
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Level { get; set; } = "Internal";
        public string Description { get; set; } = string.Empty;
    }

    // ─── إنشاء مسابقة جديدة ───
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateCompetitionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "اسم المسابقة مطلوب" });

        if (!Enum.TryParse<CompetitionLevel>(dto.Level, true, out var level))
            return BadRequest(new { message = "مستوى المسابقة غير صحيح. المستويات المتاحة: Internal, Regional, National" });

        var competition = new Competition
        {
            Title = dto.Title,
            Date = dto.Date,
            Level = level,
            Description = dto.Description
        };

        _context.Competitions.Add(competition);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = competition.Id }, new
        {
            competition.Id,
            competition.Title,
            competition.Date,
            Level = competition.Level.ToString()
        });
    }

    // ─── تعديل مسابقة ───
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateCompetitionDto dto)
    {
        var competition = await _context.Competitions.FindAsync(id);
        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        if (!Enum.TryParse<CompetitionLevel>(dto.Level, true, out var level))
            return BadRequest(new { message = "مستوى المسابقة غير صحيح" });

        competition.Title = dto.Title;
        competition.Date = dto.Date;
        competition.Level = level;
        competition.Description = dto.Description;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث المسابقة بنجاح" });
    }

    // ─── حذف مسابقة ───
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var competition = await _context.Competitions
            .Include(c => c.Results)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        _context.Competitions.Remove(competition);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف المسابقة ونتائجها بنجاح" });
    }

    // ═══════════════════════════════════════════════
    // إدارة نتائج المسابقات وتراتيب الفائزين
    // ═══════════════════════════════════════════════

    public class AddResultDto
    {
        public int StudentId { get; set; }
        public double HifzScore { get; set; }
        public double TajweedScore { get; set; }
        public double TafseerScore { get; set; }
        public string Feedback { get; set; } = string.Empty;
    }

    // ─── رصد درجات طالب في مسابقة ───
    [HttpPost("{competitionId}/results")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AddResult(int competitionId, [FromBody] AddResultDto dto)
    {
        var competition = await _context.Competitions.FindAsync(competitionId);
        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == dto.StudentId);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        // التحقق من عدم التكرار
        var exists = await _context.CompetitionResults
            .AnyAsync(r => r.CompetitionId == competitionId && r.StudentId == dto.StudentId);
        if (exists)
            return BadRequest(new { message = "الطالب مسجل بالفعل في هذه المسابقة" });

        var result = new CompetitionResult
        {
            CompetitionId = competitionId,
            StudentId = dto.StudentId,
            HifzScore = dto.HifzScore,
            TajweedScore = dto.TajweedScore,
            TafseerScore = dto.TafseerScore,
            Feedback = dto.Feedback
        };

        _context.CompetitionResults.Add(result);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم رصد الدرجات بنجاح",
            result = new
            {
                result.Id,
                StudentName = student.User.FullName,
                result.HifzScore,
                result.TajweedScore,
                result.TafseerScore,
                TotalScore = result.HifzScore + result.TajweedScore + result.TafseerScore
            }
        });
    }

    // ─── رصد درجات دفعة كاملة (Bulk) للمسابقة ───
    [HttpPost("{competitionId}/results/bulk")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> AddBulkResults(int competitionId, [FromBody] List<AddResultDto> dtos)
    {
        var competition = await _context.Competitions.FindAsync(competitionId);
        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        var added = 0;
        foreach (var dto in dtos)
        {
            var exists = await _context.CompetitionResults
                .AnyAsync(r => r.CompetitionId == competitionId && r.StudentId == dto.StudentId);
            if (exists) continue;

            _context.CompetitionResults.Add(new CompetitionResult
            {
                CompetitionId = competitionId,
                StudentId = dto.StudentId,
                HifzScore = dto.HifzScore,
                TajweedScore = dto.TajweedScore,
                TafseerScore = dto.TafseerScore,
                Feedback = dto.Feedback
            });
            added++;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = $"تم رصد درجات {added} طالب بنجاح" });
    }

    // ═══════════════════════════════════════════════
    // تراتيب الفائزين (Leaderboard) — الميزة الجوهرية
    // ═══════════════════════════════════════════════

    /// <summary>
    /// يُرجع ترتيب الطلاب في المسابقة تنازلياً حسب إجمالي الدرجات.
    /// يُستخدم من قبل لجنة التحكيم لعرض النتائج على الشاشة.
    /// </summary>
    [HttpGet("{competitionId}/leaderboard")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetLeaderboard(int competitionId)
    {
        var competition = await _context.Competitions.FindAsync(competitionId);
        if (competition == null)
            return NotFound(new { message = "المسابقة غير موجودة" });

        var results = await _context.CompetitionResults
            .Where(r => r.CompetitionId == competitionId)
            .Include(r => r.Student)
                .ThenInclude(s => s.User)
            .Include(r => r.Student)
                .ThenInclude(s => s.Circle)
            .OrderByDescending(r => r.HifzScore + r.TajweedScore + r.TafseerScore)
            .Select(r => new
            {
                r.Id,
                StudentName = r.Student.User.FullName,
                CircleName = r.Student.Circle != null ? r.Student.Circle.Name : "—",
                r.HifzScore,
                r.TajweedScore,
                r.TafseerScore,
                TotalScore = r.HifzScore + r.TajweedScore + r.TafseerScore,
                r.Feedback
            })
            .ToListAsync();

        // إضافة الترتيب (المركز) لكل طالب
        var leaderboard = results.Select((r, index) => new
        {
            Rank = index + 1,
            r.Id,
            r.StudentName,
            r.CircleName,
            r.HifzScore,
            r.TajweedScore,
            r.TafseerScore,
            r.TotalScore,
            r.Feedback
        });

        return Ok(new
        {
            CompetitionTitle = competition.Title,
            CompetitionDate = competition.Date,
            Level = competition.Level.ToString(),
            TotalParticipants = results.Count,
            Leaderboard = leaderboard
        });
    }

    // ─── تعديل نتيجة طالب ───
    [HttpPut("results/{resultId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateResult(int resultId, [FromBody] AddResultDto dto)
    {
        var result = await _context.CompetitionResults.FindAsync(resultId);
        if (result == null)
            return NotFound(new { message = "النتيجة غير موجودة" });

        result.HifzScore = dto.HifzScore;
        result.TajweedScore = dto.TajweedScore;
        result.TafseerScore = dto.TafseerScore;
        result.Feedback = dto.Feedback;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث الدرجات بنجاح" });
    }

    // ─── حذف نتيجة طالب ───
    [HttpDelete("results/{resultId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteResult(int resultId)
    {
        var result = await _context.CompetitionResults.FindAsync(resultId);
        if (result == null)
            return NotFound(new { message = "النتيجة غير موجودة" });

        _context.CompetitionResults.Remove(result);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف النتيجة بنجاح" });
    }
}
