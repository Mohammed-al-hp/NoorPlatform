namespace NoorPlatform.Api.Services;

/// <summary>تنسيق أرقام الهواتف الليبية: عرض 09XXXXXXXX — تخزين 2189XXXXXXXX</summary>
public static class LibyanPhone
{
    public const string DisplayRegex = @"^09\d{8}$";

    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return false;
        var digits = DigitsOnly(phone);
        if (digits.Length == 10 && digits.StartsWith("09")) return true;
        if (digits.Length == 12 && digits.StartsWith("218") && digits[3] == '9') return true;
        if (digits.Length == 9 && digits.StartsWith("9")) return true;
        return false;
    }

    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        var digits = DigitsOnly(phone);
        if (digits.StartsWith("218") && digits.Length >= 12)
            return digits[..12];

        if (digits.StartsWith("09") && digits.Length == 10)
            return "218" + digits[1..];

        if (digits.StartsWith("9") && digits.Length == 9)
            return "218" + digits;

        if (digits.StartsWith("0") && digits.Length == 10)
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

    /// <summary>كل أشكال اسم المستخدم المحتملة للبحث عند تسجيل الدخول (ليبي + سعودي قديم).</summary>
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

        if (digits.Length == 10 && digits.StartsWith("09"))
            keys.Add("966" + digits[1..]);

        if (digits.Length == 10 && digits.StartsWith("05"))
            keys.Add("966" + digits[1..]);

        if (digits.StartsWith("966") && digits.Length >= 12)
            keys.Add(digits[..12]);

        if (normalized.StartsWith("218") && normalized.Length >= 12)
        {
            keys.Add("0" + normalized[3..]);
            keys.Add("966" + normalized[3..]);
        }

        return keys.ToList();
    }

    private static string DigitsOnly(string value) =>
        new string(value.Where(char.IsDigit).ToArray());
}
