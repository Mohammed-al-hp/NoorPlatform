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

    public WaitingListController(NoorDbContext context, AccountProvisioningService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        var query = _context.WaitingListEntries.AsQueryable();

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
        var entry = await _context.WaitingListEntries.FindAsync(id);
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

        var phone = AccountProvisioningService.NormalizePhone(request.Phone);
        if (await _context.WaitingListEntries.AnyAsync(e =>
                e.Phone == phone && (e.Status == WaitingListStatus.Pending || e.Status == WaitingListStatus.Contacted)))
            return BadRequest(new { message = "هذا الرقم موجود بالفعل في قائمة الانتظار" });

        if (await _context.Users.AnyAsync(u => u.UserName == phone))
            return BadRequest(new { message = "يوجد حساب مسجل بهذا الرقم" });

        var entry = new WaitingListEntry
        {
            FullName = request.FullName.Trim(),
            Phone = phone,
            ParentName = request.ParentName?.Trim() ?? string.Empty,
            ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone ?? string.Empty),
            Age = request.Age,
            RequestedLevel = request.RequestedLevel ?? "مبتدئ",
            PreferredTime = request.PreferredTime?.Trim() ?? string.Empty,
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
        var entry = await _context.WaitingListEntries.FindAsync(id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            entry.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Phone))
            entry.Phone = AccountProvisioningService.NormalizePhone(request.Phone);
        if (request.ParentName != null)
            entry.ParentName = request.ParentName.Trim();
        if (request.ParentPhone != null)
            entry.ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone);
        if (request.Age.HasValue)
            entry.Age = request.Age;
        if (!string.IsNullOrWhiteSpace(request.RequestedLevel))
            entry.RequestedLevel = request.RequestedLevel;
        if (request.PreferredTime != null)
            entry.PreferredTime = request.PreferredTime.Trim();
        if (request.Notes != null)
            entry.Notes = request.Notes.Trim();
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
        var entry = await _context.WaitingListEntries.FindAsync(id);
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
        var entry = await _context.WaitingListEntries.FindAsync(id);
        if (entry == null)
            return NotFound(new { message = "السجل غير موجود" });

        if (entry.Status is WaitingListStatus.Accepted or WaitingListStatus.Rejected)
            return BadRequest(new { message = "لا يمكن تحويل سجل مكتمل أو مرفوض" });

        // ─── إصلاح عالي: التحقق لمنع إنشاء حساب لطالب موجود مسبقاً ───
        if (await _context.Students.AnyAsync(s => s.User.UserName == entry.Phone))
            return BadRequest(new { message = "هذا الطالب مسجل بالفعل مسبقاً كطالب" });

        var circle = await _context.Circles.FindAsync(request.CircleId);
        if (circle == null)
            return BadRequest(new { message = "الحلقة غير موجودة" });

        var (studentUser, tempPassword, err) = await _accounts.CreateUserAsync(
            entry.Phone, entry.FullName, UserRole.Student);

        if (err != null)
            return BadRequest(new { message = err });

        Parent? parent = null;
        if (!string.IsNullOrWhiteSpace(entry.ParentPhone))
        {
            var (p, _, pErr) = await _accounts.EnsureParentAsync(entry.ParentName, entry.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
        }

        var student = new Student
        {
            UserId = studentUser.Id,
            Level = entry.RequestedLevel,
            CircleId = request.CircleId,
            ParentId = parent?.Id,
            ParentPhone = entry.ParentPhone
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

        return Ok(new
        {
            message = "تم تحويل الطالب وإنشاء الحساب بنجاح",
            studentId = student.Id,
            credentials = new AccountCredentialsDto(
                entry.FullName,
                studentUser.UserName!,
                AccountProvisioningService.ToDisplayPhone(studentUser.UserName!),
                tempPassword,
                UserRole.Student.ToString(),
                true)
        });
    }

    // ─── إصلاح عالي: إخفاء الأرقام الخام عن المحفظين ───
    private static object MapDto(WaitingListEntry e, bool isAdmin) => new
    {
        e.Id,
        e.FullName,
        Phone = isAdmin ? e.Phone : null,
        DisplayPhone = AccountProvisioningService.ToDisplayPhone(e.Phone),
        e.ParentName,
        ParentPhone = isAdmin ? e.ParentPhone : null,
        DisplayParentPhone = string.IsNullOrEmpty(e.ParentPhone)
            ? ""
            : AccountProvisioningService.ToDisplayPhone(e.ParentPhone),
        e.Age,
        e.RequestedLevel,
        e.PreferredTime,
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

    [Required]
    public string Phone { get; set; } = string.Empty;

    public string? ParentName { get; set; }
    public string? ParentPhone { get; set; }
    public int? Age { get; set; }
    public string? RequestedLevel { get; set; }
    public string? PreferredTime { get; set; }
    public string? Notes { get; set; }
    public string? Status { get; set; }
}

public class ConvertWaitingListRequest
{
    [Required]
    public int CircleId { get; set; }
}
