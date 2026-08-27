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
                currency = "د.ل",
                oralMaxOpeningsBeforeFail = 3,
                oralAlertPenalty = 5.0,
                oralOpeningPenalty = 15.0,
                oralHesitationPenalty = 2.0,
                defaultMonthlyAthmanTarget = 8,
                weightAttendance = 1.0,
                weightHifz = 1.0,
                weightRevision = 1.0,
                weightProgress = 1.0,
                weightMatn = 1.0,
                weightDress = 1.0,
                evaluationsVisibleToStudentsAndParents = true
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
            settings.Currency,
            settings.OralMaxOpeningsBeforeFail,
            settings.OralAlertPenalty,
            settings.OralOpeningPenalty,
            settings.OralHesitationPenalty,
            settings.DefaultMonthlyAthmanTarget,
            settings.WeightAttendance,
            settings.WeightHifz,
            settings.WeightRevision,
            settings.WeightProgress,
            settings.WeightMatn,
            settings.WeightDress,
            settings.EvaluationsVisibleToStudentsAndParents
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
        if (request.OralMaxOpeningsBeforeFail.HasValue)
            settings.OralMaxOpeningsBeforeFail = request.OralMaxOpeningsBeforeFail.Value;
        if (request.OralAlertPenalty.HasValue)
            settings.OralAlertPenalty = request.OralAlertPenalty.Value;
        if (request.OralOpeningPenalty.HasValue)
            settings.OralOpeningPenalty = request.OralOpeningPenalty.Value;
        if (request.OralHesitationPenalty.HasValue)
            settings.OralHesitationPenalty = request.OralHesitationPenalty.Value;
        if (request.DefaultMonthlyAthmanTarget.HasValue)
            settings.DefaultMonthlyAthmanTarget = request.DefaultMonthlyAthmanTarget.Value;
        if (request.WeightAttendance.HasValue)
            settings.WeightAttendance = request.WeightAttendance.Value;
        if (request.WeightHifz.HasValue)
            settings.WeightHifz = request.WeightHifz.Value;
        if (request.WeightRevision.HasValue)
            settings.WeightRevision = request.WeightRevision.Value;
        if (request.WeightProgress.HasValue)
            settings.WeightProgress = request.WeightProgress.Value;
        if (request.WeightMatn.HasValue)
            settings.WeightMatn = request.WeightMatn.Value;
        if (request.WeightDress.HasValue)
            settings.WeightDress = request.WeightDress.Value;
        if (request.EvaluationsVisibleToStudentsAndParents.HasValue)
            settings.EvaluationsVisibleToStudentsAndParents = request.EvaluationsVisibleToStudentsAndParents.Value;
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
    public int? OralMaxOpeningsBeforeFail { get; set; }
    public double? OralAlertPenalty { get; set; }
    public double? OralOpeningPenalty { get; set; }
    public double? OralHesitationPenalty { get; set; }
    public int? DefaultMonthlyAthmanTarget { get; set; }
    public double? WeightAttendance { get; set; }
    public double? WeightHifz { get; set; }
    public double? WeightRevision { get; set; }
    public double? WeightProgress { get; set; }
    public double? WeightMatn { get; set; }
    public double? WeightDress { get; set; }
    public bool? EvaluationsVisibleToStudentsAndParents { get; set; }
}