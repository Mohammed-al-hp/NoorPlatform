import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'config/theme.dart';
import 'config/constants.dart';
import 'core/storage/local_cache.dart';
import 'di/injection_container.dart' as di;
import 'app_router.dart';
import 'presentation/blocs/auth/auth_bloc.dart';
import 'presentation/blocs/student_dashboard/student_dashboard_bloc.dart';
import 'presentation/blocs/parent_dashboard/parent_dashboard_bloc.dart';
import 'presentation/blocs/attendance/attendance_bloc.dart';
import 'presentation/blocs/theme/theme_cubit.dart';
import 'presentation/blocs/notifications/notifications_cubit.dart';
import 'presentation/blocs/reminder/reminder_cubit.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await LocalCache.init();
  await di.init();

  runApp(const NoorApp());
}

class NoorApp extends StatelessWidget {
  const NoorApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: [
        BlocProvider(create: (_) => di.sl<AuthBloc>()),
        BlocProvider(create: (_) => di.sl<StudentDashboardBloc>()),
        BlocProvider(create: (_) => di.sl<ParentDashboardBloc>()),
        BlocProvider(create: (_) => di.sl<AttendanceBloc>()),
        BlocProvider.value(value: di.sl<ThemeCubit>()),
        BlocProvider.value(value: di.sl<NotificationsCubit>()),
        BlocProvider.value(value: di.sl<ReminderCubit>()),
      ],
      child: BlocBuilder<ThemeCubit, ThemeMode>(
        builder: (context, themeMode) {
          return MaterialApp.router(
            title: AppConstants.appNameFull,
            debugShowCheckedModeBanner: false,
            theme: NoorTheme.lightTheme,
            darkTheme: NoorTheme.darkTheme,
            themeMode: themeMode,
            localizationsDelegates: const [
              GlobalMaterialLocalizations.delegate,
              GlobalWidgetsLocalizations.delegate,
              GlobalCupertinoLocalizations.delegate,
            ],
            supportedLocales: const [
              Locale('ar', 'LY'),
            ],
            locale: const Locale('ar', 'LY'),
            routerConfig: di.sl<AppRouter>().router,
          );
        },
      ),
    );
  }
}
