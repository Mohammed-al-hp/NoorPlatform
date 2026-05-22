using Microsoft.AspNetCore.Identity;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Infrastructure.Data;

public static class DbInitializer
{
    private static readonly string[] IdentityRoles = { "Admin", "Teacher", "Student", "Parent" };

    public static async Task SeedAsync(
        NoorDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole<int>> roleManager)
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

        if (context.Users.Any()) return;

        var adminUser = new User
        {
            UserName = "966500000000",
            Email = "admin@noor.local",
            PhoneNumber = "966500000000",
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
            UserName = "966500000001",
            PhoneNumber = "966500000001",
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
            UserName = "966505551234",
            PhoneNumber = "966505551234",
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
            UserName = "966505559999",
            PhoneNumber = "966505559999",
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

        var parent = new Parent { UserId = parentUser.Id, Phone = "0505551234" };
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
}
