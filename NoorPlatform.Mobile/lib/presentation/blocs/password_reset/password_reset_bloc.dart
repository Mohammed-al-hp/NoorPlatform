import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../domain/repositories/auth_repository.dart';

// ── Events ──
abstract class PasswordResetEvent extends Equatable {
  const PasswordResetEvent();
  @override
  List<Object?> get props => [];
}

class OtpRequested extends PasswordResetEvent {
  final String phone;
  /// قناة الإرسال: WhatsApp أو Sms (اختياري — حسب إعدادات المركز).
  final String? channel;
  const OtpRequested(this.phone, {this.channel});
  @override
  List<Object?> get props => [phone, channel];
}

class OtpResendRequested extends PasswordResetEvent {
  const OtpResendRequested();
}

class OtpSubmitted extends PasswordResetEvent {
  final String code;
  const OtpSubmitted(this.code);
  @override
  List<Object?> get props => [code];
}

class NewPasswordSubmitted extends PasswordResetEvent {
  final String password;
  const NewPasswordSubmitted(this.password);
  @override
  List<Object?> get props => [password];
}

class PasswordResetBackToPhone extends PasswordResetEvent {
  const PasswordResetBackToPhone();
}

// ── State ──
enum PasswordResetStep { phone, code, newPassword, done }

class PasswordResetState extends Equatable {
  final PasswordResetStep step;
  final String phone;
  final String code;
  final bool loading;
  final String? error;
  final String? info;
  /// وقت السماح بإعادة الإرسال (للعدّاد في الواجهة).
  final DateTime? resendAvailableAt;

  const PasswordResetState({
    this.step = PasswordResetStep.phone,
    this.phone = '',
    this.code = '',
    this.loading = false,
    this.error,
    this.info,
    this.resendAvailableAt,
  });

  PasswordResetState copyWith({
    PasswordResetStep? step,
    String? phone,
    String? code,
    bool? loading,
    String? error,
    String? info,
    DateTime? resendAvailableAt,
    bool clearError = false,
  }) =>
      PasswordResetState(
        step: step ?? this.step,
        phone: phone ?? this.phone,
        code: code ?? this.code,
        loading: loading ?? this.loading,
        error: clearError ? null : (error ?? this.error),
        info: info ?? this.info,
        resendAvailableAt: resendAvailableAt ?? this.resendAvailableAt,
      );

  @override
  List<Object?> get props => [step, phone, code, loading, error, info, resendAvailableAt];
}

/// تدفق "نسيت كلمة المرور" بثلاث خطوات: رقم الهاتف ← رمز التحقق ← كلمة المرور الجديدة.
class PasswordResetBloc extends Bloc<PasswordResetEvent, PasswordResetState> {
  final AuthRepository authRepository;
  String? _channel;
  static final _phonePattern = RegExp(r'^09\d{8}$');

  PasswordResetBloc({required this.authRepository}) : super(const PasswordResetState()) {
    on<OtpRequested>((e, emit) {
      _channel = e.channel;
      return _request(e.phone.trim(), emit);
    });
    on<OtpResendRequested>((e, emit) => _request(state.phone, emit));
    on<OtpSubmitted>(_onCode);
    on<NewPasswordSubmitted>(_onPassword);
    on<PasswordResetBackToPhone>(
        (e, emit) => emit(const PasswordResetState()));
  }

  Future<void> _request(String phone, Emitter<PasswordResetState> emit) async {
    if (!_phonePattern.hasMatch(phone)) {
      emit(state.copyWith(error: 'أدخل رقم هاتف صحيح يبدأ بـ 09 ويتكون من 10 أرقام'));
      return;
    }
    emit(state.copyWith(loading: true, clearError: true));
    final r = await authRepository.requestPasswordOtp(phone, channel: _channel);
    r.fold(
      (f) => emit(state.copyWith(loading: false, error: f.message)),
      (v) => emit(state.copyWith(
        loading: false,
        phone: phone,
        step: PasswordResetStep.code,
        info: v.$1,
        resendAvailableAt: DateTime.now().add(Duration(seconds: v.$2)),
        clearError: true,
      )),
    );
  }

  Future<void> _onCode(OtpSubmitted e, Emitter<PasswordResetState> emit) async {
    final code = e.code.trim();
    if (!RegExp(r'^\d{6}$').hasMatch(code)) {
      emit(state.copyWith(error: 'رمز التحقق يتكون من 6 أرقام'));
      return;
    }
    emit(state.copyWith(loading: true, clearError: true));
    final r = await authRepository.verifyPasswordOtp(state.phone, code);
    r.fold(
      (f) => emit(state.copyWith(loading: false, error: f.message)),
      (_) => emit(state.copyWith(
        loading: false,
        code: code,
        step: PasswordResetStep.newPassword,
        clearError: true,
      )),
    );
  }

  Future<void> _onPassword(NewPasswordSubmitted e, Emitter<PasswordResetState> emit) async {
    if (e.password.length < 8 || !RegExp(r'\d').hasMatch(e.password)) {
      emit(state.copyWith(error: 'كلمة المرور 8 أحرف على الأقل وتحتوي رقماً واحداً على الأقل'));
      return;
    }
    emit(state.copyWith(loading: true, clearError: true));
    final r = await authRepository.resetPasswordWithOtp(state.phone, state.code, e.password);
    r.fold(
      (f) => emit(state.copyWith(
        loading: false,
        error: f.message,
        // انتهاء الصلاحية أو تجاوز المحاولات يتطلب طلب رمز جديد
        step: f.message.contains('اطلب رمزاً جديداً') ? PasswordResetStep.phone : null,
      )),
      (msg) => emit(state.copyWith(loading: false, step: PasswordResetStep.done, info: msg, clearError: true)),
    );
  }
}
