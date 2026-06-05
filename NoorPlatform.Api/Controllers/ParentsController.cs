using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class ParentsController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly AccountProvisioningService _accounts;

    public ParentsController(NoorDbContext context, AccountProvisioningService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = _context.Parents
            .Include(p => p.User)
            .Include(p => p.Children).ThenInclude(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.User.FullName.Contains(term) ||
                p.Phone.Contains(term) ||
                p.User.UserName!.Contains(term));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(p => p.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new
            {
                p.Id,
                p.UserId,
                fullName = p.User.FullName,
                phone = AccountProvisioningService.ToDisplayPhone(p.Phone),
                accountPhone = AccountProvisioningService.ToDisplayPhone(p.User.UserName ?? p.Phone),
                childrenCount = p.Children.Count(c => !c.IsDeleted),
                children = p.Children.Where(c => !c.IsDeleted).Select(c => new
                {
                    c.Id,
                    fullName = c.User.FullName,
                    c.CircleId
                }).ToList(),
                p.User.IsActive,
                p.User.MustChangePassword
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var parent = await _context.Parents
            .Include(p => p.User)
            .Include(p => p.Children).ThenInclude(c => c.User)
            .Include(p => p.Children).ThenInclude(c => c.Circle)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
            return NotFound(new { message = "ولي الأمر غير موجود" });

        return Ok(new
        {
            parent.Id,
            parent.UserId,
            fullName = parent.User.FullName,
            phone = AccountProvisioningService.ToDisplayPhone(parent.Phone),
            accountPhone = AccountProvisioningService.ToDisplayPhone(parent.User.UserName ?? parent.Phone),
            parent.User.Email,
            parent.User.IsActive,
            parent.User.MustChangePassword,
            children = parent.Children.Where(c => !c.IsDeleted).Select(c => new
            {
                c.Id,
                fullName = c.User.FullName,
                circleName = c.Circle != null ? c.Circle.Name : "—"
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateParentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (parent, tempPassword, err) = await _accounts.EnsureParentAsync(request.FullName, request.Phone);
        if (err != null)
            return BadRequest(new { message = err });

        if (parent == null)
            return BadRequest(new { message = "تعذر إنشاء ولي الأمر" });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            parent.User.FullName = request.FullName.Trim();

        if (request.ChildStudentIds?.Count > 0)
        {
            var students = await _context.Students
                .Where(s => request.ChildStudentIds.Contains(s.Id) && !s.IsDeleted)
                .ToListAsync();

            // ─── إصلاح: التحقق من عدم ارتباط الطلاب بولي أمر آخر ───
            if (students.Any(s => s.ParentId != null && s.ParentId != parent.Id))
                return BadRequest(new { message = "بعض الطلاب محددين مرتبطين بالفعل بولي أمر آخر" });

            foreach (var s in students)
            {
                s.ParentId = parent.Id;
                s.ParentPhone = parent.Phone;
            }
        }

        await _context.SaveChangesAsync();

        // ─── إصلاح: عدم إرجاع كلمة المرور في الاستجابة (أمان) ───
        return Ok(new { message = "تم إضافة ولي الأمر بنجاح، سيتم إرسال بيانات الدخول له", parent.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateParentRequest request)
    {
        var parent = await _context.Parents
            .Include(p => p.User)
            .Include(p => p.Children)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
            return NotFound(new { message = "ولي الأمر غير موجود" });

        if (!string.IsNullOrWhiteSpace(request.FullName))
            parent.User.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            var normalized = AccountProvisioningService.NormalizePhone(request.Phone);
            parent.Phone = normalized;
            foreach (var child in parent.Children)
                child.ParentPhone = normalized;
        }

        if (request.ChildStudentIds != null)
        {
            var linked = await _context.Students
                .Where(s => request.ChildStudentIds.Contains(s.Id))
                .ToListAsync();

            // ─── إصلاح: التحقق من عدم ارتباط الطلاب بولي أمر آخر قبل التحديث ───
            if (linked.Any(s => s.ParentId != null && s.ParentId != parent.Id))
                return BadRequest(new { message = "بعض الطلاب محددين مرتبطين بالفعل بولي أمر آخر" });

            foreach (var s in parent.Children)
            {
                if (!request.ChildStudentIds.Contains(s.Id))
                {
                    s.ParentId = null;
                }
            }
            foreach (var s in linked)
            {
                s.ParentId = parent.Id;
                s.ParentPhone = parent.Phone;
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث ولي الأمر" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var parent = await _context.Parents
            .Include(p => p.User)
            .Include(p => p.Children)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (parent == null)
            return NotFound(new { message = "ولي الأمر غير موجود" });

        foreach (var child in parent.Children)
        {
            child.ParentId = null;
            child.ParentPhone = string.Empty;
        }

        // ─── إصلاح: الاكتفاء بالحذف المنطقي (Soft Delete) لمنع فقدان البيانات المالية ───
        parent.User.IsActive = false;
        // تمت إزالة: _context.Parents.Remove(parent); للحفاظ على الفواتير والسجلات

        await _context.SaveChangesAsync();

        return Ok(new { message = "تم تعطيل حساب ولي الأمر وفك ربط الأبناء (البيانات المالية محفوظة)" });
    }
}

public class CreateParentRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    public List<int>? ChildStudentIds { get; set; }
}

public class UpdateParentRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public List<int>? ChildStudentIds { get; set; }
}
