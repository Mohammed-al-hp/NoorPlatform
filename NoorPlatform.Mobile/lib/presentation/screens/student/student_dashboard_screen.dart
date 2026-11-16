import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/student_summary_model.dart';
import '../../blocs/student_dashboard/student_dashboard_bloc.dart';
import '../../blocs/student_dashboard/student_dashboard_event.dart';
import '../../blocs/student_dashboard/student_dashboard_state.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';
import '../../widgets/async_body.dart';
import '../../widgets/noor_widgets.dart';
import '../../widgets/monthly_rating_card.dart';
import '../../widgets/wird_reminder_card.dart';

class StudentDashboardScreen extends StatefulWidget {
  final bool embedded;
  const StudentDashboardScreen({super.key, this.embedded = false});

  @override
  State<StudentDashboardScreen> createState() => _StudentDashboardScreenState();
}

class _StudentDashboardScreenState extends State<StudentDashboardScreen> {
  @override
  void initState() {
    super.initState();
    context.read<StudentDashboardBloc>().add(LoadStudentDashboard());
  }

  Future<void> _logout() async {
    final authBloc = context.read<AuthBloc>();
    authBloc.add(LogoutRequested());
    await authBloc.stream.firstWhere((state) => state is AuthUnauthenticated);
    if (mounted) context.go('/login');
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const NoorLogo(size: 34, showCenterName: true),
        centerTitle: false,
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () => context
                .read<StudentDashboardBloc>()
                .add(LoadStudentDashboard()),
          ),
          if (!widget.embedded)
            IconButton(icon: const Icon(Icons.logout), onPressed: _logout),
        ],
      ),
      body: BlocBuilder<StudentDashboardBloc, StudentDashboardState>(
        builder: (context, state) {
          if (state is StudentDashboardLoading ||
              state is StudentDashboardInitial) {
            return const AsyncBody(
              pullToRefresh: false,
              isLoading: true,
              isEmpty: false,
              child: SizedBox.shrink(),
            );
          }
          if (state is StudentDashboardError) {
            return AsyncBody(
              pullToRefresh: false,
              isLoading: false,
              isEmpty: false,
              errorMessage: state.message,
              onRetry: () => context
                  .read<StudentDashboardBloc>()
                  .add(LoadStudentDashboard()),
              child: const SizedBox.shrink(),
            );
          }
          if (state is StudentDashboardLoaded) {
            return _DashboardBody(s: state.summary);
          }
          return const SizedBox.shrink();
        },
      ),
    );
  }
}

