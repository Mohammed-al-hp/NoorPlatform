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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            .OnDelete(DeleteBehavior.Restrict);

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
    }
}
