namespace NoorPlatform.Core.Entities;

/// <summary>الصلوات الخمس بترتيب دخول أوقاتها.</summary>
public enum PrayerName
{
    Fajr = 0,
    Dhuhr = 1,
    Asr = 2,
    Maghrib = 3,
    Isha = 4
}

/// <summary>
/// سجل يومي للصلاة يرسله الطالب. يتكوّن من سجل لكل صلاة (<see cref="Entries"/>)؛
/// كل صلاة تُقفل بعد تسجيلها ولا يعدّلها إلا الشيخ، ويمكن للطالب إضافة الصلوات
/// التي دخل وقتها لاحقاً في اليوم نفسه. نقاط استرشادية.
/// </summary>
public class PrayerDailyLog
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public DateTime Date { get; set; }

    // ─── قيم مجمّعة (تُحسب من Entries عند وجودها؛ وتبقى للسجلات القديمة) ───
    /// <summary>صلّى صلاة واحدة على الأقل في المسجد.</summary>
    public bool PrayedInMosque { get; set; }
    /// <summary>كل الصلوات المسجّلة أُدّيت في وقتها.</summary>
    public bool OnTime { get; set; }
    /// <summary>عدد الصلوات في المسجد هذا اليوم (0–5) — رصيد صلاة الجماعة.</summary>
    public int MosquePrayerCount { get; set; }
    /// <summary>عدد الصلوات المؤدّاة (0–5). null = سجل قديم بالنموذج الإجمالي.</summary>
    public int? PrayedCount { get; set; }
    /// <summary>عدد الصلوات المؤدّاة في وقتها (0–5).</summary>
    public int? OnTimeCount { get; set; }

    public bool IsLocked { get; set; } = true;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? StudentNote { get; set; }

    public string? SheikhOverrideNote { get; set; }
    public int? OverriddenByUserId { get; set; }
    public DateTime? OverriddenAt { get; set; }

    public List<PrayerEntry> Entries { get; set; } = new();

    /// <summary>يعيد حساب القيم المجمّعة من سجلات الصلوات.</summary>
    public void RecalculateTotals()
    {
        if (Entries.Count == 0) return;
        var prayed = Entries.Where(e => e.Prayed).ToList();
        PrayedCount = prayed.Count;
        // لا رصيد جماعة لصلاة لم يؤشَّر عليها «في المسجد»
        MosquePrayerCount = prayed.Count(e => e.InMosque);
        OnTimeCount = prayed.Count(e => e.OnTime);
        PrayedInMosque = MosquePrayerCount > 0;
        OnTime = prayed.Count > 0 && prayed.All(e => e.OnTime);
    }
}

/// <summary>تسجيل صلاة واحدة من الصلوات الخمس.</summary>
public class PrayerEntry
{
    public int Id { get; set; }
    public int PrayerDailyLogId { get; set; }
    public PrayerDailyLog PrayerDailyLog { get; set; } = null!;
    public PrayerName Prayer { get; set; }
    /// <summary>هل صلّاها؟</summary>
    public bool Prayed { get; set; }
    /// <summary>في المسجد (جماعة)؛ false = في مكان آخر.</summary>
    public bool InMosque { get; set; }
    /// <summary>في وقتها؛ false = قضاءً.</summary>
    public bool OnTime { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
