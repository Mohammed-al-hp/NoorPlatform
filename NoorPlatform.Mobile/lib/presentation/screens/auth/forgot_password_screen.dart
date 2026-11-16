import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/network/center_info.dart';
import '../../../di/injection_container.dart';
import '../../blocs/password_reset/password_reset_bloc.dart';

/// "نسيت كلمة المرور؟" — رمز تحقق عبر واتساب أو SMS بثلاث خطوات (نفس تدفق الويب).
class ForgotPasswordScreen extends StatelessWidget {
  final String? initialPhone;
  const ForgotPasswordScreen({super.key, this.initialPhone});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => sl<PasswordResetBloc>(),
      child: _ForgotPasswordView(initialPhone: initialPhone),
    );
  }
}

class _ForgotPasswordView extends StatefulWidget {
  final String? initialPhone;
  const _ForgotPasswordView({this.initialPhone});

  @override
  State<_ForgotPasswordView> createState() => _ForgotPasswordViewState();
}

class _ForgotPasswordViewState extends State<_ForgotPasswordView> {
  late final _phone = TextEditingController(text: widget.initialPhone);
  final _code = TextEditingController();
  final _password = TextEditingController();
  final _confirm = TextEditingController();
  Timer? _ticker;
  bool _obscure = true;
  String? _channel;

