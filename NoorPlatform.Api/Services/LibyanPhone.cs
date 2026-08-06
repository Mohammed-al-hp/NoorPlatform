namespace NoorPlatform.Api.Services;

/// <summary>تنسيق أرقام الهواتف الليبية: عرض 09XXXXXXXX — تخزين 2189XXXXXXXX</summary>
public static class LibyanPhone
{
    public const string DisplayRegex = @"^09\d{8}$";

    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        
        var digits = DigitsOnly(phone);
        
        // ─── إصلاح: تشديد الفحص لقبول الأرقام الليبية الصالحة فقط ───
        if (digits.Length == 10 && digits.StartsWith("09")) return true;
        if (digits.Length == 12 && digits.StartsWith("2189")) return true;
        
        return false;
    }

    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = DigitsOnly(phone);
        
        if (digits.StartsWith("00218") && digits.Length >= 14)
            return digits.Substring(2, 12);

        if (digits.StartsWith("218") && digits.Length >= 12)
            return digits[..12];

        if (digits.StartsWith("09") && digits.Length == 10)
            return "218" + digits[1..];

        return digits;
    }

    public static string ToDisplay(string? normalizedOrAny)
    {
        var n = Normalize(normalizedOrAny);
        if (n.StartsWith("218") && n.Length >= 12)
            return "0" + n[3..];
            
        var digits = DigitsOnly(normalizedOrAny ?? "");
        if (digits.StartsWith("09") && digits.Length == 10) return digits;
        
        return normalizedOrAny ?? string.Empty;
    }

    /// <summary>رقم دولي لروابط واتساب (بدون +)</summary>
    public static string ForWhatsApp(string? phone)
    {
        var n = Normalize(phone);
        return string.IsNullOrEmpty(n) ? string.Empty : n;
    }

    /// <summary>كل أشكال اسم المستخدم المحتملة للبحث عند تسجيل الدخول.</summary>
    public static IReadOnlyList<string> GetLoginLookupKeys(string? phone)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(phone)) return Array.Empty<string>();

        var digits = DigitsOnly(phone);
        if (digits.Length == 0) return Array.Empty<string>();

        keys.Add(digits);

        var normalized = Normalize(phone);
        if (!string.IsNullOrEmpty(normalized))
            keys.Add(normalized);

        // ─── إصلاح: تمت إزالة المنطق الخاص بالأرقام السعودية (966) لتخصيص النظام لليبيا ───
        if (normalized.StartsWith("218") && normalized.Length >= 12)
        {
            keys.Add("0" + normalized[3..]);
        }

        return keys.ToList();
    }

    // ─── تحسين أداء (Low): تقليل إرهاق الـ GC عبر string.Create بدلاً من تخصيص مصفوفات جديدة ───
    private static string DigitsOnly(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        
        int digitCount = 0;
        foreach (char c in value)
        {
            if (char.IsDigit(c)) digitCount++;
        }
            
        if (digitCount == 0) return string.Empty;
        if (digitCount == value.Length) return value;
        
        return string.Create(digitCount, value, (span, state) =>
        {
            int index = 0;
            foreach (char c in state)
            {
                if (char.IsDigit(c)) span[index++] = c;
            }
        });
    }
}
