using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

/// <summary>التقرير الشهري لولي الأمر: معاينة، إرسال يدوي (فردي/حلقة/الكل)، وسجل الإرسال.</summary>
[ApiController]
[Route("api/parent-reports")]
[Authorize(Roles = "Admin,Teacher")]
public class ParentReportsController : ControllerBase
{
    private const int MaxBatch = 500;
    private readonly NoorDbContext _context;
    private readonly MonthlyParentReportService _reports;
    private readonly WhatsAppMessenger _whatsApp;

    public ParentReportsController(NoorDbContext context, MonthlyParentReportService reports, WhatsAppMessenger whatsApp)
    {
        _context = context;
        _reports = reports;
        _whatsApp = whatsApp;
    }

    private static (int Year, int Month)? ResolveMonth(int? year, int? month)
    {
        if (year == null && month == null) return MonthlyParentReportService.PreviousMonth(LibyaTime.Today);
        if (year is null or < 2000 or > 2100 || month is null or < 1 or > 12) return null;
        return (year.Value, month.Value);
    }

    // GET /api/parent-reports/preview/{studentId}?year=&month=
    [HttpGet("preview/{studentId:int}")]
    public async Task<IActionResult> Preview(int studentId, [FromQuery] int? year, [FromQuery] int? month)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, studentId)) return Forbid();
        var ym = ResolveMonth(year, month);
        if (ym == null) return BadRequest(new { message = "الشهر غير صالح" });

        var data = await _reports.BuildAsync(studentId, ym.Value.Year, ym.Value.Month);
        if (data == null) return NotFound(new { message = "الطالب غير موجود" });
        var centerName = await _context.PlatformSettings.AsNoTracking().Select(s => s.CenterName).FirstOrDefaultAsync();
        var text = MonthlyParentReportService.Format(data, centerName);
        return Ok(new
        {
            data,
            text,
            whatsAppConfigured = _whatsApp.IsConfigured,
            shareLink = WhatsAppMessenger.ShareLink(data.GuardianPhone, text)
        });
    }

    // POST /api/parent-reports/send  { studentIds?: [], circleId?: n, all?: bool, year?, month? }
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendParentReportsRequest request)
    {
        var ym = ResolveMonth(request.Year, request.Month);
        if (ym == null) return BadRequest(new { message = "الشهر غير صالح" });

        // نطاق المستخدم: المشرف كل الطلاب، والمحفّظ طلاب حلقاته فقط
        var scope = StudentQueryHelper.ScopeForUser(_context, User).AsNoTracking();
        List<int> ids;
        if (request.StudentIds is { Count: > 0 })
        {
            var wanted = request.StudentIds.Distinct().ToList();
            ids = await scope.Where(s => wanted.Contains(s.Id)).Select(s => s.Id).ToListAsync();
            if (ids.Count != wanted.Count) return Forbid();
        }
        else if (request.CircleId.HasValue)
        {
            if (!await AuthorizationHelpers.CanAccessCircleAsync(_context, User, request.CircleId.Value)) return Forbid();
            ids = await scope.Where(s => s.CircleId == request.CircleId).Select(s => s.Id).ToListAsync();
        }
        else if (request.All)
        {
            ids = await scope.Where(s => s.User.IsActive).Select(s => s.Id).ToListAsync();
        }
        else return BadRequest(new { message = "حدّد الطلاب أو الحلقة المراد إرسال التقرير لها" });

        if (ids.Count == 0) return BadRequest(new { message = "لا يوجد طلاب في النطاق المحدد" });
        if (ids.Count > MaxBatch) return BadRequest(new { message = $"العدد كبير جداً (الحد {MaxBatch} طالب في المرة)" });

        var userId = AuthorizationHelpers.GetUserId(User);
        var results = new List<MonthlyReportResult>();
        foreach (var id in ids)
        {
            var r = await _reports.SendAsync(id, ym.Value.Year, ym.Value.Month, "Manual", userId, HttpContext.RequestAborted);
            if (r != null) results.Add(r);
        }

        var sent = results.Count(r => r.Status == "Sent");
        var prepared = results.Count(r => r.Status == "Prepared");
        var noPhone = results.Count(r => r.Status == "NoPhone");
        var failed = results.Count(r => r.Status == "Failed");
        var month = $"{MonthlyParentReportService.MonthName(ym.Value.Month)} {ym.Value.Year}";
        var message = sent > 0
            ? $"تم إرسال {sent} تقرير لشهر {month} عبر واتساب"
            : prepared > 0
                ? $"تم تجهيز {prepared} تقرير لشهر {month} — واتساب غير مُعدّ، استخدم روابط الإرسال اليدوي"
                : "لم يُرسل أي تقرير";
        if (noPhone > 0) message += $"، و{noPhone} طالب بلا رقم ولي أمر";
        if (failed > 0) message += $"، وتعذر إرسال {failed}";

        return Ok(new { message, year = ym.Value.Year, month = ym.Value.Month, sent, prepared, noPhone, failed, results });
    }

    // GET /api/parent-reports/history?year=&month=
    [HttpGet("history")]
    public async Task<IActionResult> History([FromQuery] int? year, [FromQuery] int? month)
    {
        var ym = ResolveMonth(year, month);
        if (ym == null) return BadRequest(new { message = "الشهر غير صالح" });
        var scopeIds = StudentQueryHelper.ScopeForUser(_context, User).Select(s => s.Id);
        var rows = await _context.MonthlyParentReports.AsNoTracking()
            .Where(r => r.Year == ym.Value.Year && r.Month == ym.Value.Month && scopeIds.Contains(r.StudentId))
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.StudentId, StudentName = r.Student.User.FullName, r.Status, r.Trigger, r.Phone, r.CreatedAt })
            .Take(1000)
            .ToListAsync();
        return Ok(new { year = ym.Value.Year, month = ym.Value.Month, items = rows });
    }
}

public class SendParentReportsRequest
{
    public List<int>? StudentIds { get; set; }
    public int? CircleId { get; set; }
    public bool All { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
}
