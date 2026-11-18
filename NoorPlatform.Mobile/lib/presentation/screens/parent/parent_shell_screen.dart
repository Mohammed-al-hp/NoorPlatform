import '../../widgets/today_date_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/parent_models.dart';
import '../../../di/injection_container.dart';
import '../../blocs/parent_dashboard/parent_dashboard_bloc.dart';
import '../../blocs/parent_dashboard/parent_dashboard_event.dart';
import '../../blocs/parent_dashboard/parent_dashboard_state.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_state.dart';
import '../../blocs/notifications/notifications_cubit.dart';
import '../../widgets/async_body.dart';
import '../../widgets/noor_widgets.dart';
import '../../widgets/monthly_rating_card.dart';
import 'parent_fees_screen.dart';
import 'parent_messages_screen.dart';
import 'parent_more_screen.dart';

class ParentShellScreen extends StatefulWidget {
  const ParentShellScreen({super.key});

  @override
  State<ParentShellScreen> createState() => _ParentShellScreenState();
}

class _ParentShellScreenState extends State<ParentShellScreen> {
  int _index = 0;

  @override
  void initState() {
    super.initState();
    sl<NotificationsCubit>().startPolling(isParent: true);
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
        create: (_) => sl<ParentDashboardBloc>(),
        child: const ParentDashboardScreen(embedded: true),
      ),
      const ParentFeesScreen(),
      const ParentMessagesScreen(),
      const ParentMoreScreen(),
    ];

    return BlocListener<AuthBloc, AuthState>(
      listener: (context, state) {
        if (state is AuthUnauthenticated) {
          sl<NotificationsCubit>().stopPolling();
          context.go('/login');
        }
      },
      child: BlocBuilder<NotificationsCubit, NotificationsState>(
        builder: (context, notif) {
          final unread = notif.unreadCount;
          return Scaffold(
            body: IndexedStack(index: _index, children: pages),
            bottomNavigationBar: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const TodayDateBar(),
                NavigationBar(
                  selectedIndex: _index,
                  onDestinationSelected: (i) => setState(() => _index = i),
                  destinations: [
                    const NavigationDestination(
                      icon: Icon(Icons.family_restroom_outlined),
                      selectedIcon: Icon(Icons.family_restroom_rounded),
                      label: 'الأبناء',
                    ),
                    const NavigationDestination(
                      icon: Icon(Icons.payments_outlined),
                      selectedIcon: Icon(Icons.payments_rounded),
                      label: 'الرسوم',
                    ),
                    const NavigationDestination(
                      icon: Icon(Icons.mail_outline_rounded),
                      selectedIcon: Icon(Icons.mail_rounded),
                      label: 'الرسائل',
                    ),
                    NavigationDestination(
                      icon: _badgeIcon(Icons.grid_view_outlined, unread),
                      selectedIcon: _badgeIcon(Icons.grid_view_rounded, unread),
                      label: 'المزيد',
                    ),
                  ],
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}

class ParentDashboardScreen extends StatefulWidget {
  final bool embedded;
  const ParentDashboardScreen({super.key, this.embedded = false});

  @override
  State<ParentDashboardScreen> createState() => _ParentDashboardScreenState();
}

class _ParentDashboardScreenState extends State<ParentDashboardScreen> {
  @override
  void initState() {
    super.initState();
    context.read<ParentDashboardBloc>().add(LoadParentDashboard());
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
            onPressed: () =>
                context.read<ParentDashboardBloc>().add(LoadParentDashboard()),
          ),
        ],
      ),
      body: BlocBuilder<ParentDashboardBloc, ParentDashboardState>(
        builder: (context, state) {
          if (state is ParentDashboardLoading ||
              state is ParentDashboardInitial) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state is ParentDashboardError) {
            return ErrorState(
              message: state.message,
              onRetry: () => context.read<ParentDashboardBloc>().add(
                LoadParentDashboard(),
              ),
            );
          }
          if (state is ParentDashboardLoaded) {
            return _ParentBody(summary: state.summary);
          }
          return const SizedBox.shrink();
        },
      ),
    );
  }
}

class _ParentBody extends StatelessWidget {
  final ParentSummaryModel summary;
  const _ParentBody({required this.summary});

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: () async {
        context.read<ParentDashboardBloc>().add(LoadParentDashboard());
      },
      child: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          if (summary.alerts.isNotEmpty)
            ...summary.alerts.map(
              (a) => Card(
                color: NoorTheme.warning.withValues(alpha: 0.15),
                child: ListTile(
                  leading: const Icon(
                    Icons.warning_amber,
                    color: NoorTheme.warning,
                  ),
                  title: Text(a.message),
                  subtitle: Text(
                    'المبلغ: ${Formatters.number(a.amount, fractionDigits: 2)}',
                  ),
                  trailing: TextButton(
                    onPressed: () => context.push('/parent/fees'),
                    child: const Text('الرسوم'),
                  ),
                ),
              ),
            ),
          if (summary.children.isEmpty)
            const EmptyState(
              title: 'لا يوجد أبناء مرتبطون',
              icon: Icons.child_care_outlined,
            )
          else
            ...summary.children.map((c) => _ChildCard(child: c)),
        ],
      ),
    );
  }
}

class _ChildCard extends StatelessWidget {
  final ChildDataModel child;
  const _ChildCard({required this.child});

  @override
  Widget build(BuildContext context) {
    return SectionCard(
      child: InkWell(
        onTap: () => context.push('/parent/child/${child.id}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(
                  backgroundColor: NoorTheme.primaryGreen,
                  child: Text(
                    child.fullName.isNotEmpty ? child.fullName[0] : '?',
                    style: const TextStyle(color: Colors.white),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    child.fullName,
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                ),
                const Icon(Icons.chevron_left),
              ],
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                StatChip(
                  label: 'الحفظ',
                  value: '${Formatters.number(child.progress)}%',
                  icon: Icons.menu_book,
                ),
                StatChip(
                  label: 'الحضور',
                  value: '${Formatters.number(child.attendance)}%',
                  icon: Icons.calendar_today,
                ),
                StatChip(
                  label: 'النقاط',
                  value: Formatters.number(child.points),
                  icon: Icons.star,
                  color: NoorTheme.accentGold,
                ),
                if (child.monthlyHifzRating != null)
                  StatChip(
                    label:
                        'تقييم الشهر (${child.monthlyHifzRating!.successfulSessions}/${child.monthlyHifzRating!.target})',
                    value: '${formatScore(child.monthlyHifzRating!.score)}/10',
                    icon: Icons.workspace_premium,
                    color: scoreColor(child.monthlyHifzRating!.score),
                  ),
              ],
            ),
            if (child.monthlyHifzRating?.warning != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  '⚠ ${child.monthlyHifzRating!.warning}',
                  style: Theme.of(
                    context,
                  ).textTheme.bodySmall?.copyWith(color: NoorTheme.warning),
                ),
              ),
            if (child.badgeList.isNotEmpty) ...[
              const SizedBox(height: 8),
              Wrap(
                spacing: 6,
                children: child.badgeList
                    .map(
                      (b) => Chip(
                        avatar: const Icon(Icons.military_tech, size: 14),
                        label: Text(b, style: const TextStyle(fontSize: 12)),
                      ),
                    )
                    .toList(),
              ),
            ],
            const SizedBox(height: 8),
            Text(
              'آخر ملاحظة: ${child.lastNote}',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
          ],
        ),
      ),
    );
  }
}
