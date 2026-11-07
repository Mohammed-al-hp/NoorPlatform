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
}
