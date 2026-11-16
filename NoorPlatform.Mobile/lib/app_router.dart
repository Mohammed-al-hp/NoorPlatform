import 'dart:async';
import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'presentation/screens/admin/admin_web_only_screen.dart';
import 'presentation/screens/auth/login_screen.dart';
import 'presentation/screens/auth/change_password_screen.dart';
import 'presentation/screens/auth/forgot_password_screen.dart';
import 'presentation/screens/teacher/teacher_shell_screen.dart';
import 'presentation/screens/teacher/teacher_students_screens.dart';
import 'presentation/screens/teacher/teacher_hifz_screens.dart';
import 'presentation/screens/teacher/teacher_pedagogical_screens.dart';
import 'presentation/screens/teacher/teacher_misc_screens.dart';
import 'presentation/screens/student/student_shell_screen.dart';
import 'presentation/screens/student/attendance_screen.dart';
import 'presentation/screens/parent/parent_shell_screen.dart';
import 'presentation/screens/parent/parent_child_details_screen.dart';
import 'presentation/screens/parent/parent_home_note_screen.dart';
import 'presentation/screens/parent/parent_fees_screen.dart';
import 'presentation/screens/splash/splash_screen.dart';
import 'presentation/screens/shared/hifz_screens.dart';
import 'presentation/screens/shared/portal_screens.dart';
import 'presentation/screens/shared/notifications_inbox_screen.dart';
import 'presentation/screens/shared/settings_screen.dart';
import 'core/network/api_interceptor.dart' as interceptor;
import 'core/storage/secure_storage.dart';
import 'di/injection_container.dart';
import 'presentation/blocs/attendance/attendance_bloc.dart';

class _AuthRefreshNotifier extends ChangeNotifier {
  late final StreamSubscription<interceptor.AuthEvent> _subscription;

  _AuthRefreshNotifier() {
    _subscription = interceptor.AuthEventBus().stream.listen((event) {
      if (event == interceptor.AuthEvent.tokenExpired ||
          event == interceptor.AuthEvent.forceLogout ||
          event == interceptor.AuthEvent.mustChangePassword) {
        notifyListeners();
      }
    });
  }

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}

class AppRouter {
  final SecureStorage secureStorage;
  final _AuthRefreshNotifier _refreshNotifier = _AuthRefreshNotifier();

  AppRouter(this.secureStorage);

  static const _validRoles = {'Student', 'Parent', 'Teacher', 'Admin'};

  /// الصفحة الرئيسية لكل دور.
  static String homeFor(String role) => switch (role) {
        'Student' => '/student',
        'Parent' => '/parent',
        'Admin' => '/admin',
        _ => '/teacher',
      };

