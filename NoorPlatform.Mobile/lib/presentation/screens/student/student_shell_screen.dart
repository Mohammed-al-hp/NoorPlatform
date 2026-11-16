import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../di/injection_container.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_state.dart';
import '../../blocs/notifications/notifications_cubit.dart';
import '../../blocs/student_dashboard/student_dashboard_bloc.dart';
import '../../../core/notifications/prayer_reminder_service.dart';
import 'student_dashboard_screen.dart';
import 'attendance_screen.dart';
import 'student_prayer_screen.dart';
import 'student_more_screen.dart';

class StudentShellScreen extends StatefulWidget {
  final int initialIndex;
  const StudentShellScreen({super.key, this.initialIndex = 0});

  @override
  State<StudentShellScreen> createState() => _StudentShellScreenState();
}

class _StudentShellScreenState extends State<StudentShellScreen> {
  late int _index;

  @override
  void initState() {
    super.initState();
    _index = widget.initialIndex;
    sl<NotificationsCubit>().refresh(isParent: false);
    sl<PrayerReminderService>().syncFromPreferences();
  }

  Widget _badgeIcon(IconData icon, int count) {
    return Badge(
      isLabelVisible: count > 0,
      label: Text(count > 99 ? '99+' : '$count'),
      child: Icon(icon),
    );
  }

  @override
  Widget build(BuildContext context) {
    final pages = [
      BlocProvider(
        create: (_) => sl<StudentDashboardBloc>(),
        child: const StudentDashboardScreen(embedded: true),
      ),
      const AttendanceScreen(),
      const StudentPrayerScreen(),
      const StudentMoreScreen(),
    ];

    return BlocListener<AuthBloc, AuthState>(
      listener: (context, state) {
        if (state is AuthUnauthenticated) {
          context.go('/login');
        }
      },
      child: BlocBuilder<NotificationsCubit, NotificationsState>(
        builder: (context, notif) {
          final unread = notif.unreadCount;
          return Scaffold(
            body: IndexedStack(index: _index, children: pages),
            bottomNavigationBar: NavigationBar(
              selectedIndex: _index,
              onDestinationSelected: (i) => setState(() => _index = i),
              destinations: [
                const NavigationDestination(
                  icon: Icon(Icons.home_outlined),
                  selectedIcon: Icon(Icons.home_rounded),
                  label: 'الرئيسية',
                ),
                const NavigationDestination(
                  icon: Icon(Icons.fact_check_outlined),
                  selectedIcon: Icon(Icons.fact_check_rounded),
                  label: 'الحضور',
                ),
                const NavigationDestination(
                  icon: Icon(Icons.mosque_outlined),
                  selectedIcon: Icon(Icons.mosque_rounded),
                  label: 'الصلاة',
                ),
                NavigationDestination(
                  icon: _badgeIcon(Icons.grid_view_outlined, unread),
                  selectedIcon: _badgeIcon(Icons.grid_view_rounded, unread),
                  label: 'المزيد',
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
