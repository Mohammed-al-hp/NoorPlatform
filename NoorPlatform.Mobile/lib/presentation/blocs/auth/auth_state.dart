import 'package:equatable/equatable.dart';
import '../../../../data/models/auth_models.dart';

abstract class AuthState extends Equatable {
  const AuthState();
  @override
  List<Object?> get props => [];
}

class AuthInitial extends AuthState {}

class AuthLoading extends AuthState {}

class AuthAuthenticated extends AuthState {
  final UserModel user;
  final bool mustChangePassword;

  const AuthAuthenticated(this.user, {this.mustChangePassword = false});

  @override
  List<Object?> get props => [user, mustChangePassword];
}

class AuthError extends AuthState {
  final String message;
  const AuthError(this.message);

  @override
  List<Object?> get props => [message];
}

class AuthUnauthenticated extends AuthState {
  final String? reason;
  const AuthUnauthenticated({this.reason});

  @override
  List<Object?> get props => [reason];
}

class AuthPasswordChanging extends AuthState {}

class AuthPasswordChanged extends AuthState {
  const AuthPasswordChanged();
}

class AuthPasswordChangeError extends AuthState {
  final String message;
  const AuthPasswordChangeError(this.message);

  @override
  List<Object?> get props => [message];
}
