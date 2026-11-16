using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly UserManager<User> _userManager;

    private readonly AccountProvisioningService _accounts;

    public UsersController(NoorDbContext context, UserManager<User> userManager, AccountProvisioningService accounts)
    {
        _context = context;
        _userManager = userManager;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) ||
                (u.UserName != null && u.UserName.Contains(term)) ||
                (u.Email != null && u.Email.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, true, out var roleFilter))
            query = query.Where(u => u.Role == roleFilter);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                phone = AccountProvisioningService.ToDisplayPhone(u.UserName ?? ""),
                u.Email,
                role = u.Role.ToString(),
                u.IsActive,
                u.MustChangePassword,
                u.LastLoginAt,
                u.CreatedAt
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        return Ok(new
        {
            user.Id,
            user.FullName,
            phone = AccountProvisioningService.ToDisplayPhone(user.UserName ?? ""),
            user.Email,
            role = user.Role.ToString(),
            user.IsActive,
            user.MustChangePassword,
            user.LastLoginAt,
            user.CreatedAt
        });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            if (!LibyanPhone.IsValid(request.Phone))
                return BadRequest(new { message = "رقم الهاتف غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });

            var normalized = AccountProvisioningService.NormalizePhone(request.Phone);
            if (normalized != user.UserName)
            {
                var existing = await _context.Users.AnyAsync(u =>
                    u.Id != user.Id && (u.UserName == normalized || u.PhoneNumber == normalized));
                if (existing)
                    return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل" });

                var oldPhone = user.UserName;
                user.UserName = normalized;
                user.PhoneNumber = normalized;
                await SyncProfilePhoneAsync(user, oldPhone, normalized);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();

        // ─── إصلاح حرج: حماية صارمة عند تغيير الدور ───
        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var newRole)
            && newRole != user.Role)
        {
            // ─── إصلاح: الحساب المرتبط بملف (طالب/محفّظ/ولي أمر) لا يُغيَّر دوره، وإلا يبقى الملف القديم يتيماً
            // ويفقد الحساب الجديد ملفه (حلقات، سجلات، أبناء) ───
            var hasProfile =
                await _context.Students.IgnoreQueryFilters().AnyAsync(s => s.UserId == user.Id) ||
                await _context.Teachers.IgnoreQueryFilters().AnyAsync(t => t.UserId == user.Id) ||
                await _context.Parents.IgnoreQueryFilters().AnyAsync(p => p.UserId == user.Id);
            if (hasProfile)
                return BadRequest(new { message = "لا يمكن تغيير دور حساب مرتبط بملف طالب أو محفّظ أو ولي أمر. أنشئ حساباً جديداً بالدور المطلوب من الصفحة المختصة." });

            // منع الترقية إلى Admin لحماية النظام من التلاعب
            if (newRole == UserRole.Admin)
                return BadRequest(new { message = "لا يمكن ترقية المستخدم إلى مشرف من هذه الواجهة. تواصل مع مدير النظام." });

            // منع تخفيض دور Admin الأخير
            if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
            {
                var adminCount = await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
                if (adminCount <= 1)
                    return BadRequest(new { message = "لا يمكن تغيير دور آخر مشرف نشط" });
            }

            var oldRole = user.Role;
            user.Role = newRole;

            // تحديث دور Identity أيضاً ليبقى متسقاً مع الـ Enum
            await _userManager.RemoveFromRoleAsync(user, oldRole.ToString());
            await _userManager.AddToRoleAsync(user, newRole.ToString());
        }

        if (request.IsActive == false && user.IsActive && user.Role == UserRole.Admin &&
    await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive) <= 1)
            return BadRequest(new { message = "لا يمكن تعطيل آخر مشرف نشط" });
        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;
            // إصلاح: إبطال التوكنات عند تعطيل الحساب
            if (!request.IsActive.Value)
                await _userManager.UpdateSecurityStampAsync(user);
        }

        if (request.MustChangePassword.HasValue)
            user.MustChangePassword = request.MustChangePassword.Value;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "تم تحديث المستخدم" });
    }

    /// <summary>يبقي رقم الهاتف في ملف الطالب/ولي الأمر متطابقاً مع رقم الدخول بعد تعديله.</summary>
    private async Task SyncProfilePhoneAsync(User user, string? oldPhone, string newPhone)
    {
        if (user.Role == UserRole.Student)
        {
            var student = await _context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (student != null) student.StudentPhone = newPhone;
        }
        else if (user.Role == UserRole.Parent)
        {
            var parent = await _context.Parents.IgnoreQueryFilters()
                .Include(p => p.Children)
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (parent == null) return;
            var previousParentPhone = parent.Phone;
            parent.Phone = newPhone;
            foreach (var child in parent.Children.Where(c =>
                         string.IsNullOrWhiteSpace(c.ParentPhone) || c.ParentPhone == previousParentPhone || c.ParentPhone == oldPhone))
                child.ParentPhone = newPhone;
        }
    }

    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        if (user.IsActive && user.Role == UserRole.Admin &&
    await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive) <= 1)
            return BadRequest(new { message = "لا يمكن تعطيل آخر مشرف نشط" });
        user.IsActive = !user.IsActive;

        // إصلاح: إبطال JWT tokens النشطة فوراً عند تعطيل الحساب
        if (!user.IsActive)
            await _userManager.UpdateSecurityStampAsync(user);

        await _userManager.UpdateAsync(user);
        return Ok(new { message = user.IsActive ? "تم تفعيل الحساب" : "تم تعطيل الحساب", user.IsActive });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _context.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive);
            if (adminCount <= 1)
                return BadRequest(new { message = "لا يمكن حذف آخر مشرف نشط" });
        }

        user.IsActive = false;

        // إصلاح: إبطال JWT tokens النشطة فوراً عند حذف (تعطيل) الحساب
        await _userManager.UpdateSecurityStampAsync(user);

        await _userManager.UpdateAsync(user);
        return Ok(new { message = "تم تعطيل الحساب" });
    }

    // POST /api/users/{id}/reset-password — إعادة تعيين كلمة المرور من الأدمن
    // تُعاد كلمة المرور المؤقتة مرة واحدة فقط في هذه الاستجابة ولا تُخزَّن نصاً في أي مكان
    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        var tempPassword = AccountProvisioningService.GenerateSecurePassword();
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, tempPassword); // يحدّث SecurityStamp فتُبطَل الجلسات القديمة
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        user.MustChangePassword = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System",
            Action = AuditAction.Update,
            EntityName = nameof(Core.Entities.User),
            EntityId = user.Id.ToString(),
            NewValues = "{\"Event\":\"PasswordResetByAdmin\",\"MustChangePassword\":true}"
        });
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تمت إعادة تعيين كلمة المرور. سلّمها للمستخدم — لن تظهر مرة أخرى.",
            credentials = new AccountCredentialsDto(
                user.FullName,
                user.UserName!,
                AccountProvisioningService.ToDisplayPhone(user.UserName!),
                tempPassword,
                user.Role.ToString(),
                true)
        });
    }

    // POST /api/users/admin — إنشاء مشرف جديد (Admin فقط، محمي بشكل صارم)
    [HttpPost("admin")]
    public async Task<IActionResult> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "الاسم الثلاثي ورقم الهاتف مطلوبان" });

        var (user, tempPassword, err) = await _accounts.CreateUserAsync(
            request.Phone, request.FullName, UserRole.Admin);

        if (err != null)
            return BadRequest(new { message = err });

        return Ok(new
        {
            message = "تم إنشاء حساب المشرف بنجاح",
            userId = user.Id,
            credentials = new AccountCredentialsDto(
                request.FullName,
                user.UserName!,
                AccountProvisioningService.ToDisplayPhone(user.UserName!),
                tempPassword,
                UserRole.Admin.ToString(),
                true)
        });
    }
}

public class CreateAdminRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
    public bool? MustChangePassword { get; set; }
}
