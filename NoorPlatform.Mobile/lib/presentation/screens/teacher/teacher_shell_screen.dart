import 'package:dartz/dartz.dart' hide State;
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../widgets/noor_widgets.dart';
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
              icon: Icon(Icons.space_dashboard_outlined),
              selectedIcon: Icon(Icons.space_dashboard_rounded),
              label: 'الرئيسية',
            ),
            NavigationDestination(
              icon: Icon(Icons.school_outlined),
              selectedIcon: Icon(Icons.school_rounded),
              label: 'الطلاب',
            ),
            NavigationDestination(
              icon: Icon(Icons.fact_check_outlined),
              selectedIcon: Icon(Icons.fact_check_rounded),
              label: 'الحضور',
            ),
            NavigationDestination(
              icon: Icon(Icons.menu_book_outlined),
              selectedIcon: Icon(Icons.menu_book_rounded),
              label: 'تقدم الحفظ',
            ),
            NavigationDestination(
              icon: Icon(Icons.grid_view_outlined),
              selectedIcon: Icon(Icons.grid_view_rounded),
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
      appBar: AppBar(
        title: const NoorLogo(size: 34),
        centerTitle: false,
      ),
      body: LoadView<_DashboardData>(
        loader: _loadDashboard,
        builder: (context, d, reload) {
          final s = d.stats;
          final theme = Theme.of(context);
          return ListView(
            padding: const EdgeInsets.fromLTRB(NoorTheme.spacingMd, 4, NoorTheme.spacingMd, NoorTheme.spacingXl),
            children: [
              // ── الرأس: تحية + أبرز أرقام اليوم ──
              GradientHeader(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text('السلام عليكم ورحمة الله',
                        style: TextStyle(color: NoorTheme.goldSoft, fontSize: 14, fontWeight: FontWeight.w600)),
                    const SizedBox(height: 4),
                    Text('لوحة المحفّظ',
                        style: theme.textTheme.headlineSmall?.copyWith(color: Colors.white, fontWeight: FontWeight.w800)),
                    const SizedBox(height: 14),
                    Row(
                      children: [
                        Expanded(child: _HeaderMetric(label: 'حضور اليوم', value: s.attendanceToday)),
                        Container(width: 1, height: 36, color: Colors.white24),
                        Expanded(child: _HeaderMetric(label: 'حضور 30 يوماً', value: '${s.attendanceLast30}%')),
                        Container(width: 1, height: 36, color: Colors.white24),
                        Expanded(child: _HeaderMetric(label: 'طلابي', value: '${s.students}')),
                      ],
                    ),
                  ],
                ),
              ),

              // ── شبكة الإحصائيات ──
              LayoutBuilder(builder: (context, c) {
                final w = (c.maxWidth - 10) / 2;
                Widget tile(Widget child) => SizedBox(width: w, child: child);
                return Wrap(
                  spacing: 10,
                  runSpacing: 10,
                  children: [
                    tile(StatChip(label: 'حلقاتي', value: '${s.circles}', icon: Icons.groups_rounded, color: NoorTheme.info)),
                    tile(StatChip(
                        label: 'آيات/طالب أسبوعياً',
                        value: Formatters.number(s.hifzVelocity, fractionDigits: 1),
                        icon: Icons.speed_rounded,
                        color: NoorTheme.accentGold)),
                    tile(StatChip(
                        label: 'جدد هذا الشهر',
                        value: '${s.studentsThisMonth}',
                        icon: Icons.person_add_alt_1_rounded,
                        color: NoorTheme.success)),
                    tile(StatChip(
                        label: 'متقدمون',
                        value: '${s.levelAdvanced}',
                        icon: Icons.workspace_premium_rounded,
                        color: NoorTheme.primaryGreenLight)),
                  ],
                );
              }),
              const SizedBox(height: NoorTheme.spacingMd),

              SectionCard(
                title: 'حلقاتي',
                icon: Icons.groups_rounded,
                child: d.circles.isEmpty
                    ? const Text('لا توجد حلقات مسندة إليك')
                    : Column(
                        children: [
                          for (final c in d.circles)
                            Padding(
                              padding: const EdgeInsets.symmetric(vertical: 5),
                              child: Row(
                                children: [
                                  Container(
                                    width: 42,
                                    height: 42,
                                    decoration: BoxDecoration(
                                      color: (c.isExtra ? NoorTheme.accentGold : NoorTheme.primaryGreen).withValues(alpha: 0.1),
                                      borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
                                    ),
                                    child: Icon(c.isExtra ? Icons.add_circle_outline : Icons.mosque_outlined,
                                        color: c.isExtra ? NoorTheme.accentGold : theme.colorScheme.primary),
                                  ),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(c.isExtra ? '${c.name} (إضافية)' : c.name, style: theme.textTheme.titleSmall),
                                        Text([c.time, c.location].where((e) => e.isNotEmpty).join(' · '),
                                            style: theme.textTheme.bodySmall),
                                      ],
                                    ),
                                  ),
                                  Chip(label: Text('${c.studentCount} طالب')),
                                ],
                              ),
                            ),
                        ],
                      ),
              ),

              SectionCard(
                title: 'الحضور خلال الأسبوع',
                icon: Icons.insights_rounded,
                child: Column(
                  children: [
                    for (final w in s.weeklyAttendance)
                      Padding(
                        padding: const EdgeInsets.symmetric(vertical: 5),
                        child: Row(
                          children: [
                            SizedBox(width: 70, child: Text(w.dayName, style: theme.textTheme.labelLarge)),
                            Expanded(
                              child: LinearProgressIndicator(
                                value: w.percentage / 100,
                                minHeight: 10,
                                borderRadius: BorderRadius.circular(10),
                                color: w.percentage >= 80
                                    ? NoorTheme.success
                                    : w.percentage >= 50
                                        ? NoorTheme.accentGold
                                        : NoorTheme.error,
                              ),
                            ),
                            const SizedBox(width: 10),
                            SizedBox(
                              width: 44,
                              child: Text(w.total == 0 ? '—' : '${w.percentage}%',
                                  textAlign: TextAlign.end, style: theme.textTheme.labelLarge),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              ),

              SectionCard(
                title: 'مستويات الطلاب',
                icon: Icons.stacked_bar_chart_rounded,
                child: Row(
                  children: [
                    Expanded(child: _LevelBox(label: 'مبتدئ', value: s.levelBeginner, color: NoorTheme.info)),
                    const SizedBox(width: 8),
                    Expanded(child: _LevelBox(label: 'متوسط', value: s.levelIntermediate, color: NoorTheme.accentGold)),
                    const SizedBox(width: 8),
                    Expanded(child: _LevelBox(label: 'متقدم', value: s.levelAdvanced, color: NoorTheme.success)),
                  ],
                ),
              ),

              SectionCard(
                title: 'لوحة الشرف',
                icon: Icons.emoji_events_rounded,
                child: d.leaderboard.isEmpty
                    ? const Text('لا توجد بيانات')
                    : Column(
                        children: [
                          for (final l in d.leaderboard)
                            _LeaderRow(
                              entry: l,
                              onTap: () => context.push('/teacher/student/${l.studentId}'),
                            ),
                        ],
                      ),
              ),

              SectionCard(
                title: 'آخر نشاط التسميع',
                icon: Icons.menu_book_rounded,
                child: s.recentHifz.isEmpty
                    ? const Text('لا توجد جلسات بعد')
                    : Column(
                        children: [
                          for (final r in s.recentHifz)
                            Padding(
                              padding: const EdgeInsets.symmetric(vertical: 6),
                              child: Row(
                                children: [
                                  InitialsAvatar(name: r.studentName, radius: 18),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(r.studentName, style: theme.textTheme.titleSmall),
                                        Text('${r.surahName} (${r.verses}) · ${r.circleName}',
                                            style: theme.textTheme.bodySmall),
                                      ],
                                    ),
                                  ),
                                  if (r.evaluation.isNotEmpty) _EvalBadge(r.evaluation),
                                ],
                              ),
                            ),
                        ],
                      ),
              ),

              SectionCard(
                title: 'آخر النشاطات',
                icon: Icons.history_rounded,
                child: d.activities.isEmpty
                    ? const Text('لا توجد نشاطات')
                    : Column(
                        children: [
                          for (final a in d.activities.take(8))
                            Padding(
                              padding: const EdgeInsets.symmetric(vertical: 6),
                              child: Row(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Container(
                                    width: 34,
                                    height: 34,
                                    alignment: Alignment.center,
                                    decoration: BoxDecoration(
                                      color: theme.colorScheme.primaryContainer,
                                      shape: BoxShape.circle,
                                    ),
                                    child: Text(a.icon, style: const TextStyle(fontSize: 16)),
                                  ),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment: CrossAxisAlignment.start,
                                      children: [
                                        Text(a.description, style: theme.textTheme.bodyLarge?.copyWith(fontSize: 14)),
                                        Text(Formatters.displayDateTime(a.createdAt), style: theme.textTheme.bodySmall),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                        ],
                      ),
              ),
            ],
          );
        },
      ),
    );
  }
}

