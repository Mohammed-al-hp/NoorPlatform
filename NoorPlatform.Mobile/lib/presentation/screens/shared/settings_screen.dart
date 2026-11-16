import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../config/constants.dart';
import '../../../config/theme.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_state.dart';
import '../../blocs/reminder/reminder_cubit.dart';
import '../../blocs/theme/theme_cubit.dart';
import '../../widgets/async_body.dart';

class SettingsScreen extends StatelessWidget {
  const SettingsScreen({super.key});

  Future<void> _pickTime(BuildContext context) async {
    final cubit = context.read<ReminderCubit>();
    final picked = await showTimePicker(
      context: context,
      initialTime: TimeOfDay(hour: cubit.state.hour, minute: cubit.state.minute),
      builder: (context, child) {
        return Localizations.override(
          context: context,
          locale: const Locale('ar', 'LY'),
          child: child,
        );
      },
    );
    if (picked != null) {
      await cubit.setTime(hour: picked.hour, minute: picked.minute);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الإعدادات')),
      body: MultiBlocListener(
        listeners: [
          BlocListener<ReminderCubit, ReminderState>(
            listenWhen: (p, c) => p.message != c.message && c.message != null,
            listener: (context, state) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(content: Text(state.message!)),
              );
            },
          ),
        ],
        child: ListView(
          padding: const EdgeInsets.all(NoorTheme.spacingMd),
          children: [
            BlocBuilder<AuthBloc, AuthState>(
              builder: (context, auth) {
                if (auth is! AuthAuthenticated) return const SizedBox.shrink();
                final u = auth.user;
                return SectionCard(
                  child: ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: CircleAvatar(
                      backgroundColor: NoorTheme.primaryGreen,
                      foregroundColor: Colors.white,
                      child: Text(u.fullName.isEmpty ? '؟' : u.fullName.characters.first),
                    ),
                    title: Text(u.fullName, style: const TextStyle(fontWeight: FontWeight.w800)),
                    subtitle: Text(u.roleLabel),
                  ),
                );
              },
            ),
            SectionCard(
              title: 'المظهر',
              child: BlocBuilder<ThemeCubit, ThemeMode>(
                builder: (context, mode) {
                  return Column(
                    children: [
                      _ThemeOption(
                        title: 'حسب النظام',
                        subtitle: 'يتبع إعدادات الجهاز تلقائيًا',
                        icon: Icons.brightness_auto,
                        selected: mode == ThemeMode.system,
                        onTap: () => context
                            .read<ThemeCubit>()
                            .setThemeMode(ThemeMode.system),
                      ),
                      _ThemeOption(
                        title: 'فاتح',
                        subtitle: 'واجهة مضيئة',
                        icon: Icons.light_mode_outlined,
                        selected: mode == ThemeMode.light,
                        onTap: () => context
                            .read<ThemeCubit>()
                            .setThemeMode(ThemeMode.light),
                      ),
                      _ThemeOption(
                        title: 'داكن',
                        subtitle: 'واجهة مريحة للعين ليلًا',
                        icon: Icons.dark_mode_outlined,
                        selected: mode == ThemeMode.dark,
                        onTap: () => context
                            .read<ThemeCubit>()
                            .setThemeMode(ThemeMode.dark),
                      ),
                    ],
                  );
                },
              ),
            ),
            SectionCard(
              title: 'تذكير الصلاة والورد',
              child: BlocBuilder<ReminderCubit, ReminderState>(
                builder: (context, state) {
                  return Column(
                    children: [
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        secondary: const Icon(Icons.mosque_outlined),
                        title: const Text('تنبيه الأذان والورد اليومي'),
                        subtitle: Text(
                          state.supported
                              ? 'تنبيه عند دخول وقت كل صلاة في مصراتة + «هل قرأت وردك اليوم؟»'
                              : 'متاح على Android / iOS — ليس على المتصفح',
                        ),
                        value: state.enabled,
                        onChanged: state.busy
                            ? null
                            : (v) =>
                                context.read<ReminderCubit>().setEnabled(v),
                      ),
                      ListTile(
                        contentPadding: EdgeInsets.zero,
                        enabled: state.supported,
                        leading: const Icon(Icons.schedule),
                        title: const Text('وقت تذكير الورد اليومي'),
                        subtitle: Text(state.timeLabel),
                        trailing: const Icon(Icons.chevron_left),
                        onTap: state.supported && !state.busy
                            ? () => _pickTime(context)
                            : null,
                      ),
                    ],
                  );
                },
              ),
            ),
            SectionCard(
              title: 'حول التطبيق',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    leading: const Icon(Icons.auto_stories_outlined),
                    title: Text(AppConstants.appNameFull),
                    subtitle: const Text('واجهة الطالب وولي الأمر'),
                  ),
                  const Text(
                    'مركز الإشعارات يجمع الإعلانات وتنبيهات الفواتير، مع تنبيه عند كل أذان (مواقيت مصراتة) وتذكير يومي بالورد.',
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ThemeOption extends StatelessWidget {
  final String title;
  final String subtitle;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  const _ThemeOption({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(
        icon,
        color: selected ? NoorTheme.primaryGreen : null,
      ),
      title: Text(title),
      subtitle: Text(subtitle),
      trailing: selected
          ? const Icon(Icons.check_circle, color: NoorTheme.primaryGreen)
          : const Icon(Icons.circle_outlined),
      onTap: onTap,
    );
  }
}
