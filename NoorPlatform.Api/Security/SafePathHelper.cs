using System.Net;

namespace NoorPlatform.Api.Security;

public static class SafePathHelper
{
    /// <summary>
    /// يحل مساراً نسبياً تحت جذر wwwroot ويرفض أي محاولة Path Traversal.
    /// </summary>
    public static bool TryResolveUnderWebRoot(string webRootPath, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(webRootPath) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        // ─── إصلاح أمني: فك تشفير URL لمنع تجاوز الفحص باستخدام ترميزات مثل %2e%2e ───
        var decodedPath = WebUtility.UrlDecode(relativePath);

        var normalized = decodedPath.TrimStart('/', '\\')
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        if (normalized.Contains("..", StringComparison.Ordinal))
            return false;

        var rootFull = Path.GetFullPath(webRootPath);
        var candidate = Path.GetFullPath(Path.Combine(rootFull, normalized));

        if (!candidate.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            return false;

        fullPath = candidate;
        return true;
    }

    public static string SanitizeUploadFileName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(name))
            return "file.pdf";

        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return string.IsNullOrWhiteSpace(name) ? "file.pdf" : name;
    }
}
