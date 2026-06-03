using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Text;

namespace NoorPlatform.Api.Controllers;

/// <summary>
/// توليد شهادات وتقارير PDF
/// يستخدم مكتبة QuestPDF (مجانية للمشاريع التعليمية)
/// تثبيت: Install-Package QuestPDF -ProjectName NoorPlatform.Api
/// </summary>
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

    // ─────────────────────────────────────────────────
    // GET /api/reports/certificate/{studentId}
    // شهادة إتمام حفظ جزء أو سورة — تُنزَّل كـ PDF
    // ─────────────────────────────────────────────────
    [HttpGet("certificate/{studentId}")]
    [Authorize(Roles = "Admin,Teacher,Parent,Student")]
    public async Task<IActionResult> GetCertificate(int studentId, [FromQuery] string? surah, [FromQuery] bool grantBadge = false)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t.User)
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

        var html = GenerateCertificateHtml(
            studentName: System.Net.WebUtility.HtmlEncode(student.User.FullName),
            teacherName: System.Net.WebUtility.HtmlEncode(teacherName),
            circleName: System.Net.WebUtility.HtmlEncode(circleName),
            achievement: System.Net.WebUtility.HtmlEncode(achievement),
            progress: progress,
            date: DateTime.Now.ToString("yyyy/MM/dd")
        );

        if (grantBadge && (User.IsInRole("Admin") || User.IsInRole("Teacher")))
        {
            var badgeToGrant = "شهادة " + achievement;
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
                    Description = $"تم إصدار شهادة تقدير للطالب {student.User.FullName} ({achievement})",
                    Icon = "📜",
                    Color = "text-teal-500"
                });
            }

            await _context.SaveChangesAsync();
        }

        // إرجاع HTML للطباعة (بديل عن PDF حتى تُثبَّت QuestPDF)
        return Content(html, "text/html", Encoding.UTF8);
    }

    // ─────────────────────────────────────────────────
    // GET /api/reports/monthly/{studentId}
    // التقرير الشهري لطالب — يُرسَل لولي الأمر
    // ─────────────────────────────────────────────────
    [HttpGet("monthly/{studentId}")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetMonthlyReport(int studentId, [FromQuery] int? month, [FromQuery] int? year)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId))
            return Forbid();

        var targetMonth = month ?? DateTime.Now.Month;
        var targetYear = year ?? DateTime.Now.Year;

        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t.User)
            .Include(s => s.Attendances)
            .Include(s => s.HifzRecords)
            .Include(s => s.ExamResults).ThenInclude(e => e.Exam)
            .FirstOrDefaultAsync(s => s.Id == studentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        // بيانات الشهر المطلوب
        var monthAttendances = student.Attendances
            .Where(a => a.Date.Month == targetMonth && a.Date.Year == targetYear).ToList();
        var monthHifz = student.HifzRecords
            .Where(r => r.Date.Month == targetMonth && r.Date.Year == targetYear).ToList();

        var attendanceRate = monthAttendances.Any()
            ? (int)Math.Round((double)monthAttendances.Count(a => a.Status == AttendanceStatus.Present)
              / monthAttendances.Count * 100)
            : 0;

        var totalVerses = monthHifz
            .Where(r => r.Type == RecordType.Memorization)
            .Sum(r => r.VerseCount > 0 ? r.VerseCount : HifzRecord.ParseVerseCount(r.Verses));

        var html = GenerateMonthlyReportHtml(
            studentName: student.User.FullName,
            teacherName: student.Circle?.Teacher?.User?.FullName ?? "—",
            circleName: student.Circle?.Name ?? "—",
            month: GetArabicMonth(targetMonth),
            year: targetYear.ToString(),
            attendanceRate: attendanceRate,
            presentDays: monthAttendances.Count(a => a.Status == AttendanceStatus.Present),
            absentDays: monthAttendances.Count(a => a.Status == AttendanceStatus.Absent),
            totalVerses: totalVerses,
            sessionsCount: monthHifz.Count,
            hifzRecords: monthHifz.Select(r => new { r.SurahName, r.Verses, r.Evaluation, r.Notes, r.Date }).ToList<object>()
        );

        return Content(html, "text/html", Encoding.UTF8);
    }

    // ─────────────────────────────────────────────────
    // GET /api/reports/center-summary
    // ملخص المركز الشهري (للإدارة)
    // ─────────────────────────────────────────────────
    [HttpGet("center-summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetCenterSummary()
    {
        var today = DateTime.Now;
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

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
    // HTML Templates
    // ─────────────────────────────────────────────────
    private static string GenerateCertificateHtml(
        string studentName, string teacherName, string circleName,
        string achievement, int progress, string date) => $$"""
        <!DOCTYPE html>
        <html dir="rtl" lang="ar">
        <head>
          <meta charset="UTF-8">
          <title>شهادة تقدير — {{studentName}}</title>
          <style>
            @import url('https://fonts.googleapis.com/css2?family=Amiri:wght@400;700&display=swap');
            * { margin:0; padding:0; box-sizing:border-box; }
            body { font-family:'Amiri',serif; background:#f8f4e8; display:flex; justify-content:center; align-items:center; min-height:100vh; }
            .cert { width:794px; min-height:560px; background:white; border:12px double #b8860b; padding:48px; text-align:center; position:relative; box-shadow:0 8px 32px rgba(0,0,0,.15); }
            .cert::before { content:''; position:absolute; inset:20px; border:2px solid #b8860b; pointer-events:none; }
            .logo { font-size:48px; margin-bottom:8px; }
            .center-name { font-size:22px; color:#1a5c3a; font-weight:700; margin-bottom:4px; }
            .cert-title { font-size:36px; color:#b8860b; font-weight:700; margin:24px 0 16px; }
            .divider { width:200px; height:2px; background:linear-gradient(to right,transparent,#b8860b,transparent); margin:16px auto; }
            .student-name { font-size:32px; color:#1a3a2a; font-weight:700; margin:16px 0; }
            .achievement { font-size:18px; color:#333; line-height:1.8; margin:16px 0; }
            .progress-text { font-size:22px; color:#1a5c3a; font-weight:700; margin:16px 0; }
            .footer { display:flex; justify-content:space-between; margin-top:40px; font-size:14px; color:#666; }
            .signature { text-align:center; }
            .signature-line { width:140px; height:1px; background:#333; margin:32px auto 4px; }
            @media print { body { background:white; } .cert { box-shadow:none; } }
          </style>
        </head>
        <body>
          <div class="cert">
            <div class="logo">📚</div>
            <div class="center-name">مركز نور لتحفيظ القرآن الكريم</div>
            <div class="cert-title">شهـادة تقـدير</div>
            <div class="divider"></div>
            <p style="font-size:18px;color:#444">يُشهد المركز بأن الطالب/ة</p>
            <div class="student-name">{{studentName}}</div>
            <div class="achievement">
              قد أتم/أتمت بعون الله تعالى<br>
              <strong>{{achievement}}</strong>
            </div>
            <div class="progress-text">نسبة الحفظ الإجمالية: {{progress}}%</div>
            <p style="font-size:16px;color:#555">في حلقة {{circleName}}</p>
            <div class="footer">
              <div class="signature">
                <div class="signature-line"></div>
                <p>المحفظ: {{teacherName}}</p>
              </div>
              <div style="font-size:16px;color:#888;align-self:flex-end">
                التاريخ: {{date}}
              </div>
              <div class="signature">
                <div class="signature-line"></div>
                <p>مدير المركز</p>
              </div>
            </div>
          </div>
          <script>setTimeout(() => window.print(), 500);</script>
        </body>
        </html>
        """;

    private static string GenerateMonthlyReportHtml(
        string studentName, string teacherName, string circleName,
        string month, string year, int attendanceRate,
        int presentDays, int absentDays, int totalVerses,
        int sessionsCount, List<object> hifzRecords)
    {
        var rows = string.Join("", hifzRecords.Select((r, i) =>
        {
            dynamic d = r;
            return $"<tr><td>{i + 1}</td><td>{d.SurahName}</td><td>{d.Verses}</td><td>{d.Evaluation}</td><td>{d.Notes}</td></tr>";
        }));

        return $$"""
        <!DOCTYPE html>
        <html dir="rtl" lang="ar">
        <head>
          <meta charset="UTF-8">
          <title>التقرير الشهري — {{studentName}}</title>
          <style>
            @import url('https://fonts.googleapis.com/css2?family=Tajawal:wght@400;700&display=swap');
            * { margin:0;padding:0;box-sizing:border-box; }
            body { font-family:'Tajawal',sans-serif;background:#f1f5f9;padding:24px; }
            .report { max-width:800px;margin:auto;background:white;border-radius:16px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,.1); }
            .header { background:linear-gradient(135deg,#1a5c3a,#10b981);color:white;padding:32px;text-align:center; }
            .header h1 { font-size:24px;margin-bottom:4px; }
            .header p { opacity:.85;font-size:14px; }
            .body { padding:32px; }
            .info-grid { display:grid;grid-template-columns:1fr 1fr;gap:16px;margin-bottom:24px; }
            .info-card { background:#f8fafc;border-radius:12px;padding:16px;border:1px solid #e2e8f0; }
            .info-card label { font-size:12px;color:#64748b;display:block;margin-bottom:4px; }
            .info-card p { font-size:16px;font-weight:700;color:#1e293b; }
            .stats-row { display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:24px; }
            .stat-box { background:#f0fdf4;border-radius:12px;padding:16px;text-align:center;border:1px solid #bbf7d0; }
            .stat-box .num { font-size:28px;font-weight:700;color:#16a34a; }
            .stat-box .lbl { font-size:12px;color:#166534; }
            table { width:100%;border-collapse:collapse;margin-top:16px; }
            th { background:#f1f5f9;padding:10px 12px;font-size:13px;text-align:right;color:#475569; }
            td { padding:10px 12px;font-size:13px;border-bottom:1px solid #f1f5f9;color:#334155; }
            .footer { background:#f8fafc;padding:16px 32px;text-align:center;font-size:12px;color:#94a3b8; }
            @media print { body { background:white; } }
          </style>
        </head>
        <body>
          <div class="report">
            <div class="header">
              <h1>📋 التقرير الشهري</h1>
              <p>مركز نور لتحفيظ القرآن الكريم — {{month}} {{year}}</p>
            </div>
            <div class="body">
              <div class="info-grid">
                <div class="info-card"><label>اسم الطالب</label><p>{{studentName}}</p></div>
                <div class="info-card"><label>الحلقة</label><p>{{circleName}}</p></div>
                <div class="info-card"><label>المحفظ</label><p>{{teacherName}}</p></div>
                <div class="info-card"><label>الشهر</label><p>{{month}} {{year}}</p></div>
              </div>
              <div class="stats-row">
                <div class="stat-box"><div class="num">{{attendanceRate}}%</div><div class="lbl">نسبة الحضور</div></div>
                <div class="stat-box"><div class="num">{{sessionsCount}}</div><div class="lbl">جلسات التسميع</div></div>
                <div class="stat-box"><div class="num">{{totalVerses}}</div><div class="lbl">آيات محفوظة</div></div>
              </div>
              <p style="font-size:13px;color:#64748b;margin-bottom:8px">
                الحضور: {{presentDays}} يوم ✅ | الغياب: {{absentDays}} يوم ❌
              </p>
              <h3 style="font-size:16px;margin:16px 0 8px;color:#1e293b">سجل التسميع الشهري</h3>
              <table>
                <thead><tr><th>#</th><th>السورة</th><th>الآيات</th><th>التقييم</th><th>ملاحظات</th></tr></thead>
                <tbody>{{rows}}</tbody>
              </table>
            </div>
            <div class="footer">تم إنشاء هذا التقرير تلقائياً بواسطة منصة نور — {{DateTime.Now:yyyy/MM/dd}}</div>
          </div>
          <script>setTimeout(() => window.print(), 600);</script>
        </body>
        </html>
        """;
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
