using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

/// <summary>
/// خدمة إشعارات واتساب عبر WhatsApp Business API (Meta)
/// يمكن استبدالها بـ Twilio أو أي مزود آخر
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NoorDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationsController> logger)
    {
        _context    = context;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("WhatsApp");
        _logger     = logger;
    }

    // ─────────────────────────────────────────────────
    // POST /api/notifications/absence
    // إرسال إشعار غياب لولي أمر طالب معين
    // يُستدعى تلقائياً عند تسجيل الغياب في AttendanceController
    // ─────────────────────────────────────────────────
    [HttpPost("absence")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendAbsenceNotification([FromBody] AbsenceNotificationRequest request)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Parent).ThenInclude(p => p!.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t.User)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var parentPhone = student.ParentPhone ?? student.Parent?.Phone;
        if (string.IsNullOrEmpty(parentPhone))
            return BadRequest(new { message = "لا يوجد رقم هاتف لولي الأمر" });

        var teacherName = student.Circle?.Teacher?.User?.FullName ?? "المحفظ";
        var circleName  = student.Circle?.Name ?? "الحلقة";
        var date        = request.Date?.ToString("yyyy/MM/dd") ?? DateTime.Now.ToString("yyyy/MM/dd");

        var message = $"""
            📚 *منصة نور لتحفيظ القرآن*

            السلام عليكم ورحمة الله وبركاته
            نُعلمكم بأن ابنكم/ابنتكم *{student.User.FullName}* 
            غاب عن حلقة *{circleName}* اليوم {date}.

            المحفظ: {teacherName}
            
            للاستفسار تواصل مع المركز.
            جزاكم الله خيراً 🌙
            """;

        var result = await SendWhatsAppMessage(parentPhone, message);

        if (!result)
            return StatusCode(503, new { message = "تعذر إرسال الرسالة - تحقق من إعدادات واتساب" });

        // تسجيل الإشعار في قاعدة البيانات
        _logger.LogInformation("تم إرسال إشعار غياب للطالب {StudentId} على رقم {Phone}", student.Id, parentPhone);

        return Ok(new { message = $"تم إرسال إشعار واتساب لولي أمر {student.User.FullName}" });
    }

    // ─────────────────────────────────────────────────
    // POST /api/notifications/bulk-absence
    // إرسال إشعارات غياب جماعي (بعد تسجيل الحضور اليومي)
    // ─────────────────────────────────────────────────
    [HttpPost("bulk-absence")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendBulkAbsenceNotifications([FromBody] BulkAbsenceRequest request)
    {
        var today = DateTime.UtcNow.Date;

        // جلب جميع الغائبين اليوم في الحلقة المحددة
        var absentStudents = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Parent)
            .Where(s => s.CircleId == request.CircleId &&
                        s.Attendances.Any(a => a.Date.Date == today && a.Status == AttendanceStatus.Absent))
            .ToListAsync();

        int sent = 0, failed = 0;

        foreach (var student in absentStudents)
        {
            var phone = student.ParentPhone ?? student.Parent?.Phone;
            if (string.IsNullOrEmpty(phone)) { failed++; continue; }

            var message = $"📚 منصة نور: ابنكم/ابنتكم *{student.User.FullName}* غاب اليوم {today:yyyy/MM/dd}. للاستفسار تواصل مع المركز.";

            if (await SendWhatsAppMessage(phone, message)) sent++;
            else failed++;

            // تجنب الـ Rate Limiting
            await Task.Delay(500);
        }

        return Ok(new
        {
            message = $"تم الإرسال: {sent} ✅ | فشل: {failed} ❌",
            sent, failed
        });
    }

    // ─────────────────────────────────────────────────
    // POST /api/notifications/hifz-praise
    // رسالة مديح عند تسميع ممتاز
    // ─────────────────────────────────────────────────
    [HttpPost("hifz-praise")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendHifzPraise([FromBody] HifzPraiseRequest request)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Parent)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var phone = student.ParentPhone ?? student.Parent?.Phone;
        if (string.IsNullOrEmpty(phone))
            return BadRequest(new { message = "لا يوجد رقم هاتف لولي الأمر" });

        var message = $"""
            🌟 *منصة نور - بشرى سارة!*

            أحسن ابنكم/ابنتكم *{student.User.FullName}* 
            في تسميع سورة *{request.SurahName}* ({request.Verses})
            وحصل على تقييم: *{request.Evaluation}* ⭐

            بارك الله فيه وجعله من حفاظ كتاب الله 🤲
            """;

        var result = await SendWhatsAppMessage(phone, message);

        return result
            ? Ok(new { message = "تم إرسال رسالة المديح لولي الأمر" })
            : StatusCode(503, new { message = "تعذر إرسال الرسالة" });
    }

    // ─────────────────────────────────────────────────
    // الإرسال الفعلي عبر WhatsApp Cloud API (Meta)
    // ─────────────────────────────────────────────────
    private async Task<bool> SendWhatsAppMessage(string phone, string message)
    {
        try
        {
            var phoneId = _configuration["WhatsApp:PhoneNumberId"];
            var token   = _configuration["WhatsApp:AccessToken"];

            // إذا لم تُعيَّن الإعدادات — وضع المحاكاة (للتطوير)
            if (string.IsNullOrEmpty(phoneId) || string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("⚠️ واتساب غير مُعيَّن — محاكاة الإرسال لـ {Phone}: {Message}", phone, message);
                return true; // نُرجع true في التطوير حتى لا يتوقف العمل
            }

            // تنظيف رقم الهاتف (إزالة + والمسافات)
            phone = phone.Replace("+", "").Replace(" ", "").Replace("-", "");
            if (!phone.StartsWith("966")) phone = "966" + phone.TrimStart('0');

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new { body = message }
            };

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var response = await _httpClient.PostAsJsonAsync(
                $"https://graph.facebook.com/v19.0/{phoneId}/messages", payload);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في إرسال واتساب");
            return false;
        }
    }
}

// ─────────────────────────────────────────────────
// Request Models
// ─────────────────────────────────────────────────
public class AbsenceNotificationRequest
{
    public int       StudentId { get; set; }
    public DateTime? Date      { get; set; }
}

public class BulkAbsenceRequest
{
    public int CircleId { get; set; }
}

public class HifzPraiseRequest
{
    public int    StudentId  { get; set; }
    public string SurahName  { get; set; } = string.Empty;
    public string Verses     { get; set; } = string.Empty;
    public string Evaluation { get; set; } = string.Empty;
}
