using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using NoorPlatform.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly NoorDbContext _context;
    private readonly OtpService _otp;
    private readonly IOtpSender _otpSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(UserManager<User> userManager, SignInManager<User> signInManager, IConfiguration configuration,
        NoorDbContext context, OtpService otp, IOtpSender otpSender, ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _context = context;
        _otp = otp;
        _otpSender = otpSender;
        _logger = logger;
    }

    // POST /api/auth/login — رقم الهاتف + كلمة المرور
    [HttpPost("login")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "بيانات تسجيل الدخول غير صالحة" });

        var user = await FindActiveUserByPhoneAsync(request.Phone);
        if (user == null)
            return Unauthorized(new { message = "رقم الهاتف أو كلمة المرور غير صحيحة" });

        var check = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
            return StatusCode(429, new { message = "تم قفل الحساب مؤقتاً بسبب محاولات خاطئة. حاول بعد 15 دقيقة." });
        if (!check.Succeeded)
            return Unauthorized(new { message = "رقم الهاتف أو كلمة المرور غير صحيحة" });

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (user.Role == UserRole.Student)
        {
            var isDeleted = await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.UserId == user.Id && s.IsDeleted);
            if (isDeleted)
                return Unauthorized(new { message = "هذا الحساب مؤرشف ولا يمكن تسجيل الدخول إليه." });
        }

        var token = GenerateJwtToken(user);
        return Ok(new
        {
            token,
            mustChangePassword = user.MustChangePassword,
            user = BuildUserDto(user, request.Phone)
        });
    }

    // GET /api/auth/me — بيانات المستخدم الحالي (المستوى الإداري والصلاحيات وتسمية الدور)
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });
        return Ok(BuildUserDto(user, null));
    }

    /// <summary>تسمية الدور بالعربية للواجهات.</summary>
    public static string RoleLabel(User user) => user.Role switch
    {
        UserRole.Admin => user.IsSuperAdmin ? "مدير عام" : "مشرف",
        UserRole.Teacher => "محفّظ",
        UserRole.Student => "طالب",
        UserRole.Parent => "ولي أمر",
        _ => ""
    };

    private static object BuildUserDto(User user, string? fallbackPhone) => new
    {
        user.Id,
        user.FullName,
        phone = AccountProvisioningService.ToDisplayPhone(user.UserName ?? user.PhoneNumber ?? fallbackPhone ?? ""),
        user.Email,
        role = user.Role.ToString(),
        roleLabel = RoleLabel(user),
        isSuperAdmin = user.Role == UserRole.Admin && user.IsSuperAdmin,
        adminPermissions = user.Role == UserRole.Admin
            ? NoorPlatform.Api.Security.AdminAccess.ToNames(user.IsSuperAdmin, user.AdminPermissions)
            : Array.Empty<string>()
    };

    // POST /api/auth/change-password — إجباري عند أول دخول
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة" });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null)
            return NotFound();

        var check = await _userManager.CheckPasswordAsync(user, request.CurrentPassword);
        if (!check)
            return BadRequest(new { message = "كلمة المرور الحالية غير صحيحة" });

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        user.MustChangePassword = false;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "تم تغيير كلمة المرور بنجاح", token = GenerateJwtToken(user) });
    }

    // ─────────────────────────────────────────────────
    // استعادة كلمة المرور برمز تحقق (OTP) — 3 خطوات
    // الاستجابة موحّدة سواء وُجد الرقم أم لا لمنع تعداد المستخدمين
    // ─────────────────────────────────────────────────

    // POST /api/auth/forgot-password {phone}
    [HttpPost("forgot-password")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("otp-request")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (!ModelState.IsValid || !LibyanPhone.IsValid(request.Phone))
            return BadRequest(new { message = "رقم الهاتف غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });

        var phone = LibyanPhone.Normalize(request.Phone);

        // حدود الإرسال تُطبَّق على الرقم قبل البحث عن الحساب حتى لا تختلف الاستجابة
        var issued = _otp.Issue(phone);
        if (issued.Status != OtpIssueStatus.Issued)
        {
            var msg = issued.Status == OtpIssueStatus.Cooldown
                ? $"يرجى الانتظار {issued.RetryAfterSeconds} ثانية قبل طلب رمز جديد"
                : "تجاوزت الحد المسموح لطلب الرموز. حاول لاحقاً.";
            return StatusCode(429, new { message = msg, retryAfterSeconds = issued.RetryAfterSeconds });
        }

        var channelSetting = await _context.PlatformSettings.AsNoTracking()
            .Select(s => s.OtpChannel).FirstOrDefaultAsync();
        var channel = OtpChannels.Resolve(channelSetting, request.Channel);

        var user = await FindActiveUserByPhoneAsync(phone);
        if (user != null && !await IsArchivedStudentAsync(user))
        {
            var code = issued.Code!;
            var userId = user.Id;
            // الإرسال في الخلفية حتى لا يكشف زمن الاستجابة وجود الحساب
            _ = Task.Run(async () =>
            {
                if (!await _otpSender.SendAsync(phone, code, channel))
                    _logger.LogWarning("تعذر إرسال رمز التحقق للمستخدم {UserId} عبر {Channel}", userId, channel);
            });
        }

        return Ok(new
        {
            message = channel == OtpChannels.Sms
                ? "إذا كان الرقم مسجلاً لدينا فستصلك رسالة نصية (SMS) برمز التحقق خلال لحظات."
                : "إذا كان الرقم مسجلاً لدينا فستصلك رسالة واتساب برمز التحقق خلال لحظات.",
            channel,
            expiresInSeconds = (int)OtpService.Lifetime.TotalSeconds,
            resendAfterSeconds = (int)OtpService.ResendCooldown.TotalSeconds
        });
    }

    // POST /api/auth/verify-otp {phone, code} — يتحقق دون استهلاك الرمز
    [HttpPost("verify-otp")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("otp-verify")]
    public IActionResult VerifyOtp([FromBody] VerifyOtpRequest request)
    {
        if (!ModelState.IsValid || !LibyanPhone.IsValid(request.Phone))
            return BadRequest(new { message = "بيانات غير صالحة" });

        var status = _otp.Verify(LibyanPhone.Normalize(request.Phone), request.Code);
        return status == OtpVerifyStatus.Valid
            ? Ok(new { message = "تم التحقق من الرمز. أدخل كلمة المرور الجديدة." })
            : BadRequest(new { message = OtpErrorMessage(status) });
    }

    // POST /api/auth/reset-password {phone, code, newPassword} — يستهلك الرمز مرة واحدة
    [HttpPost("reset-password")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("otp-verify")]
    public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpRequest request)
    {
        if (!ModelState.IsValid || !LibyanPhone.IsValid(request.Phone))
            return BadRequest(new { message = "بيانات غير صالحة. كلمة المرور 8 أحرف على الأقل والرمز 6 أرقام." });

        var phone = LibyanPhone.Normalize(request.Phone);
        var user = await FindActiveUserByPhoneAsync(phone);
        if (user != null && await IsArchivedStudentAsync(user))
            user = null;

        // التحقق من قوة كلمة المرور قبل استهلاك الرمز حتى لا يضيع الرمز بسبب خطأ في الإدخال
        if (user != null)
        {
            foreach (var validator in _userManager.PasswordValidators)
            {
                var vr = await validator.ValidateAsync(_userManager, user, request.NewPassword);
                if (!vr.Succeeded)
                    return BadRequest(new { message = string.Join("، ", vr.Errors.Select(e => e.Description)) });
            }
        }

        var status = _otp.Consume(phone, request.Code);
        if (status != OtpVerifyStatus.Valid || user == null)
            return BadRequest(new { message = OtpErrorMessage(status == OtpVerifyStatus.Valid ? OtpVerifyStatus.Invalid : status) });

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        user.MustChangePassword = false;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id.ToString(),
            Action = AuditAction.Update,
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            NewValues = "{\"Event\":\"PasswordResetViaOtp\"}"
        });
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تعيين كلمة المرور الجديدة بنجاح. يمكنك تسجيل الدخول الآن." });
    }

    private static string OtpErrorMessage(OtpVerifyStatus status) => status switch
    {
        OtpVerifyStatus.TooManyAttempts => "تجاوزت عدد المحاولات المسموح. اطلب رمزاً جديداً.",
        OtpVerifyStatus.Expired => "انتهت صلاحية رمز التحقق أو لم يُطلب. اطلب رمزاً جديداً.",
        _ => "رمز التحقق غير صحيح"
    };

    private async Task<bool> IsArchivedStudentAsync(User user) =>
        user.Role == UserRole.Student &&
        await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.UserId == user.Id && s.IsDeleted);

    /// <summary>البحث عن حساب نشط بكل أشكال الرقم المحتملة (نفس منطق تسجيل الدخول).</summary>
    private async Task<User?> FindActiveUserByPhoneAsync(string phone)
    {
        var candidates = new List<User>();
        foreach (var key in LibyanPhone.GetLoginLookupKeys(phone))
        {
            var match = await _userManager.FindByNameAsync(key)
                        ?? await _userManager.Users.FirstOrDefaultAsync(u =>
                            u.PhoneNumber == key || u.NormalizedUserName == key.ToUpperInvariant());
            if (match != null && candidates.All(c => c.Id != match.Id))
                candidates.Add(match);
        }
        return candidates.FirstOrDefault(c => c.IsActive);
    }

    // التسجيل الذاتي معطّل — الحسابات تُنشأ من الإدارة فقط
    [HttpPost("register")]
    public IActionResult Register()
    {
        return StatusCode(403, new { message = "التسجيل الذاتي غير متاح. يرجى التواصل مع إدارة المركز." });
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var keyStr = _configuration["Jwt:Key"] 
                     ?? Environment.GetEnvironmentVariable("JWT__Key") 
                     ?? Environment.GetEnvironmentVariable("NOOR_JWT_KEY");

        if (string.IsNullOrEmpty(keyStr))
            throw new InvalidOperationException("JWT Key is missing from configuration and environment variables.");

        var key = Encoding.UTF8.GetBytes(keyStr);
        var expiryDays = int.Parse(_configuration["Jwt:ExpiryDays"] ?? "1");
        var issuer   = _configuration["Jwt:Issuer"]   ?? "NoorPlatform";
        var audience = _configuration["Jwt:Audience"] ?? "NoorPlatformClients";

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString()) ,        
                new Claim("stamp", user.SecurityStamp ?? "")
            }),
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddDays(expiryDays),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            )
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

public class LoginRequest
{
    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;
}

public class ForgotPasswordRequest
{
    [Required]
    public string Phone { get; set; } = string.Empty;
    /// <summary>القناة المفضلة: WhatsApp أو Sms (تُحترم إن كانت مفعّلة في الإعدادات).</summary>
    public string? Channel { get; set; }
}

public class VerifyOtpRequest
{
    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordWithOtpRequest
{
    [Required]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(6, MinimumLength = 6)]
    public string Code { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}
