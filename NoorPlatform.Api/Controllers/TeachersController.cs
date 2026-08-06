using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;
using System.Security.Claims;

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

    // GET /api/teachers?search=
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetTeachers([FromQuery] string? search)
    {
        var query = _context.Teachers
            .Include(t => t.User)
            .Include(t => t.Circles).ThenInclude(c => c.Students)
            .AsQueryable();
        // ─── إصلاح: تقييد المحفّظ برؤية نفسه فقط ───
        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacher)
        {
            var userId = int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);
            query = query.Where(t => t.UserId == userId);
        }

        // ✅ جديد: دعم البحث
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(t =>
                t.User.FullName.Contains(term) ||
                t.Qualification.Contains(term));
        }

        var teachers = await query
            .Select(t => new
            {
                t.Id,
                t.User.FullName,
                t.User.Email,
                CircleName = t.Circles.Any() ? t.Circles.First().Name : "بدون حلقة",
                t.Qualification,
                t.BirthDate,
                // ✅ جديد: إرجاع AverageRating
                AverageRating = t.AverageRating > 0 ? t.AverageRating : 0.0,
                StudentCount = t.Circles.Sum(c => c.Students.Count)
            })
            .ToListAsync();

        return Ok(teachers);
    }

    // GET /api/teachers/{id}
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Teacher")]
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
            teacher.BirthDate,
            teacher.AverageRating,
            Circles = teacher.Circles.Select(c => new { c.Id, c.Name, StudentCount = c.Students.Count })
        });
    }

    // POST /api/teachers — Admin فقط
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateTeacherRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone))
            return BadRequest(new { message = "الاسم الثلاثي ورقم الهاتف مطلوبان" });

        var (user, tempPassword, err) = await _accounts.CreateUserAsync(
            request.Phone, request.FullName, UserRole.Teacher);

        if (err != null)
            return BadRequest(new { message = err });

        var teacher = new Teacher
        {
            UserId = user.Id,
            Qualification = request.Qualification ?? string.Empty,
            BirthDate = request.BirthDate,
            AverageRating = 0.0
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

        // ✅ إصلاح: السماح بتفريغ Qualification بـ ""
        if (request.Qualification != null)
            teacher.Qualification = request.Qualification;

        // ✅ جديد: تحديث BirthDate
        if (request.BirthDate.HasValue)
            teacher.BirthDate = request.BirthDate;

        // ✅ جديد: تحديث التقييم يدويًا (بحد أقصى 5)
        if (request.AverageRating.HasValue)
            teacher.AverageRating = Math.Clamp(request.AverageRating.Value, 0, 5);

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

        if (teacher.Circles.Any())
            return BadRequest(new { message = $"لا يمكن أرشفة المحفظ لأنه مرتبط بـ {teacher.Circles.Count} حلقة. يرجى إعادة تعيين الحلقات أولاً." });

        // ─── إصلاح: أرشفة (Soft Delete) بدل الحذف الفعلي — للحفاظ على السجلات التاريخية ───
        teacher.IsDeleted = true;
        teacher.User.IsActive = false;
        await _userManager.UpdateSecurityStampAsync(teacher.User);

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم أرشفة المحفظ" });
    }
    // GET /api/teachers/archived
    [HttpGet("archived")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetArchivedTeachers()
    {
        var teachers = await _context.Teachers
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .Include(t => t.User)
            .Select(t => new
            {
                t.Id,
                t.User.FullName,
                t.Qualification,
                t.User.UserName
            })
            .ToListAsync();

        return Ok(teachers);
    }

    // POST /api/teachers/{id}/restore
    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreTeacher(int id)
    {
        var teacher = await _context.Teachers
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.IsDeleted);

        if (teacher == null)
            return NotFound(new { message = "المحفظ غير موجود في الأرشيف" });

        teacher.IsDeleted = false;
        teacher.User.IsActive = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم استعادة المحفظ بنجاح" });
    }
}

public class CreateTeacherRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    // ✅ جديد
    public DateOnly? BirthDate { get; set; }
}

public class UpdateTeacherRequest
{
    public string? FullName { get; set; }
    public string? Qualification { get; set; }
    // ✅ جديد
    public DateOnly? BirthDate { get; set; }
    public double? AverageRating { get; set; }
}