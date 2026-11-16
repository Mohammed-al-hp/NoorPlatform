using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Infrastructure.Data;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using Microsoft.AspNetCore.Authorization;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HifzController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly MonthlyHifzRatingService _monthlyRating;

    public HifzController(NoorDbContext context, MonthlyHifzRatingService monthlyRating)
    {
        _context = context;
        _monthlyRating = monthlyRating;
    }

    // GET /api/hifz/monthly-rating/my?year=&month= — تقييم الطالب الشهري بحسب جلسات الحفظ الجديد الناجحة
    [HttpGet("monthly-rating/my")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetMyMonthlyRating([FromQuery] int? year, [FromQuery] int? month)
    {
        var userId = AuthorizationHelpers.GetUserId(User);
        var studentId = await _context.Students.Where(s => s.UserId == userId).Select(s => (int?)s.Id).FirstOrDefaultAsync();
        if (studentId == null)
            return NotFound(new { message = "لم يُعثر على بيانات الطالب" });
        return await MonthlyRatingFor(studentId.Value, year, month);
    }

    // GET /api/hifz/monthly-rating/{studentId}?year=&month=
    [HttpGet("monthly-rating/{studentId:int}")]
    public async Task<IActionResult> GetMonthlyRating(int studentId, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();
        return await MonthlyRatingFor(studentId, year, month);
    }

    private async Task<IActionResult> MonthlyRatingFor(int studentId, int? year, int? month)
    {
        var (y, m) = MonthlyHifzRatingService.ResolveMonth(year, month);
        var r = await _monthlyRating.GetForStudentAsync(studentId, y, m);
        return r == null
            ? NotFound(new { message = "الطالب غير موجود" })
            : Ok(MonthlyHifzRatingService.ToDto(r));
    }

    // GET /api/hifz/monthly-ratings?year=&month=&circleId= — تقرير المحفظ/الأدمن (المحفظ يرى طلابه فقط)
    [HttpGet("monthly-ratings")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetMonthlyRatings([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? circleId)
    {
        var (y, m) = MonthlyHifzRatingService.ResolveMonth(year, month);
        var query = StudentQueryHelper.ScopeForUser(_context, User);
        if (circleId.HasValue)
            query = query.Where(s => s.CircleId == circleId || s.ExtraEnrollments.Any(e => e.CircleId == circleId));

        var students = await query
            .Select(s => new { s.Id, fullName = s.User.FullName, circleName = s.Circle != null ? s.Circle.Name : "بدون حلقة" })
            .OrderBy(s => s.fullName)
            .ToListAsync();

        var ratings = await _monthlyRating.GetForStudentsAsync(students.Select(s => s.Id), y, m);
        var cfg = await _monthlyRating.GetConfigAsync();

        return Ok(new
        {
            year = y,
            month = m,
            passingEvaluations = cfg.PassingEvaluations,
            targets = new { age5To7 = cfg.TargetAge5To7, age8To10 = cfg.TargetAge8To10, age11Plus = cfg.TargetAge11Plus },
            items = students.Select(s => new
            {
                studentId = s.Id,
                s.fullName,
                s.circleName,
                rating = ratings.TryGetValue(s.Id, out var r) ? MonthlyHifzRatingService.ToDto(r) : null
            })
        });
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
    public async Task<IActionResult> GetLastPosition(int studentId, [FromQuery] string mode = "Memorization")
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var studentExists = await _context.Students.AnyAsync(s => s.Id == studentId);
        if (!studentExists)
            return NotFound(new { message = "الطالب غير موجود" });

        var isSequentialRevision = mode.Equals("Sequential", StringComparison.OrdinalIgnoreCase);

        var lastRecord = await _context.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId &&
                        (isSequentialRevision
                            ? (r.Type == RecordType.Revision && r.RevisionMode == "Sequential")
                            : r.Type == RecordType.Memorization))
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
        // الحفظ الجديد يتحرك نحو أول المصحف (الفاتحة)، والمراجعة التسلسلية نحو آخره (الناس)
        var (suggestedSurah, nextVerse) = GetSuggestedStart(effectiveSurah, lastVerseNumber, moveForward: isSequentialRevision);

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
    private static (string SurahName, int NextVerse) GetSuggestedStart(string currentSurah, int lastVerse, bool moveForward = true)
    {
        var idx = Array.FindIndex(SurahsData, s => s.Name == currentSurah);
        if (idx < 0)
            return (currentSurah, lastVerse + 1); // سورة غير معروفة بالجدول — نُبقي السلوك القديم

        var totalVerses = SurahsData[idx].VerseCount;

        if (lastVerse >= totalVerses)
        {
            var nextIdx = moveForward ? idx + 1 : idx - 1;

            if (nextIdx >= 0 && nextIdx < SurahsData.Length)
                return (SurahsData[nextIdx].Name, 1); // أول آية بالسورة التالية بنفس اتجاه الوضع

            // لا يوجد بعدها شيء في هذا الاتجاه (الناس للأمام، أو الفاتحة للخلف)
            return (currentSurah, lastVerse);
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

        var validationError = ValidateRecord(request, out var recordType);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        var record = new HifzRecord { StudentId = request.StudentId };
        ApplyRequest(record, request, recordType);
        record.Date = request.Date ?? DateTime.UtcNow;

        _context.HifzRecords.Add(record);

        // Gamification & ActivityFeed — الجلسة الناجحة فقط تمنح نقاطاً، والقيمة تُحفظ في السجل
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == request.StudentId);
        if (student != null)
        {
            var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
            record.PointsAwarded = PointsLedger.Apply(student, 0, PointsLedger.ForHifz(recordType, record.Evaluation, settings));

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

        // سحب النقاط التي منحتها هذه الجلسة
        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == record.StudentId);
        if (student != null)
            PointsLedger.Apply(student, record.PointsAwarded, 0);

        _context.HifzRecords.Remove(record);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حذف السجل" });
    }

    // PUT /api/hifz/{id} — تعديل جلسة تسميع (تصحيح خطأ إدخال) مع إعادة حساب النقاط
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> Update(int id, [FromBody] AddHifzRecordRequest request)
    {
        var record = await _context.HifzRecords.FirstOrDefaultAsync(r => r.Id == id);
        if (record == null)
            return NotFound(new { message = "السجل غير موجود" });

        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, record.StudentId))
            return Forbid();

        request.StudentId = record.StudentId; // لا يُسمح بنقل الجلسة لطالب آخر
        // الحقول غير المُرسلة تبقى كما هي (نموذج التعديل المختصر لا يمحو تفاصيل الجلسة)
        request.StartVerseText ??= record.StartVerseText;
        request.EndVerseText ??= record.EndVerseText;
        request.RevisionMode ??= record.RevisionMode;
        request.SessionDetailsJson ??= record.SessionDetailsJson;
        request.Notes ??= record.Notes;
        var validationError = ValidateRecord(request, out var recordType);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        ApplyRequest(record, request, recordType);
        if (request.Date.HasValue) record.Date = request.Date.Value;

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == record.StudentId);
        if (student != null)
        {
            var settings = await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync();
            record.PointsAwarded = PointsLedger.Apply(student, record.PointsAwarded,
                PointsLedger.ForHifz(recordType, record.Evaluation, settings));
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تعديل جلسة التسميع", record.Id, record.VerseCount, record.PointsAwarded });
    }

    private const int MaxSessionDetailsLength = 20_000;

    /// <summary>التحقق من مدخلات جلسة التسميع (مشترك بين الإضافة والتعديل).</summary>
    private static string? ValidateRecord(AddHifzRecordRequest request, out RecordType recordType)
    {
        if (!Enum.TryParse(request.Type, true, out recordType) || !Enum.IsDefined(recordType))
            return $"نوع السجل غير صالح: {request.Type}. القيم المقبولة: Memorization, Revision";
        if (string.IsNullOrWhiteSpace(request.SurahName))
            return "اسم السورة مطلوب";
        if (request.SurahName.Trim().Length > 50 || (request.ToSurahName?.Trim().Length ?? 0) > 50)
            return "اسم السورة طويل جداً";
        if (string.IsNullOrWhiteSpace(request.Evaluation))
            return "التقييم مطلوب";
        if (request.Evaluation.Trim().Length > 30)
            return "التقييم طويل جداً";
        if ((request.Verses?.Trim().Length ?? 0) > 50)
            return "نطاق الآيات طويل جداً";
        if ((request.Notes?.Length ?? 0) > 1000)
            return "الملاحظات طويلة جداً (الحد 1000 حرف)";
        if ((request.StartVerseText?.Length ?? 0) > 500 || (request.EndVerseText?.Length ?? 0) > 500)
            return "نص الآية طويل جداً";
        if (request.Date.HasValue && request.Date.Value > DateTime.UtcNow.AddDays(1))
            return "لا يمكن تسجيل جلسة بتاريخ مستقبلي";
        if (!string.IsNullOrEmpty(request.SessionDetailsJson))
        {
            if (request.SessionDetailsJson.Length > MaxSessionDetailsLength)
                return "تفاصيل الجلسة كبيرة جداً";
            try { using var _ = System.Text.Json.JsonDocument.Parse(request.SessionDetailsJson); }
            catch (System.Text.Json.JsonException) { return "تفاصيل الجلسة ليست بصيغة صحيحة"; }
        }
        return null;
    }

    private static void ApplyRequest(HifzRecord record, AddHifzRecordRequest request, RecordType recordType)
    {
        var verses = request.Verses?.Trim() ?? string.Empty;
        record.SurahName = request.SurahName.Trim();
        record.ToSurahName = string.IsNullOrWhiteSpace(request.ToSurahName) ? null : request.ToSurahName.Trim();
        record.Verses = verses;
        record.VerseCount = HifzRecord.ParseVerseCount(verses); // يُحسب تلقائياً من نص الآيات
        record.StartVerseText = request.StartVerseText?.Trim() ?? string.Empty;
        record.EndVerseText = request.EndVerseText?.Trim() ?? string.Empty;
        record.RevisionMode = request.RevisionMode?.Trim();
        record.SessionDetailsJson = string.IsNullOrWhiteSpace(request.SessionDetailsJson) ? null : request.SessionDetailsJson;
        record.Type = recordType;
        record.Evaluation = request.Evaluation.Trim();
        record.Notes = request.Notes?.Trim() ?? string.Empty;
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
    public string? Notes { get; set; }
    public DateTime? Date { get; set; }
}
