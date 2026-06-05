using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NoorPlatform.Api.Services;
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationsController> _logger;
    // إصلاح: قراءة إعدادات واتساب مرة واحدة عبر IOptions بدلاً من IConfiguration في كل استدعاء
    private readonly WhatsAppSettings _whatsAppSettings;

    public NotificationsController(
        NoorDbContext context,
        IOptions<WhatsAppSettings> whatsAppOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationsController> logger)
    {
        _context          = context;
        _whatsAppSettings = whatsAppOptions.Value;
        _httpClient       = httpClientFactory.CreateClient("WhatsApp");
        _logger           = logger;
    }

    // ─────────────────────────────────────────────────
    // POST /api/notifications/absence
    // إرسال إشعار غياب لولي أمر طالب معين
    // إصلاح: إضافة فحص ملكية المحفظ للطالب
    // ─────────────────────────────────────────────────
    [HttpPost("absence")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendAbsenceNotification([FromBody] AbsenceNotificationRequest request)
    {
        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Parent).ThenInclude(p => p!.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t!.User)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        // ─── إصلاح حرج: فحص ملكية المحفظ — لا يحق للمحفظ إرسال إشعار لطالب خارج حلقته ───
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isTeacher = User.IsInRole("Teacher");

        if (isTeacher && student.Circle?.Teacher?.UserId != currentUserId)
            return Forbid();

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
    // إصلاح: فحص ملكية الحلقة للمحفظ
    // ─────────────────────────────────────────────────
    [HttpPost("bulk-absence")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> SendBulkAbsenceNotifications([FromBody] BulkAbsenceRequest request)
    {
        // ─── إصلاح حرج: التحقق من أن المحفظ يملك الحلقة المُستهدفة ───
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isTeacher = User.IsInRole("Teacher");

        if (isTeacher)
        {
            var ownsCircle = await _context.Circles
                .AnyAsync(c => c.Id == request.CircleId && c.Teacher != null && c.Teacher.UserId == currentUserId);

            if (!ownsCircle)
                return Forbid();
        }

        var today = DateTime.UtcNow.Date;

        // جلب جميع الغائبين اليوم في الحلقة المحددة
        var absentStudents = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Parent)
            .Where(s => s.CircleId == request.CircleId &&
                        s.Attendances.Any(a => a.Date.Date == today && a.Status == AttendanceStatus.UnexcusedAbsence))
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
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher)
            .FirstOrDefaultAsync(s => s.Id == request.StudentId);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        // فحص ملكية المحفظ للطالب (نفس النمط المُطبق في absence)
        var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isTeacher = User.IsInRole("Teacher");

        if (isTeacher && student.Circle?.Teacher?.UserId != currentUserId)
            return Forbid();

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
    // إصلاح: استخدام WhatsAppSettings المحقونة عبر IOptions بدلاً من IConfiguration
    // ─────────────────────────────────────────────────
    private async Task<bool> SendWhatsAppMessage(string phone, string message)
    {
        try
        {
            // إذا لم تُعيَّن الإعدادات — وضع المحاكاة (للتطوير)
            if (!_whatsAppSettings.IsConfigured)
            {
                _logger.LogWarning("⚠️ واتساب غير مُعيَّن — تم تخطي الإرسال لـ {Phone}", phone);
                return false;
            }

            // تنظيف رقم الهاتف (إزالة + والمسافات)
            phone = phone.Replace("+", "").Replace(" ", "").Replace("-", "");
            phone = LibyanPhone.ForWhatsApp(phone);
            if (string.IsNullOrEmpty(phone)) return false;

            var payload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new { body = message }
            };

            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://graph.facebook.com/v19.0/{_whatsAppSettings.PhoneNumberId}/messages");
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_whatsAppSettings.AccessToken}");
            request.Content = JsonContent.Create(payload);

            var response = await _httpClient.SendAsync(request);

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