  @override
  void initState() {
    super.initState();
    // تحديث عدّاد إعادة الإرسال كل ثانية
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (mounted) setState(() {});
    });
  }

  @override
  void dispose() {
    _ticker?.cancel();
    _phone.dispose();
    _code.dispose();
    _password.dispose();
    _confirm.dispose();
    super.dispose();
  }

  int _secondsLeft(PasswordResetState s) {
    final at = s.resendAvailableAt;
    if (at == null) return 0;
    final left = at.difference(DateTime.now()).inSeconds;
    return left < 0 ? 0 : left + 1;
  }

  @override
  Widget build(BuildContext context) {
    final bloc = context.read<PasswordResetBloc>();
    return Scaffold(
      appBar: AppBar(title: const Text('استرجاع كلمة المرور')),
      body: BlocConsumer<PasswordResetBloc, PasswordResetState>(
        listener: (context, state) {
          if (state.step == PasswordResetStep.done) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(state.info ?? 'تم تعيين كلمة المرور الجديدة')),
            );
            final nav = Navigator.of(context);
            if (nav.canPop()) {
              nav.pop();
            } else {
              context.go('/login');
            }
          }
        },
        builder: (context, state) {
          final stepNo = switch (state.step) {
            PasswordResetStep.phone => 1,
            PasswordResetStep.code => 2,
            _ => 3,
          };
          final left = _secondsLeft(state);
          return SingleChildScrollView(
            padding: const EdgeInsets.all(NoorTheme.spacingLg),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text('الخطوة $stepNo من 3', style: Theme.of(context).textTheme.bodySmall),
                const SizedBox(height: 8),
                LinearProgressIndicator(value: stepNo / 3),
                const SizedBox(height: NoorTheme.spacingMd),
                if (state.error != null)
                  Container(
                    padding: const EdgeInsets.all(12),
                    margin: const EdgeInsets.only(bottom: 12),
                    decoration: BoxDecoration(
                      color: NoorTheme.error.withValues(alpha: 0.08),
                      borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
                      border: Border.all(color: NoorTheme.error.withValues(alpha: 0.3)),
                    ),
                    child: Text(state.error!, style: const TextStyle(color: NoorTheme.error)),
                  ),
                if (state.step == PasswordResetStep.phone) ...[
                  ValueListenableBuilder<List<String>>(
                    valueListenable: sl<CenterInfo>().otpChannels,
                    builder: (context, channels, _) {
                      final selected = channels.contains(_channel) ? _channel! : channels.first;
                      if (channels.length < 2) {
                        return Text(selected == 'Sms'
                            ? 'أدخل رقم هاتفك المسجّل وسنرسل لك رمز تحقق برسالة نصية (SMS).'
                            : 'أدخل رقم هاتفك المسجّل وسنرسل لك رمز تحقق عبر واتساب.');
                      }
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          const Text('أدخل رقم هاتفك المسجّل واختر طريقة استلام الرمز:'),
                          const SizedBox(height: 10),
                          SegmentedButton<String>(
                            segments: const [
                              ButtonSegment(value: 'WhatsApp', label: Text('واتساب'), icon: Icon(Icons.chat)),
                              ButtonSegment(value: 'Sms', label: Text('رسالة SMS'), icon: Icon(Icons.sms_outlined)),
                            ],
                            selected: {selected},
                            onSelectionChanged: (s) => setState(() => _channel = s.first),
                          ),
                        ],
                      );
                    },
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _phone,
                    keyboardType: TextInputType.phone,
                    textDirection: TextDirection.ltr,
                    decoration: const InputDecoration(
                      labelText: 'رقم الهاتف',
                      hintText: '09xxxxxxxx',
                      prefixIcon: Icon(Icons.phone),
                    ),
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: state.loading
                        ? null
                        : () => bloc.add(OtpRequested(_phone.text, channel: _channel ?? sl<CenterInfo>().otpChannels.value.first)),
                    child: state.loading
                        ? const CircularProgressIndicator()
                        : const Text('إرسال رمز التحقق'),
                  ),
                ],
                if (state.step == PasswordResetStep.code) ...[
                  if (state.info != null) Text(state.info!, style: const TextStyle(color: NoorTheme.primaryGreen)),
                  const SizedBox(height: 12),
                  TextField(
                    controller: _code,
                    keyboardType: TextInputType.number,
                    maxLength: 6,
                    textAlign: TextAlign.center,
                    textDirection: TextDirection.ltr,
                    autofillHints: const [AutofillHints.oneTimeCode],
                    style: const TextStyle(fontSize: 24, letterSpacing: 8),
                    decoration: const InputDecoration(labelText: 'رمز التحقق (6 أرقام)'),
                  ),
                  FilledButton(
                    onPressed: state.loading ? null : () => bloc.add(OtpSubmitted(_code.text)),
                    child: state.loading ? const CircularProgressIndicator() : const Text('تحقق'),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      TextButton(
                        onPressed: () => bloc.add(const PasswordResetBackToPhone()),
                        child: const Text('تغيير الرقم'),
                      ),
                      OutlinedButton(
                        onPressed: left > 0 || state.loading ? null : () => bloc.add(const OtpResendRequested()),
                        child: Text(left > 0 ? 'إعادة الإرسال ($left)' : 'إعادة الإرسال'),
                      ),
                    ],
                  ),
                ],
                if (state.step == PasswordResetStep.newPassword) ...[
                  TextField(
                    controller: _password,
                    obscureText: _obscure,
                    decoration: InputDecoration(
                      labelText: 'كلمة المرور الجديدة',
                      helperText: '8 أحرف على الأقل وتحتوي رقماً',
                      suffixIcon: IconButton(
                        icon: Icon(_obscure ? Icons.visibility : Icons.visibility_off),
                        onPressed: () => setState(() => _obscure = !_obscure),
                      ),
                    ),
                  ),
                  TextField(
                    controller: _confirm,
                    obscureText: _obscure,
                    decoration: const InputDecoration(labelText: 'تأكيد كلمة المرور'),
                  ),
                  const SizedBox(height: 16),
                  FilledButton(
                    onPressed: state.loading
                        ? null
                        : () {
                            if (_password.text != _confirm.text) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('كلمة المرور وتأكيدها غير متطابقين'),
                                  backgroundColor: NoorTheme.error,
                                ),
                              );
                              return;
                            }
                            bloc.add(NewPasswordSubmitted(_password.text));
                          },
                    child: state.loading ? const CircularProgressIndicator() : const Text('حفظ كلمة المرور'),
                  ),
                ],
                const SizedBox(height: 24),
                const Text(
                  'لم يصلك الرمز أو لا تملك رقماً مسجلاً؟ تواصل مع إدارة المركز لإعادة تعيين كلمة المرور.',
                  style: TextStyle(fontSize: 12, color: NoorTheme.textSecondary),
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
