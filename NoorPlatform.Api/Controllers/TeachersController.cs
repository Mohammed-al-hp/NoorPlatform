using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TeachersController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly AccountProvisioningService _accounts;
    private readonly UserManager<User> _userManager;

    public TeachersController(NoorDbContext context, AccountProvisioningService accounts, UserManager<User> userManager)
    {
        _context = context;
        _accounts = accounts;
        _userManager = userManager;
    }

    // GET /api/teachers
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetTeachers()
    {
        var teachers = await _context.Teachers
            .Include(t => t.User)
            .Include(t => t.Circles).ThenInclude(c => c.Students)
            .Select(t => new
            {
                t.Id,
                t.User.FullName,
                t.User.Email,
                CircleName = t.Circles.Any() ? t.Circles.First().Name : "بدون حلقة",
                t.Qualification,
                StudentCount = t.Circles.Sum(c => c.Students.Count)
            })
            .ToListAsync();

        return Ok(teachers);
    }

    // GET /api/teachers/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var teacher = await _context.Teachers
            .Include(t => t.User)
            .Include(t => t.Circles).ThenInclude(c => c.Students)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound(new { message = "المحفظ غير موجود" });

        return Ok(new
        {
            teacher.Id,
            teacher.User.FullName,
            teacher.User.Email,
            teacher.Qualification,
            Circles = teacher.Circles.Select(c => new { c.Id, c.Name, StudentCount = c.Students.Count })
        });
    }

    // POST /api/teachers — Admin فقط
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTeacherRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "الاسم ورقم الهاتف مطلوبان" });

        var (user, tempPassword, err) = await _accounts.CreateUserAsync(
            request.Phone, request.FullName, UserRole.Teacher);

        if (err != null)
            return BadRequest(new { message = err });

        var teacher = new Teacher
        {
            UserId = user.Id,
            Qualification = request.Qualification ?? string.Empty
        };

        _context.Teachers.Add(teacher);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "تم إضافة المحفظ بنجاح",
            teacherId = teacher.Id,
            credentials = new AccountCredentialsDto(
                request.FullName,
                user.UserName!,
                AccountProvisioningService.ToDisplayPhone(user.UserName!),
                tempPassword,
                UserRole.Teacher.ToString(),
                true)
        });
    }

    // PUT /api/teachers/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTeacherRequest request)
    {
        var teacher = await _context.Teachers
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound(new { message = "المحفظ غير موجود" });

        if (!string.IsNullOrEmpty(request.FullName))
            teacher.User.FullName = request.FullName;

        if (!string.IsNullOrEmpty(request.Qualification))
            teacher.Qualification = request.Qualification;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث بيانات المحفظ" });
    }

    // DELETE /api/teachers/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var teacher = await _context.Teachers
            .Include(t => t.User)
            .Include(t => t.Circles)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (teacher == null)
            return NotFound(new { message = "المحفظ غير موجود" });

        // منع الحذف إذا كان المحفظ لديه حلقات نشطة
        if (teacher.Circles.Any())
            return BadRequest(new { message = $"لا يمكن حذف المحفظ لأنه مرتبط بـ {teacher.Circles.Count} حلقة. يرجى إعادة تعيين الحلقات أولاً." });

        var user = teacher.User;
        _context.Teachers.Remove(teacher);
        await _context.SaveChangesAsync();

        await _userManager.DeleteAsync(user);

        return Ok(new { message = "تم حذف المحفظ" });
    }
}

public class CreateTeacherRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Qualification { get; set; }
}

public class UpdateTeacherRequest
{
    public string? FullName { get; set; }
    public string? Qualification { get; set; }
}