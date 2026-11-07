/// Global application constants.
class AppConstants {
  AppConstants._();

  static const String appName = 'نور';
  static const String appNameFull = 'منصة نور لتحفيظ القرآن';
  static const int totalQuranVerses = 6236;

  // ── Cache TTL (in minutes) ──
  static const int cacheTtlShort = 2;
  static const int cacheTtlMedium = 5;
  static const int cacheTtlLong = 10;
  static const int cacheTtlLibrary = 30;

  // ── Hive Box Names ──
  static const String hiveUserBox = 'user_box';
  static const String hiveStudentCache = 'student_cache';
  static const String hiveParentCache = 'parent_cache';
  static const String hiveHifzCache = 'hifz_cache';
  static const String hiveAttendanceCache = 'attendance_cache';
  static const String hiveLibraryCache = 'library_cache';
  static const String hivePaymentsCache = 'payments_cache';

  // ── Secure Storage Keys ──
  static const String tokenKey = 'jwt_token';
  static const String userKey = 'user_data';
  static const String mustChangePasswordKey = 'must_change_password';
  static const String cachedStudentIdKey = 'cached_student_id';

  // ── UI Debounce ──
  static const Duration debounceDuration = Duration(milliseconds: 500);
  static const Duration searchDebounceDuration = Duration(milliseconds: 800);
}
