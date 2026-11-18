using NoorPlatform.Core.Entities;

namespace NoorPlatform.Core.Services;

/// <summary>
/// منطق موحّد لنقاط التحفيز: كل سجل (حضور/تسميع/متن) يحفظ النقاط التي منحها فعلاً،
/// فيُعدَّل رصيد الطالب بالفرق عند تغيير الحالة أو التقدير، ويُسحب عند الحذف.
/// </summary>
public static class PointsLedger
{
    /// <summary>يطبّق الفرق على رصيد الطالب ويعيد القيمة الجديدة التي يجب حفظها في السجل.</summary>
    public static int Apply(Student student, int previouslyAwarded, int nowAwarded)
    {
        student.Points = Math.Max(0, student.Points + (nowAwarded - previouslyAwarded));
        return nowAwarded;
    }

    /// <summary>نقاط الحضور حسب الحالة (حاضر، متأخر، غائب بإذن، غائب دون إذن).</summary>
    public static int ForAttendance(AttendanceStatus status, PlatformSettings? settings) => status switch
    {
        AttendanceStatus.Present => settings?.PointsAttendancePresent ?? 10,
        AttendanceStatus.Late => settings?.PointsAttendanceLate ?? 5,
        AttendanceStatus.ExcusedAbsence => settings?.PointsAttendanceExcused ?? 0,
        AttendanceStatus.UnexcusedAbsence => settings?.PointsAttendanceUnexcused ?? 0,
        _ => 0
    };

    /// <summary>نقاط جلسة الحفظ/المراجعة حسب التقدير. «ضعيف ويحتاج مراجعة» جلسة راسبة لا تمنح نقاطاً.</summary>
    public static int ForHifz(RecordType type, string? evaluation, PlatformSettings? settings)
    {
        var grade = GradeScale.Parse(evaluation);
        if (type == RecordType.Memorization)
        {
            return grade switch
            {
                SessionGrade.Excellent => settings?.PointsHifzMemorization ?? 50,
                SessionGrade.VeryGood => settings?.PointsHifzMemVeryGood ?? 40,
                // «مقبول» غير معتمد للحفظ الجديد؛ السجلات القديمة به تُعامل كـ«جيد»
                SessionGrade.Good or SessionGrade.Acceptable => settings?.PointsHifzMemGood ?? 30,
                _ => 0
            };
        }

        return grade switch
        {
            SessionGrade.Excellent => settings?.PointsHifzRevision ?? 20,
            SessionGrade.VeryGood => settings?.PointsHifzRevVeryGood ?? 15,
            SessionGrade.Good => settings?.PointsHifzRevGood ?? 10,
            SessionGrade.Acceptable => settings?.PointsHifzRevAcceptable ?? 5,
            _ => 0
        };
    }

    /// <summary>نقاط المتن: حفظ جزء جديد بتقدير ناجح، وفقط عند تفعيل المتون في الإعدادات.</summary>
    public static int ForMatn(MatnRecordType type, string? evaluation, PlatformSettings? settings)
    {
        if (settings is { EnableMatn: false }) return 0;
        if (type != MatnRecordType.Memorization || !GradeScale.IsPassing(evaluation)) return 0;
        return settings?.PointsMatnMemorization ?? 20;
    }
}
