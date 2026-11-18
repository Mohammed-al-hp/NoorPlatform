using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/waiting-list")]
[Authorize(Roles = "Admin,Teacher")]
public class WaitingListController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly AccountProvisioningService _accounts;
    private readonly StudentRestorationService _restoration;

    public WaitingListController(NoorDbContext context, AccountProvisioningService accounts, StudentRestorationService restoration)
    {
        _context = context;
        _accounts = accounts;
        _restoration = restoration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var query = _context.WaitingListEntries.AsNoTracking().Include(e => e.PreferredCircle).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<WaitingListStatus>(status, true, out var st))
            query = query.Where(e => e.Status == st);
        else
            query = query.Where(e => e.Status == WaitingListStatus.Pending || e.Status == WaitingListStatus.Contacted);

        var isAdmin = User.IsInRole("Admin");

        var items = await query
            .OrderBy(e => e.RegistrationDate)
            .Select(e => MapDto(e, isAdmin))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var entry = await _context.WaitingListEntries.Include(e => e.PreferredCircle).FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        var isAdmin = User.IsInRole("Admin");
        return Ok(MapDto(entry, isAdmin));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertWaitingListRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة" });

        var phoneError = ValidatePhones(request.Phone, request.ParentPhone, requireParent: true);
        if (phoneError != null)
            return BadRequest(new { message = phoneError });

        // هاتف الطالب اختياري: فحوص التكرار تُطبَّق فقط عند إدخاله
        var phone = NormalizeOptionalPhone(request.Phone);
        if (phone != null)
        {
            if (await _context.WaitingListEntries.AnyAsync(e =>
                    e.Phone == phone && (e.Status == WaitingListStatus.Pending || e.Status == WaitingListStatus.Contacted)))
                return BadRequest(new { message = "هذا الرقم موجود بالفعل في قائمة الانتظار" });

            if (await _context.Users.AnyAsync(u => u.UserName == phone))
                return BadRequest(new { message = "يوجد حساب مسجل بهذا الرقم" });
        }

        Circle? preferredCircle = null;
        if (request.PreferredCircleId is > 0)
        {
            preferredCircle = await _context.Circles.FirstOrDefaultAsync(c => c.Id == request.PreferredCircleId.Value);
            if (preferredCircle == null)
                return BadRequest(new { message = "الحلقة المختارة غير موجودة" });
        }

        var entry = new WaitingListEntry
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            ParentName = request.ParentName?.Trim() ?? string.Empty,
            ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone!),
            Age = request.Age,
            RequestedLevel = request.RequestedLevel ?? "مبتدئ",
            PreferredTime = request.PreferredTime?.Trim() ?? string.Empty,
            PreferredCircleId = preferredCircle?.Id,
            PreferredCircle = preferredCircle,
            Notes = request.Notes?.Trim() ?? string.Empty,
            Status = WaitingListStatus.Pending,
            RegistrationDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _context.WaitingListEntries.Add(entry);
        await _context.SaveChangesAsync();
        
        var isAdmin = User.IsInRole("Admin");
        return Ok(new { message = "تمت الإضافة لقائمة الانتظار", entry.Id, entry = MapDto(entry, isAdmin) });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpsertWaitingListRequest request)
    {
        var entry = await _context.WaitingListEntries.Include(e => e.PreferredCircle).FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        // في التعديل: إرسال هاتف ولي أمر فارغ لا يمحو القيمة الحالية (يبقى إلزامياً)
        var phoneError = ValidatePhones(request.Phone, request.ParentPhone, requireParent: false);
        if (phoneError != null)
            return BadRequest(new { message = phoneError });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            entry.FullName = request.FullName.Trim();
        if (request.Phone != null)
        {
            var phone = NormalizeOptionalPhone(request.Phone);
            if (phone != null && phone != entry.Phone && await _context.WaitingListEntries.AnyAsync(e =>
                    e.Id != entry.Id && e.Phone == phone &&
                    (e.Status == WaitingListStatus.Pending || e.Status == WaitingListStatus.Contacted)))
                return BadRequest(new { message = "هذا الرقم موجود بالفعل في قائمة الانتظار" });
            entry.Phone = phone; // سلسلة فارغة = حذف رقم الطالب
        }
        if (request.ParentName != null)
            entry.ParentName = request.ParentName.Trim();
        if (!string.IsNullOrWhiteSpace(request.ParentPhone))
            entry.ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone);
        if (request.Age.HasValue)
            entry.Age = request.Age;
        if (!string.IsNullOrWhiteSpace(request.RequestedLevel))
            entry.RequestedLevel = request.RequestedLevel;
        if (request.PreferredTime != null)
            entry.PreferredTime = request.PreferredTime.Trim();
        if (request.Notes != null)
            entry.Notes = request.Notes.Trim();
        if (request.PreferredCircleId.HasValue)
        {
            if (request.PreferredCircleId.Value <= 0)
            {
                entry.PreferredCircleId = null;
                entry.PreferredCircle = null;
            }
            else
            {
                var circle = await _context.Circles.FirstOrDefaultAsync(c => c.Id == request.PreferredCircleId.Value);
                if (circle == null)
                    return BadRequest(new { message = "الحلقة المختارة غير موجودة" });
                entry.PreferredCircleId = circle.Id;
                entry.PreferredCircle = circle;
            }
        }
        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<WaitingListStatus>(request.Status, true, out var status))
            entry.Status = status;

        entry.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var isAdmin = User.IsInRole("Admin");
        return Ok(new { message = "تم التحديث", entry = MapDto(entry, isAdmin) });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _context.WaitingListEntries.Include(e => e.PreferredCircle).FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        _context.WaitingListEntries.Remove(entry);
        await _context.SaveChangesAsync();
        return Ok(new { message = "تم الحذف" });
    }

    /// <summary>
    /// تحويل سجل قائمة الانتظار إلى طالب مسجل مع إنشاء الحسابات تلقائياً.
    /// </summary>
    [HttpPost("{id}/convert-to-student")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConvertToStudent(int id, [FromBody] ConvertWaitingListRequest request)
    {
        var entry = await _context.WaitingListEntries.Include(e => e.PreferredCircle).FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        if (entry.Status is WaitingListStatus.Accepted or WaitingListStatus.Rejected)
            return BadRequest(new { message = "لا يمكن تحويل سجل مكتمل أو مرفوض" });

        var studentPhone = NormalizeOptionalPhone(entry.Phone);

        // ─── إصلاح عالي: التحقق لمنع إنشاء حساب لطالب موجود مسبقاً ───
        if (studentPhone != null && await _context.Students.AnyAsync(s => s.User.UserName == studentPhone))
            return BadRequest(new { message = "هذا الطالب مسجل بالفعل مسبقاً كطالب" });

        var circle = await _context.Circles.FindAsync(request.CircleId);
        if (circle == null)
            return BadRequest(new { message = "الحلقة غير موجودة" });

        // ─── عودة الطالب بعد المغادرة ───
        if (request.RestoreArchivedStudentId.HasValue)
            return await RestoreArchivedFromEntryAsync(entry, request);

        if (!request.IgnoreArchivedMatch)
        {
            var archived = await _restoration.FindArchivedMatchAsync(studentPhone, entry.FullName, entry.ParentPhone);
            if (archived != null)
                return Conflict(new
                {
                    code = StudentRestorationService.ArchivedMatchCode,
                    canRestore = true,
                    message = "هذا طالب سابق — استعادة بياناته؟",
                    archivedStudent = StudentRestorationService.ToMatchDto(archived)
                });
        }

        var phoneConflict = await _restoration.GetActivePhoneConflictAsync(studentPhone);
        if (phoneConflict != null)
            return BadRequest(new { message = phoneConflict });

        await using var tx = await _context.Database.BeginTransactionAsync();
        // بدون رقم للطالب: يُولَّد اسم دخول مثل st123456 (نفس سلوك إضافة طالب)
        var (studentUser, tempPassword, err) = studentPhone == null
            ? await _accounts.CreateUserAsync("", entry.FullName, UserRole.Student,
                usernameOverride: await _accounts.GenerateStudentUsernameAsync())
            : await _accounts.CreateUserAsync(studentPhone, entry.FullName, UserRole.Student);

        if (err != null)
            return BadRequest(new { message = err });

        Parent? parent = null;
        string? parentTempPassword = null;
        if (!string.IsNullOrWhiteSpace(entry.ParentPhone))
        {
            var (p, pPass, pErr) = await _accounts.EnsureParentAsync(entry.ParentName, entry.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
            parentTempPassword = pPass;
        }

        var student = new Student
        {
            UserId = studentUser.Id,
            Level = entry.RequestedLevel,
            CircleId = request.CircleId,
            Parent = parent,
            ParentPhone = entry.ParentPhone,
            GuardianName = entry.ParentName,
            StudentPhone = studentPhone
        };
        _context.Students.Add(student);

        entry.Status = WaitingListStatus.Accepted;
        entry.UpdatedAt = DateTime.UtcNow;

        var adminId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = adminId,
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "WaitingList",
            Description = $"تم تحويل {entry.FullName} من قائمة الانتظار إلى طالب",
            Icon = "🎓",
            Color = "text-green-500"
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "تم تحويل الطالب وإنشاء الحساب بنجاح",
            studentId = student.Id,
            parentCredentials = (parentTempPassword == null || parent == null) ? null : new AccountCredentialsDto(
    parent.User.FullName,
    parent.User.UserName!,
    AccountProvisioningService.ToDisplayPhone(parent.User.UserName!),
    parentTempPassword,
    UserRole.Parent.ToString(),
    true),
            credentials = new AccountCredentialsDto(
                entry.FullName,
                studentUser.UserName!,
                AccountProvisioningService.ToDisplayPhone(studentUser.UserName!),
                tempPassword,
                UserRole.Student.ToString(),
                true)
        });
    }

    /// <summary>إعادة تفعيل طالب سابق مؤرشف من بيانات سجل الانتظار دون إنشاء حساب جديد.</summary>
    private async Task<IActionResult> RestoreArchivedFromEntryAsync(WaitingListEntry entry, ConvertWaitingListRequest request)
    {
        var student = await _context.Students.IgnoreQueryFilters()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.RestoreArchivedStudentId!.Value);
        if (student == null)
            return NotFound(new { message = "الطالب السابق غير موجود" });
        if (!student.IsDeleted)
            return BadRequest(new { message = "هذا الطالب نشط بالفعل ولا يحتاج استعادة" });

        var studentPhone = NormalizeOptionalPhone(entry.Phone);
        var phoneConflict = await _restoration.GetActivePhoneConflictAsync(studentPhone, student.UserId);
        if (phoneConflict != null)
            return BadRequest(new { message = phoneConflict });

        await using var tx = await _context.Database.BeginTransactionAsync();

        Parent? parent = null;
        string? parentTempPassword = null;
        if (!string.IsNullOrWhiteSpace(entry.ParentPhone))
        {
            var (p, pPass, pErr) = await _accounts.EnsureParentAsync(entry.ParentName, entry.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
            parentTempPassword = pPass;
        }

        if (studentPhone != null && student.User.UserName != studentPhone)
        {
            student.User.UserName = studentPhone;
            student.User.NormalizedUserName = studentPhone.ToUpperInvariant();
            student.User.PhoneNumber = studentPhone;
            student.StudentPhone = studentPhone;
        }
        student.User.FullName = entry.FullName.Trim();
        student.Level = entry.RequestedLevel;
        student.CircleId = request.CircleId;
        student.RegistrationDate = DateTime.UtcNow;
        if (parent != null)
        {
            student.Parent = parent;
            student.ParentPhone = parent.Phone;
            if (!string.IsNullOrWhiteSpace(entry.ParentName)) student.GuardianName = entry.ParentName.Trim();
        }

        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value;
        var (tempPassword, rErr) = await _restoration.ReactivateAsync(student, actorId);
        if (rErr != null)
            return BadRequest(new { message = rErr });

        entry.Status = WaitingListStatus.Accepted;
        entry.UpdatedAt = DateTime.UtcNow;

        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = int.Parse(actorId),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "WaitingList",
            Description = $"عاد الطالب السابق {entry.FullName} من قائمة الانتظار وتمت استعادة بياناته",
            Icon = "🔄",
            Color = "text-green-500"
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "تمت استعادة الطالب السابق وتحديث بياناته مع الإبقاء على سجلاته السابقة",
            restored = true,
            studentId = student.Id,
            parentCredentials = (parentTempPassword == null || parent == null) ? null : new AccountCredentialsDto(
                parent.User.FullName,
                parent.User.UserName!,
                AccountProvisioningService.ToDisplayPhone(parent.User.UserName!),
                parentTempPassword,
                UserRole.Parent.ToString(),
                true),
            credentials = new AccountCredentialsDto(
                student.User.FullName,
                student.User.UserName!,
                AccountProvisioningService.ToDisplayPhone(student.User.UserName!),
                tempPassword!,
                UserRole.Student.ToString(),
                true)
        });
    }

    /// <summary>رقم اختياري: فارغ ← null، وإلا الصيغة المخزنة 2189XXXXXXXX.</summary>
    private static string? NormalizeOptionalPhone(string? phone) =>
        string.IsNullOrWhiteSpace(phone) ? null : AccountProvisioningService.NormalizePhone(phone);

    private static string? ValidatePhones(string? studentPhone, string? parentPhone, bool requireParent)
    {
        if (!string.IsNullOrWhiteSpace(studentPhone) && !LibyanPhone.IsValid(studentPhone))
            return "رقم هاتف الطالب غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام";
        if (string.IsNullOrWhiteSpace(parentPhone))
            return requireParent ? "رقم هاتف ولي الأمر مطلوب" : null;
        if (!LibyanPhone.IsValid(parentPhone))
            return "رقم هاتف ولي الأمر غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام";
        return null;
    }

    // ─── إصلاح عالي: إخفاء الأرقام الخام عن المحفظين ───
    private static object MapDto(WaitingListEntry e, bool isAdmin) => new
    {
        e.Id,
        e.FullName,
        Phone = isAdmin ? e.Phone : null,
        DisplayPhone = string.IsNullOrEmpty(e.Phone) ? "" : AccountProvisioningService.ToDisplayPhone(e.Phone),
        e.ParentName,
        ParentPhone = isAdmin ? e.ParentPhone : null,
        DisplayParentPhone = string.IsNullOrEmpty(e.ParentPhone)
            ? ""
            : AccountProvisioningService.ToDisplayPhone(e.ParentPhone),
        e.Age,
        e.RequestedLevel,
        e.PreferredTime,
        e.PreferredCircleId,
        PreferredCircleName = e.PreferredCircle != null ? e.PreferredCircle.Name : null,
        e.Notes,
        Status = e.Status.ToString(),
        e.RegistrationDate,
        e.CreatedAt,
        e.UpdatedAt
    };
}

public class UpsertWaitingListRequest
{
    [Required, MinLength(2)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>هاتف الطالب — اختياري.</summary>
    public string? Phone { get; set; }

    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public int? Age { get; set; }
    public string? RequestedLevel { get; set; }
    public string? PreferredTime { get; set; }
    /// <summary>الحلقة المفضلة (اختيارية). 0 في التعديل = إزالة الاختيار.</summary>
    public int? PreferredCircleId { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}

public class ConvertWaitingListRequest
{
    [Required]
    public int CircleId { get; set; }

    /// <summary>عند تأكيد الأدمن: استعادة هذا الطالب المؤرشف بدل إنشاء طالب جديد.</summary>
    public int? RestoreArchivedStudentId { get; set; }

    /// <summary>تجاهل التطابق مع طالب سابق وإنشاء طالب جديد.</summary>
    public bool IgnoreArchivedMatch { get; set; }
}
