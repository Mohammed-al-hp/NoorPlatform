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

        if (!isProduction)
        {
            await EnsureTestLoginAccountsAsync(userManager, roleManager);
        }

        if (context.Users.Any()) return;

        var adminUser = new User
        {
            UserName = "218912345678",
            Email = "admin@noor.local",
            PhoneNumber = "218912345678",
            FullName = "المشرف العام",
            Role = UserRole.Admin,
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
            Email = "teacher@noor.local",
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
            Email = "parent@noor.local",
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
            Email = "student@noor.local",
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
                Email = $"{userName}@noor.test",
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

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await userManager.ResetPasswordAsync(user, resetToken, password);

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
