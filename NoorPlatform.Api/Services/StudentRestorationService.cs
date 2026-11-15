using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

/// <summary>
/// اكتشاف الطالب السابق (المؤرشف حذفاً ناعماً) عند إعادة تسجيله واستعادته بدل إنشاء حساب جديد،
/// مع الحفاظ على سجلاته التاريخية (حفظ/حضور/نقاط) لأن معرّف Student نفسه يُعاد تفعيله.
/// </summary>
public class StudentRestorationService
{
    public const string ArchivedMatchCode = "ARCHIVED_STUDENT_FOUND";

    private readonly NoorDbContext _context;
    private readonly UserManager<User> _userManager;

    public StudentRestorationService(NoorDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// يبحث عن طالب مؤرشف: أولاً برقم هاتف الطالب (اسم الدخول أو StudentPhone)،
    /// وإن لم يُدخل رقم للطالب فبالاسم الكامل + رقم ولي الأمر.
    /// </summary>
    public async Task<Student?> FindArchivedMatchAsync(string? studentPhone, string fullName, string? parentPhone)
    {
        var archived = _context.Students.IgnoreQueryFilters()
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Where(s => s.IsDeleted);

        var phone = string.IsNullOrWhiteSpace(studentPhone) ? null : LibyanPhone.Normalize(studentPhone);
        if (phone != null)
        {
            var byPhone = await archived
                .Where(s => s.User.UserName == phone || s.User.PhoneNumber == phone || s.StudentPhone == phone)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();
            if (byPhone != null) return byPhone;
        }

        var parent = string.IsNullOrWhiteSpace(parentPhone) ? null : LibyanPhone.Normalize(parentPhone);
        var name = (fullName ?? string.Empty).Trim();
        if (parent == null || name.Length == 0) return null;

        return await archived
            .Where(s => s.User.FullName == name &&
                        (s.ParentPhone == parent || (s.Parent != null && s.Parent.Phone == parent)))
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync();
    }

    /// <summary>بيانات مختصرة تُعرض للأدمن في رسالة "هذا طالب سابق".</summary>
    public static object ToMatchDto(Student s) => new
    {
        s.Id,
        fullName = s.User.FullName,
        displayPhone = s.User.UserName != null && s.User.UserName.StartsWith("st", StringComparison.OrdinalIgnoreCase)
            ? s.User.UserName
            : LibyanPhone.ToDisplay(s.User.UserName),
        circleName = s.Circle?.Name ?? "بدون حلقة",
        s.Level,
        s.Points,
        registrationDate = s.RegistrationDate.ToString("yyyy-MM-dd")
    };

    /// <summary>
    /// يعيد رسالة خطأ إن كان الرقم مستخدماً لحساب نشط آخر (غير الحساب المستثنى).
    /// </summary>
    public async Task<string?> GetActivePhoneConflictAsync(string? phone, int? exceptUserId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var normalized = LibyanPhone.Normalize(phone);
        var other = await _context.Users
            .Where(u => (u.UserName == normalized || u.PhoneNumber == normalized) &&
                        (exceptUserId == null || u.Id != exceptUserId))
            .Select(u => new { u.FullName, u.Role, u.IsActive })
            .FirstOrDefaultAsync();
        if (other == null) return null;

        var role = other.Role switch
        {
            UserRole.Admin => "مشرف",
            UserRole.Teacher => "محفظ",
            UserRole.Parent => "ولي أمر",
            _ => "طالب"
        };
        return other.IsActive
            ? $"رقم الهاتف مستخدم لحساب نشط آخر ({role}: {other.FullName}). استخدم رقماً مختلفاً أو اترك رقم الطالب فارغاً."
            : $"رقم الهاتف مرتبط بحساب معطّل ({role}: {other.FullName}). فعّل ذلك الحساب أو استخدم رقماً آخر.";
    }

    /// <summary>
    /// يعيد تفعيل الطالب وحسابه دون إنشاء User/Student جديد، ويولّد كلمة مرور مؤقتة جديدة.
    /// لا يحفظ التغييرات — المُستدعي يحفظ ضمن نفس المعاملة.
    /// </summary>
    public async Task<(string? TempPassword, string? Error)> ReactivateAsync(Student student, string? actorUserId)
    {
        student.IsDeleted = false;
        student.User.IsActive = true;

        var tempPassword = AccountProvisioningService.GenerateSecurePassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(student.User);
        var reset = await _userManager.ResetPasswordAsync(student.User, token, tempPassword);
        if (!reset.Succeeded)
            return (null, string.Join("، ", reset.Errors.Select(e => e.Description)));

        student.User.MustChangePassword = true;
        student.User.AccessFailedCount = 0;
        student.User.LockoutEnd = null;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = actorUserId ?? "System",
            Action = AuditAction.Update,
            EntityName = nameof(Student),
            EntityId = student.Id.ToString(),
            NewValues = "{\"Event\":\"ArchivedStudentRestoredOnReRegistration\"}"
        });

        return (tempPassword, null);
    }
}
