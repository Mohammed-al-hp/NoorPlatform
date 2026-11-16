using Microsoft.EntityFrameworkCore;
using NoorPlatform.Core.Entities;
using NoorPlatform.Infrastructure.Data;

namespace NoorPlatform.Api.Services;

public static class NotificationKinds
{
    public const string Hifz = "Hifz";
    public const string Absence = "Absence";
    public const string Announcement = "Announcement";
    public const string Message = "Message";
    public const string MessageReply = "MessageReply";
    public const string MonthlyReport = "MonthlyReport";
    public const string System = "System";
}

/// <summary>
/// ينشئ إشعارات داخل التطبيق للأدوار المختلفة. فشل الإشعار لا يُفشل العملية الأصلية أبداً
/// (يُسجَّل في السجل فقط)، لأن الإشعار ميزة مساندة لا جزء من البيانات الأساسية.
/// </summary>
public class InAppNotificationService
{
    private const int MaxTitle = 150, MaxBody = 1000;
    private readonly NoorDbContext _db;
    private readonly ILogger<InAppNotificationService> _logger;

    public InAppNotificationService(NoorDbContext db, ILogger<InAppNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task NotifyUsersAsync(IEnumerable<int> userIds, string kind, string title, string body,
        string? link = null, int? studentId = null)
        => SafeAsync(async () =>
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0) return;
            var now = DateTime.UtcNow;
            _db.UserNotifications.AddRange(ids.Select(id => new UserNotification
            {
                UserId = id,
                Kind = kind,
                Title = Trim(title, MaxTitle),
                Body = Trim(body, MaxBody),
                Link = link,
                StudentId = studentId,
                CreatedAt = now
            }));
            await _db.SaveChangesAsync();
        });

    /// <summary>إشعار الطالب نفسه وولي أمره (إن كان له حساب).</summary>
    public async Task NotifyStudentAndGuardianAsync(int studentId, string kind, string title, string body,
        string? link = null, bool includeStudent = true)
    {
        var ids = await _db.Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => new { s.UserId, ParentUserId = s.Parent != null ? (int?)s.Parent.UserId : null })
            .FirstOrDefaultAsync();
        if (ids == null) return;
        var targets = new List<int>();
        if (includeStudent) targets.Add(ids.UserId);
        if (ids.ParentUserId.HasValue) targets.Add(ids.ParentUserId.Value);
        await NotifyUsersAsync(targets, kind, title, body, link, studentId);
    }

    /// <summary>إشعارات الغياب لأولياء أمور مجموعة طلاب (بعد رصد الحضور).</summary>
    public async Task NotifyAbsencesAsync(IEnumerable<int> studentIds, DateTime date)
    {
        var list = studentIds.Distinct().ToList();
        if (list.Count == 0) return;
        var rows = await _db.Students.AsNoTracking()
            .Where(s => list.Contains(s.Id) && s.Parent != null)
            .Select(s => new { s.Id, s.Parent!.UserId, s.User.FullName })
            .ToListAsync();
        await SafeAsync(async () =>
        {
            var now = DateTime.UtcNow;
            _db.UserNotifications.AddRange(rows.Select(r => new UserNotification
            {
                UserId = r.UserId,
                Kind = NotificationKinds.Absence,
                Title = "تنبيه غياب",
                Body = Trim($"سُجّل غياب {r.FullName} عن الحلقة بتاريخ {date:yyyy/MM/dd}.", MaxBody),
                Link = "parentHome",
                StudentId = r.Id,
                CreatedAt = now
            }));
            await _db.SaveChangesAsync();
        });
    }

    /// <summary>توزيع إشعار إعلان على الفئة المستهدفة (إعلان المحفّظ يصل لطلاب حلقاته وأولياء أمورهم فقط).</summary>
    public async Task NotifyAnnouncementAsync(Announcement a)
    {
        var users = _db.Users.AsNoTracking().Where(u => u.IsActive);
        List<int> ids;
        if (a.TeacherId.HasValue)
        {
            var students = _db.Students.AsNoTracking().Where(s =>
                (s.Circle != null && s.Circle.TeacherId == a.TeacherId) ||
                s.ExtraEnrollments.Any(e => e.Circle.TeacherId == a.TeacherId));
            var studentUsers = a.Target is AnnouncementTarget.All or AnnouncementTarget.Students
                ? await students.Select(s => s.UserId).ToListAsync() : new List<int>();
            var parentUsers = a.Target is AnnouncementTarget.All or AnnouncementTarget.Parents
                ? await students.Where(s => s.Parent != null).Select(s => s.Parent!.UserId).ToListAsync() : new List<int>();
            ids = studentUsers.Concat(parentUsers).ToList();
        }
        else
        {
            ids = a.Target switch
            {
                AnnouncementTarget.Teachers => await users.Where(u => u.Role == UserRole.Teacher).Select(u => u.Id).ToListAsync(),
                AnnouncementTarget.Students => await users.Where(u => u.Role == UserRole.Student).Select(u => u.Id).ToListAsync(),
                AnnouncementTarget.Parents => await users.Where(u => u.Role == UserRole.Parent).Select(u => u.Id).ToListAsync(),
                _ => await users.Where(u => u.Role != UserRole.Admin).Select(u => u.Id).ToListAsync()
            };
        }
        await NotifyUsersAsync(ids, NotificationKinds.Announcement, $"إعلان جديد: {a.Title}", a.Content, "announcements");
    }

    private async Task SafeAsync(Func<Task> work)
    {
        try { await work(); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر إنشاء الإشعار داخل التطبيق");
            // إزالة الإشعارات العالقة حتى لا تُحفظ مع أي عملية لاحقة على نفس السياق
            foreach (var entry in _db.ChangeTracker.Entries<UserNotification>()
                         .Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
        }
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
