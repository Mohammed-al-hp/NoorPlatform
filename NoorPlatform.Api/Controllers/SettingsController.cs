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
            return Ok(new
            {
                centerName = "",
                contactPhone = "",
                email = "",
                address = "",
                workDays = "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء,الخميس",
                workStartTime = "08:00",
                workEndTime = "12:00",
                defaultMonthlyFee = 0m,
                currency = "د.ل"
            });
        }
        return Ok(new
        {
            settings.CenterName,
            settings.ContactPhone,
            settings.Email,
            settings.Address,
            settings.WorkDays,
            settings.WorkStartTime,
            settings.WorkEndTime,
            settings.DefaultMonthlyFee,
            settings.Currency
        });
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
        settings.Email = request.Email?.Trim() ?? string.Empty;
        settings.Address = request.Address?.Trim() ?? string.Empty;
        settings.WorkDays = request.WorkDays?.Trim() ?? "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء,الخميس";
        settings.WorkStartTime = request.WorkStartTime?.Trim() ?? "08:00";
        settings.WorkEndTime = request.WorkEndTime?.Trim() ?? "12:00";
        settings.DefaultMonthlyFee = request.DefaultMonthlyFee ?? 0;
        settings.Currency = request.Currency?.Trim() ?? "د.ل";
        settings.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم حفظ الإعدادات بنجاح" });
    }

    /// <summary>
    /// معلومات النظام العامة — قراءة فقط
    /// </summary>
    [HttpGet("system-info")]
    public async Task<IActionResult> GetSystemInfo()
    {
        var students = await _context.Students.CountAsync();
        var teachers = await _context.Teachers.CountAsync();
        var circles = await _context.Circles.CountAsync();
        var parents = await _context.Parents.CountAsync();
        var hifzRecords = await _context.HifzRecords.CountAsync();
        var attendanceRecords = await _context.Attendances.CountAsync();
        var users = await _context.Users.CountAsync();

        var settings = await _context.PlatformSettings.FirstOrDefaultAsync();

        return Ok(new
        {
            totalStudents = students,
            totalTeachers = teachers,
            totalCircles = circles,
            totalParents = parents,
            totalHifzRecords = hifzRecords,
            totalAttendanceRecords = attendanceRecords,
            totalUsers = users,
            lastSettingsUpdate = settings?.UpdatedAt,
            serverTime = DateTime.UtcNow,
            dotnetVersion = Environment.Version.ToString(),
            platform = $"{Environment.OSVersion.Platform}"
        });
    }
}

public class UpdateSettingsRequest
{
    public string CenterName { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? WorkDays { get; set; }
    public string? WorkStartTime { get; set; }
    public string? WorkEndTime { get; set; }
    public decimal? DefaultMonthlyFee { get; set; }
    public string? Currency { get; set; }
}