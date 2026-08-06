using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class SettingsController : ControllerBase
{
    private readonly NoorDbContext _context;

    public SettingsController(NoorDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            return Ok(new { centerName = "", contactPhone = "" });
        }
        return Ok(new { settings.CenterName, settings.ContactPhone });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CenterName))
            return BadRequest(new { message = "اسم المركز مطلوب" });

        var settings = await _context.PlatformSettings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new PlatformSettings();
            _context.PlatformSettings.Add(settings);
        }

        settings.CenterName = request.CenterName.Trim();
        settings.ContactPhone = request.ContactPhone?.Trim() ?? string.Empty;
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حفظ الإعدادات بنجاح" });
    }
}

public class UpdateSettingsRequest
{
    public string CenterName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
}