import 'dart:async';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../../core/network/api_interceptor.dart' as interceptor;
import '../../../../data/models/auth_models.dart';
import '../../../../domain/repositories/auth_repository.dart';
import 'auth_event.dart';
import 'auth_state.dart';

class AuthBloc extends Bloc<AuthEvent, AuthState> {
  final AuthRepository authRepository;
  StreamSubscription<interceptor.AuthEvent>? _authEventSubscription;

  AuthBloc({required this.authRepository}) : super(AuthInitial()) {
    on<LoginRequested>(_onLoginRequested);
    on<LogoutRequested>(_onLogoutRequested);
    on<SessionExpired>(_onSessionExpired);
    on<ChangePasswordRequested>(_onChangePassword);

    _authEventSubscription = interceptor.AuthEventBus().stream.listen((event) {
      if (event == interceptor.AuthEvent.tokenExpired ||
          event == interceptor.AuthEvent.forceLogout) {
        add(const SessionExpired());
      }
    });
  }

  Future<void> _onLoginRequested(
    LoginRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(AuthLoading());
    final result = await authRepository.login(
      LoginRequestModel(phone: event.phone, password: event.password),
    );
    result.fold(
      (failure) => emit(AuthError(failure.message)),
      (login) => emit(
        AuthAuthenticated(
          login.user,
          mustChangePassword: login.mustChangePassword,
        ),
      ),
    );
  }

  Future<void> _onChangePassword(
    ChangePasswordRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(AuthPasswordChanging());
    final result = await authRepository.changePassword(
      ChangePasswordRequestModel(
        currentPassword: event.currentPassword,
        newPassword: event.newPassword,
      ),
    );
    result.fold(
      (failure) => emit(AuthPasswordChangeError(failure.message)),
      (_) => emit(const AuthPasswordChanged()),
    );
  }

  Future<void> _onLogoutRequested(
    LogoutRequested event,
    Emitter<AuthState> emit,
  ) async {
    await authRepository.logout();
    emit(const AuthUnauthenticated());
  }

  Future<void> _onSessionExpired(
    SessionExpired event,
    Emitter<AuthState> emit,
  ) async {
    await authRepository.logout();
    emit(
      const AuthUnauthenticated(
        reason: 'انتهت صلاحية الجلسة، الرجاء تسجيل الدخول مرة أخرى',
      ),
    );
  }

  @override
  Future<void> close() {
    _authEventSubscription?.cancel();
    return super.close();
  }
}
