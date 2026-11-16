import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../config/theme.dart';
import '../blocs/notifications/notifications_cubit.dart';

/// جرس الإشعارات مع شارة بعدد غير المقروء — يفتح مركز الإشعارات للدور الحالي.
class NotificationBell extends StatelessWidget {
  final String route;
  const NotificationBell({super.key, required this.route});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<NotificationsCubit, NotificationsState>(
      buildWhen: (a, b) => a.unreadCount != b.unreadCount,
      builder: (context, state) {
        final n = state.unreadCount;
        return IconButton(
          tooltip: n > 0 ? 'الإشعارات ($n غير مقروء)' : 'الإشعارات',
          onPressed: () => context.push(route),
          icon: Badge(
            isLabelVisible: n > 0,
            backgroundColor: NoorTheme.accentGold,
            label: Text(n > 99 ? '99+' : '$n'),
            child: Icon(n > 0 ? Icons.notifications_active_outlined : Icons.notifications_none_rounded),
          ),
        );
      },
    );
  }
}
