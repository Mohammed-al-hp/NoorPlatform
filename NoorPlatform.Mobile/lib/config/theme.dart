import 'package:flutter/material.dart';

/// نظام تصميم منصة نور — الهوية الموحّدة مع منصة الويب:
/// أخضر غامق #1A5C3A + ذهبي #B8860B، خط Tajawal للواجهة و Amiri للنص القرآني (محمّلان محلياً).
class NoorTheme {
  NoorTheme._();

  // ── الخطوط (مضمّنة في assets/fonts) ──
  static const String fontFamily = 'Tajawal';
  static const String quranFontFamily = 'Amiri';

  // ── ألوان الهوية ──
  static const Color primaryGreen = Color(0xFF1A5C3A);
  static const Color primaryGreenLight = Color(0xFF2D8B57);
  static const Color primaryDark = Color(0xFF0F1A14);
  static const Color accentGold = Color(0xFFB8860B);
  static const Color accentGoldLight = Color(0xFFDAA520);
  static const Color goldSoft = Color(0xFFF6EBD0);
  static const Color greenSoft = Color(0xFFE3F1E8);
  static const Color cream = Color(0xFFFFFDF5);

  // ── ألوان دلالية ──
  static const Color success = Color(0xFF16A34A);
  static const Color warning = Color(0xFFF59E0B);
  static const Color error = Color(0xFFDC2626);
  static const Color info = Color(0xFF2563EB);

  // ── الأسطح (مطابقة لمتغيرات الويب --bg / --card / --border) ──
  static const Color surfaceLight = Color(0xFFF7F5EE);
  static const Color surfaceCard = Color(0xFFFFFFFF);
  static const Color surfaceDark = Color(0xFF1A2E23);
  static const Color surfaceGlass = Color(0x1AFFFFFF);
  static const Color borderLight = Color(0xFFE7E2D3);
  static const Color borderDark = Color(0xFF2A3F33);

  // ── النصوص ──
  static const Color textPrimary = Color(0xFF1A2E23);
  static const Color textSecondary = Color(0xFF6B7280);
  static const Color textOnPrimary = Color(0xFFFFFFFF);
  static const Color textOnDark = Color(0xFFE8F5E9);

  // ── التدرجات ──
  static const LinearGradient primaryGradient = LinearGradient(
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
    colors: [Color(0xFF1A5C3A), Color(0xFF2D8B57)],
  );

  static const LinearGradient goldGradient = LinearGradient(
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
    colors: [Color(0xFFB8860B), Color(0xFFDAA520)],
  );

  static const LinearGradient darkGradient = LinearGradient(
    begin: Alignment.topCenter,
    end: Alignment.bottomCenter,
    colors: [Color(0xFF0F1A14), Color(0xFF1A2E23)],
  );

  /// تدرج رأس الصفحات الرئيسية (لوحة المحفّظ/الطالب/ولي الأمر).
  static const LinearGradient headerGradient = LinearGradient(
    begin: AlignmentDirectional.topStart,
    end: AlignmentDirectional.bottomEnd,
    colors: [Color(0xFF0F3D27), Color(0xFF1A5C3A), Color(0xFF2D8B57)],
  );

  // ── الظلال الناعمة ──
  static List<BoxShadow> cardShadow = [
    BoxShadow(
      color: primaryGreen.withValues(alpha: 0.06),
      blurRadius: 18,
      offset: const Offset(0, 6),
    ),
  ];

  static List<BoxShadow> elevatedShadow = [
    BoxShadow(
      color: primaryGreen.withValues(alpha: 0.18),
      blurRadius: 24,
      offset: const Offset(0, 10),
    ),
  ];

  // ── الانحناءات ──
  static const double radiusSm = 8.0;
  static const double radiusMd = 12.0;
  static const double radiusLg = 16.0;
  static const double radiusXl = 24.0;
  static const double radiusFull = 999.0;

  // ── المسافات ──
  static const double spacingXs = 4.0;
  static const double spacingSm = 8.0;
  static const double spacingMd = 16.0;
  static const double spacingLg = 24.0;
  static const double spacingXl = 32.0;
  static const double spacingXxl = 48.0;

  /// نمط النص القرآني (أسماء السور والآيات).
  static TextStyle quran({double size = 20, Color? color, FontWeight weight = FontWeight.w700}) =>
      TextStyle(fontFamily: quranFontFamily, fontSize: size, fontWeight: weight, color: color, height: 1.6);

  // ── بيانات الثيم ──
  static ThemeData get lightTheme => _build(Brightness.light);
  static ThemeData get darkTheme => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final dark = brightness == Brightness.dark;
    final primary = dark ? primaryGreenLight : primaryGreen;
    final bg = dark ? primaryDark : surfaceLight;
    final card = dark ? surfaceDark : surfaceCard;
    final onSurface = dark ? textOnDark : textPrimary;
    final muted = dark ? const Color(0xFF9CB3A5) : textSecondary;
    final border = dark ? borderDark : borderLight;

    final scheme = ColorScheme.fromSeed(
      seedColor: primaryGreen,
      brightness: brightness,
    ).copyWith(
      primary: primary,
      onPrimary: textOnPrimary,
      secondary: dark ? accentGoldLight : accentGold,
      onSecondary: Colors.white,
      secondaryContainer: dark ? const Color(0xFF3A2F12) : goldSoft,
      onSecondaryContainer: dark ? goldSoft : const Color(0xFF5C4305),
      primaryContainer: dark ? const Color(0xFF1F3A2B) : greenSoft,
      onPrimaryContainer: dark ? greenSoft : primaryGreen,
      surface: card,
      onSurface: onSurface,
      surfaceContainerLowest: card,
      surfaceContainerLow: dark ? const Color(0xFF16261D) : const Color(0xFFFBFAF5),
      surfaceContainer: dark ? const Color(0xFF16261D) : const Color(0xFFF3F0E6),
      surfaceContainerHigh: dark ? const Color(0xFF213729) : const Color(0xFFEFEBDD),
      outline: border,
      outlineVariant: border,
      error: error,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: brightness,
      fontFamily: fontFamily,
      colorScheme: scheme,
    );

