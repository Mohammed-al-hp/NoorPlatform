import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/storage/secure_storage.dart';
import '../../../core/utils/file_open_helper.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';
import '../../blocs/notifications/notifications_cubit.dart';
import '../../widgets/async_body.dart';

class StudentMoreScreen extends StatelessWidget {
  const StudentMoreScreen({super.key});

  Future<void> _logout(BuildContext context) async {
    final authBloc = context.read<AuthBloc>();
    authBloc.add(LogoutRequested());
    await authBloc.stream.firstWhere((s) => s is AuthUnauthenticated);
    if (context.mounted) context.go('/login');
  }

  Future<int?> _studentId() => sl<SecureStorage>().getCachedStudentId();

  Future<void> _openCertificate(BuildContext context) async {
    final id = await _studentId();
    if (id == null) {
      if (context.mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('تعذر تحديد هوية الطالب، حدّث لوحة الرئيسية أولاً'),
          ),
        );
      }
      return;
    }

    if (!context.mounted) return;
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(child: CircularProgressIndicator()),
    );

    final result = await sl<PortalRepository>().downloadCertificate(id);
    if (!context.mounted) return;
    Navigator.of(context, rootNavigator: true).pop();

    await result.fold(
      (f) async {
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(f.message),
              backgroundColor: NoorTheme.error,
            ),
          );
        }
      },
      (path) async {
        await FileOpenHelper.openResult(path);
        if (context.mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: Text(
                FileOpenHelper.isBrowserDownload(path)
                    ? 'بدأ تنزيل الشهادة'
                    : 'تم فتح الشهادة',
              ),
            ),
          );
        }
      },
    );
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
                    : 'الإعلانات والتنبيهات',
                badgeCount: notif.unreadCount,
                onTap: () => context.push('/student/notifications'),
              ),
              MoreMenuTile(
                icon: Icons.map_outlined,
                title: 'خريطة الحفظ',
                subtitle: '114 سورة',
                onTap: () => context.push('/student/quran-map'),
              ),
              MoreMenuTile(
                icon: Icons.menu_book_outlined,
                title: 'سجل التسميع والمراجعة',
                onTap: () => context.push('/student/hifz'),
              ),
              MoreMenuTile(
                icon: Icons.assessment_outlined,
                title: 'التقييمات الدورية',
                onTap: () => context.push('/student/evaluations'),
              ),
              MoreMenuTile(
                icon: Icons.flag_outlined,
                title: 'الأهداف الشهرية',
                onTap: () async {
                  final id = await _studentId();
                  if (id == null) {
                    if (context.mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'تعذر تحديد هوية الطالب، حدّث لوحة الرئيسية أولاً',
                          ),
                        ),
                      );
                    }
                    return;
                  }
                  if (context.mounted) {
                    context.push('/student/monthly-targets?studentId=$id');
                  }
                },
              ),
              MoreMenuTile(
                icon: Icons.auto_stories_outlined,
                title: 'سجل المتون',
                onTap: () async {
                  final id = await _studentId();
                  if (id == null || !context.mounted) return;
                  context.push('/student/matn?studentId=$id');
                },
              ),
              MoreMenuTile(
                icon: Icons.quiz_outlined,
                title: 'الاختبارات والنتائج',
                onTap: () async {
                  final id = await _studentId();
                  if (id == null || !context.mounted) return;
                  context.push('/student/exams?studentId=$id');
                },
              ),
              MoreMenuTile(
                icon: Icons.workspace_premium_outlined,
                title: 'الشهادة',
                subtitle: 'تنزيل شهادة الطالب',
                onTap: () => _openCertificate(context),
              ),
              MoreMenuTile(
                icon: Icons.campaign_outlined,
                title: 'الإعلانات',
                onTap: () => context.push('/student/announcements'),
              ),
              MoreMenuTile(
                icon: Icons.library_books_outlined,
                title: 'المكتبة الرقمية',
                onTap: () => context.push('/student/library'),
              ),
              MoreMenuTile(
                icon: Icons.settings_outlined,
                title: 'الإعدادات',
                subtitle: 'المظهر والمعلومات',
                onTap: () => context.push('/student/settings'),
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
