using System.Text;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

/// <summary>بيانات التقرير الشهري لطالب واحد.</summary>
public sealed record MonthlyReportData(
    int StudentId,
    string StudentName,
    string? CircleName,
    string? TeacherName,
    string? GuardianPhone,
    int Year,
    int Month,
    List<string> MemorizedSurahs,
    List<string> RevisedSurahs,
    int MemorizationSessions,
    int RevisionSessions,
    int NewVerses,
    int AttendanceDays,
    int PresentDays,
    double? AttendanceRate,
    int PrayerDays,
    double? PrayerRate,
    string? TeacherNote,
    int Points);

public sealed record MonthlyReportResult(int StudentId, string StudentName, string Status, string? Phone, string? ShareLink);

/// <summary>
/// يولّد التقرير الشهري لولي الأمر ويرسله عبر واتساب (أو يجهّزه برابط wa.me إن لم يُعدّ واتساب)
/// ويسجّل النتيجة في MonthlyParentReports حتى لا يتكرر الإرسال التلقائي.
/// </summary>
public class MonthlyParentReportService
{
    private static readonly string[] ArabicMonths =
        { "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو", "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر" };

    private readonly NoorDbContext _db;
    private readonly WhatsAppMessenger _whatsApp;
    private readonly InAppNotificationService _notifications;

    public MonthlyParentReportService(NoorDbContext db, WhatsAppMessenger whatsApp, InAppNotificationService notifications)
    {
        _db = db;
        _whatsApp = whatsApp;
        _notifications = notifications;
    }

    public static (int Year, int Month) PreviousMonth(DateTime today)
    {
        var d = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        return (d.Year, d.Month);
    }

