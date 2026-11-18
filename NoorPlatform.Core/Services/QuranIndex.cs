namespace NoorPlatform.Core.Services;

/// <summary>سورة من سور المصحف: رقمها واسمها (مطابق لقائمة الويب والهاتف) وعدد آياتها والجزء الذي تبدأ فيه.</summary>
public sealed record QuranSurah(int Number, string Name, int VerseCount, int Juz);

/// <summary>الفهرس المرجعي لسور القرآن الكريم (114 سورة).</summary>
public static class QuranIndex
{
    public static readonly IReadOnlyList<QuranSurah> Surahs = new QuranSurah[]
    {
        new(1, "الفاتحة", 7, 1),
        new(2, "البقرة", 285, 1),
        new(3, "آل عمران", 200, 3),
        new(4, "النساء", 175, 4),
        new(5, "المائدة", 122, 6),
        new(6, "الأنعام", 167, 7),
        new(7, "الأعراف", 206, 8),
        new(8, "الأنفال", 76, 9),
        new(9, "التوبة", 130, 10),
        new(10, "يونس", 109, 11),
        new(11, "هود", 121, 11),
        new(12, "يوسف", 111, 12),
        new(13, "الرعد", 44, 13),
        new(14, "إبراهيم", 54, 13),
        new(15, "الحجر", 99, 14),
        new(16, "النحل", 128, 14),
        new(17, "الإسراء", 110, 15),
        new(18, "الكهف", 105, 15),
        new(19, "مريم", 99, 16),
        new(20, "طه", 134, 16),
        new(21, "الأنبياء", 111, 17),
        new(22, "الحج", 76, 17),
        new(23, "المؤمنون", 119, 18),
        new(24, "النور", 62, 18),
        new(25, "الفرقان", 77, 18),
        new(26, "الشعراء", 226, 19),
        new(27, "النمل", 95, 19),
        new(28, "القصص", 88, 20),
        new(29, "العنكبوت", 69, 20),
        new(30, "الروم", 59, 21),
        new(31, "لقمان", 33, 21),
        new(32, "السجدة", 30, 21),
        new(33, "الأحزاب", 73, 21),
        new(34, "سبأ", 54, 22),
        new(35, "فاطر", 46, 22),
        new(36, "يس", 82, 22),
        new(37, "الصافات", 182, 23),
        new(38, "ص", 86, 23),
        new(39, "الزمر", 72, 23),
        new(40, "غافر", 84, 24),
        new(41, "فصلت", 53, 24),
        new(42, "الشورى", 50, 25),
        new(43, "الزخرف", 89, 25),
        new(44, "الدخان", 56, 25),
        new(45, "الجاثية", 36, 25),
        new(46, "الأحقاف", 34, 26),
        new(47, "محمد", 39, 26),
        new(48, "الفتح", 29, 26),
        new(49, "الحجرات", 18, 26),
        new(50, "ق", 45, 26),
        new(51, "الذاريات", 60, 26),
        new(52, "الطور", 47, 27),
        new(53, "النجم", 61, 27),
        new(54, "القمر", 55, 27),
        new(55, "الرحمن", 77, 27),
        new(56, "الواقعة", 99, 27),
        new(57, "الحديد", 28, 27),
        new(58, "المجادلة", 21, 28),
        new(59, "الحشر", 24, 28),
        new(60, "الممتحنة", 13, 28),
        new(61, "الصف", 14, 28),
        new(62, "الجمعة", 11, 28),
        new(63, "المنافقون", 11, 28),
        new(64, "التغابن", 18, 28),
        new(65, "الطلاق", 12, 28),
        new(66, "التحريم", 12, 28),
        new(67, "الملك", 31, 29),
        new(68, "القلم", 52, 29),
        new(69, "الحاقة", 52, 29),
        new(70, "المعارج", 44, 29),
        new(71, "نوح", 30, 29),
        new(72, "الجن", 28, 29),
        new(73, "المزمل", 18, 29),
        new(74, "المدثر", 55, 29),
        new(75, "القيامة", 39, 29),
        new(76, "الإنسان", 31, 29),
        new(77, "المرسلات", 50, 29),
        new(78, "النبأ", 40, 30),
        new(79, "النازعات", 45, 30),
        new(80, "عبس", 42, 30),
        new(81, "التكوير", 29, 30),
        new(82, "الانفطار", 19, 30),
        new(83, "المطففين", 36, 30),
        new(84, "الانشقاق", 25, 30),
        new(85, "البروج", 22, 30),
        new(86, "الطارق", 17, 30),
        new(87, "الأعلى", 19, 30),
        new(88, "الغاشية", 26, 30),
        new(89, "الفجر", 32, 30),
        new(90, "البلد", 20, 30),
        new(91, "الشمس", 15, 30),
        new(92, "الليل", 21, 30),
        new(93, "الضحى", 11, 30),
        new(94, "الشرح", 8, 30),
        new(95, "التين", 8, 30),
        new(96, "العلق", 20, 30),
        new(97, "القدر", 5, 30),
        new(98, "البينة", 8, 30),
        new(99, "الزلزلة", 9, 30),
        new(100, "العاديات", 11, 30),
        new(101, "القارعة", 10, 30),
        new(102, "التكاثر", 8, 30),
        new(103, "العصر", 3, 30),
        new(104, "الهمزة", 9, 30),
        new(105, "الفيل", 5, 30),
        new(106, "قريش", 5, 30),
        new(107, "الماعون", 6, 30),
        new(108, "الكوثر", 3, 30),
        new(109, "الكافرون", 6, 30),
        new(110, "النصر", 3, 30),
        new(111, "المسد", 5, 30),
        new(112, "الإخلاص", 4, 30),
        new(113, "الفلق", 5, 30),
        new(114, "الناس", 6, 30),
    };