class _DashboardBody extends StatelessWidget {
  final StudentSummaryModel s;
  const _DashboardBody({required this.s});

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: () async {
        context.read<StudentDashboardBloc>().add(LoadStudentDashboard());
      },
      child: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          const WirdReminderCard(),
          Container(
            padding: const EdgeInsets.all(NoorTheme.spacingMd),
            decoration: BoxDecoration(
              gradient: NoorTheme.primaryGradient,
              borderRadius: BorderRadius.circular(NoorTheme.radiusLg),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'مرحباً، ${s.fullName}',
                  style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                        color: NoorTheme.textOnPrimary,
                      ),
                ),
                const SizedBox(height: 6),
                Text(
                  '${s.circleName} · المحفظ: ${s.teacherName}',
                  style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                        color: NoorTheme.surfaceLight,
                      ),
                ),
                if (s.badgeList.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: s.badgeList
                        .map(
                          (b) => Chip(
                            avatar: const Icon(Icons.military_tech, size: 16),
                            label: Text(b),
                            backgroundColor: Colors.white.withValues(alpha: 0.15),
                            labelStyle: const TextStyle(color: Colors.white),
                          ),
                        )
                        .toList(),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: NoorTheme.spacingMd),
          Row(
            children: [
              Expanded(
                child: _MiniStat(
                  label: 'الحضور',
                  value: '${Formatters.number(s.attendancePercentage)}%',
                  icon: Icons.calendar_today,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _MiniStat(
                  label: 'النقاط',
                  value: Formatters.number(s.points),
                  icon: Icons.star,
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: _MiniStat(
                  label: 'الحفظ',
                  value: '${Formatters.number(s.hifzProgress)}%',
                  icon: Icons.menu_book,
                ),
              ),
            ],
          ),
          const SizedBox(height: NoorTheme.spacingMd),
          if (s.monthlyHifzRating != null) MonthlyRatingCard(rating: s.monthlyHifzRating!),
          SectionCard(
            title: 'الهدف الأسبوعي',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                LinearProgressIndicator(
                  value: s.weeklyGoal > 0 ? s.weeklyVerses / s.weeklyGoal : 0,
                  minHeight: 10,
                  borderRadius: BorderRadius.circular(5),
                ),
                const SizedBox(height: 8),
                Text(
                  '${Formatters.number(s.weeklyVerses)} / ${Formatters.number(s.weeklyGoal)} آية',
                ),
              ],
            ),
          ),
          if (s.nextReview != null)
            SectionCard(
              title: 'ورد المراجعة القادم',
              child: ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const Icon(Icons.replay, color: NoorTheme.primaryGreen),
                title: Text(s.nextReview!.surah),
                subtitle: Text(
                  '${s.nextReview!.verses} · ${Formatters.displayDate(s.nextReview!.date)}',
                ),
              ),
            ),
          if (s.circleTop3.isNotEmpty)
            SectionCard(
              title: 'أعلى 3 في الحلقة',
              child: Column(
                children: s.circleTop3.asMap().entries.map((e) {
                  final icons = [
                    Icons.looks_one,
                    Icons.looks_two,
                    Icons.looks_3,
                  ];
                  return ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: Icon(
                      icons[e.key.clamp(0, 2)],
                      color: NoorTheme.accentGold,
                    ),
                    title: Text(
                      e.value.fullName,
                      style: TextStyle(
                        fontWeight: e.value.isCurrentUser
                            ? FontWeight.bold
                            : FontWeight.normal,
                      ),
                    ),
                    trailing: Text('${Formatters.number(e.value.points)} نقطة'),
                  );
                }).toList(),
              ),
            ),
          if (s.recentHifz.isNotEmpty)
            SectionCard(
              title: 'آخر جلسات التسميع',
              actions: [
                TextButton(
                  onPressed: () => context.push('/student/hifz'),
                  child: const Text('الكل'),
                ),
              ],
              child: Column(
                children: s.recentHifz
                    .take(3)
                    .map(
                      (h) => ListTile(
                        contentPadding: EdgeInsets.zero,
                        title: Text(h.surahName),
                        subtitle: Text(
                          '${h.verses} · ${Formatters.displayDate(h.date)}',
                        ),
                        trailing: Text(h.evaluation),
                      ),
                    )
                    .toList(),
              ),
            ),
          if (s.teacherNotes.isNotEmpty)
            SectionCard(
              title: 'ملاحظات المحفّظ',
              child: Column(
                children: s.teacherNotes
                    .take(3)
                    .map(
                      (n) => ListTile(
                        contentPadding: EdgeInsets.zero,
                        leading: const Icon(Icons.sticky_note_2_outlined),
                        title: Text(n.notes),
                        subtitle: Text(
                          '${n.teacherName} · ${Formatters.displayDate(n.date)}',
                        ),
                      ),
                    )
                    .toList(),
              ),
            ),
        ],
      ),
    );
  }
}

class _MiniStat extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  const _MiniStat({
    required this.label,
    required this.value,
    required this.icon,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Theme.of(context).cardColor,
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
        boxShadow: NoorTheme.cardShadow,
      ),
      child: Column(
        children: [
          Icon(icon, color: NoorTheme.primaryGreen),
          const SizedBox(height: 4),
          Text(value, style: Theme.of(context).textTheme.titleMedium),
          Text(label, style: Theme.of(context).textTheme.bodySmall),
        ],
      ),
    );
  }
}
