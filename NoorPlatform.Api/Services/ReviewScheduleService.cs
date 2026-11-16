using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

/// <summary>حالة سورة محفوظة كما تُعرض للطالب (جدول المراجعة والخريطة الحرارية).</summary>
public sealed record SurahStrengthDto(
    int Number,
    string Name,
    int Juz,
    int VerseCount,
    double Retention,
    string Level,
    int Stage,
    int Sessions,
    DateTime LastReviewed,
    int DaysSinceReview,
    DateTime DueDate,
    int OverdueDays,
    int IntervalDays,
    string LastEvaluation,
    double Priority);

/// <summary>
/// يحلّل سجلات التسميع لكل سورة عبر <see cref="MemoryStrength"/> لبناء جدول المراجعة اليومي
/// والخريطة الحرارية. السجل الممتد على عدة سور يُحتسب لكل سورة في نطاقه.
/// </summary>
public class ReviewScheduleService
{
    private readonly NoorDbContext _db;

    public ReviewScheduleService(NoorDbContext db) => _db = db;

    public async Task<List<SurahStrengthDto>> GetStrengthAsync(int studentId, DateTime? today = null)
    {
        var records = await _db.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId)
            .Select(r => new { r.Date, r.SurahName, r.ToSurahName, r.Type, r.Evaluation })
            .ToListAsync();

        var day = (today ?? LibyaTime.Today).Date;
        var perSurah = new Dictionary<int, List<(ReviewSession Session, string Evaluation)>>();
        foreach (var r in records)
        {
            var session = new ReviewSession(r.Date, r.Type == RecordType.Revision, MemoryStrength.Quality(r.Evaluation));
            foreach (var s in QuranIndex.Range(r.SurahName, r.ToSurahName))
            {
                if (!perSurah.TryGetValue(s.Number, out var list)) perSurah[s.Number] = list = new();
                list.Add((session, r.Evaluation));
            }
        }

        var result = new List<SurahStrengthDto>();
        foreach (var (number, list) in perSurah)
        {
            var state = MemoryStrength.Evaluate(list.Select(x => x.Session));
            if (state == null) continue;
            var surah = QuranIndex.Surahs[number - 1];
            var retention = MemoryStrength.Retention(state, day);
            var lastEval = list.OrderBy(x => x.Session.Date).Last().Evaluation;
            result.Add(new SurahStrengthDto(
                surah.Number, surah.Name, surah.Juz, surah.VerseCount,
                retention, MemoryStrength.Level(retention), state.Stage, state.Sessions,
                state.LastDate, Math.Max(0, (int)(day - state.LastDate).TotalDays),
                state.DueDate, MemoryStrength.OverdueDays(state, day), state.IntervalDays,
                lastEval, MemoryStrength.Priority(state, day)));
        }
        return result.OrderBy(r => r.Number).ToList();
    }

    /// <summary>السور التي حان (أو فات) موعد مراجعتها مرتبة بالأولوية، مع ما يقترب موعده.</summary>
    public async Task<object> GetScheduleAsync(int studentId, int max = 10)
    {
        var today = LibyaTime.Today;
        var all = await GetStrengthAsync(studentId, today);
        var due = all.Where(s => s.OverdueDays >= 0)
            .OrderByDescending(s => s.Priority).ThenBy(s => s.Retention)
            .Take(Math.Clamp(max, 1, 50)).ToList();
        var upcoming = all.Where(s => s.OverdueDays is < 0 and >= -3)
            .OrderBy(s => s.DueDate).Take(5).ToList();

        return new
        {
            date = today,
            totalMemorized = all.Count,
            dueCount = all.Count(s => s.OverdueDays >= 0),
            averageRetention = all.Count == 0 ? 0 : Math.Round(all.Average(s => s.Retention), 1),
            due,
            upcoming
        };
    }

    /// <summary>الخريطة الحرارية: كل السور الـ114 (غير المحفوظة بقيمة null) مع ملخص لكل جزء.</summary>
    public async Task<object> GetHeatmapAsync(int studentId)
    {
        var strength = (await GetStrengthAsync(studentId)).ToDictionary(s => s.Number);
        var surahs = QuranIndex.Surahs.Select(q =>
        {
            strength.TryGetValue(q.Number, out var s);
            return new
            {
                q.Number,
                q.Name,
                q.Juz,
                q.VerseCount,
                Memorized = s != null,
                Retention = s?.Retention,
                Level = s?.Level ?? "none",
                DaysSinceReview = s?.DaysSinceReview,
                LastReviewed = s?.LastReviewed,
                DueDate = s?.DueDate,
                s?.LastEvaluation
            };
        }).ToList();

        var juz = Enumerable.Range(1, 30).Select(j =>
        {
            var inJuz = QuranIndex.Surahs.Where(q => QuranIndex.CoversJuz(q, j)).Select(q => q.Number).ToList();
            var mem = inJuz.Where(strength.ContainsKey).Select(n => strength[n]).ToList();
            double? avg = mem.Count == 0 ? null : Math.Round(mem.Average(m => m.Retention), 1);
            return new
            {
                Juz = j,
                SurahCount = inJuz.Count,
                MemorizedCount = mem.Count,
                Retention = avg,
                Level = avg.HasValue ? MemoryStrength.Level(avg.Value) : "none"
            };
        }).ToList();

        return new
        {
            memorizedCount = strength.Count,
            averageRetention = strength.Count == 0 ? 0 : Math.Round(strength.Values.Average(s => s.Retention), 1),
            surahs,
            juz
        };
    }
}
