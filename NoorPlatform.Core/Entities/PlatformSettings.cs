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
    public int OralMaxOpeningsBeforeFail { get; set; } = 3;
    /// <summary>وزن التنبيه في خصم درجة السؤال (نقاط لكل تنبيه).</summary>
    public double OralAlertPenalty { get; set; } = 5;
    /// <summary>وزن الفتح في خصم درجة السؤال.</summary>
    public double OralOpeningPenalty { get; set; } = 15;
    /// <summary>وزن التردد (غالباً خفيف).</summary>
    public double OralHesitationPenalty { get; set; } = 2;
    /// <summary>الهدف الشهري الافتراضي بالأثمان إن لم يُحدد للطالب.</summary>
    public int DefaultMonthlyAthmanTarget { get; set; } = 8;

    // ─── أوزان أبعاد تقييم الفترة (نسب نسبية؛ تُطبَّع تلقائياً) ───
    public double WeightAttendance { get; set; } = 1;
    public double WeightHifz { get; set; } = 1;
    public double WeightRevision { get; set; } = 1;
    public double WeightProgress { get; set; } = 1;
    public double WeightMatn { get; set; } = 1;
    public double WeightDress { get; set; } = 1;

    /// <summary>هل يظهر تقييم الفترة للطالب وولي الأمر؟</summary>
    public bool EvaluationsVisibleToStudentsAndParents { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}