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
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionResult> CompetitionResults => Set<CompetitionResult>();
    public DbSet<LibraryItem> LibraryItems => Set<LibraryItem>();
    public DbSet<WaitingListEntry> WaitingListEntries => Set<WaitingListEntry>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<CircleEnrollment> CircleEnrollments => Set<CircleEnrollment>();
    public DbSet<OralExamSession> OralExamSessions => Set<OralExamSession>();
    public DbSet<OralExamQuestion> OralExamQuestions => Set<OralExamQuestion>();
    public DbSet<MatnRecord> MatnRecords => Set<MatnRecord>();
    public DbSet<StudentMonthlyTarget> StudentMonthlyTargets => Set<StudentMonthlyTarget>();
    public DbSet<EvaluationPeriod> EvaluationPeriods => Set<EvaluationPeriod>();
    public DbSet<StudentPeriodEvaluation> StudentPeriodEvaluations => Set<StudentPeriodEvaluation>();
    public DbSet<DressRecord> DressRecords => Set<DressRecord>();
    public DbSet<PrayerDailyLog> PrayerDailyLogs => Set<PrayerDailyLog>();
    public DbSet<ParentHomeFeedback> ParentHomeFeedbacks => Set<ParentHomeFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 🟢 Global Query Filter for Soft Delete
        modelBuilder.Entity<Student>()
            .HasIndex(s => s.IsDeleted)
            .HasDatabaseName("IX_Student_IsDeleted");

        modelBuilder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);
        modelBuilder.Entity<Teacher>().HasQueryFilter(t => !t.IsDeleted);
        modelBuilder.Entity<Parent>().HasQueryFilter(p => !p.IsDeleted);
        // ⚡ Performance Indexes
        modelBuilder.Entity<Student>()
            .HasIndex(s => s.ParentId)
            .HasDatabaseName("IX_Student_ParentId");

        modelBuilder.Entity<ExamResult>()
            .HasIndex(e => e.ExamId)
            .HasDatabaseName("IX_ExamResult_ExamId");

        modelBuilder.Entity<Attendance>()
            .HasIndex(a => a.Date)
            .HasDatabaseName("IX_Attendance_Date");

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<PlatformSettings>()
            .Property(p => p.DefaultMonthlyFee)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<Expense>()
            .Property(e => e.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<Expense>()
            .Property(e => e.Category)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<Competition>()
            .Property(c => c.Level)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.Action)
            .HasConversion<string>();

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

        modelBuilder.Entity<CompetitionResult>()
            .HasOne(cr => cr.Competition)
            .WithMany(c => c.Results)
            .HasForeignKey(cr => cr.CompetitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CompetitionResult>()
            .HasOne(cr => cr.Student)
            .WithMany()
            .HasForeignKey(cr => cr.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Expense>()
            .HasOne(e => e.RecordedByUser)
            .WithMany()
            .HasForeignKey(e => e.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

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
            
        modelBuilder.Entity<Message>()
            .HasOne(m => m.SenderUser)
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.RecipientTeacher)
            .WithMany()
            .HasForeignKey(m => m.RecipientTeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Message>()
            .HasOne(m => m.ParentMessage)
            .WithMany()
            .HasForeignKey(m => m.ParentMessageId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Message>()
            .HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_Message_CreatedAt");

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

        modelBuilder.Entity<Expense>()
            .HasIndex(e => e.Date)
            .HasDatabaseName("IX_Expense_Date");

        modelBuilder.Entity<CompetitionResult>()
            .HasIndex(cr => cr.CompetitionId)
            .HasDatabaseName("IX_CompetitionResult_CompetitionId");

        // ─── حلقات إضافية + تسجيل الطلاب ───
        modelBuilder.Entity<Circle>()
            .HasOne(c => c.ParentCircle)
            .WithMany()
            .HasForeignKey(c => c.ParentCircleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CircleEnrollment>()
            .HasOne(e => e.Circle)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CircleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CircleEnrollment>()
            .HasOne(e => e.Student)
            .WithMany(s => s.ExtraEnrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CircleEnrollment>()
            .HasIndex(e => new { e.CircleId, e.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_CircleEnrollment_Circle_Student");

        modelBuilder.Entity<Attendance>()
            .HasOne(a => a.Circle)
            .WithMany()
            .HasForeignKey(a => a.CircleId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── اختبارات شفوية ───
        modelBuilder.Entity<OralExamSession>()
            .HasOne(s => s.Student)
            .WithMany(st => st.OralExamSessions)
            .HasForeignKey(s => s.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OralExamSession>()
            .HasOne(s => s.Circle)
            .WithMany()
            .HasForeignKey(s => s.CircleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OralExamSession>()
            .Property(s => s.Kind)
            .HasConversion<string>()
            .HasMaxLength(30);

        modelBuilder.Entity<OralExamQuestion>()
            .HasOne(q => q.Session)
            .WithMany(s => s.Questions)
            .HasForeignKey(q => q.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OralExamSession>()
            .HasIndex(s => new { s.StudentId, s.Date })
            .HasDatabaseName("IX_OralExam_Student_Date");

        // ─── متون + أهداف شهرية ───
        modelBuilder.Entity<MatnRecord>()
            .HasOne(m => m.Student)
            .WithMany(s => s.MatnRecords)
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MatnRecord>()
            .Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<StudentMonthlyTarget>()
            .HasOne(t => t.Student)
            .WithMany(s => s.MonthlyTargets)
            .HasForeignKey(t => t.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StudentMonthlyTarget>()
            .HasIndex(t => new { t.StudentId, t.Year, t.Month })
            .IsUnique()
            .HasDatabaseName("IX_MonthlyTarget_Student_YearMonth");

        // ─── فترات التقييم ───
        modelBuilder.Entity<EvaluationPeriod>()
            .HasOne(p => p.Circle)
            .WithMany()
            .HasForeignKey(p => p.CircleId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StudentPeriodEvaluation>()
            .HasOne(e => e.Period)
            .WithMany(p => p.StudentEvaluations)
            .HasForeignKey(e => e.PeriodId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StudentPeriodEvaluation>()
            .HasOne(e => e.Student)
            .WithMany()
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StudentPeriodEvaluation>()
            .HasIndex(e => new { e.PeriodId, e.StudentId })
            .IsUnique()
            .HasDatabaseName("IX_PeriodEval_Period_Student");

        // ─── لباس + صلاة + ولي الأمر ───
        modelBuilder.Entity<DressRecord>()
            .HasOne(d => d.Student)
            .WithMany(s => s.DressRecords)
            .HasForeignKey(d => d.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DressRecord>()
            .HasIndex(d => new { d.StudentId, d.Date })
            .IsUnique()
            .HasDatabaseName("IX_Dress_Student_Date");

        modelBuilder.Entity<PrayerDailyLog>()
            .HasOne(p => p.Student)
            .WithMany(s => s.PrayerLogs)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PrayerDailyLog>()
            .HasIndex(p => new { p.StudentId, p.Date })
            .IsUnique()
            .HasDatabaseName("IX_Prayer_Student_Date");

        modelBuilder.Entity<ParentHomeFeedback>()
            .HasOne(f => f.Student)
            .WithMany(s => s.ParentHomeFeedbacks)
            .HasForeignKey(f => f.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ParentHomeFeedback>()
            .HasOne(f => f.Parent)
            .WithMany()
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ParentHomeFeedback>()
            .Property(f => f.Rating)
            .HasConversion<string>()
            .HasMaxLength(20);

        modelBuilder.Entity<ParentHomeFeedback>()
            .HasIndex(f => new { f.StudentId, f.WeekStartDate })
            .IsUnique()
            .HasDatabaseName("IX_ParentHome_Student_Week");
    }
}