    public async Task<MonthlyReportData?> BuildAsync(int studentId, int year, int month)
    {
        var student = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new
            {
                s.Id,
                s.User.FullName,
                s.ParentPhone,
                ParentAccountPhone = s.Parent != null ? s.Parent.Phone : null,
                CircleName = s.Circle != null ? s.Circle.Name : null,
                TeacherName = s.Circle != null && s.Circle.Teacher != null ? s.Circle.Teacher.User.FullName : null,
                s.Points
            })
            .FirstOrDefaultAsync();
        if (student == null) return null;

        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);

        var hifz = await _db.HifzRecords.AsNoTracking()
            .Where(r => r.StudentId == studentId && r.Date >= from && r.Date < to)
            .OrderBy(r => r.Date)
            .Select(r => new { r.SurahName, r.ToSurahName, r.Type, r.Evaluation, r.VerseCount, r.Notes, r.Date })
            .ToListAsync();

        static string Label(string s, string? t) => string.IsNullOrEmpty(t) || t == s ? s : $"{s} – {t}";
        var successful = hifz.Where(h => MemoryStrength.Quality(h.Evaluation) >= 3).ToList();
        var memorized = successful.Where(h => h.Type == RecordType.Memorization)
            .Select(h => Label(h.SurahName, h.ToSurahName)).Distinct().ToList();
        var revised = successful.Where(h => h.Type == RecordType.Revision)
            .Select(h => Label(h.SurahName, h.ToSurahName)).Distinct().ToList();

        var attendance = await _db.Attendances.AsNoTracking()
            .Where(a => a.StudentId == studentId && a.Date >= from && a.Date < to)
            .Select(a => new { a.Date, a.Status })
            .ToListAsync();
        // يوم واحد لكل تاريخ (قد يوجد سجل للحلقة الإضافية في نفس اليوم): يُعدّ حاضراً إن حضر أيّاً منها
        var days = attendance.GroupBy(a => a.Date.Date)
            .Select(g => g.Any(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late)).ToList();
        int present = days.Count(x => x);

        var prayers = await _db.PrayerDailyLogs.AsNoTracking()
            .Where(p => p.StudentId == studentId && p.Date >= from && p.Date < to)
            .Select(p => new { p.PrayedCount, p.OnTime, p.PrayedInMosque })
            .ToListAsync();
        var prayed = prayers.Sum(p => p.PrayedCount ?? (p.OnTime || p.PrayedInMosque ? 5 : 0));
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var elapsedDays = from <= LibyaTime.Today && LibyaTime.Today < to ? LibyaTime.Today.Day : daysInMonth;
        double? prayerRate = prayers.Count == 0 ? null : Math.Round(100.0 * prayed / (elapsedDays * 5), 0);

        var periodNote = await _db.StudentPeriodEvaluations.AsNoTracking()
            .Where(e => e.StudentId == studentId && e.SheikhNotes != null && e.SheikhNotes != "" && e.Period.StartDate < to)
            .OrderByDescending(e => e.Period.EndDate)
            .Select(e => e.SheikhNotes)
            .FirstOrDefaultAsync();
        var lastSessionNote = hifz.LastOrDefault(h => !string.IsNullOrWhiteSpace(h.Notes))?.Notes;

        var phone = !string.IsNullOrWhiteSpace(student.ParentPhone) ? student.ParentPhone
            : !string.IsNullOrWhiteSpace(student.ParentAccountPhone) ? student.ParentAccountPhone : null;

        return new MonthlyReportData(
            student.Id, student.FullName, student.CircleName, student.TeacherName, phone, year, month,
            memorized, revised,
            hifz.Count(h => h.Type == RecordType.Memorization), hifz.Count(h => h.Type == RecordType.Revision),
            successful.Where(h => h.Type == RecordType.Memorization).Sum(h => h.VerseCount),
            days.Count, present, days.Count == 0 ? null : Math.Round(100.0 * present / days.Count, 0),
            prayers.Count, prayerRate is > 100 ? 100 : prayerRate,
            (lastSessionNote ?? periodNote)?.Trim(), student.Points);
    }

    /// <summary>نص التقرير المنسّق لواتساب (تنسيق *عريض* مدعوم في واتساب).</summary>
    public static string Format(MonthlyReportData d, string? centerName = null)
    {
        static string List(List<string> items) => items.Count == 0 ? "—" : string.Join("، ", items.Take(15)) + (items.Count > 15 ? " …" : "");
        var sb = new StringBuilder();
        sb.AppendLine($"📚 *{(string.IsNullOrWhiteSpace(centerName) ? "منصة نور لتحفيظ القرآن الكريم" : centerName)}*");
        sb.AppendLine($"التقرير الشهري — {ArabicMonths[d.Month - 1]} {d.Year}");
        sb.AppendLine();
        sb.AppendLine("السلام عليكم ورحمة الله وبركاته،");
        sb.AppendLine($"نضع بين أيديكم ملخص أداء *{d.StudentName}*{(d.CircleName != null ? $" في حلقة {d.CircleName}" : "")}:");
        sb.AppendLine();
        sb.AppendLine($"📖 *الحفظ الجديد* ({d.MemorizationSessions} جلسة{(d.NewVerses > 0 ? $"، {d.NewVerses} آية" : "")}):");
        sb.AppendLine(List(d.MemorizedSurahs));
        sb.AppendLine($"🔁 *المراجعة* ({d.RevisionSessions} جلسة):");
        sb.AppendLine(List(d.RevisedSurahs));
        sb.AppendLine();
        sb.AppendLine(d.AttendanceRate.HasValue
            ? $"✅ *الحضور:* {d.AttendanceRate}٪ ({d.PresentDays} من {d.AttendanceDays} يوماً)"
            : "✅ *الحضور:* لم يُرصد حضور هذا الشهر");
        sb.AppendLine(d.PrayerRate.HasValue
            ? $"🕌 *الصلوات:* {d.PrayerRate}٪ (سُجّلت في {d.PrayerDays} يوماً)"
            : "🕌 *الصلوات:* لم تُسجَّل صلوات هذا الشهر");
        sb.AppendLine($"⭐ *رصيد النقاط:* {d.Points}");
        if (!string.IsNullOrWhiteSpace(d.TeacherNote))
        {
            sb.AppendLine();
            sb.AppendLine($"📝 *ملاحظة المحفّظ{(d.TeacherName != null ? $" ({d.TeacherName})" : "")}:*");
            sb.AppendLine(d.TeacherNote.Length > 400 ? d.TeacherNote[..400] + "…" : d.TeacherNote);
        }
        sb.AppendLine();
        sb.Append("جزاكم الله خيراً على متابعتكم 🌙");
        return sb.ToString();
    }

    /// <summary>يرسل التقرير لطالب واحد ويسجّل النتيجة.</summary>
    public async Task<MonthlyReportResult?> SendAsync(int studentId, int year, int month, string trigger,
        int? sentByUserId, CancellationToken ct = default)
    {
        var data = await BuildAsync(studentId, year, month);
        if (data == null) return null;

        var centerName = await _db.PlatformSettings.AsNoTracking().Select(s => s.CenterName).FirstOrDefaultAsync(ct);
        var content = Format(data, centerName);
        var result = string.IsNullOrWhiteSpace(data.GuardianPhone)
            ? WhatsAppSendResult.InvalidPhone
            : await _whatsApp.SendTextAsync(data.GuardianPhone, content, ct);

        var status = result switch
        {
            WhatsAppSendResult.Sent => "Sent",
            WhatsAppSendResult.NotConfigured => "Prepared",
            WhatsAppSendResult.InvalidPhone => "NoPhone",
            _ => "Failed"
        };

        _db.MonthlyParentReports.Add(new MonthlyParentReport
        {
            StudentId = studentId,
            Year = year,
            Month = month,
            Phone = data.GuardianPhone ?? string.Empty,
            Content = content.Length > 4000 ? content[..4000] : content,
            Status = status,
            Trigger = trigger,
            SentByUserId = sentByUserId
        });
        await _db.SaveChangesAsync(ct);

        // نسخة داخل التطبيق لولي الأمر (حتى لو لم يُعدّ واتساب)
        await _notifications.NotifyStudentAndGuardianAsync(studentId, NotificationKinds.MonthlyReport,
            $"التقرير الشهري — {ArabicMonths[month - 1]} {year}", content, "parentHome", includeStudent: false);

        var link = status is "Prepared" or "Failed" ? WhatsAppMessenger.ShareLink(data.GuardianPhone, content) : null;
        return new MonthlyReportResult(studentId, data.StudentName, status, data.GuardianPhone, link);
    }

    /// <summary>الإرسال التلقائي لكل الطلاب النشطين الذين لم يُرسل لهم تقرير هذا الشهر بعد.</summary>
    public async Task<int> SendPendingAutoAsync(int year, int month, CancellationToken ct = default)
    {
        var done = await _db.MonthlyParentReports.AsNoTracking()
            .Where(r => r.Year == year && r.Month == month && (r.Trigger == "Auto" || r.Status == "Sent"))
            .Select(r => r.StudentId).Distinct().ToListAsync(ct);
        var pending = await _db.Students.AsNoTracking()
            .Where(s => s.User.IsActive && !done.Contains(s.Id))
            .Select(s => s.Id).ToListAsync(ct);

        var count = 0;
        foreach (var id in pending)
        {
            ct.ThrowIfCancellationRequested();
            if (await SendAsync(id, year, month, "Auto", null, ct) != null) count++;
        }
        return count;
    }

    public static string MonthName(int month) => ArabicMonths[month - 1];
}

/// <summary>
/// خدمة خلفية تتحقق كل ساعة: في أول يوم من الشهر (بتوقيت ليبيا) بعد الساعة التاسعة صباحاً
/// ترسل تقرير الشهر السابق لكل ولي أمر لم يصله بعد. تُعطَّل بالإعداد MonthlyReports:AutoSend=false.
/// </summary>
public class MonthlyParentReportBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MonthlyParentReportBackgroundService> _logger;

    public MonthlyParentReportBackgroundService(IServiceScopeFactory scopes, ILogger<MonthlyParentReportBackgroundService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            var now = LibyaTime.Now;
            if (now.Day == 1 && now.Hour >= 9)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<MonthlyParentReportService>();
                    var (year, month) = MonthlyParentReportService.PreviousMonth(now.Date);
                    var sent = await service.SendPendingAutoAsync(year, month, stoppingToken);
                    if (sent > 0)
                        _logger.LogInformation("التقارير الشهرية: عولج {Count} تقرير لشهر {Month}/{Year}", sent, month, year);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "تعذر تنفيذ الإرسال التلقائي للتقارير الشهرية");
                }
            }
        }
        while (await SafeTick(timer, stoppingToken));
    }

    private static async Task<bool> SafeTick(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
