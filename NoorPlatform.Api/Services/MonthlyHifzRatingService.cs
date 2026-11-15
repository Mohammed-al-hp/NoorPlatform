using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

/// <summary>
/// التقييم الشهري للطالب بحسب جلسات الحفظ الجديد الناجحة — يجمع البيانات من قاعدة البيانات
/// ويطبّق منطق <see cref="MonthlyHifzRating"/> بإعدادات PlatformSettings القابلة للتعديل.
/// </summary>
public class MonthlyHifzRatingService
{
    private readonly NoorDbContext _context;

    public MonthlyHifzRatingService(NoorDbContext context)
    {
        _context = context;
    }

    public async Task<MonthlyHifzRatingConfig> GetConfigAsync() =>
        MonthlyHifzRatingConfig.From(await _context.PlatformSettings.AsNoTracking().FirstOrDefaultAsync());

    /// <summary>يعيد السنة/الشهر المطلوبين أو الشهر الحالي (بتوقيت ليبيا) عند عدم التحديد أو عدم الصلاحية.</summary>
    public static (int Year, int Month) ResolveMonth(int? year, int? month)
    {
        var today = LibyaTime.Today;
        if (year is >= 2000 and <= 2100 && month is >= 1 and <= 12)
            return (year.Value, month.Value);
        return (today.Year, today.Month);
    }

    public async Task<MonthlyHifzRatingResult?> GetForStudentAsync(int studentId, int year, int month)
    {
        var map = await GetForStudentsAsync(new[] { studentId }, year, month);
        return map.TryGetValue(studentId, out var r) ? r : null;
    }

    public async Task<Dictionary<int, MonthlyHifzRatingResult>> GetForStudentsAsync(IEnumerable<int> studentIds, int year, int month)
    {
        var ids = studentIds.Distinct().ToList();
        if (ids.Count == 0) return new();

        var cfg = await GetConfigAsync();
        var (start, end) = MonthlyHifzRating.MonthRange(year, month);

        var births = await _context.Students
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.DateOfBirth })
            .ToListAsync();

        var records = await _context.HifzRecords
            .AsNoTracking()
            .Where(r => ids.Contains(r.StudentId) && r.Type == RecordType.Memorization && r.Date >= start && r.Date < end)
            .Select(r => new HifzRecord { StudentId = r.StudentId, Date = r.Date, Type = r.Type, Evaluation = r.Evaluation })
            .ToListAsync();

        var byStudent = records.ToLookup(r => r.StudentId);
        return births.ToDictionary(
            b => b.Id,
            b => MonthlyHifzRating.Calculate(byStudent[b.Id], b.DateOfBirth, year, month, cfg));
    }

    public static object ToDto(MonthlyHifzRatingResult r) => new
    {
        year = r.Year,
        month = r.Month,
        ageYears = r.AgeYears,
        ageBand = r.AgeBand switch
        {
            HifzAgeBand.Age11Plus => "11 فأكثر",
            HifzAgeBand.Age8To10 => "8–10",
            _ => "5–7"
        },
        target = r.Target,
        successfulSessions = r.SuccessfulSessions,
        totalMemorizationSessions = r.TotalMemorizationSessions,
        score = r.Score,
        warning = r.Warning
    };
}