class _HeaderMetric extends StatelessWidget {
  final String label;
  final String value;
  const _HeaderMetric({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(value,
            style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w800)),
        const SizedBox(height: 2),
        Text(label, style: const TextStyle(color: Colors.white70, fontSize: 12)),
      ],
    );
  }
}

class _LevelBox extends StatelessWidget {
  final String label;
  final int value;
  final Color color;
  const _LevelBox({required this.label, required this.value, required this.color});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      ),
      child: Column(
        children: [
          Text('$value', style: TextStyle(color: color, fontSize: 22, fontWeight: FontWeight.w800)),
          Text(label, style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}

/// صف في لوحة الشرف: ميداليات ملونة للمراكز الثلاثة الأولى.
class _LeaderRow extends StatelessWidget {
  final LeaderboardEntryModel entry;
  final VoidCallback onTap;
  const _LeaderRow({required this.entry, required this.onTap});

  static const _medals = [Color(0xFFDAA520), Color(0xFF9CA3AF), Color(0xFFB87333)];

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final top = entry.rank >= 1 && entry.rank <= 3;
    final medal = top ? _medals[entry.rank - 1] : null;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      child: Container(
        margin: const EdgeInsets.symmetric(vertical: 4),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 10),
        decoration: BoxDecoration(
          color: top ? medal!.withValues(alpha: 0.08) : null,
          borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
        ),
        child: Row(
          children: [
            Container(
              width: 34,
              height: 34,
              alignment: Alignment.center,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                color: medal ?? theme.colorScheme.surfaceContainerHigh,
              ),
              child: top
                  ? const Icon(Icons.emoji_events_rounded, color: Colors.white, size: 18)
                  : Text('${entry.rank}', style: theme.textTheme.labelLarge),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(entry.fullName, style: theme.textTheme.titleSmall),
                  Text(
                    '${entry.circleName} · حضور ${Formatters.number(entry.attendanceRate)}% · حفظ ${entry.hifzProgress}%',
                    style: theme.textTheme.bodySmall,
                  ),
                ],
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                gradient: NoorTheme.goldGradient,
                borderRadius: BorderRadius.circular(NoorTheme.radiusFull),
              ),
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.star_rounded, size: 14, color: Colors.white),
                  const SizedBox(width: 3),
                  Text('${entry.points}',
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w800, fontSize: 13)),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _EvalBadge extends StatelessWidget {
  final String evaluation;
  const _EvalBadge(this.evaluation);

  @override
  Widget build(BuildContext context) {
    final good = evaluation.contains('ممتاز') || evaluation.contains('جيد');
    final c = good ? NoorTheme.success : NoorTheme.warning;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(NoorTheme.radiusFull),
      ),
      child: Text(evaluation, style: TextStyle(color: c, fontSize: 12, fontWeight: FontWeight.w700)),
    );
  }
}

// قائمة "المزيد" للمحفّظ: الرسائل، التقييم التربوي، التقرير الشهري، الإعلانات، المكتبة، الإعدادات.
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