  late final GoRouter router = GoRouter(
    initialLocation: '/',
    debugLogDiagnostics: kDebugMode,
    refreshListenable: _refreshNotifier,
    redirect: (context, state) async {
      final loc = state.matchedLocation;
      final isLoggingIn = loc == '/login';
      final isForgot = loc == '/forgot-password';
      final isSplash = loc == '/';
      final isChangePassword = loc.startsWith('/change-password');

      if (isSplash) return null;

      final hasToken = await secureStorage.hasToken();
      final user = await secureStorage.getUserJson();
      final mustChange = await secureStorage.getMustChangePassword();

      if (!hasToken || user == null) {
        return (isLoggingIn || isForgot) ? null : '/login';
      }

      final role = user['role'] as String?;
      if (role == null || !_validRoles.contains(role)) {
        await secureStorage.clearAll();
        return '/login';
      }

      if (mustChange && !isChangePassword) {
        return '/change-password?forced=true';
      }

      if (isLoggingIn || isForgot) {
        if (mustChange) return '/change-password?forced=true';
        return homeFor(role);
      }

      // حماية المسارات بالدور: /student للطالب، /parent لولي الأمر، /teacher للمحفّظ
      final routeRole = loc.startsWith('/student')
          ? 'Student'
          : loc.startsWith('/parent')
              ? 'Parent'
              : loc.startsWith('/teacher')
                  ? 'Teacher'
                  : loc.startsWith('/admin')
                      ? 'Admin'
                      : null;
      // المشرف: شاشة «لوحة التحكم عبر الويب» فقط (وتغيير كلمة المرور)
      if (role == 'Admin' && routeRole != 'Admin' && !isChangePassword) return '/admin';
      if (routeRole != null && routeRole != role) return homeFor(role);

      return null;
    },
    routes: [
      GoRoute(path: '/', builder: (_, __) => const SplashScreen()),
      GoRoute(path: '/login', builder: (_, __) => const LoginScreen()),
      GoRoute(path: '/admin', builder: (_, __) => const AdminWebOnlyScreen()),
      GoRoute(path: '/forgot-password', builder: (_, __) => const ForgotPasswordScreen()),

      // Teacher (المحفّظ)
      GoRoute(path: '/teacher', builder: (_, __) => const TeacherShellScreen()),
      GoRoute(
        path: '/teacher/student/:id',
        builder: (_, state) =>
            TeacherStudentDetailsScreen(studentId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/teacher/student/:id/hifz',
        builder: (_, state) =>
            HifzLogScreen(studentId: int.parse(state.pathParameters['id']!), isMine: false),
      ),
      GoRoute(
        path: '/teacher/student/:id/quran-map',
        builder: (_, state) =>
            QuranMapScreen(studentId: int.parse(state.pathParameters['id']!), isMine: false),
      ),
      GoRoute(
        path: '/teacher/student/:id/exams',
        builder: (_, state) => OralExamsScreen(studentId: int.parse(state.pathParameters['id']!)),
      ),
      GoRoute(
        path: '/teacher/hifz/new',
        builder: (_, state) => HifzSessionFormScreen(
          studentId: int.tryParse(state.uri.queryParameters['studentId'] ?? ''),
        ),
      ),
      GoRoute(
        path: '/teacher/monthly-ratings',
        builder: (_, __) => const MonthlyRatingsReportScreen(),
      ),
      GoRoute(
        path: '/teacher/evaluation',
        builder: (_, state) => TeacherEvaluationScreen(
          initialTab: int.tryParse(state.uri.queryParameters['tab'] ?? '') ?? 0,
          studentId: int.tryParse(state.uri.queryParameters['studentId'] ?? ''),
        ),
      ),
      GoRoute(path: '/teacher/messages', builder: (_, __) => const TeacherInboxScreen()),
      GoRoute(path: '/teacher/announcements', builder: (_, __) => const TeacherAnnouncementsScreen()),
      GoRoute(path: '/teacher/library', builder: (_, __) => const TeacherLibraryScreen()),
      GoRoute(path: '/teacher/settings', builder: (_, __) => const SettingsScreen()),
      GoRoute(
        path: '/change-password',
        builder: (context, state) {
          final forced = state.uri.queryParameters['forced'] == 'true';
          return ChangePasswordScreen(forced: forced);
        },
      ),

      // Student
      GoRoute(path: '/student', builder: (_, __) => const StudentShellScreen()),
      GoRoute(
        path: '/student/attendance',
        builder: (_, __) => BlocProvider(
          create: (_) => sl<AttendanceBloc>(),
          child: const AttendanceScreen(),
        ),
      ),
      GoRoute(
        path: '/student/hifz',
        builder: (_, __) => const HifzLogScreen(isMine: true),
      ),
      GoRoute(
        path: '/student/quran-map',
        builder: (_, __) => const QuranMapScreen(isMine: true),
      ),
      GoRoute(
        path: '/student/evaluations',
        builder: (_, __) => const EvaluationsScreen(),
      ),
      GoRoute(
        path: '/student/announcements',
        builder: (_, __) => const AnnouncementsScreen(),
      ),
      GoRoute(
        path: '/student/library',
        builder: (_, __) => const LibraryScreen(),
      ),
      GoRoute(
        path: '/teacher/notifications',
        builder: (_, __) => const NotificationsInboxScreen(isParent: false, isTeacher: true),
      ),
      GoRoute(
        path: '/student/notifications',
        builder: (_, __) => const NotificationsInboxScreen(isParent: false),
      ),
      GoRoute(
        path: '/student/settings',
        builder: (_, __) => const SettingsScreen(),
      ),
      GoRoute(
        path: '/student/monthly-targets',
        builder: (context, state) {
          final id = int.parse(state.uri.queryParameters['studentId']!);
          return MonthlyTargetsScreen(studentId: id);
        },
      ),
      GoRoute(
        path: '/student/matn',
        builder: (context, state) {
          final id = int.parse(state.uri.queryParameters['studentId']!);
          return MatnLogScreen(studentId: id);
        },
      ),
      GoRoute(
        path: '/student/exams',
        builder: (context, state) {
          final id = int.parse(state.uri.queryParameters['studentId']!);
          return OralExamsScreen(studentId: id);
        },
      ),

      // Parent
      GoRoute(path: '/parent', builder: (_, __) => const ParentShellScreen()),
      GoRoute(
        path: '/parent/fees',
        builder: (_, __) => const ParentFeesScreen(),
      ),
      GoRoute(
        path: '/parent/home-note',
        builder: (_, __) => const ParentHomeNoteScreen(),
      ),
      GoRoute(
        path: '/parent/evaluations',
        builder: (_, __) => const EvaluationsScreen(),
      ),
      GoRoute(
        path: '/parent/announcements',
        builder: (_, __) => const AnnouncementsScreen(),
      ),
      GoRoute(
        path: '/parent/library',
        builder: (_, __) => const LibraryScreen(),
      ),
      GoRoute(
        path: '/parent/notifications',
        builder: (_, __) => const NotificationsInboxScreen(isParent: true),
      ),
      GoRoute(
        path: '/parent/settings',
        builder: (_, __) => const SettingsScreen(),
      ),
      GoRoute(
        path: '/parent/child/:id',
        builder: (context, state) {
          final id = int.parse(state.pathParameters['id']!);
          return ParentChildDetailsScreen(studentId: id);
        },
      ),
      GoRoute(
        path: '/parent/child/:id/quran-map',
        builder: (context, state) {
          final id = int.parse(state.pathParameters['id']!);
          return QuranMapScreen(studentId: id, isMine: false);
        },
      ),
      GoRoute(
        path: '/parent/child/:id/exams',
        builder: (context, state) {
          final id = int.parse(state.pathParameters['id']!);
          return OralExamsScreen(studentId: id);
        },
      ),
      GoRoute(
        path: '/parent/child/:id/matn',
        builder: (context, state) {
          final id = int.parse(state.pathParameters['id']!);
          return MatnLogScreen(studentId: id);
        },
      ),
      GoRoute(
        path: '/parent/child/:id/monthly-targets',
        builder: (context, state) {
          final id = int.parse(state.pathParameters['id']!);
          return MonthlyTargetsScreen(studentId: id);
        },
      ),
    ],
  );
}
