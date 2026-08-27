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
    /// آخر موضع حفظ (تسميع جديد فقط) للطالب — يُستخدم لاقتراح نقطة البداية
    /// تلقائيًا في نموذج تسجيل جلسة تسميع جديدة.
    /// GET /api/hifz/last-position/{studentId}
    /// </summary>
    [HttpGet("last-position/{studentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetLastPosition(int studentId)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var studentExists = await _context.Students.AnyAsync(s => s.Id == studentId);
        if (!studentExists)
            return NotFound(new { message = "الطالب غير موجود" });

        var lastRecord = await _context.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Type == RecordType.Memorization)
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync();

        if (lastRecord == null)
        {
            return Ok(new
            {
                hasPrevious = false,
                surahName = (string?)null,
                nextVerse = 1
            });
        }

        // آخر سورة حفظها الطالب فعليًا: لو الجلسة امتدت لأكثر من سورة (ToSurahName)
        // نعتبر آخر سورة وصل لها هي ToSurahName، وإلا SurahName نفسها.
        var effectiveSurah = string.IsNullOrWhiteSpace(lastRecord.ToSurahName)
            ? lastRecord.SurahName
            : lastRecord.ToSurahName;

        // استخراج آخر رقم آية من نص Verses (مثال: "17-30" → 30، أو "20" → 20)
        var lastVerseNumber = ExtractLastVerseNumber(lastRecord.Verses);
        var (suggestedSurah, nextVerse) = GetSuggestedStart(effectiveSurah, lastVerseNumber);

        return Ok(new
        {
            hasPrevious = true,
            lastSurahName = effectiveSurah,   // آخر سورة توقف فيها فعليًا
            lastVerse = lastVerseNumber,       // آخر آية سمّعها فعليًا
            surahName = suggestedSurah,        // السورة المقترحة لبدء الجلسة القادمة
            nextVerse,                         // الآية المقترحة لبدء الجلسة القادمة
            lastSessionDate = lastRecord.Date.ToString("yyyy-MM-dd")
        });
    }
    // بيانات عدد آيات كل سورة (114 سورة) — لحساب الانتقال التلقائي بين السور
    private static readonly (string Name, int VerseCount)[] SurahsData = new (string, int)[]
    {
        ("الفاتحة", 7), ("البقرة", 286), ("آل عمران", 200), ("النساء", 176), ("المائدة", 120),
        ("الأنعام", 165), ("الأعراف", 206), ("الأنفال", 75), ("التوبة", 129), ("يونس", 109),
        ("هود", 123), ("يوسف", 111), ("الرعد", 43), ("إبراهيم", 52), ("الحجر", 99),
        ("النحل", 128), ("الإسراء", 111), ("الكهف", 110), ("مريم", 98), ("طه", 135),
        ("الأنبياء", 112), ("الحج", 78), ("المؤمنون", 118), ("النور", 64), ("الفرقان", 77),
        ("الشعراء", 227), ("النمل", 93), ("القصص", 88), ("العنكبوت", 69), ("الروم", 60),
        ("لقمان", 34), ("السجدة", 30), ("الأحزاب", 73), ("سبأ", 54), ("فاطر", 45),
        ("يس", 83), ("الصافات", 182), ("ص", 88), ("الزمر", 75), ("غافر", 85),
        ("فصلت", 54), ("الشورى", 53), ("الزخرف", 89), ("الدخان", 59), ("الجاثية", 37),
        ("الأحقاف", 35), ("محمد", 38), ("الفتح", 29), ("الحجرات", 18), ("ق", 45),
        ("الذاريات", 60), ("الطور", 49), ("النجم", 62), ("القمر", 55), ("الرحمن", 78),
        ("الواقعة", 96), ("الحديد", 29), ("المجادلة", 22), ("الحشر", 24), ("الممتحنة", 13),
        ("الصف", 14), ("الجمعة", 11), ("المنافقون", 11), ("التغابن", 18), ("الطلاق", 12),
        ("التحريم", 12), ("الملك", 30), ("القلم", 52), ("الحاقة", 52), ("المعارج", 44),
        ("نوح", 28), ("الجن", 28), ("المزمل", 20), ("المدثر", 56), ("القيامة", 40),
        ("الإنسان", 31), ("المرسلات", 50), ("النبأ", 40), ("النازعات", 46), ("عبس", 42),
        ("التكوير", 29), ("الانفطار", 19), ("المطففين", 36), ("الانشقاق", 25), ("البروج", 22),
        ("الطارق", 17), ("الأعلى", 19), ("الغاشية", 26), ("الفجر", 30), ("البلد", 20),
        ("الشمس", 15), ("الليل", 21), ("الضحى", 11), ("الشرح", 8), ("التين", 8),
        ("العلق", 19), ("القدر", 5), ("البينة", 8), ("الزلزلة", 8), ("العاديات", 11),
        ("القارعة", 11), ("التكاثر", 8), ("العصر", 3), ("الهمزة", 9), ("الفيل", 5),
        ("قريش", 4), ("الماعون", 7), ("الكوثر", 3), ("الكافرون", 6), ("النصر", 3),
        ("المسد", 5), ("الإخلاص", 4), ("الفلق", 5), ("الناس", 6)
    };

    /// <summary>
    /// يحسب موضع البداية المقترح للجلسة القادمة: لو آخر آية مسجلة هي آخر آية بالسورة،
    /// ينتقل تلقائيًا لأول آية بالسورة التالية. غير ذلك يرجّع نفس السورة والآية + 1.
    /// </summary>
    private static (string SurahName, int NextVerse) GetSuggestedStart(string currentSurah, int lastVerse)
    {
        var idx = Array.FindIndex(SurahsData, s => s.Name == currentSurah);
        if (idx < 0)
            return (currentSurah, lastVerse + 1); // سورة غير معروفة بالجدول — نُبقي السلوك القديم

        var totalVerses = SurahsData[idx].VerseCount;

        if (lastVerse >= totalVerses)
        {
            if (idx + 1 < SurahsData.Length)
                return (SurahsData[idx + 1].Name, 1); // أول آية بالسورة التالية

            return (currentSurah, lastVerse); // سورة الناس — آخر سورة بالمصحف، لا يوجد بعدها شيء
        }

        return (currentSurah, lastVerse + 1);
    }
    private static int ExtractLastVerseNumber(string verses)
    {
        if (string.IsNullOrWhiteSpace(verses)) return 0;
        var parts = verses.Trim().Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var to))
            return to;
        if (int.TryParse(verses.Trim(), out var single))
            return single;
        return 0;
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
