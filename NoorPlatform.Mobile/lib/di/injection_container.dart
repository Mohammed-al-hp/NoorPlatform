import 'package:get_it/get_it.dart';
import 'package:connectivity_plus/connectivity_plus.dart';

import '../data/datasources/remote/attendance_remote_datasource.dart';
import '../domain/repositories/attendance_repository.dart';
import '../data/repositories/attendance_repository_impl.dart';
import '../presentation/blocs/attendance/attendance_bloc.dart';

// Core
import '../core/network/api_client.dart';
import '../core/network/network_info.dart';
import '../core/storage/app_preferences.dart';
import '../core/storage/secure_storage.dart';
import '../app_router.dart';

// Data Sources
import '../data/datasources/remote/auth_remote_datasource.dart';
import '../data/datasources/remote/student_remote_datasource.dart';
import '../data/datasources/remote/parent_remote_datasource.dart';
import '../data/datasources/remote/portal_remote_datasource.dart';

// Repositories
import '../domain/repositories/auth_repository.dart';
import '../data/repositories/auth_repository_impl.dart';
import '../domain/repositories/student_repository.dart';
import '../data/repositories/student_repository_impl.dart';
import '../domain/repositories/parent_repository.dart';
import '../data/repositories/parent_repository_impl.dart';
import '../domain/repositories/portal_repository.dart';
import '../data/repositories/portal_repository_impl.dart';

// Blocs
import '../presentation/blocs/auth/auth_bloc.dart';
import '../presentation/blocs/student_dashboard/student_dashboard_bloc.dart';
import '../presentation/blocs/parent_dashboard/parent_dashboard_bloc.dart';
import '../presentation/blocs/theme/theme_cubit.dart';
import '../presentation/blocs/notifications/notifications_cubit.dart';
import '../presentation/blocs/reminder/reminder_cubit.dart';
import '../core/notifications/prayer_reminder_service.dart';

final sl = GetIt.instance;

Future<void> init() async {
  // ── App services ──
  sl.registerLazySingleton(() => AppPreferences());
  sl.registerLazySingleton(() => PrayerReminderService(sl()));
  sl.registerLazySingleton(() => ThemeCubit(sl()));
  sl.registerLazySingleton(
    () => ReminderCubit(
      preferences: sl(),
      reminderService: sl(),
    ),
  );
  sl.registerLazySingleton(
    () => NotificationsCubit(
      portalRepository: sl(),
      parentRepository: sl(),
      preferences: sl(),
    ),
  );

  // ── Blocs ──
  sl.registerFactory(() => AttendanceBloc(attendanceRepository: sl()));
  sl.registerFactory(() => AuthBloc(authRepository: sl()));
  sl.registerFactory(() => StudentDashboardBloc(studentRepository: sl()));
  sl.registerFactory(() => ParentDashboardBloc(parentRepository: sl()));

  // ── Repositories ──
  sl.registerLazySingleton<AttendanceRepository>(
    () => AttendanceRepositoryImpl(remoteDataSource: sl(), networkInfo: sl()),
  );
  sl.registerLazySingleton<AuthRepository>(
    () => AuthRepositoryImpl(
      remoteDataSource: sl(),
      secureStorage: sl(),
      networkInfo: sl(),
    ),
  );
  sl.registerLazySingleton<StudentRepository>(
    () => StudentRepositoryImpl(
      remoteDataSource: sl(),
      networkInfo: sl(),
      secureStorage: sl(),
    ),
  );
  sl.registerLazySingleton<ParentRepository>(
    () => ParentRepositoryImpl(remoteDataSource: sl(), networkInfo: sl()),
  );
  sl.registerLazySingleton<PortalRepository>(
    () => PortalRepositoryImpl(remote: sl(), networkInfo: sl()),
  );

  // ── Data Sources ──
  sl.registerLazySingleton<AttendanceRemoteDataSource>(
    () => AttendanceRemoteDataSourceImpl(sl()),
  );
  sl.registerLazySingleton<AuthRemoteDataSource>(
    () => AuthRemoteDataSourceImpl(sl()),
  );
  sl.registerLazySingleton<StudentRemoteDataSource>(
    () => StudentRemoteDataSourceImpl(sl()),
  );
  sl.registerLazySingleton<ParentRemoteDataSource>(
    () => ParentRemoteDataSourceImpl(sl()),
  );
  sl.registerLazySingleton<PortalRemoteDataSource>(
    () => PortalRemoteDataSourceImpl(sl()),
  );

  // ── Core ──
  sl.registerLazySingleton(() => SecureStorage());
  sl.registerLazySingleton(() => ApiClient(sl()));
  sl.registerLazySingleton(() => NetworkInfo(connectivity: sl()));
  sl.registerLazySingleton(() => AppRouter(sl()));

  // ── External ──
  sl.registerLazySingleton(() => Connectivity());

  // Load persisted theme / reminder prefs before first frame.
  await sl<ThemeCubit>().load();
  await sl<ReminderCubit>().load();
  await sl<PrayerReminderService>().syncFromPreferences();
}
