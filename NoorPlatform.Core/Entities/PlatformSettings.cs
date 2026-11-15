namespace NoorPlatform.Core.Entities;

public class PlatformSettings
{
    public int Id { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    // ─── معلومات المركز الإضافية ───
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // ─── إعدادات الدوام ───
    /// <summary>أيام الدوام — مخزنة كـ CSV مثل "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء"</summary>
    public string WorkDays { get; set; } = "السبت,الأحد,الاثنين,الثلاثاء,الأربعاء,الخميس";
    /// <summary>وقت بداية الدوام مثل "08:00"</summary>
    public string WorkStartTime { get; set; } = "08:00";
    /// <summary>وقت نهاية الدوام مثل "12:00"</summary>
    public string WorkEndTime { get; set; } = "12:00";

    // ─── إعدادات المالية ───
    public decimal DefaultMonthlyFee { get; set; } = 0;
    public string Currency { get; set; } = "د.ل";

    // ─── إعدادات التقييم التربوي (ملاحظات المشرف) ───
    /// <summary>عدد الفتحات عبر أسئلة الجلسة لاعتبار الطالب غير حافظ.</summary>
    public int OralMaxOpeningsBeforeFail { get; set; } = 1;
    /// <summary>وزن التنبيه في خصم درجة السؤال (نقاط لكل تنبيه).</summary>
    public double OralAlertPenalty { get; set; } = 1;
    /// <summary>وزن الفتح في خصم درجة السؤال.</summary>
    public double OralOpeningPenalty { get; set; } = 2;
    /// <summary>وزن التردد (غالباً خفيف).</summary>
    public double OralHesitationPenalty { get; set; } = 0.5;
    /// <summary>الهدف الشهري الافتراضي بالأثمان إن لم يُحدد للطالب.</summary>
    public int DefaultMonthlyAthmanTarget { get; set; } = 8;

    // ─── عتبات تحويل نسبة الإنجاز الشهري إلى درجة من 10 (قابلة للتعديل) ───
    /// <summary>نسبة الإنجاز اللازمة للحصول على 10/10 (افتراضي 75%).</summary>
    public double ProgressThreshold10 { get; set; } = 0.75;
    /// <summary>نسبة الإنجاز اللازمة للحصول على 9/10 (افتراضي 62.5%).</summary>
    public double ProgressThreshold9 { get; set; } = 0.625;
    /// <summary>نسبة الإنجاز اللازمة للحصول على 8/10 (افتراضي 50%).</summary>
    public double ProgressThreshold8 { get; set; } = 0.5;
    /// <summary>نسبة الإنجاز اللازمة للحصول على 6/10 (افتراضي 37.5%).</summary>
    public double ProgressThreshold6 { get; set; } = 0.375;
    /// <summary>نسبة الإنجاز اللازمة للحصول على 4/10 (افتراضي 25%).</summary>
    public double ProgressThreshold4 { get; set; } = 0.25;

    // ─── أوزان أبعاد تقييم الفترة (نسب نسبية؛ تُطبَّع تلقائياً) ───
    public double WeightAttendance { get; set; } = 1;
    public double WeightHifz { get; set; } = 1;
    public double WeightRevision { get; set; } = 1;
    public double WeightProgress { get; set; } = 1;
    public double WeightMatn { get; set; } = 1;
    public double WeightDress { get; set; } = 1;

    /// <summary>هل يظهر تقييم الفترة للطالب وولي الأمر؟</summary>
    public bool EvaluationsVisibleToStudentsAndParents { get; set; } = true;

    /// <summary>نقاط تُضاف للطالب عند تسجيل حضور (حاضر).</summary>
    public int PointsAttendancePresent { get; set; } = 10;
    /// <summary>نقاط تُضاف عند تسجيل حفظ جديد.</summary>
    public int PointsHifzMemorization { get; set; } = 50;
    /// <summary>نقاط تُضاف عند تسجيل مراجعة.</summary>
    public int PointsHifzRevision { get; set; } = 20;

    // ─── التقييم الشهري بحسب جلسات الحفظ الجديد الناجحة (هدف 10/10 حسب العمر) ───
    /// <summary>عدد جلسات الحفظ الجديد الناجحة المطلوبة شهرياً لعمر 5–7 سنوات (وأقل من 5 أو عمر غير معروف).</summary>
    public int HifzRatingTargetAge5To7 { get; set; } = 4;
    /// <summary>الهدف الشهري لعمر 8–10 سنوات.</summary>
    public int HifzRatingTargetAge8To10 { get; set; } = 6;
    /// <summary>الهدف الشهري لعمر 11 سنة فأكثر.</summary>
    public int HifzRatingTargetAge11Plus { get; set; } = 8;
    /// <summary>قيم التقييم (Evaluation) التي تُعدّ الجلسة بها ناجحة — مفصولة بفواصل.</summary>
    public string HifzRatingPassingEvaluations { get; set; } = "ممتاز,جيد جداً,جيد,ضعيف";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}