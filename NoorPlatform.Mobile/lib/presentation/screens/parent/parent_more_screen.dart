import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';
import '../../blocs/notifications/notifications_cubit.dart';
import '../../widgets/async_body.dart';

class ParentMoreScreen extends StatelessWidget {
  const ParentMoreScreen({super.key});

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
      body: BlocBuilder<NotificationsCubit, NotificationsState>(
        builder: (context, notif) {
          return ListView(
            children: [
              MoreMenuTile(
                icon: Icons.notifications_outlined,
                title: 'مركز الإشعارات',
                subtitle: notif.unreadCount > 0
                    ? '${notif.unreadCount} غير مقروء'
                    : 'الإعلانات وتنبيهات الفواتير',
                badgeCount: notif.unreadCount,
                onTap: () => context.push('/parent/notifications'),
              ),
              MoreMenuTile(
                icon: Icons.home_outlined,
                title: 'الملاحظة المنزلية الأسبوعية',
                onTap: () => context.push('/parent/home-note'),
              ),
              MoreMenuTile(
                icon: Icons.assessment_outlined,
                title: 'تقييمات الأبناء',
                onTap: () => context.push('/parent/evaluations'),
              ),
              MoreMenuTile(
                icon: Icons.campaign_outlined,
                title: 'الإعلانات',
                onTap: () => context.push('/parent/announcements'),
              ),
              MoreMenuTile(
                icon: Icons.library_books_outlined,
                title: 'المكتبة الرقمية',
                onTap: () => context.push('/parent/library'),
              ),
              MoreMenuTile(
                icon: Icons.settings_outlined,
                title: 'الإعدادات',
                subtitle: 'المظهر والمعلومات',
                onTap: () => context.push('/parent/settings'),
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
          );
        },
      ),
    );
  }
}
