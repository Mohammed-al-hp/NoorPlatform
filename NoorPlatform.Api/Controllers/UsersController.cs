using System.ComponentModel.DataAnnotations;
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

    public UsersController(NoorDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
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
            var normalized = AccountProvisioningService.NormalizePhone(request.Phone);
            var existing = await _userManager.FindByNameAsync(normalized);
            if (existing != null && existing.Id != user.Id)
                return BadRequest(new { message = "رقم الهاتف مستخدم بالفعل" });
            user.UserName = normalized;
            user.PhoneNumber = normalized;
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
            user.Email = request.Email.Trim();

        if (!string.IsNullOrWhiteSpace(request.Role) && Enum.TryParse<UserRole>(request.Role, true, out var newRole))
            user.Role = newRole;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        if (request.MustChangePassword.HasValue)
            user.MustChangePassword = request.MustChangePassword.Value;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("، ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "تم تحديث المستخدم" });
    }

    [HttpPatch("{id}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
            return NotFound(new { message = "المستخدم غير موجود" });

        user.IsActive = !user.IsActive;
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
        await _userManager.UpdateAsync(user);
        return Ok(new { message = "تم تعطيل الحساب" });
    }
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
