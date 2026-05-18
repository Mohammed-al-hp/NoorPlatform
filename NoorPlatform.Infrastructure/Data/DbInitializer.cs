using Microsoft.AspNetCore.Identity;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(NoorDbContext context, UserManager<User> userManager)
    {
        if (context.Users.Any()) return;

        // ===== USERS via UserManager (يملأ NormalizedEmail تلقائياً) =====
        var adminUser = new User
        {
            UserName = "admin@noor.sa",
            Email = "admin@noor.sa",
            FullName = "المشرف العام",
            Role = UserRole.Admin,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");

        var teacherUser = new User
        {
            UserName = "teacher@noor.sa",
            Email = "teacher@noor.sa",
            FullName = "عبدالله السلمي",
            Role = UserRole.Teacher,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(teacherUser, "Teacher123!");

        var parentUser = new User
        {
            UserName = "parent@noor.sa",
            Email = "parent@noor.sa",
            FullName = "محمد الزهراني",
            Role = UserRole.Parent,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(parentUser, "Parent123!");

        var studentUser = new User
        {
            UserName = "student@noor.sa",
            Email = "student@noor.sa",
            FullName = "أحمد الزهراني",
            Role = UserRole.Student,
            EmailConfirmed = true
        };
        await userManager.CreateAsync(studentUser, "Student123!");

        // ===== باقي البيانات كما هي =====
        var teacher = new Teacher { UserId = teacherUser.Id, Qualification = "حافظ كامل + إجازة" };
        context.Teachers.Add(teacher);
        await context.SaveChangesAsync();

        var parent = new Parent { UserId = parentUser.Id, Phone = "0501234567" };
        context.Parents.Add(parent);
        await context.SaveChangesAsync();

        var circle = new Circle { Name = "حلقة الفجر", TeacherId = teacher.Id, Time = "يومياً بعد الفجر", Location = "قاعة A", Icon = "🌅" };
        context.Circles.Add(circle);
        await context.SaveChangesAsync();

        var student = new Student { UserId = studentUser.Id, ParentId = parent.Id, CircleId = circle.Id, Level = "متقدم" };
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
        await context.SaveChangesAsync();
    }
}