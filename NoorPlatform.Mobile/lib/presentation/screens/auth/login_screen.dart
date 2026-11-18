import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../app_router.dart';
import '../../../config/theme.dart';
import '../../widgets/noor_widgets.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';
import 'forgot_password_screen.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _obscurePassword = true;

  @override
  void dispose() {
    _phoneController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  /// رقم هاتف ليبي من 10 أرقام (09XXXXXXXX) لكل المستخدمين، أو اسم المستخدم stXXXXXX للطالب الذي لا يملك هاتفاً.
  static final _loginPattern = RegExp(
    r'^(st\d{6}|[0٠][9٩][0-9٠-٩]{8}|(\+|00)?218[9٩][0-9٠-٩]{8})$',
    caseSensitive: false,
  );

  void _submitLogin() {
    if (_phoneController.text.trim().isEmpty ||
        _passwordController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('الرجاء إدخال رقم الهاتف أو اسم المستخدم وكلمة المرور')),
      );
      return;
    }
    if (!_loginPattern.hasMatch(_phoneController.text.replaceAll(' ', ''))) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('أدخل رقم هاتف ليبي من 10 أرقام يبدأ بـ 09، أو اسم المستخدم للطالب (مثل st123456)'),
        ),
      );
      return;
    }

    context.read<AuthBloc>().add(
      LoginRequested(
        _phoneController.text.trim(),
        _passwordController.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(gradient: NoorTheme.darkGradient),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(NoorTheme.spacingLg),
              child: BlocConsumer<AuthBloc, AuthState>(
                listener: (context, state) {
                  if (state is AuthError) {
                    ScaffoldMessenger.of(context).showSnackBar(
                      SnackBar(
                        content: Text(state.message),
                        backgroundColor: NoorTheme.error,
                      ),
                    );
                  } else if (state is AuthAuthenticated) {
                    // التوجيه حسب الدور (المشرف ← شاشة «لوحة التحكم عبر الويب»)
                    if (const {
                      'Student',
                      'Parent',
                      'Teacher',
                      'Admin',
                    }.contains(state.user.role)) {
                      context.go(AppRouter.homeFor(state.user.role));
                    } else {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                            'هذا الحساب غير مدعوم في تطبيق الهاتف — استخدم المنصة على الويب',
                          ),
                          backgroundColor: NoorTheme.error,
                        ),
                      );
                      context.read<AuthBloc>().add(LogoutRequested());
                    }
                  }
                },
                builder: (context, state) {
                  return Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      // شعار المنصة (نفس شعار منصة الويب)
                      const NoorLogo(size: 84, vertical: true, onDark: true),
                      const SizedBox(height: NoorTheme.spacingLg),
                      Card(
                        child: Padding(
                          padding: const EdgeInsets.fromLTRB(22, 26, 22, 18),
                          child: Column(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                'تسجيل الدخول',
                                style: Theme.of(
                                  context,
                                ).textTheme.headlineSmall,
                              ),
                              const SizedBox(height: 6),
                              Text(
                                'أدخل رقم هاتفك وكلمة المرور للمتابعة',
                                style: Theme.of(context).textTheme.bodyMedium,
                                textAlign: TextAlign.center,
                              ),
                              const SizedBox(height: NoorTheme.spacingLg),
                              TextField(
                                controller: _phoneController,
                                decoration: const InputDecoration(
                                  labelText: 'رقم الهاتف أو اسم المستخدم',
                                  hintText: '09xxxxxxxx — أو اسم المستخدم للطالب',
                                  prefixIcon: Icon(Icons.person_outline_rounded),
                                ),
                                // نص لا أرقام فقط: الطالب بلا هاتف يدخل باسم المستخدم (stXXXXXX)
                                keyboardType: TextInputType.text,
                                autocorrect: false,
                                textDirection: TextDirection.ltr,
                                enabled: state is! AuthLoading,
                              ),
                              const SizedBox(height: NoorTheme.spacingMd),
                              TextField(
                                controller: _passwordController,
                                obscureText: _obscurePassword,
                                enabled: state is! AuthLoading,
                                decoration: InputDecoration(
                                  labelText: 'كلمة المرور',
                                  hintText: 'أدخل كلمة المرور',
                                  prefixIcon: const Icon(
                                    Icons.lock_outline_rounded,
                                  ),
                                  suffixIcon: IconButton(
                                    icon: Icon(
                                      _obscurePassword
                                          ? Icons.visibility
                                          : Icons.visibility_off,
                                    ),
                                    onPressed: () {
                                      setState(
                                        () => _obscurePassword =
                                            !_obscurePassword,
                                      );
                                    },
                                  ),
                                ),
                              ),
                              const SizedBox(height: NoorTheme.spacingLg),
                              SizedBox(
                                width: double.infinity,
                                child: ElevatedButton(
                                  onPressed: state is AuthLoading
                                      ? null
                                      : _submitLogin,
                                  child: state is AuthLoading
                                      ? const SizedBox(
                                          width: 24,
                                          height: 24,
                                          child: CircularProgressIndicator(
                                            color: Colors.white,
                                            strokeWidth: 2.5,
                                          ),
                                        )
                                      : const Text('دخول'),
                                ),
                              ),
                              const SizedBox(height: NoorTheme.spacingSm),
                              TextButton(
                                onPressed: state is AuthLoading
                                    ? null
                                    : () => Navigator.of(context).push(
                                        MaterialPageRoute(
                                          builder: (_) => ForgotPasswordScreen(
                                            initialPhone: _phoneController.text
                                                .trim(),
                                          ),
                                        ),
                                      ),
                                child: const Text('نسيت كلمة المرور؟'),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}
