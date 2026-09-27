using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/matns")]
[Authorize]
public class MatnsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public MatnsController(NoorDbContext context)
    {
        _context = context;
    }

    // GET /api/matns — المتون النشطة فقط (للاستخدام بنماذج التسجيل)
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
    {
        var query = _context.Matns.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        var matns = await query
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name, m.IsActive })
            .ToListAsync();

        return Ok(matns);
    }

    // POST /api/matns
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateMatnDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "اسم المتن مطلوب" });

        var name = request.Name.Trim();
        var exists = await _context.Matns.AnyAsync(m => m.Name == name);
        if (exists)
            return BadRequest(new { message = "هذا المتن مُضاف مسبقًا" });

        var matn = new Matn { Name = name };
        _context.Matns.Add(matn);
        await _context.SaveChangesAsync();

        return Ok(new { message = "تم إضافة المتن", matn.Id, matn.Name });
    }

    // PUT /api/matns/{id}/toggle — تفعيل/تعطيل بدل الحذف (حفاظًا على السجلات التاريخية المرتبطة بالاسم)
    [HttpPut("{id}/toggle")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Toggle(int id)
    {
        var matn = await _context.Matns.FindAsync(id);
        if (matn == null)
            return NotFound(new { message = "المتن غير موجود" });

        matn.IsActive = !matn.IsActive;
        await _context.SaveChangesAsync();

        return Ok(new { message = matn.IsActive ? "تم تفعيل المتن" : "تم تعطيل المتن", matn.IsActive });
    }
}

public class CreateMatnDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
}