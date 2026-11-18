using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Infrastructure.Data;

public static class DbInitializer
{
    private static readonly string[] IdentityRoles = { "Admin", "Teacher", "Student", "Parent" };

    public static async Task SeedAsync(
        NoorDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        bool isProduction)
    {
        foreach (var roleName in IdentityRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<int>(roleName));
        }

        // مزامنة أدوار Identity مع User.Role للقواعد الموجودة مسبقاً
        foreach (var user in userManager.Users.ToList())
        {
            var roleName = user.Role.ToString();
            if (!await userManager.IsInRoleAsync(user, roleName))
                await userManager.AddToRoleAsync(user, roleName);
        }

        if (!isProduction && Environment.GetEnvironmentVariable("NOOR_SEED_TEST") == "1")
        {
            await EnsureTestLoginAccountsAsync(userManager, roleManager);
        }

        await NormalizeLoginNamesAsync(context);

        // قواعد موجودة قبل إضافة «المدير العام»: يُرقّى أقدم مشرف نشط ليبقى للنظام مدير عام واحد على الأقل
        if (context.Users.Any(u => u.Role == UserRole.Admin) &&
            !context.Users.Any(u => u.Role == UserRole.Admin && u.IsSuperAdmin && u.IsActive))
        {
            var firstAdmin = context.Users
                .Where(u => u.Role == UserRole.Admin && u.IsActive)
                .OrderBy(u => u.Id)
                .FirstOrDefault();
            if (firstAdmin != null)
            {
                firstAdmin.IsSuperAdmin = true;
                await context.SaveChangesAsync();
            }
        }

        if (context.Users.Any()) return;

        if (isProduction)
        {
            var pwd = Environment.GetEnvironmentVariable("NOOR_ADMIN_PASSWORD");
            if (string.IsNullOrWhiteSpace(pwd)) return;
            var prodAdmin = new User
            {
                UserName = Environment.GetEnvironmentVariable("NOOR_ADMIN_PHONE") ?? "218900000000",
                PhoneNumber = Environment.GetEnvironmentVariable("NOOR_ADMIN_PHONE") ?? "218900000000",
                FullName = "المشرف العام",
                Role = UserRole.Admin,
                IsSuperAdmin = true,
                EmailConfirmed = true,
                MustChangePassword = true,
                IsActive = true
            };
            var r = await userManager.CreateAsync(prodAdmin, pwd);
            if (r.Succeeded) await userManager.AddToRoleAsync(prodAdmin, "Admin");
            return;
        }

        var adminUser = new User
        {
            UserName = "218912345678",
            PhoneNumber = "218912345678",
            FullName = "المشرف العام",
            Role = UserRole.Admin,
            IsSuperAdmin = true,
            EmailConfirmed = true,
            MustChangePassword = false,
            IsActive = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");

        var teacherUser = new User
        {
            UserName = "218912345679",
            PhoneNumber = "218912345679",
            FullName = "عبدالله السلمي",
            Role = UserRole.Teacher,
            EmailConfirmed = true,
            MustChangePassword = false,
            IsActive = true
        };
        await userManager.CreateAsync(teacherUser, "Teacher123!");
        await userManager.AddToRoleAsync(teacherUser, "Teacher");

        var parentUser = new User
        {
            UserName = "218921234567",
            PhoneNumber = "218921234567",
            FullName = "محمد الزهراني",
            Role = UserRole.Parent,
            EmailConfirmed = true,
            MustChangePassword = false,
            IsActive = true
        };
        await userManager.CreateAsync(parentUser, "Parent123!");
        await userManager.AddToRoleAsync(parentUser, "Parent");

        var studentUser = new User
        {
            UserName = "218931234567",
            PhoneNumber = "218931234567",
            FullName = "أحمد الزهراني",
            Role = UserRole.Student,
            EmailConfirmed = true,
            MustChangePassword = false,
            IsActive = true
        };
        await userManager.CreateAsync(studentUser, "Student123!");
        await userManager.AddToRoleAsync(studentUser, "Student");

        var teacher = new Teacher { UserId = teacherUser.Id, Qualification = "حافظ كامل + إجازة" };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        var parent = new Parent { UserId = parentUser.Id, Phone = "2189505551234" };
        context.Parents.Add(parent);
        await context.SaveChangesAsync();

        var circle = new Circle { Name = "حلقة الفجر", TeacherId = teacher.Id, Time = "يومياً بعد الفجر", Location = "قاعة A", Icon = "🌅" };
        context.Circles.Add(circle);
        await context.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ParentId = parent.Id, CircleId = circle.Id, Level = "متقدم", Points = 150, Badges = "متميز,مبادر" };
        context.Students.Add(student);
        await context.SaveChangesAsync();

