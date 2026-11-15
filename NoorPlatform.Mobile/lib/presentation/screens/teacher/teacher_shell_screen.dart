import 'package:dartz/dartz.dart' hide State;
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/errors/failures.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/teacher_models.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';
import '../../widgets/async_body.dart';
import 'teacher_attendance_screen.dart';
import 'teacher_hifz_screens.dart';
import 'teacher_students_screens.dart';
import 'teacher_ui.dart';

/// واجهة المحفّظ الرئيسية — نفس أقسام الويب: الرئيسية، الطلاب، الحضور، تقدم الحفظ والمراجعة، المزيد.
class TeacherShellScreen extends StatefulWidget {
  const TeacherShellScreen({super.key});

  @override
  State<TeacherShellScreen> createState() => _TeacherShellScreenState();
}

class _TeacherShellScreenState extends State<TeacherShellScreen> {
  int _index = 0;

  @override
  Widget build(BuildContext context) {
    const pages = [
      TeacherDashboardScreen(),
      TeacherStudentsScreen(),
      TeacherAttendanceScreen(),
      TeacherHifzHubScreen(),
      TeacherMoreScreen(),
    ];

    return BlocListener<AuthBloc, AuthState>(
      listener: (context, state) {
        if (state is AuthUnauthenticated) context.go('/login');
      },
      child: Scaffold(
        body: IndexedStack(index: _index, children: pages),
        bottomNavigationBar: NavigationBar(
          selectedIndex: _index,
          onDestinationSelected: (i) => setState(() => _index = i),
          destinations: const [
            NavigationDestination(
              icon: Icon(Icons.dashboard_outlined),
              selectedIcon: Icon(Icons.dashboard),
              label: 'الرئيسية',
            ),
            NavigationDestination(
              icon: Icon(Icons.school_outlined),
              selectedIcon: Icon(Icons.school),
              label: 'الطلاب',
            ),
            NavigationDestination(
              icon: Icon(Icons.fact_check_outlined),
              selectedIcon: Icon(Icons.fact_check),
              label: 'الحضور',
            ),
            NavigationDestination(
              icon: Icon(Icons.menu_book_outlined),
              selectedIcon: Icon(Icons.menu_book),
              label: 'تقدم الحفظ',
            ),
            NavigationDestination(
              icon: Icon(Icons.menu),
              selectedIcon: Icon(Icons.menu_open),
              label: 'المزيد',
            ),
          ],
        ),
      ),
    );
  }
}

class _DashboardData {
  final TeacherStatsModel stats;
  final List<CircleModel> circles;
  final List<LeaderboardEntryModel> leaderboard;
  final List<ActivityModel> activities;

  const _DashboardData(this.stats, this.circles, this.leaderboard, this.activities);
}

Future<Either<Failure, _DashboardData>> _loadDashboard() async {
  // الطلبات تُرسل بالتوازي مع الحفاظ على نوع كل نتيجة (Future.wait يفقد الأنواع
  // فيفشل getOrElse وقت التشغيل)
  final statsF = teacherRepo.getStats();
  final circlesF = teacherRepo.getCircles();
  final leaderboardF = teacherRepo.getLeaderboard();
  final activitiesF = teacherRepo.getActivities();

  final stats = await statsF;
  final circles = await circlesF;
  final leaderboard = await leaderboardF;
  final activities = await activitiesF;

  // الإحصائيات أساسية؛ بقية الأقسام تُعرض فارغة عند فشلها بدل إيقاف اللوحة كاملة
  return stats.map((s) => _DashboardData(
        s,
        circles.fold((_) => const <CircleModel>[], (v) => v),
        leaderboard.fold((_) => const <LeaderboardEntryModel>[], (v) => v),
        activities.fold((_) => const <ActivityModel>[], (v) => v),
      ));
}

