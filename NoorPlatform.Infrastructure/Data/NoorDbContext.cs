using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;

namespace NoorPlatform.Infrastructure.Data;

public class NoorDbContext : IdentityDbContext<User, IdentityRole<int>, int>
{
    public NoorDbContext(DbContextOptions<NoorDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Teacher> Teachers => Set<Teacher>();
    public DbSet<Parent> Parents => Set<Parent>();
    public DbSet<Circle> Circles => Set<Circle>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<HifzRecord> HifzRecords => Set<HifzRecord>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamResult> ExamResults => Set<ExamResult>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ActivityFeed> ActivityFeeds => Set<ActivityFeed>();
    public DbSet<Payment> Payments { get; set; } = null!;
    public DbSet<LibraryItem> LibraryItems { get; set; } = null!;
    public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 🟢 Global Query Filter for Soft Delete
        modelBuilder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Student)
            .WithMany()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Parent)
            .WithMany()
            .HasForeignKey(p => p.ParentId)
            .OnDelete(DeleteBehavior.NoAction);

        // ─────────────────────────────────────────────
        // العلاقات
        // ─────────────────────────────────────────────

        modelBuilder.Entity<Student>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            // Restrict بدل Cascade لحماية البيانات التاريخية
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Teacher>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Parent>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Circle>()
            .HasOne(c => c.Teacher)
            .WithMany(t => t.Circles)
            .HasForeignKey(c => c.TeacherId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Student>()
            .HasOne(s => s.Circle)
            .WithMany(c => c.Students)
            .HasForeignKey(s => s.CircleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Student>()
            .HasOne(s => s.Parent)
            .WithMany(p => p.Children)
            .HasForeignKey(s => s.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HifzRecord>()
            .HasOne(h => h.Student)
            .WithMany(s => s.HifzRecords)
            .HasForeignKey(h => h.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Student)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExamResult>()
            .HasOne(er => er.Exam)
            .WithMany(e => e.Results)
            .HasForeignKey(er => er.ExamId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExamResult>()
            .HasOne(er => er.Student)
            .WithMany(s => s.ExamResults)
            .HasForeignKey(er => er.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ActivityFeed>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LibraryItem>()
            .HasOne(l => l.UploadedByUser)
            .WithMany()
            .HasForeignKey(l => l.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LibraryItem>()
            .HasOne(l => l.Circle)
            .WithMany()
            .HasForeignKey(l => l.CircleId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─────────────────────────────────────────────
        // Indexes — لتسريع الاستعلامات الشائعة
        // ─────────────────────────────────────────────

        // الحضور: نُبحث دائماً بالطالب والتاريخ
        modelBuilder.Entity<Attendance>()
            .HasIndex(a => new { a.StudentId, a.Date })
            .HasDatabaseName("IX_Attendance_StudentId_Date");

        // سجلات التسميع: نُبحث دائماً بالطالب والتاريخ
        modelBuilder.Entity<HifzRecord>()
            .HasIndex(h => new { h.StudentId, h.Date })
            .HasDatabaseName("IX_HifzRecord_StudentId_Date");

        // الطلاب: بحث متكرر بالحلقة
        modelBuilder.Entity<Student>()
            .HasIndex(s => s.CircleId)
            .HasDatabaseName("IX_Student_CircleId");

        modelBuilder.Entity<WaitingListEntry>()
            .HasIndex(w => w.RegistrationDate)
            .HasDatabaseName("IX_WaitingList_RegistrationDate");

        modelBuilder.Entity<WaitingListEntry>()
            .HasIndex(w => w.Phone)
            .HasDatabaseName("IX_WaitingList_Phone");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.PhoneNumber)
            .HasDatabaseName("IX_User_PhoneNumber");

        modelBuilder.Entity<User>()
            .HasIndex(u => u.UserName)
            .HasDatabaseName("IX_User_UserName");

        modelBuilder.Entity<Payment>()
            .HasIndex(p => new { p.ParentId, p.Status })
            .HasDatabaseName("IX_Payment_ParentId_Status");

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.DueDate)
            .HasDatabaseName("IX_Payment_DueDate");

        modelBuilder.Entity<Circle>()
            .HasIndex(c => c.TeacherId)
            .HasDatabaseName("IX_Circle_TeacherId");

        modelBuilder.Entity<LibraryItem>()
            .HasIndex(l => l.CreatedAt)
            .HasDatabaseName("IX_LibraryItem_CreatedAt");

        modelBuilder.Entity<ActivityFeed>()
            .HasIndex(a => a.CreatedAt)
            .HasDatabaseName("IX_ActivityFeed_CreatedAt");
    }
}
