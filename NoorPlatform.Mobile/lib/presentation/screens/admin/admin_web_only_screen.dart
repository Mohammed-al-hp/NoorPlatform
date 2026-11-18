import '../../widgets/today_date_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../config/api_config.dart';
import '../../../config/theme.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';

/// شاشة المشرف في تطبيق الهاتف: لوحة التحكم الإدارية متاحة عبر منصة الويب فقط.
class AdminWebOnlyScreen extends StatelessWidget {
  const AdminWebOnlyScreen({super.key});

  /// منصة الويب تُخدَم من نفس خادم الـ API.
  static String get webUrl => ApiConfig.baseUrl;

  Future<void> _logout(BuildContext context) async {
    final authBloc = context.read<AuthBloc>();
    authBloc.add(LogoutRequested());
    await authBloc.stream.firstWhere((s) => s is AuthUnauthenticated);
    if (context.mounted) context.go('/login');
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      bottomNavigationBar: const SafeArea(top: false, child: TodayDateBar()),
      body: Container(
        decoration: const BoxDecoration(gradient: NoorTheme.darkGradient),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(NoorTheme.spacingLg),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 420),
                child: Card(
                  elevation: 0,
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(NoorTheme.radiusXl)),
                  child: Padding(
                    padding: const EdgeInsets.fromLTRB(24, 32, 24, 24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Container(
                          width: 84,
                          height: 84,
                          decoration: const BoxDecoration(shape: BoxShape.circle, gradient: NoorTheme.goldGradient),
                          child: const Icon(Icons.admin_panel_settings_outlined, size: 44, color: Colors.white),
                        ),
                        const SizedBox(height: 20),
                        Text('مرحباً بك أيها المشرف',
                            style: theme.textTheme.headlineSmall?.copyWith(fontWeight: FontWeight.w800),
                            textAlign: TextAlign.center),
                        const SizedBox(height: 12),
                        Text(
                          'لوحة تحكم المشرف متاحة عبر منصة الويب فقط.\nيمكنك إدارة الحلقات والمحفّظين والطلاب والفواتير والتقارير من المتصفح.',
                          style: theme.textTheme.bodyMedium?.copyWith(height: 1.7, color: NoorTheme.textSecondary),
                          textAlign: TextAlign.center,
                        ),
                        const SizedBox(height: 20),
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                          decoration: BoxDecoration(
                            color: NoorTheme.primaryGreen.withValues(alpha: 0.08),
                            borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
                          ),
                          child: Row(
                            children: [
                              const Icon(Icons.language, color: NoorTheme.primaryGreen, size: 20),
                              const SizedBox(width: 8),
                              Expanded(
                                child: SelectableText(
                                  webUrl,
                                  textDirection: TextDirection.ltr,
                                  style: const TextStyle(fontWeight: FontWeight.w600, color: NoorTheme.primaryGreen),
                                ),
                              ),
                              IconButton(
                                tooltip: 'نسخ الرابط',
                                icon: const Icon(Icons.copy_rounded, size: 20),
                                onPressed: () async {
                                  await Clipboard.setData(ClipboardData(text: webUrl));
                                  if (context.mounted) {
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      const SnackBar(content: Text('تم نسخ رابط منصة الويب')),
                                    );
                                  }
                                },
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(height: 24),
                        SizedBox(
                          width: double.infinity,
                          child: FilledButton.icon(
                            onPressed: () => _logout(context),
                            icon: const Icon(Icons.logout_rounded),
                            label: const Text('تسجيل الخروج'),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
