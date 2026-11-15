/// API configuration constants mapping to the existing NoorPlatform .NET backend.
///
/// Override base URL without editing this file:
///   flutter run --dart-define=API_BASE_URL=http://host:port
class ApiConfig {
  ApiConfig._();

  static const String baseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://192.168.8.101:5032',
  );

  static const Duration connectTimeout = Duration(seconds: 30);
  static const Duration receiveTimeout = Duration(seconds: 60);
  static const Duration sendTimeout = Duration(seconds: 30);

  // Auth
  static const String login = '/api/auth/login';
  static const String changePassword = '/api/auth/change-password';

  // Dashboard
  static const String studentSummary = '/api/dashboard/student-summary';
  static const String parentSummary = '/api/dashboard/parent-summary';

  // Hifz
  static const String myHifz = '/api/hifz/my';
  static String studentHifz(int studentId) => '/api/hifz/student/$studentId';

  // Attendance
  static const String myAttendance = '/api/attendance/my';
  static String studentAttendance(int studentId) =>
      '/api/attendance/student/$studentId';

  // Payments (parent views invoices; settlement is recorded by admin)
  static const String parentPayments = '/api/payments/parent';

  // Library
  static const String library = '/api/library';
  static String libraryFile(int id) => '/api/library/$id/file';

  // Reports
  static String certificate(int studentId) =>
      '/api/reports/certificate/$studentId';
  static String monthlyReport(int studentId) =>
      '/api/reports/monthly/$studentId';

  // Pedagogical
  static const String portalConfig = '/api/pedagogical/portal-config';
  static const String myEvaluations = '/api/pedagogical/my-evaluations';
  static const String myPrayer = '/api/pedagogical/prayer/my';
  static String prayerByStudent(int studentId) =>
      '/api/pedagogical/prayer?studentId=$studentId';
  static String parentHome(int studentId) =>
      '/api/pedagogical/parent-home?studentId=$studentId';
  static const String parentHomeSubmit = '/api/pedagogical/parent-home';
  static String matn(int studentId) =>
      '/api/pedagogical/matn?studentId=$studentId';
  static String monthlyTargets(int studentId) =>
      '/api/pedagogical/monthly-targets?studentId=$studentId';

  // Messages
  static const String messages = '/api/messages';
  static const String messagesSent = '/api/messages/sent';
  static const String messagesRecipients = '/api/messages/available-recipients';

  // Announcements
  static const String announcements = '/api/announcements';

  // Oral exams
  static String oralExamsByStudent(int studentId) =>
      '/api/oral-exams/student/$studentId';

  static const String health = '/api/health';

  // ── استعادة كلمة المرور (OTP) ──
  static const String forgotPassword = '/api/auth/forgot-password';
  static const String verifyOtp = '/api/auth/verify-otp';
  static const String resetPassword = '/api/auth/reset-password';

  // ── التقييم الشهري للحفظ الجديد ──
  static const String myMonthlyRating = '/api/hifz/monthly-rating/my';
  static String studentMonthlyRating(int studentId) =>
      '/api/hifz/monthly-rating/$studentId';
  static const String monthlyRatings = '/api/hifz/monthly-ratings';

  // ── المحفّظ (نفس endpoints واجهة الويب) ──
  static const String dashboardStats = '/api/dashboard/stats';
  static const String dashboardActivities = '/api/dashboard/activities';
  static const String dashboardLeaderboard = '/api/dashboard/leaderboard';
  static const String circles = '/api/circles';
  static String circle(int id) => '/api/circles/$id';
  static String attendanceByCircle(int circleId) => '/api/attendance/circle/$circleId';
  static const String attendanceBulk = '/api/attendance/bulk';
  static const String notifyAbsence = '/api/notifications/absence';
  static const String notifyBulkAbsence = '/api/notifications/bulk-absence';
  static const String notifyHifzPraise = '/api/notifications/hifz-praise';
  static const String students = '/api/students';
  static String student(int id) => '/api/students/$id';
  static const String parentsLookup = '/api/parents/lookup';
  static const String waitingList = '/api/waiting-list';
  static String waitingEntry(int id) => '/api/waiting-list/$id';
  static const String hifz = '/api/hifz';
  static String hifzRecord(int id) => '/api/hifz/$id';
  static const String hifzRecent = '/api/hifz/recent';
  static String hifzLastPosition(int studentId) => '/api/hifz/last-position/$studentId';
  static const String oralExams = '/api/oral-exams';
  static const String gradingDefaults = '/api/pedagogical/grading-defaults';
  static const String matns = '/api/matns';
  static const String matnRecords = '/api/pedagogical/matn';
  static String matnRecord(int id) => '/api/pedagogical/matn/$id';
  static const String monthlyTargetsBase = '/api/pedagogical/monthly-targets';
  static const String monthlyTargetsSync = '/api/pedagogical/monthly-targets/sync-from-oral';
  static const String periods = '/api/pedagogical/periods';
  static String periodEvaluations(int id) => '/api/pedagogical/periods/$id/evaluations';
  static String periodAutoDraft(int id, int studentId) =>
      '/api/pedagogical/periods/$id/auto-draft/$studentId';
  static const String dress = '/api/pedagogical/dress';
  static const String dressBulk = '/api/pedagogical/dress/bulk';
  static String prayerOverride(int id) => '/api/pedagogical/prayer/$id/override';
  static const String messagesInbox = '/api/messages/inbox';
  static String messageRead(int id) => '/api/messages/$id/read';
  static const String libraryUpload = '/api/library/upload';
  static String libraryItem(int id) => '/api/library/$id';
}
