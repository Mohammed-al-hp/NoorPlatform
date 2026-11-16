using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Api.Security;
using NoorPlatform.Api.Services;
using NoorPlatform.Core.Entities;
using NoorPlatform.Core.Services;
using NoorPlatform.Infrastructure.Data;
using System.Security.Claims;

namespace NoorPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly NoorDbContext _context;
    private readonly AccountProvisioningService _accounts;
    private readonly StudentRestorationService _restoration;

    public StudentsController(NoorDbContext context, AccountProvisioningService accounts, StudentRestorationService restoration)
    {
        _context = context;
        _accounts = accounts;
        _restoration = restoration;
    }

    // GET /api/students
    [HttpGet]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetAll()
    {
        var query = StudentQueryHelper.ScopeForUser(_context, User);

        var rawResult = await query.Select(s => new
        {
            s.Id,
            fullName = s.User.FullName,
            s.User.Email,
            s.ParentPhone,
            s.GuardianName,
            s.ParentId,
            parentName = s.Parent != null && s.Parent.User != null ? s.Parent.User.FullName : s.GuardianName,
            s.CircleId,
            circleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
            teacherName = s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.User != null ? s.Circle.Teacher.User.FullName : "—",
            s.Level,
            attendance = s.Attendances.Any()
                ? (int)Math.Round(
    (s.Attendances.Count(a => a.Status == AttendanceStatus.Present)
     + AttendanceScore.LateWeight * s.Attendances.Count(a => a.Status == AttendanceStatus.Late)
     + AttendanceScore.ExcusedWeight * s.Attendances.Count(a => a.Status == AttendanceStatus.ExcusedAbsence))
    / (double)s.Attendances.Count * 100)
                : 0,
            s.DateOfBirth,
            // أداء: جمع عدد الآيات في قاعدة البيانات بدل تحميل كل سجلات التسميع لكل طالب،
            // ولا يُحمَّل نص الآيات إلا للسجلات القديمة التي لا تحمل VerseCount
            MemorizedVerseSum = s.HifzRecords
                .Where(r => r.Type == RecordType.Memorization && r.VerseCount > 0)
                .Sum(r => (int?)r.VerseCount) ?? 0,
            LegacyVerses = s.HifzRecords
                .Where(r => r.Type == RecordType.Memorization && r.VerseCount <= 0)
                .Select(r => r.Verses)
                .ToList()
        }).AsSplitQuery().ToListAsync();

        var result = rawResult.Select(s => new
        {
            s.Id,
            s.fullName,
            s.Email,
            s.ParentPhone,
            s.GuardianName,
            s.ParentId,
            s.parentName,
            s.CircleId,
            s.circleName,
            s.teacherName,
            s.Level,
            s.attendance,
            dateOfBirth = s.DateOfBirth.HasValue ? s.DateOfBirth.Value.ToString("yyyy-MM-dd") : null,
            progress = Math.Min(100.0, Math.Round(
                (double)(s.MemorizedVerseSum + s.LegacyVerses.Sum(HifzRecord.ParseVerseCount)) / 6236.0 * 100, 1))
        });

        return Ok(result);
    }

    [HttpGet("count")]
    [Authorize(Roles = "Admin,Teacher")]
    public async Task<IActionResult> GetCount()
    {
        var count = await StudentQueryHelper.ScopeForUser(_context, User).CountAsync();
        return Ok(new { count });
    }

    // GET /api/students/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, id))
            return Forbid();

        var student = await _context.Students
            .Include(s => s.User)
            .Include(s => s.Circle).ThenInclude(c => c!.Teacher).ThenInclude(t => t!.User)
            .Include(s => s.Parent).ThenInclude(p => p!.User)
            .Include(s => s.Attendances)
            .Include(s => s.HifzRecords)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        return Ok(new
        {
            student.Id,
            fullName = student.User.FullName,
            student.User.Email,
            student.Level,
            teacherName = student.Circle?.Teacher?.User?.FullName ?? "—",
            circleName = student.Circle?.Name ?? "بدون حلقة",
            circleId = student.CircleId,
            parentName = !string.IsNullOrWhiteSpace(student.GuardianName)
                ? student.GuardianName
                : student.Parent?.User?.FullName ?? "—",
            parentPhone = student.ParentPhone,
            guardianRelationship = student.GuardianRelationship?.ToString(),
            dateOfBirth = student.DateOfBirth?.ToString("yyyy-MM-dd"),
            registrationDate = student.RegistrationDate.ToString("yyyy-MM-dd"),
            studentPhone = student.StudentPhone,
            residence = student.Residence,
            attendance = student.Attendances.Any()
               ? (int)Math.Round(AttendanceScore.Percent(student.Attendances.Select(a => a.Status)))
                : 0,
            progress = HifzProgressCalculator.Calculate(student.HifzRecords),
            recentHifz = student.HifzRecords
                                .OrderByDescending(r => r.Date)
                                .Take(5)
                                .Select(r => new { r.SurahName, r.Verses, r.VerseCount, r.Type, r.Evaluation, r.Date })
        });
    }

    // POST /api/students
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateStudentRequest request)
    {
        // ─── إصلاح: إن أُرسل ParentId جاهز، لا حاجة لبيانات ولي أمر جديد ───
        var linkingExistingParent = request.ParentId.HasValue;

        if (!linkingExistingParent)
        {
            var validationError = ValidateStudentPayload(request.FullName, request.DateOfBirth, request.GuardianName,
                request.ParentPhone, request.GuardianRelationship, request.RegistrationDate);
            if (validationError != null)
                return BadRequest(new { message = validationError });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || request.DateOfBirth == null || request.RegistrationDate == null)
                return BadRequest(new { message = "الاسم الثلاثي وتاريخ الميلاد وتاريخ التسجيل مطلوبة" });

            var parentId = request.ParentId!.Value;
            var parentExists = await _context.Parents.AnyAsync(p => p.Id == parentId && !p.IsDeleted);
            if (!parentExists)
                return BadRequest(new { message = "ولي الأمر المحدد غير موجود" });
        }

        // ─── إصلاح: منع المحفّظ من إضافة طالب لحلقة خارج نطاقه ───
        var isTeacher = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        // المحفّظ يضيف الطالب إلى إحدى حلقاته دائماً، وإلا يختفي الطالب من قائمته بعد الحفظ
        if (isTeacher && !request.CircleId.HasValue)
            return BadRequest(new { message = "اختر إحدى حلقاتك لإضافة الطالب إليها" });
        if (!linkingExistingParent && !LibyanPhone.IsValid(request.ParentPhone))
            return BadRequest(new { message = "رقم هاتف ولي الأمر غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });
        if (isTeacher && request.CircleId.HasValue)
        {
            var ownsCircle = await _context.Circles.AnyAsync(c =>
                c.Id == request.CircleId.Value &&
                c.Teacher != null &&
                c.Teacher.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));

            if (!ownsCircle)
                return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.Phone) && !LibyanPhone.IsValid(request.Phone))
            return BadRequest(new { message = "رقم هاتف الطالب غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });

        // ─── عودة الطالب بعد المغادرة: استعادة الحساب المؤرشف بدل إنشاء حساب جديد ───
        var isAdminUser = User.IsInRole("Admin");
        if (request.RestoreArchivedStudentId.HasValue)
        {
            if (!isAdminUser)
                return StatusCode(403, new { message = "استعادة الطلاب السابقين متاحة للإدارة فقط" });
            return await RestoreOnReRegistrationAsync(request.RestoreArchivedStudentId.Value, request, linkingExistingParent);
        }

        if (!request.IgnoreArchivedMatch)
        {
            var parentPhoneForMatch = linkingExistingParent
                ? await _context.Parents.Where(p => p.Id == request.ParentId!.Value).Select(p => p.Phone).FirstOrDefaultAsync()
                : request.ParentPhone;
            var archived = await _restoration.FindArchivedMatchAsync(request.Phone, request.FullName, parentPhoneForMatch);
            if (archived != null)
                return ArchivedMatchConflict(archived, isAdminUser);
        }

        var phoneConflict = await _restoration.GetActivePhoneConflictAsync(request.Phone);
        if (phoneConflict != null)
            return BadRequest(new { message = phoneConflict });

        await using var tx = await _context.Database.BeginTransactionAsync();
        var (user, tempPassword, err) = string.IsNullOrWhiteSpace(request.Phone)
    ? await _accounts.CreateUserAsync("", request.FullName.Trim(), UserRole.Student,
        null, await _accounts.GenerateStudentUsernameAsync())
    : await _accounts.CreateUserAsync(request.Phone, request.FullName.Trim(), UserRole.Student);

        if (err != null)
            return BadRequest(new { message = err });

        Parent? parent = null;
        string? parentTempPassword = null;
        if (linkingExistingParent)
        {
            parent = await _context.Parents.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == request.ParentId!.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.ParentPhone))
        {
            var (p, pPass, pErr) = await _accounts.EnsureParentAsync(
                request.GuardianName,
                request.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
            parentTempPassword = pPass;
        }

        GuardianRelationship? relationship = null;
        if (!linkingExistingParent)
        {
            if (!Enum.TryParse<GuardianRelationship>(request.GuardianRelationship, true, out var parsedRelationship))
                return BadRequest(new { message = "صلة القرابة غير صالحة" });
            relationship = parsedRelationship;
        }

        var student = new Student
        {
            UserId = user.Id,
            
            Level = request.Level ?? "مبتدئ",
            CircleId = request.CircleId,
            ParentPhone = linkingExistingParent ? (parent?.Phone ?? string.Empty) : AccountProvisioningService.NormalizePhone(request.ParentPhone),
            GuardianName = linkingExistingParent ? (parent?.User?.FullName ?? string.Empty) : request.GuardianName.Trim(),
            GuardianRelationship = relationship,
            DateOfBirth = request.DateOfBirth,
            RegistrationDate = request.RegistrationDate ?? LibyaTime.Today,
            StudentPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : AccountProvisioningService.NormalizePhone(request.Phone),
            Residence = string.IsNullOrWhiteSpace(request.Residence) ? null : request.Residence.Trim()
        };

        // ─── إصلاح حرج: استخدام خاصية العلاقة (Navigation) بدل الـ Id الخام
        // لأن parent الجديد لم يُحفظ بعد (Id = 0)، وربط الـ FK مباشرة يسبب فشل الحفظ ───
        if (parent != null)
        {
            student.Parent = parent;
        }
        else if (request.ParentId.HasValue)
        {
            student.ParentId = request.ParentId;
        }

        _context.Students.Add(student);

        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "User",
            ActivityType = "Student",
            Description = $"تمت إضافة الطالب الجديد {request.FullName}",
            Icon = "🎓",
            Color = "blue"
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "تم إضافة الطالب بنجاح",
            studentId = student.Id,
            userId = user.Id,
            parentCredentials = (parentTempPassword == null || parent == null) ? null : new AccountCredentialsDto(
    parent.User.FullName,
    parent.User.UserName!,
    AccountProvisioningService.ToDisplayPhone(parent.User.UserName!),
    parentTempPassword,
    UserRole.Parent.ToString(),
    true),
            credentials = new AccountCredentialsDto(
                request.FullName,
                user.UserName!,
                AccountProvisioningService.ToDisplayPhone(user.UserName!),
                tempPassword,
                UserRole.Student.ToString(),
                true)
        });
    }

    private ObjectResult ArchivedMatchConflict(Student archived, bool canRestore) => Conflict(new
    {
        code = StudentRestorationService.ArchivedMatchCode,
        canRestore,
        message = canRestore
            ? "هذا طالب سابق — استعادة بياناته؟"
            : "هذا طالب سابق مؤرشف. يرجى التواصل مع الإدارة لاستعادة حسابه.",
        archivedStudent = StudentRestorationService.ToMatchDto(archived)
    });

    /// <summary>
    /// يعيد تفعيل طالب مؤرشف ويحدّث بياناته بالبيانات الجديدة مع إبقاء سجلاته التاريخية.
    /// </summary>
    private async Task<IActionResult> RestoreOnReRegistrationAsync(int archivedId, CreateStudentRequest request, bool linkingExistingParent)
    {
        var student = await _context.Students.IgnoreQueryFilters()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == archivedId);
        if (student == null)
            return NotFound(new { message = "الطالب السابق غير موجود" });
        if (!student.IsDeleted)
            return BadRequest(new { message = "هذا الطالب نشط بالفعل ولا يحتاج استعادة" });

        var newPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : AccountProvisioningService.NormalizePhone(request.Phone);
        var phoneConflict = await _restoration.GetActivePhoneConflictAsync(newPhone, student.UserId);
        if (phoneConflict != null)
            return BadRequest(new { message = phoneConflict });

        GuardianRelationship? relationship = student.GuardianRelationship;
        if (!linkingExistingParent)
        {
            if (!Enum.TryParse<GuardianRelationship>(request.GuardianRelationship, true, out var parsed))
                return BadRequest(new { message = "صلة القرابة غير صالحة" });
            relationship = parsed;
        }

        await using var tx = await _context.Database.BeginTransactionAsync();

        Parent? parent;
        string? parentTempPassword = null;
        if (linkingExistingParent)
        {
            parent = await _context.Parents.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == request.ParentId!.Value);
        }
        else
        {
            var (p, pPass, pErr) = await _accounts.EnsureParentAsync(request.GuardianName, request.ParentPhone);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            parent = p;
            parentTempPassword = pPass;
        }

        // اسم الدخول يتحول إلى رقم الطالب الجديد إن أُدخل (وإلا يبقى كما هو، مثل st123456)
        if (newPhone != null && student.User.UserName != newPhone)
        {
            student.User.UserName = newPhone;
            student.User.NormalizedUserName = newPhone.ToUpperInvariant();
            student.User.PhoneNumber = newPhone;
        }

        student.User.FullName = request.FullName.Trim();
        student.Level = request.Level ?? student.Level;
        student.CircleId = request.CircleId;
        student.DateOfBirth = request.DateOfBirth ?? student.DateOfBirth;
        student.RegistrationDate = request.RegistrationDate ?? LibyaTime.Today;
        student.StudentPhone = newPhone ?? student.StudentPhone;
        student.Residence = string.IsNullOrWhiteSpace(request.Residence) ? student.Residence : request.Residence.Trim();
        student.GuardianRelationship = relationship;
        if (parent != null)
        {
            student.Parent = parent;
            student.ParentPhone = parent.Phone;
            student.GuardianName = linkingExistingParent ? parent.User.FullName : request.GuardianName.Trim();
        }

        var (tempPassword, rErr) = await _restoration.ReactivateAsync(student, User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (rErr != null)
            return BadRequest(new { message = rErr });

        _context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "استعادة طالب",
            Description = $"عاد الطالب السابق {student.User.FullName} وتمت استعادة بياناته",
            Icon = "🔄",
            Color = "text-green-500"
        });

        await _context.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new
        {
            message = "تمت استعادة الطالب السابق وتحديث بياناته مع الإبقاء على سجلاته السابقة",
            restored = true,
            studentId = student.Id,
            userId = student.UserId,
            parentCredentials = (parentTempPassword == null || parent == null) ? null : new AccountCredentialsDto(
                parent.User.FullName,
                parent.User.UserName!,
                AccountProvisioningService.ToDisplayPhone(parent.User.UserName!),
                parentTempPassword,
                UserRole.Parent.ToString(),
                true),
            credentials = new AccountCredentialsDto(
                student.User.FullName,
                student.User.UserName!,
                AccountProvisioningService.ToDisplayPhone(student.User.UserName!),
                tempPassword!,
                UserRole.Student.ToString(),
                true)
        });
    }

    // PUT /api/students/{id}
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateStudentRequest request)
    {
        if (!await AuthorizationHelpers.CanAccessStudentAsync(_context, User, id))
            return Forbid();

        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        var validationError = ValidateStudentPayload(request.FullName, request.DateOfBirth, request.GuardianName,
            request.ParentPhone, request.GuardianRelationship, request.RegistrationDate);
        if (validationError != null)
            return BadRequest(new { message = validationError });

        if (!LibyanPhone.IsValid(request.ParentPhone))
            return BadRequest(new { message = "رقم هاتف ولي الأمر غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });
        if (!string.IsNullOrWhiteSpace(request.StudentPhone) && !LibyanPhone.IsValid(request.StudentPhone))
            return BadRequest(new { message = "رقم هاتف الطالب غير صالح. يجب أن يبدأ بـ 09 ويتكون من 10 أرقام" });

        // ─── إصلاح: منع المحفّظ من نقل طالب إلى حلقة خارج نطاقه ───
        var isTeacherUpdate = User.IsInRole("Teacher") && !User.IsInRole("Admin");
        if (isTeacherUpdate)
        {
            // تعديل البيانات الشخصية لطلاب الحلقات الرسمية للمحفّظ فقط (لا طلاب الحلقات الإضافية)
            var teacherUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var ownsPrimary = await _context.Students.AnyAsync(s => s.Id == id &&
                s.Circle != null && s.Circle.Teacher != null && s.Circle.Teacher.UserId == teacherUserId);
            if (!ownsPrimary)
                return StatusCode(403, new { message = "يمكنك تعديل بيانات طلاب حلقاتك الرسمية فقط" });
            if (!request.CircleId.HasValue)
                return BadRequest(new { message = "لا يمكن إخراج الطالب من الحلقات — تواصل مع الإدارة لنقله" });
        }
        if (isTeacherUpdate && request.CircleId.HasValue && request.CircleId != student.CircleId)
        {
            var ownsTargetCircle = await _context.Circles.AnyAsync(c =>
                c.Id == request.CircleId.Value &&
                c.Teacher != null &&
                c.Teacher.UserId == int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));

            if (!ownsTargetCircle)
                return Forbid();
        }

        student.User.FullName = request.FullName!.Trim();
        student.Level = request.Level ?? student.Level;
        student.CircleId = request.CircleId;
        student.ParentPhone = AccountProvisioningService.NormalizePhone(request.ParentPhone!);
        student.GuardianName = request.GuardianName!.Trim();
        student.DateOfBirth = request.DateOfBirth;
        student.RegistrationDate = request.RegistrationDate ?? student.RegistrationDate;
        student.Residence = string.IsNullOrWhiteSpace(request.Residence) ? null : request.Residence.Trim();
        var newStudentPhone = string.IsNullOrWhiteSpace(request.StudentPhone)
            ? null
            : AccountProvisioningService.NormalizePhone(request.StudentPhone);
        if (newStudentPhone != null && newStudentPhone != student.StudentPhone)
        {
            var conflict = await _restoration.GetActivePhoneConflictAsync(newStudentPhone, student.UserId);
            if (conflict != null)
                return BadRequest(new { message = conflict });
            // رقم الطالب هو اسم الدخول: نحدّثه معه حتى يدخل الطالب برقمه الجديد
            student.User.UserName = newStudentPhone;
            student.User.NormalizedUserName = newStudentPhone.ToUpperInvariant();
            student.User.PhoneNumber = newStudentPhone;
        }
        // عند حذف الرقم يبقى اسم الدخول الحالي كما هو (الحساب يحتاج اسم دخول دائماً)
        student.StudentPhone = newStudentPhone;

        if (!Enum.TryParse<GuardianRelationship>(request.GuardianRelationship, true, out var relationship))
            return BadRequest(new { message = "صلة القرابة غير صالحة" });
        student.GuardianRelationship = relationship;

        if (!string.IsNullOrWhiteSpace(request.ParentName))
        {
            var (p, _, pErr) = await _accounts.EnsureParentAsync(request.ParentName.Trim(), request.ParentPhone!);
            if (pErr != null)
                return BadRequest(new { message = pErr });
            student.Parent = p;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم تحديث بيانات الطالب" });
    }

    private static string? ValidateStudentPayload(
        string? fullName,
        DateTime? dateOfBirth,
        string? guardianName,
        string? parentPhone,
        string? guardianRelationship,
        DateTime? registrationDate)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "الاسم الثلاثي مطلوب";
        if (fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3)
            return "يرجى إدخال الاسم الثلاثي كاملاً";
        if (dateOfBirth == null)
            return "تاريخ الميلاد مطلوب";
        if (string.IsNullOrWhiteSpace(guardianName))
            return "اسم ولي الأمر مطلوب";
        if (string.IsNullOrWhiteSpace(parentPhone))
            return "رقم هاتف ولي الأمر مطلوب";
        if (string.IsNullOrWhiteSpace(guardianRelationship))
            return "صلة القرابة مطلوبة";
        if (registrationDate == null)
            return "تاريخ التسجيل مطلوب";
        return null;
    }

    // DELETE /api/students/{id}
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [RequireAdminPermission(AdminPermission.DeleteRecords)]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await _context.Students.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == id);
        if (student == null)
            return NotFound(new { message = "الطالب غير موجود" });

        student.IsDeleted = true;
        student.User.IsActive = false;

        _context.ActivityFeeds.Add(new ActivityFeed {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "أرشفة طالب",
            Description = $"تم أرشفة الطالب {student.User.FullName}",
            Icon = "📦",
            Color = "text-gray-500"
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم أرشفة الطالب بنجاح" });
    }

    [HttpGet("archived")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetArchivedStudents()
    {
        var students = await _context.Students
            .IgnoreQueryFilters()
            .Where(s => s.IsDeleted)
            .Include(s => s.User)
            .Include(s => s.Circle)
            .Select(s => new
            {
                s.Id,
                s.User.FullName,
                s.User.UserName,
                s.Level,
                CircleName = s.Circle != null ? s.Circle.Name : "بدون حلقة",
                s.ParentPhone,
                s.CreatedAt
            })
            .ToListAsync();

        return Ok(students);
    }

    [HttpPost("{id}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RestoreStudent(int id)
    {
        var student = await _context.Students
            .IgnoreQueryFilters()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id && s.IsDeleted);

        if (student == null)
            return NotFound(new { message = "الطالب غير موجود في الأرشيف" });

        student.IsDeleted = false;
        student.User.IsActive = true;

        _context.ActivityFeeds.Add(new ActivityFeed {
            UserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            UserName = User.Identity?.Name ?? "Admin",
            ActivityType = "استعادة طالب",
            Description = $"تمت استعادة الطالب {student.User.FullName} من الأرشيف",
            Icon = "🔄",
            Color = "text-green-500"
        });

        await _context.SaveChangesAsync();
        return Ok(new { message = "تم استعادة الطالب بنجاح" });
    }

    // ─────────────────────────────────────────────────
    // Helper: حساب نسبة تقدم الحفظ الحقيقية
    // ─────────────────────────────────────────────────
}

// Request Models
public class CreateStudentRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string GuardianRelationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? Residence { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
    public int? ParentId { get; set; }

    /// <summary>عند تأكيد الأدمن: معرّف الطالب المؤرشف الذي يُستعاد بدل إنشاء طالب جديد.</summary>
    public int? RestoreArchivedStudentId { get; set; }

    /// <summary>تجاهل التطابق مع طالب سابق وإنشاء طالب جديد (عند تشابه الاسم فقط).</summary>
    public bool IgnoreArchivedMatch { get; set; }
}

public class UpdateStudentRequest
{
    public string? FullName { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public string ParentPhone { get; set; } = string.Empty;
    public string GuardianRelationship { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public DateTime? RegistrationDate { get; set; }
    public string? StudentPhone { get; set; }
    public string? Residence { get; set; }
    public string? ParentName { get; set; }
    public string? Level { get; set; }
    public int? CircleId { get; set; }
}
