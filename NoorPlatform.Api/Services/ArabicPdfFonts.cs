using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace NoorPlatform.Api.Services;

/// <summary>
/// تسجيل خط عربي (Amiri) لدعم RTL وتشكيل النص في تقارير QuestPDF.
/// </summary>
public static class ArabicPdfFonts
{
    private static bool _registered;
    private static readonly object Lock = new();
    public const string FontFamilyName = "Amiri";

    public static void EnsureRegistered()
    {
        if (_registered) return;
        lock (Lock)
        {
            if (_registered) return;

            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "Amiri-Regular.ttf"),
                Path.Combine(AppContext.BaseDirectory, "Amiri-Regular.ttf")
            };

            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                using var stream = File.OpenRead(path);
                FontManager.RegisterFontWithCustomName(FontFamilyName, stream);
                _registered = true;
                return;
            }

            _registered = true;
        }
    }

    public static TextStyle DefaultStyle(float fontSize = 12) =>
        TextStyle.Default
            .FontFamily(FontFamilyName, "Tahoma", "Arial")
            .FontSize(fontSize)
            .DirectionFromRightToLeft();
}