        context.Attendances.Add(new Attendance { StudentId = student.Id, Date = DateTime.UtcNow, Status = AttendanceStatus.Present });
        context.HifzRecords.Add(new HifzRecord { StudentId = student.Id, SurahName = "البقرة", Verses = "1-10", Type = RecordType.Memorization, Evaluation = "ممتاز" });
        context.Announcements.Add(new Announcement
        {
            Title = "مرحباً بكم في منصة نور",
            Content = "تم إطلاق النسخة الجديدة من النظام بنجاح.",
            CreatedAt = DateTime.UtcNow
        });

        context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = adminUser.Id,
            UserName = adminUser.FullName,
            ActivityType = "System",
            Description = "تم تهيئة النظام وبدء الاستخدام",
            Icon = "🚀",
            Color = "purple"
        });
        context.ActivityFeeds.Add(new ActivityFeed
        {
            UserId = studentUser.Id,
            UserName = studentUser.FullName,
            ActivityType = "Hifz",
            Description = "أكمل الطالب أحمد حفظ سورة البقرة (1-10)",
            Icon = "📖",
            Color = "green"
        });

        await context.SaveChangesAsync();
    }

    private static readonly System.Text.RegularExpressions.Regex LibyanUserName = new(@"^2189\d{8}$");
    private static readonly System.Text.RegularExpressions.Regex StudentUserName = new(@"^st\d{6}$");

    /// <summary>
    /// توحيد أسماء الدخول (يعمل عند كل تشغيل ولا يغيّر شيئاً إن كانت البيانات سليمة):
    /// الدخول برقم هاتف ليبي (2189XXXXXXXX) لكل المستخدمين، واسم مستخدم (stXXXXXX) للطالب الذي لا يملك هاتفاً.
    /// - أرقام مسجلة بالبادئة 966 بالخطأ (9669XXXXXXXX) ← 2189XXXXXXXX إن لم يكن الرقم مستخدماً.
    /// - طالب باسم دخول قديم (مثل 1778869770528@noor.sa) ← رقم هاتفه إن وُجد، وإلا اسم مستخدم جديد.
    /// - البريد الإلكتروني لم يعد مستخدماً في المنصة فيُفرَّغ.
    /// كل تغيير يُسجَّل في سجل التدقيق ليعرف المشرف أسماء الدخول الجديدة.
    /// </summary>
    private static async Task NormalizeLoginNamesAsync(NoorDbContext context)
    {
        var users = await context.Users.ToListAsync();
        var taken = new HashSet<string>(users.Select(u => u.UserName ?? string.Empty), StringComparer.OrdinalIgnoreCase);
        var studentPhones = await context.Students.IgnoreQueryFilters()
            .Select(s => new { s.UserId, s.StudentPhone })
            .ToListAsync();
        var phoneByUser = studentPhones.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First().StudentPhone);
        var changed = false;

        foreach (var user in users)
        {
            var current = user.UserName ?? string.Empty;
            string? target = null;

            if (current.Length == 12 && current.StartsWith("9669") && current.All(char.IsDigit))
            {
                var fixedPhone = "218" + current[3..];
                if (!taken.Contains(fixedPhone)) target = fixedPhone;
            }
            else if (user.Role == UserRole.Student && !LibyanUserName.IsMatch(current) && !StudentUserName.IsMatch(current))
            {
                var phone = NormalizeLibyanPhone(phoneByUser.GetValueOrDefault(user.Id));
                target = phone != null && !taken.Contains(phone) ? phone : NewStudentUserName(taken);
            }

            if (target != null)
            {
                var old = current;
                user.UserName = target;
                user.NormalizedUserName = target.ToUpperInvariant();
                user.PhoneNumber = LibyanUserName.IsMatch(target) ? target : null;
                taken.Add(target);
                if (user.Role == UserRole.Parent && LibyanUserName.IsMatch(target))
                {
                    var parent = await context.Parents.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.UserId == user.Id);
                    if (parent != null) parent.Phone = target;
                }
                context.AuditLogs.Add(new AuditLog
                {
                    UserId = "System",
                    Action = AuditAction.Update,
                    EntityName = nameof(User),
                    EntityId = user.Id.ToString(),
                    OldValues = System.Text.Json.JsonSerializer.Serialize(new { UserName = old }),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(new { UserName = target, Event = "LoginNameNormalized" })
                });
                changed = true;
            }

            if (user.Email != null || user.NormalizedEmail != null)
            {
                user.Email = null;
                user.NormalizedEmail = null;
                changed = true;
            }
        }

        if (changed) await context.SaveChangesAsync();
    }

    private static string? NormalizeLibyanPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00218")) digits = digits[2..];
        if (digits.Length == 10 && digits.StartsWith("09")) digits = "218" + digits[1..];
        return LibyanUserName.IsMatch(digits) ? digits : null;
    }

    private static string NewStudentUserName(HashSet<string> taken)
    {
        while (true)
        {
            var name = "st" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999);
            if (!taken.Contains(name)) return name;
        }
    }

    /// <summary>حسابات اختبار بكلمات مرور معروفة — تُضاف أو تُحدَّث دون حذف المستخدمين الحاليين.</summary>
    private static async Task EnsureTestLoginAccountsAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        await EnsureLoginAccountAsync(userManager, roleManager,
            userName: "218911111111",
            fullName: "مشرف الاختبار",
            role: UserRole.Admin,
            password: "Admin123!");

        await EnsureLoginAccountAsync(userManager, roleManager,
            userName: "218922222222",
            fullName: "معلم الاختبار",
            role: UserRole.Teacher,
            password: "Teacher123!");

        // محمد النعاس — حساب محفّظ موجود مسبقاً (UserName: 218912984190)
        await EnsureLoginAccountAsync(userManager, roleManager,
            userName: "218912984190",
            fullName: "محمد النعاس",
            role: UserRole.Teacher,
            password: "Teacher123!");
    }

    private static async Task EnsureLoginAccountAsync(
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager,
        string userName,
        string fullName,
        UserRole role,
        string password)
    {
        var roleName = role.ToString();
        if (!await roleManager.RoleExistsAsync(roleName))
            await roleManager.CreateAsync(new IdentityRole<int>(roleName));

        var user = await userManager.FindByNameAsync(userName);
        if (user == null)
        {
            user = new User
            {
                UserName = userName,
                PhoneNumber = userName,
                FullName = fullName,
                Role = role,
                EmailConfirmed = true,
                MustChangePassword = false,
                IsActive = true
            };
            var create = await userManager.CreateAsync(user, password);
            if (!create.Succeeded) return;
            await userManager.AddToRoleAsync(user, roleName);
            return;
        }

        user.IsActive = true;
        user.MustChangePassword = false;
        user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? fullName : user.FullName;
        if (string.IsNullOrEmpty(user.PhoneNumber))
            user.PhoneNumber = userName;
        await userManager.UpdateAsync(user);

        if (!await userManager.IsInRoleAsync(user, roleName))
            await userManager.AddToRoleAsync(user, roleName);
    }

    /// <summary>إعادة تعيين كلمة مرور مستخدم موجود (بالـ UserName) — للاستعادة في التطوير.</summary>
    public static async Task<bool> ResetUserPasswordAsync(UserManager<User> userManager, string userName, string newPassword)
    {
        var user = await userManager.FindByNameAsync(userName);
        if (user == null) return false;

        IdentityResult result;
        if (await userManager.HasPasswordAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            result = await userManager.ResetPasswordAsync(user, token, newPassword);
        }
        else
        {
            result = await userManager.AddPasswordAsync(user, newPassword);
        }

        if (!result.Succeeded) return false;

        user.MustChangePassword = false;
        user.IsActive = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        await userManager.UpdateAsync(user);
        return true;
    }
}