    private static readonly Dictionary<string, QuranSurah> ByName =
        Surahs.ToDictionary(s => Normalize(s.Name));

    /// <summary>البحث عن سورة بالاسم مع تجاهل «سورة» والهمزات والمسافات الزائدة.</summary>
    public static QuranSurah? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return ByName.TryGetValue(Normalize(name), out var s) ? s : null;
    }

    /// <summary>السور من from إلى to (شاملة) بترتيب المصحف.</summary>
    public static IEnumerable<QuranSurah> Range(string from, string? to)
    {
        var a = Find(from);
        if (a == null) yield break;
        var b = Find(to) ?? a;
        var (lo, hi) = a.Number <= b.Number ? (a.Number, b.Number) : (b.Number, a.Number);
        for (var n = lo; n <= hi; n++) yield return Surahs[n - 1];
    }

    /// <summary>
    /// هل تمتد السورة في الجزء j؟ السورة تمتد من جزء بدايتها حتى جزء بداية السورة التالية
    /// (تقريب كافٍ للخريطة: البقرة تغطي الأجزاء 1–3، وآل عمران 3–4...).
    /// </summary>
    public static bool CoversJuz(QuranSurah surah, int juz)
    {
        int end;
        if (surah.Number >= Surahs.Count) end = 30;
        else
        {
            var next = Surahs[surah.Number];
            // الأجزاء التي تبدأ ببداية سورة: الأحقاف (26)، المجادلة (28)، الملك (29)، النبأ (30)
            var nextStartsJuz = next.Number is 46 or 58 or 67 or 78;
            end = Math.Max(surah.Juz, nextStartsJuz ? next.Juz - 1 : next.Juz);
        }
        return juz >= surah.Juz && juz <= end;
    }

    private static string Normalize(string name)
    {
        var s = name.Trim();
        if (s.StartsWith("سورة ")) s = s[5..];
        return s.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه")
                .Replace("ى", "ي").Replace(" ", "");
    }
}