    TextStyle t(double size, FontWeight w, [Color? c]) =>
        TextStyle(fontFamily: fontFamily, fontSize: size, fontWeight: w, color: c ?? onSurface, height: 1.4);

    final rounded = RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusMd + 2));

    return base.copyWith(
      scaffoldBackgroundColor: bg,
      textTheme: base.textTheme.copyWith(
        displaySmall: t(32, FontWeight.w800),
        headlineLarge: t(28, FontWeight.w800),
        headlineMedium: t(24, FontWeight.w800),
        headlineSmall: t(20, FontWeight.w700),
        titleLarge: t(18, FontWeight.w700),
        titleMedium: t(16, FontWeight.w700),
        titleSmall: t(14, FontWeight.w700),
        bodyLarge: t(16, FontWeight.w400),
        bodyMedium: t(14, FontWeight.w400, muted),
        bodySmall: t(12, FontWeight.w400, muted),
        labelLarge: t(14, FontWeight.w700),
        labelMedium: t(12, FontWeight.w500, muted),
      ),
      appBarTheme: AppBarTheme(
        backgroundColor: bg,
        foregroundColor: onSurface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0.5,
        centerTitle: true,
        titleTextStyle: t(19, FontWeight.w800),
        iconTheme: IconThemeData(color: onSurface),
      ),
      cardTheme: CardThemeData(
        color: card,
        elevation: 0,
        margin: EdgeInsets.zero,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(radiusLg + 4),
          side: BorderSide(color: border.withValues(alpha: 0.7)),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        height: 70,
        // ظل علوي ناعم يفصل الشريط السفلي عن المحتوى
        elevation: 8,
        shadowColor: primaryGreen.withValues(alpha: 0.18),
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        indicatorColor: dark ? const Color(0xFF1F3A2B) : greenSoft,
        indicatorShape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusLg)),
        labelBehavior: NavigationDestinationLabelBehavior.alwaysShow,
        iconTheme: WidgetStateProperty.resolveWith((states) => IconThemeData(
              size: 24,
              color: states.contains(WidgetState.selected) ? primary : muted,
            )),
        labelTextStyle: WidgetStateProperty.resolveWith((states) => t(
              12,
              states.contains(WidgetState.selected) ? FontWeight.w800 : FontWeight.w500,
              states.contains(WidgetState.selected) ? primary : muted,
            )),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: primary,
          foregroundColor: textOnPrimary,
          minimumSize: const Size(double.infinity, 52),
          shape: rounded,
          textStyle: t(16, FontWeight.w700),
          elevation: 0,
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: primary,
          foregroundColor: textOnPrimary,
          minimumSize: const Size(64, 50),
          shape: rounded,
          textStyle: t(15, FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: primary,
          minimumSize: const Size(64, 48),
          shape: rounded,
          side: BorderSide(color: primary.withValues(alpha: 0.35)),
          textStyle: t(14, FontWeight.w700),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: primary, textStyle: t(14, FontWeight.w700)),
      ),
      floatingActionButtonTheme: FloatingActionButtonThemeData(
        backgroundColor: dark ? accentGoldLight : accentGold,
        foregroundColor: Colors.white,
        elevation: 2,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusLg + 2)),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: dark ? const Color(0xFF213729) : const Color(0xFFFBFAF5),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusMd + 2),
          borderSide: BorderSide(color: border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusMd + 2),
          borderSide: BorderSide(color: border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(radiusMd + 2),
          borderSide: BorderSide(color: primary, width: 1.8),
        ),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 16),
        hintStyle: t(14, FontWeight.w400, muted),
        labelStyle: t(14, FontWeight.w500, muted),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: dark ? const Color(0xFF213729) : const Color(0xFFF3F0E6),
        side: BorderSide.none,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusFull)),
        labelStyle: t(12, FontWeight.w600),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
      ),
      listTileTheme: ListTileThemeData(
        iconColor: primary,
        titleTextStyle: t(15, FontWeight.w700),
        subtitleTextStyle: t(13, FontWeight.w400, muted),
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 2),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusMd)),
      ),
      dividerTheme: DividerThemeData(color: border, thickness: 1, space: 1),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        backgroundColor: dark ? const Color(0xFF213729) : primaryDark,
        contentTextStyle: t(14, FontWeight.w500, Colors.white),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusMd)),
      ),
      bottomSheetTheme: BottomSheetThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.vertical(top: Radius.circular(radiusXl)),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: card,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(radiusXl)),
        titleTextStyle: t(18, FontWeight.w800),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: primary,
        unselectedLabelColor: muted,
        indicatorColor: dark ? accentGoldLight : accentGold,
        labelStyle: t(14, FontWeight.w800),
        unselectedLabelStyle: t(14, FontWeight.w500),
        dividerColor: Colors.transparent,
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(
        color: primary,
        linearTrackColor: dark ? const Color(0xFF213729) : greenSoft,
        circularTrackColor: dark ? const Color(0xFF213729) : greenSoft,
      ),
    );
  }
}
