import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../blocs/notifications/notifications_cubit.dart';
import '../../widgets/async_body.dart';

/// أيقونة ولون كل نوع إشعار.
(IconData, Color) inboxVisual(InboxItem item) {
  if (item.kind == InboxKind.payment) return (Icons.payments_outlined, NoorTheme.warning);
  if (item.kind == InboxKind.announcement) return (Icons.campaign_outlined, NoorTheme.primaryGreen);
  switch (item.serverKind) {
    case 'Hifz':
      return (Icons.menu_book_rounded, NoorTheme.primaryGreen);
    case 'Absence':
      return (Icons.event_busy_rounded, NoorTheme.error);
    case 'Announcement':
      return (Icons.campaign_outlined, NoorTheme.accentGold);
    case 'Message':
    case 'MessageReply':
      return (Icons.mail_outline_rounded, NoorTheme.info);
    case 'MonthlyReport':
      return (Icons.summarize_outlined, NoorTheme.accentGold);
    default:
      return (Icons.notifications_none_rounded, NoorTheme.primaryGreen);
  }
}

class NotificationsInboxScreen extends StatefulWidget {
  final bool isParent;
  final bool isTeacher;
  const NotificationsInboxScreen({super.key, required this.isParent, this.isTeacher = false});

  @override
  State<NotificationsInboxScreen> createState() =>
      _NotificationsInboxScreenState();
}

class _NotificationsInboxScreenState extends State<NotificationsInboxScreen> {
  @override
  void initState() {
    super.initState();
    sl<NotificationsCubit>().refresh(isParent: widget.isParent, isTeacher: widget.isTeacher);
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider.value(
      value: sl<NotificationsCubit>(),
      child: Scaffold(
        appBar: AppBar(
          title: const Text('مركز الإشعارات'),
          actions: [
            BlocBuilder<NotificationsCubit, NotificationsState>(
              builder: (context, state) {
                if (state.unreadCount == 0) return const SizedBox.shrink();
                return TextButton(
                  onPressed: () =>
                      context.read<NotificationsCubit>().markAllRead(),
                  child: const Text('قراءة الكل'),
                );
              },
            ),
          ],
        ),
        body: BlocBuilder<NotificationsCubit, NotificationsState>(
          builder: (context, state) {
            return AsyncBody(
              pullToRefresh: false,
              isLoading: state.status == NotificationsStatus.loading ||
                  state.status == NotificationsStatus.initial,
              isEmpty: state.status == NotificationsStatus.loaded &&
                  state.items.isEmpty,
              emptyTitle: 'لا توجد إشعارات',
              emptySubtitle: 'ستظهر هنا تنبيهات التسميع والغياب والإعلانات والرسائل',
              emptyIcon: Icons.notifications_none_outlined,
              errorMessage: state.status == NotificationsStatus.error
                  ? state.error
                  : null,
              onRetry: () => context
                  .read<NotificationsCubit>()
                  .refresh(isParent: widget.isParent, isTeacher: widget.isTeacher),
              child: RefreshIndicator(
                onRefresh: () => context
                    .read<NotificationsCubit>()
                    .refresh(isParent: widget.isParent, isTeacher: widget.isTeacher),
                child: ListView.separated(
                  padding: const EdgeInsets.all(NoorTheme.spacingMd),
                  itemCount: state.items.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 8),
                  itemBuilder: (context, i) {
                    final item = state.items[i];
                    return _InboxTile(
                      item: item,
                      onTap: () async {
                        await context
                            .read<NotificationsCubit>()
                            .markRead(item.id);
                        if (!context.mounted) return;
                        _showDetails(context, item);
                      },
                    );
                  },
                ),
              ),
            );
          },
        ),
      ),
    );
  }

  void _showDetails(BuildContext context, InboxItem item) {
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) {
        return Padding(
          padding: EdgeInsets.only(
            left: 20,
            right: 20,
            top: 20,
            bottom: MediaQuery.of(ctx).viewInsets.bottom + 24,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                children: [
                  Icon(inboxVisual(item).$1, color: inboxVisual(item).$2),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      item.title,
                      style: Theme.of(ctx).textTheme.titleLarge,
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              ConstrainedBox(
                constraints: BoxConstraints(maxHeight: MediaQuery.of(ctx).size.height * 0.55),
                child: SingleChildScrollView(child: Text(item.body)),
              ),
              if (item.date != null) ...[
                const SizedBox(height: 12),
                Text(
                  Formatters.displayDateTime(item.date),
                  style: Theme.of(ctx).textTheme.bodySmall,
                ),
              ],
              if (item.actionRoute != null) ...[
                const SizedBox(height: 16),
                ElevatedButton(
                  onPressed: () {
                    Navigator.pop(ctx);
                    context.push(item.actionRoute!);
                  },
                  child: Text(
                    item.kind == InboxKind.payment
                        ? 'عرض الرسوم'
                        : item.kind == InboxKind.announcement || item.serverKind == 'Announcement'
                            ? 'عرض الإعلانات'
                            : 'عرض التفاصيل',
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}

class _InboxTile extends StatelessWidget {
  final InboxItem item;
  final VoidCallback onTap;

  const _InboxTile({required this.item, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: item.isRead
          ? scheme.surface
          : NoorTheme.primaryGreen.withValues(alpha: 0.08),
      borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      child: InkWell(
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              CircleAvatar(
                backgroundColor: inboxVisual(item).$2.withValues(alpha: 0.15),
                child: Icon(inboxVisual(item).$1, color: inboxVisual(item).$2),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            item.title,
                            style: Theme.of(context).textTheme.titleSmall?.copyWith(
                                  fontWeight: item.isRead
                                      ? FontWeight.w500
                                      : FontWeight.w800,
                                ),
                          ),
                        ),
                        if (!item.isRead)
                          Container(
                            width: 8,
                            height: 8,
                            decoration: const BoxDecoration(
                              color: NoorTheme.accentGold,
                              shape: BoxShape.circle,
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      item.body,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: Theme.of(context).textTheme.bodyMedium,
                    ),
                    if (item.date != null) ...[
                      const SizedBox(height: 6),
                      Text(
                        Formatters.displayDateTime(item.date),
                        style: Theme.of(context).textTheme.bodySmall,
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
