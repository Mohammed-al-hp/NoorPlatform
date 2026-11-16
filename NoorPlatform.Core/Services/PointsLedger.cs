using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

/// <summary>
/// منطق موحّد لنقاط التحفيز: كل سجل (حضور/تسميع) يحفظ النقاط التي منحها فعلاً،
/// فيُعدَّل رصيد الطالب بالفرق عند تغيير الحالة أو التقييم، ويُسحب عند الحذف.
/// </summary>
public static class PointsLedger
{
    /// <summary>يطبّق الفرق على رصيد الطالب ويعيد القيمة الجديدة التي يجب حفظها في السجل.</summary>
    public static int Apply(Student student, int previouslyAwarded, int nowAwarded)
    {
        student.Points = Math.Max(0, student.Points + (nowAwarded - previouslyAwarded));
        return nowAwarded;
    }

    public static int ForAttendance(AttendanceStatus status, PlatformSettings? settings) =>
        status == AttendanceStatus.Present ? (settings?.PointsAttendancePresent ?? 10) : 0;

    /// <summary>الجلسة الناجحة فقط تمنح نقاطاً (نفس قائمة التقييمات الناجحة المستخدمة في التقييم الشهري).</summary>
    public static int ForHifz(RecordType type, string? evaluation, PlatformSettings? settings)
    {
        var cfg = MonthlyHifzRatingConfig.From(settings);
        if (!MonthlyHifzRating.IsPassing(evaluation, cfg.PassingEvaluations))
            return 0;
        return type == RecordType.Memorization
            ? settings?.PointsHifzMemorization ?? 50
            : settings?.PointsHifzRevision ?? 20;
    }
}
