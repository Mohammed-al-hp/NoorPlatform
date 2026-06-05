using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public ReportsController(NoorDbContext context)
    {
        _context = context;
    }

    [HttpGet("certificate/{studentId}")]
    [Authorize(Roles = "Admin,Teacher,Parent,Student")]
    public async Task<IActionResult> GetCertificate(int studentId, [FromQuery] string? surah)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t!.User)
            .Include(s => s.HifzRecords)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var totalVerses = student.HifzRecords
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses));

        var progress = Math.Min((int)Math.Round((double)totalVerses / 6236 * 100), 100);
        var teacherName = student.Circle?.Teacher?.User?.FullName ?? "—";
        var circleName = student.Circle?.Name ?? "—";
        var achievement = surah ?? GetAchievementText(progress);

        var pdfBytes = GenerateCertificatePdf(
            student.User.FullName,
            teacherName,
            circleName,
            achievement,
            progress,
            DateTime.UtcNow.ToString("yyyy/MM/dd")
        );

        return File(pdfBytes, "application/pdf", $"شهادة_{student.User.FullName}.pdf");
    }

    [HttpPost("certificate/{studentId}/grant-badge")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GrantBadge(int studentId, [FromBody] GrantBadgeRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var student = await _context.Students
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var badgeToGrant = "شهادة " + (request.Achievement ?? "تقدير");
        if (string.IsNullOrEmpty(student.Badges))
            student.Badges = badgeToGrant;
        else if (!student.Badges.Contains(badgeToGrant))
            student.Badges += $",{badgeToGrant}";

        var userId = AuthorizationHelpers.GetUserId(User);
        if (userId != null)
        {
            _context.ActivityFeeds.Add(new ActivityFeed
            {
                UserId = userId.Value,
                UserName = User.Identity?.Name ?? "User",
                ActivityType = "Certificate",
                Description = $"تم إصدار شهادة تقدير للطالب {student.User.FullName} ({request.Achievement})",
                Icon = "📜",
                Color = "text-teal-500"
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم منح الوسام بنجاح" });
    }

    [HttpGet("monthly/{studentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetMonthlyReport(int studentId, [FromQuery] int? month, [FromQuery] int? year)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var targetMonth = month ?? DateTime.UtcNow.Month;
        var targetYear = year ?? DateTime.UtcNow.Year;

        var student = await _context.Students.AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t!.User)
            .Include(s => s.Attendances.Where(a => a.Date.Month == targetMonth && a.Date.Year == targetYear))
            .Include(s => s.HifzRecords.Where(r => r.Date.Month == targetMonth && r.Date.Year == targetYear))
            .Include(s => s.ExamResults).ThenInclude(e => e.Exam)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var monthAttendances = student.Attendances.ToList();
        var monthHifz = student.HifzRecords.ToList();

        var attendanceRate = monthAttendances.Any()
            ? (int)Math.Round((double)monthAttendances.Count(a => a.Status == AttendanceStatus.Present) / monthAttendances.Count * 100)
            : 0;

        var totalVerses = monthHifz
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses));

        var pdfBytes = GenerateMonthlyReportPdf(
            student.User.FullName,
            student.Circle?.Teacher?.User?.FullName ?? "—",
            student.Circle?.Name ?? "—",
            GetArabicMonth(targetMonth),
            targetYear.ToString(),
            attendanceRate,
            monthAttendances.Count(a => a.Status == AttendanceStatus.Present),
            monthAttendances.Count(a => a.Status == AttendanceStatus.ExcusedAbsence || a.Status == AttendanceStatus.UnexcusedAbsence),
            totalVerses,
            monthHifz.Count,
            monthHifz
        );

        return File(pdfBytes, "application/pdf", $"تقرير_{student.User.FullName}_{targetMonth}_{targetYear}.pdf");
    }

    [HttpGet("center-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCenterSummary()
    {
        var today = DateTime.UtcNow;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var stats = new
        {
            TotalStudents = await _context.Students.CountAsync(),
            TotalTeachers = await _context.Teachers.CountAsync(),
            TotalCircles = await _context.Circles.CountAsync(),
            MonthSessions = await _context.HifzRecords.CountAsync(r => r.Date >= startOfMonth),
            MonthAttendance = await _context.Attendances
                .CountAsync(a => a.Date >= startOfMonth && a.Status == AttendanceStatus.Present),
            GeneratedAt = today.ToString("yyyy/MM/dd HH:mm")
        };

        return Ok(stats);
    }

    // ─────────────────────────────────────────────────
    // PDF Generation via QuestPDF
    // ─────────────────────────────────────────────────

    private static byte[] GenerateCertificatePdf(
        string studentName, string teacherName, string circleName,
        string achievement, int progress, string date)
    {
        ArabicPdfFonts.EnsureRegistered();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(ArabicPdfFonts.DefaultStyle(14));

                page.Content().Border(5).BorderColor("#b8860b").Padding(30).Column(col =>
                {
                    col.Spacing(20);
                    col.Item().AlignCenter().Text("📚").FontSize(40);
                    col.Item().AlignCenter().Text("مركز نور لتحفيظ القرآن الكريم").FontSize(22).FontColor("#1a5c3a").Bold();
                    col.Item().AlignCenter().Text("شهـادة تقـدير").FontSize(36).FontColor("#b8860b").Bold();
                    
                    col.Item().PaddingTop(20).AlignCenter().Text("يُشهد المركز بأن الطالب/ة").FontSize(18).FontColor("#444");
                    col.Item().AlignCenter().Text(studentName).FontSize(32).FontColor("#1a3a2a").Bold();
                    
                    col.Item().PaddingTop(10).AlignCenter().Text("قد أتم/أتمت بعون الله تعالى").FontSize(18);
                    col.Item().AlignCenter().Text(achievement).FontSize(24).Bold();
                    
                    col.Item().PaddingTop(10).AlignCenter().Text($"نسبة الحفظ الإجمالية: {progress}%").FontSize(22).FontColor("#1a5c3a").Bold();
                    col.Item().AlignCenter().Text($"في حلقة {circleName}").FontSize(16).FontColor("#555");

                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(Colors.Black);
                            c.Item().PaddingTop(5).Text($"المحفظ: {teacherName}");
                        });
                        row.RelativeItem().AlignCenter().PaddingTop(10).Text($"التاريخ: {date}").FontColor("#888").FontSize(12);
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().LineHorizontal(1).LineColor(Colors.Black);
                            c.Item().PaddingTop(5).Text("مدير المركز");
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    private static byte[] GenerateMonthlyReportPdf(
        string studentName, string teacherName, string circleName,
        string month, string year, int attendanceRate,
        int presentDays, int absentDays, int totalVerses,
        int sessionsCount, List<HifzRecord> hifzRecords)
    {
        ArabicPdfFonts.EnsureRegistered();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(ArabicPdfFonts.DefaultStyle(12));

                page.Header().Background("#1a5c3a").Padding(15).Column(col =>
                {
                    col.Item().AlignCenter().Text("📋 التقرير الشهري").FontSize(24).FontColor(Colors.White).Bold();
                    col.Item().AlignCenter().Text($"مركز نور لتحفيظ القرآن الكريم — {month} {year}").FontSize(14).FontColor("#e8f5e9");
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(15);
                    
                    // Info
                    col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(c => { c.Item().Text("اسم الطالب").FontSize(10).FontColor(Colors.Grey.Medium); c.Item().Text(studentName).Bold(); });
                        row.RelativeItem().Column(c => { c.Item().Text("الحلقة").FontSize(10).FontColor(Colors.Grey.Medium); c.Item().Text(circleName).Bold(); });
                        row.RelativeItem().Column(c => { c.Item().Text("المحفظ").FontSize(10).FontColor(Colors.Grey.Medium); c.Item().Text(teacherName).Bold(); });
                        row.RelativeItem().Column(c => { c.Item().Text("الشهر").FontSize(10).FontColor(Colors.Grey.Medium); c.Item().Text($"{month} {year}").Bold(); });
                    });

                    // Stats
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Background("#f0fdf4").Border(1).BorderColor("#bbf7d0").Padding(10).AlignCenter().Column(c => { c.Item().Text($"{attendanceRate}%").FontSize(20).FontColor("#16a34a").Bold(); c.Item().Text("نسبة الحضور").FontSize(10).FontColor("#166534"); });
                        row.RelativeItem().Background("#f0fdf4").Border(1).BorderColor("#bbf7d0").Padding(10).AlignCenter().Column(c => { c.Item().Text($"{sessionsCount}").FontSize(20).FontColor("#16a34a").Bold(); c.Item().Text("جلسات التسميع").FontSize(10).FontColor("#166534"); });
                        row.RelativeItem().Background("#f0fdf4").Border(1).BorderColor("#bbf7d0").Padding(10).AlignCenter().Column(c => { c.Item().Text($"{totalVerses}").FontSize(20).FontColor("#16a34a").Bold(); c.Item().Text("آيات محفوظة").FontSize(10).FontColor("#166534"); });
                    });

                    col.Item().Text($"الحضور: {presentDays} يوم | الغياب: {absentDays} يوم").FontSize(11).FontColor(Colors.Grey.Darken2);

                    // Table
                    col.Item().PaddingTop(10).Text("سجل التسميع الشهري").FontSize(16).Bold();
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(3);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("#").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("السورة").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("الآيات").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("التقييم").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("ملاحظات").Bold();
                        });

                        int i = 1;
                        foreach (var r in hifzRecords)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(i.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(r.SurahName);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(r.Verses);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(r.Evaluation);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(r.Notes ?? "—");
                            i++;
                        }
                    });
                });

                page.Footer().AlignCenter().Text($"تم إنشاء هذا التقرير تلقائياً بواسطة منصة نور — {DateTime.UtcNow:yyyy/MM/dd}").FontSize(10).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    public class GrantBadgeRequest
    {
        public string? Achievement { get; set; }
    }

    private static string GetAchievementText(int progress) => progress switch
    {
        >= 100 => "حفظ القرآن الكريم كاملاً",
        >= 75 => "حفظ ثلاثة أرباع القرآن الكريم",
        >= 50 => "حفظ نصف القرآن الكريم",
        >= 25 => "حفظ ربع القرآن الكريم",
        >= 10 => "إتقان الأجزاء الأولى من القرآن الكريم",
        _ => "التقدم في حفظ القرآن الكريم"
    };

    private static string GetArabicMonth(int month) => month switch
    {
        1 => "يناير",
        2 => "فبراير",
        3 => "مارس",
        4 => "أبريل",
        5 => "مايو",
        6 => "يونيو",
        7 => "يوليو",
        8 => "أغسطس",
        9 => "سبتمبر",
        10 => "أكتوبر",
        11 => "نوفمبر",
        _ => "ديسمبر"
    };
}