/// لوحة المحفّظ: إحصائيات حلقاته، حضور اليوم والأسبوع، آخر الجلسات، لوحة الشرف، النشاطات.
class TeacherDashboardScreen extends StatelessWidget {
  const TeacherDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('لوحة المحفّظ')),
      body: LoadView<_DashboardData>(
        loader: _loadDashboard,
        builder: (context, d, reload) {
          final s = d.stats;
          return ListView(
            padding: const EdgeInsets.all(NoorTheme.spacingMd),
            children: [
              Wrap(
                spacing: 8,
                runSpacing: 8,
                children: [
                  StatChip(label: 'طلابي', value: '${s.students}', icon: Icons.school),
                  StatChip(label: 'حلقاتي', value: '${s.circles}', icon: Icons.groups, color: NoorTheme.info),
                  StatChip(
                      label: 'حضور اليوم',
                      value: s.attendanceToday,
                      icon: Icons.fact_check,
                      color: NoorTheme.success),
                  StatChip(
                      label: 'حضور 30 يوماً',
                      value: '${s.attendanceLast30}%',
                      icon: Icons.trending_up,
                      color: NoorTheme.accentGold),
                  StatChip(
                      label: 'آيات/طالب أسبوعياً',
                      value: Formatters.number(s.hifzVelocity, fractionDigits: 1),
                      icon: Icons.speed,
                      color: NoorTheme.warning),
                  StatChip(label: 'جدد هذا الشهر', value: '${s.studentsThisMonth}', icon: Icons.person_add),
                ],
              ),
              const SizedBox(height: NoorTheme.spacingMd),
              SectionCard(
                title: 'حلقاتي',
                child: d.circles.isEmpty
                    ? const Text('لا توجد حلقات مسندة إليك')
                    : Column(
                        children: d.circles
                            .map((c) => ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  leading: const Icon(Icons.groups, color: NoorTheme.primaryGreen),
                                  title: Text(c.isExtra ? '${c.name} (إضافية)' : c.name),
                                  subtitle: Text([c.time, c.location].where((e) => e.isNotEmpty).join(' · ')),
                                  trailing: Text('${c.studentCount} طالب'),
                                ))
                            .toList(),
                      ),
              ),
              SectionCard(
                title: 'الحضور خلال الأسبوع',
                child: Column(
                  children: s.weeklyAttendance
                      .map((w) => Padding(
                            padding: const EdgeInsets.symmetric(vertical: 4),
                            child: Row(
                              children: [
                                SizedBox(width: 72, child: Text(w.dayName)),
                                Expanded(
                                  child: LinearProgressIndicator(
                                    value: w.percentage / 100,
                                    minHeight: 8,
                                    borderRadius: BorderRadius.circular(8),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                SizedBox(width: 44, child: Text(w.total == 0 ? '—' : '${w.percentage}%')),
                              ],
                            ),
                          ))
                      .toList(),
                ),
              ),
              SectionCard(
                title: 'مستويات الطلاب',
                child: Wrap(spacing: 8, children: [
                  Chip(label: Text('مبتدئ: ${s.levelBeginner}')),
                  Chip(label: Text('متوسط: ${s.levelIntermediate}')),
                  Chip(label: Text('متقدم: ${s.levelAdvanced}')),
                ]),
              ),
              SectionCard(
                title: 'آخر نشاط التسميع',
                child: s.recentHifz.isEmpty
                    ? const Text('لا توجد جلسات بعد')
                    : Column(
                        children: s.recentHifz
                            .map((r) => ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  dense: true,
                                  title: Text(r.studentName),
                                  subtitle: Text('${r.surahName} (${r.verses}) · ${r.circleName}'),
                                  trailing: Text(r.evaluation),
                                ))
                            .toList(),
                      ),
              ),
              SectionCard(
                title: 'لوحة الشرف',
                child: d.leaderboard.isEmpty
                    ? const Text('لا توجد بيانات')
                    : Column(
                        children: d.leaderboard
                            .map((l) => ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  dense: true,
                                  leading: CircleAvatar(radius: 14, child: Text('${l.rank}')),
                                  title: Text(l.fullName),
                                  subtitle: Text(
                                      '${l.circleName} · حضور ${Formatters.number(l.attendanceRate)}% · حفظ ${l.hifzProgress}%'),
                                  trailing: Text('${l.points} ⭐'),
                                  onTap: () => context.push('/teacher/student/${l.studentId}'),
                                ))
                            .toList(),
                      ),
              ),
              SectionCard(
                title: 'آخر النشاطات',
                child: d.activities.isEmpty
                    ? const Text('لا توجد نشاطات')
                    : Column(
                        children: d.activities
                            .take(10)
                            .map((a) => ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  dense: true,
                                  leading: Text(a.icon, style: const TextStyle(fontSize: 18)),
                                  title: Text(a.description),
                                  subtitle: Text(Formatters.displayDateTime(a.createdAt)),
                                ))
                            .toList(),
                      ),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// قائمة "المزيد" للمحفّظ: الرسائل، التقييم التربوي، التقرير الشهري، الإعلانات، المكتبة، الإعدادات.
class TeacherMoreScreen extends StatelessWidget {
  const TeacherMoreScreen({super.key});

  Future<void> _logout(BuildContext context) async {
    final authBloc = context.read<AuthBloc>();
    authBloc.add(LogoutRequested());
    await authBloc.stream.firstWhere((s) => s is AuthUnauthenticated);
    if (context.mounted) context.go('/login');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('المزيد')),
      body: ListView(
        children: [
          MoreMenuTile(
            icon: Icons.mail_outline,
            title: 'الرسائل الواردة',
            subtitle: 'رسائل أولياء الأمور',
            onTap: () => context.push('/teacher/messages'),
          ),
          MoreMenuTile(
            icon: Icons.assignment_outlined,
            title: 'التقييم التربوي',
            subtitle: 'متون · أهداف · فترات · لباس · صلاة',
            onTap: () => context.push('/teacher/evaluation'),
          ),
          MoreMenuTile(
            icon: Icons.workspace_premium_outlined,
            title: 'التقييم الشهري للحفظ الجديد',
            onTap: () => context.push('/teacher/monthly-ratings'),
          ),
          MoreMenuTile(
            icon: Icons.campaign_outlined,
            title: 'الإعلانات',
            subtitle: 'عرض ونشر الإعلانات',
            onTap: () => context.push('/teacher/announcements'),
          ),
          MoreMenuTile(
            icon: Icons.library_books_outlined,
            title: 'مكتبة الطلاب',
            subtitle: 'عرض ورفع ملفات PDF',
            onTap: () => context.push('/teacher/library'),
          ),
          MoreMenuTile(
            icon: Icons.settings_outlined,
            title: 'الإعدادات',
            subtitle: 'المظهر والمعلومات',
            onTap: () => context.push('/teacher/settings'),
          ),
          MoreMenuTile(
            icon: Icons.lock_outline,
            title: 'تغيير كلمة المرور',
            onTap: () => context.push('/change-password'),
          ),
          const Divider(),
          ListTile(
            leading: const Icon(Icons.logout, color: NoorTheme.error),
            title: const Text('تسجيل الخروج'),
            onTap: () => _logout(context),
          ),
        ],
      ),
    );
  }
}
