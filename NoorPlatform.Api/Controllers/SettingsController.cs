using NoorPlatform.Api.Services;
using NoorPlatform.Api.Security;
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
                address = "",
                workDays = "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء,الخميس",
                workStartTime = "08:00",
                workEndTime = "12:00",
                defaultMonthlyFee = 0m,
                currency = "د.ل",
                oralMaxOpeningsBeforeFail = 1,
                oralAlertPenalty = 1.0,
                oralOpeningPenalty = 2.0,
                oralHesitationPenalty = 0.5,
                defaultMonthlyAthmanTarget = 8,
                progressThreshold10 = 0.75,
                progressThreshold9 = 0.625,
                progressThreshold8 = 0.5,
                progressThreshold6 = 0.375,
                progressThreshold4 = 0.25,
                weightAttendance = 1.0,
                weightHifz = 1.0,
                weightRevision = 1.0,
                weightProgress = 1.0,
                weightMatn = 1.0,
                weightDress = 1.0,
                evaluationsVisibleToStudentsAndParents = true,
                pointsAttendancePresent = 10,
                pointsHifzMemorization = 50,
                pointsHifzRevision = 20,
                pointsAttendanceLate = 5,
                pointsAttendanceExcused = 0,
                pointsAttendanceUnexcused = 0,
                pointsHifzMemVeryGood = 40,
                pointsHifzMemGood = 30,
                pointsHifzRevVeryGood = 15,
                pointsHifzRevGood = 10,
                pointsHifzRevAcceptable = 5,
                pointsMatnMemorization = 20,
                enableMatn = true,
                enableDress = true,
                enablePrayer = true,
                enableParentHome = true,
                hifzRatingTargetAge5To7 = 4,
                hifzRatingTargetAge8To10 = 6,
                hifzRatingTargetAge11Plus = 8,
                otpChannel = "WhatsApp",
                studentRemindersEnabled = true
            });
        }
        if (settings.ProgressThreshold10 <= 0)
        {
            settings.ProgressThreshold10 = 0.75;
            settings.ProgressThreshold9 = 0.625;
            settings.ProgressThreshold8 = 0.5;
            settings.ProgressThreshold6 = 0.375;
            settings.ProgressThreshold4 = 0.25;
            await _context.SaveChangesAsync();
        }
        return Ok(new
        {
            settings.CenterName,
            settings.ContactPhone,
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
            settings.ProgressThreshold10,
            settings.ProgressThreshold9,
            settings.ProgressThreshold8,
            settings.ProgressThreshold6,
            settings.ProgressThreshold4,
            settings.WeightAttendance,
            settings.WeightHifz,
            settings.WeightRevision,
            settings.WeightProgress,
            settings.WeightMatn,
            settings.WeightDress,
            settings.EvaluationsVisibleToStudentsAndParents,
            settings.PointsAttendancePresent,
            settings.PointsHifzMemorization,
            settings.PointsHifzRevision,
            settings.PointsAttendanceLate,
            settings.PointsAttendanceExcused,
            settings.PointsAttendanceUnexcused,
            settings.PointsHifzMemVeryGood,
            settings.PointsHifzMemGood,
            settings.PointsHifzRevVeryGood,
            settings.PointsHifzRevGood,
            settings.PointsHifzRevAcceptable,
            settings.PointsMatnMemorization,
            settings.EnableMatn,
            settings.EnableDress,
            settings.EnablePrayer,
            settings.EnableParentHome,
            settings.HifzRatingTargetAge5To7,
            settings.HifzRatingTargetAge8To10,
            settings.HifzRatingTargetAge11Plus,
            settings.OtpChannel,
            settings.StudentRemindersEnabled
        });
    }

    /// <summary>
    /// إعدادات عامة غير حساسة: اسم المركز (للشريط الجانبي وشاشة الدخول)، قنوات رمز التحقق، وتفعيل التذكيرات.
    /// متاحة دون تسجيل دخول لأن شاشة «نسيت كلمة المرور» تحتاج القنوات المتاحة.
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic()
    {
        var s = await _context.PlatformSettings.AsNoTracking()
            .Select(x => new { x.CenterName, x.OtpChannel, x.StudentRemindersEnabled })
            .FirstOrDefaultAsync();
        var centerName = string.IsNullOrWhiteSpace(s?.CenterName) ? "مركز تحفيظ القرآن الكريم" : s!.CenterName;
        return Ok(new
        {
            centerName,
            tagline = "لإدارة مركز وحلقات تحفيظ القرآن الكريم",
            otpChannels = OtpChannels.Available(s?.OtpChannel),
            studentRemindersEnabled = s?.StudentRemindersEnabled ?? true
        });
    }

    [HttpPut]
    [RequireAdminPermission(AdminPermission.ManageSettings)]
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
        if (request.ProgressThreshold10.HasValue)
            settings.ProgressThreshold10 = request.ProgressThreshold10.Value;
        if (request.ProgressThreshold9.HasValue)
            settings.ProgressThreshold9 = request.ProgressThreshold9.Value;
        if (request.ProgressThreshold8.HasValue)
            settings.ProgressThreshold8 = request.ProgressThreshold8.Value;
        if (request.ProgressThreshold6.HasValue)
            settings.ProgressThreshold6 = request.ProgressThreshold6.Value;
        if (request.ProgressThreshold4.HasValue)
            settings.ProgressThreshold4 = request.ProgressThreshold4.Value;
        var t = new[] { settings.ProgressThreshold10, settings.ProgressThreshold9, settings.ProgressThreshold8, settings.ProgressThreshold6, settings.ProgressThreshold4 };
        if (t.Any(x => x <= 0 || x > 1) || t.Zip(t.Skip(1)).Any(p => p.First < p.Second))
            return BadRequest(new { message = "نسب الهدف الشهري يجب أن تكون بين 1% و100% وتتناقص من درجة 10 إلى درجة 4" });
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
        // نقاط الحضور: يُسمح بقيمة سالبة للغياب (خصم)، ورصيد الطالب لا ينزل عن صفر
        static int Att(int v) => Math.Clamp(v, -100, 1000);
        static int Pts(int v) => Math.Clamp(v, 0, 1000);
        if (request.PointsAttendancePresent.HasValue) settings.PointsAttendancePresent = Att(request.PointsAttendancePresent.Value);
        if (request.PointsAttendanceLate.HasValue) settings.PointsAttendanceLate = Att(request.PointsAttendanceLate.Value);
        if (request.PointsAttendanceExcused.HasValue) settings.PointsAttendanceExcused = Att(request.PointsAttendanceExcused.Value);
        if (request.PointsAttendanceUnexcused.HasValue) settings.PointsAttendanceUnexcused = Att(request.PointsAttendanceUnexcused.Value);
        if (request.PointsHifzMemorization.HasValue) settings.PointsHifzMemorization = Pts(request.PointsHifzMemorization.Value);
        if (request.PointsHifzMemVeryGood.HasValue) settings.PointsHifzMemVeryGood = Pts(request.PointsHifzMemVeryGood.Value);
        if (request.PointsHifzMemGood.HasValue) settings.PointsHifzMemGood = Pts(request.PointsHifzMemGood.Value);
        if (request.PointsHifzRevision.HasValue) settings.PointsHifzRevision = Pts(request.PointsHifzRevision.Value);
        if (request.PointsHifzRevVeryGood.HasValue) settings.PointsHifzRevVeryGood = Pts(request.PointsHifzRevVeryGood.Value);
        if (request.PointsHifzRevGood.HasValue) settings.PointsHifzRevGood = Pts(request.PointsHifzRevGood.Value);
        if (request.PointsHifzRevAcceptable.HasValue) settings.PointsHifzRevAcceptable = Pts(request.PointsHifzRevAcceptable.Value);
        if (request.PointsMatnMemorization.HasValue) settings.PointsMatnMemorization = Pts(request.PointsMatnMemorization.Value);
        if (request.EnableMatn.HasValue) settings.EnableMatn = request.EnableMatn.Value;
        if (request.EnableDress.HasValue) settings.EnableDress = request.EnableDress.Value;
        if (request.EnablePrayer.HasValue) settings.EnablePrayer = request.EnablePrayer.Value;
        if (request.EnableParentHome.HasValue) settings.EnableParentHome = request.EnableParentHome.Value;
        if (request.HifzRatingTargetAge5To7.HasValue)
            settings.HifzRatingTargetAge5To7 = Math.Clamp(request.HifzRatingTargetAge5To7.Value, 1, 60);
        if (request.HifzRatingTargetAge8To10.HasValue)
            settings.HifzRatingTargetAge8To10 = Math.Clamp(request.HifzRatingTargetAge8To10.Value, 1, 60);
        if (request.HifzRatingTargetAge11Plus.HasValue)
            settings.HifzRatingTargetAge11Plus = Math.Clamp(request.HifzRatingTargetAge11Plus.Value, 1, 60);
        if (request.HifzRatingPassingEvaluations != null)
        {
            var passing = string.Join(",", request.HifzRatingPassingEvaluations
                .Split(new[] { ',', '،' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct());
            if (passing.Length == 0)
                return BadRequest(new { message = "حدد تقييماً واحداً على الأقل يُعدّ ناجحاً" });
            settings.HifzRatingPassingEvaluations = passing;
        }
        if (request.OtpChannel != null)
        {
            if (!OtpChannels.IsValidSetting(request.OtpChannel))
                return BadRequest(new { message = "قناة رمز التحقق غير صالحة (واتساب، SMS، أو كلاهما)" });
            settings.OtpChannel = request.OtpChannel;
        }
        if (request.StudentRemindersEnabled.HasValue)
            settings.StudentRemindersEnabled = request.StudentRemindersEnabled.Value;
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
    public double? ProgressThreshold10 { get; set; }
    public double? ProgressThreshold9 { get; set; }
    public double? ProgressThreshold8 { get; set; }
    public double? ProgressThreshold6 { get; set; }
    public double? ProgressThreshold4 { get; set; }
    public double? WeightAttendance { get; set; }
    public double? WeightHifz { get; set; }
    public double? WeightRevision { get; set; }
    public double? WeightProgress { get; set; }
    public double? WeightMatn { get; set; }
    public double? WeightDress { get; set; }
    public bool? EvaluationsVisibleToStudentsAndParents { get; set; }
    public int? PointsAttendancePresent { get; set; }
    public int? PointsHifzMemorization { get; set; }
    public int? PointsHifzRevision { get; set; }
    public int? PointsAttendanceLate { get; set; }
    public int? PointsAttendanceExcused { get; set; }
    public int? PointsAttendanceUnexcused { get; set; }
    public int? PointsHifzMemVeryGood { get; set; }
    public int? PointsHifzMemGood { get; set; }
    public int? PointsHifzRevVeryGood { get; set; }
    public int? PointsHifzRevGood { get; set; }
    public int? PointsHifzRevAcceptable { get; set; }
    public int? PointsMatnMemorization { get; set; }
    public bool? EnableMatn { get; set; }
    public bool? EnableDress { get; set; }
    public bool? EnablePrayer { get; set; }
    public bool? EnableParentHome { get; set; }
    public int? HifzRatingTargetAge5To7 { get; set; }
    public int? HifzRatingTargetAge8To10 { get; set; }
    public int? HifzRatingTargetAge11Plus { get; set; }
    public string? HifzRatingPassingEvaluations { get; set; }
    /// <summary>WhatsApp | Sms | Both</summary>
    public string? OtpChannel { get; set; }
    public bool? StudentRemindersEnabled { get; set; }
}