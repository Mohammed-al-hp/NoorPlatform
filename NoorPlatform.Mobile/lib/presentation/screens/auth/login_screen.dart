import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';

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

  void _submitLogin() {
    if (_phoneController.text.trim().isEmpty || _passwordController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('الرجاء إدخال رقم الهاتف وكلمة المرور')),
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
        decoration: const BoxDecoration(
          gradient: NoorTheme.darkGradient,
        ),
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
                    // Navigate based on role
                    if (state.user.role == 'Student') {
                      context.go('/student');
                    } else if (state.user.role == 'Parent') {
                      context.go('/parent');
                    }
                  }
                },
                builder: (context, state) {
                  return Card(
                    child: Padding(
                      padding: const EdgeInsets.all(NoorTheme.spacingLg),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          // شعار المنصة
                          Container(
                            width: 80,
                            height: 80,
                            decoration: BoxDecoration(
                              gradient: NoorTheme.primaryGradient,
                              borderRadius: BorderRadius.circular(20),
                              boxShadow: NoorTheme.elevatedShadow,
                            ),
                            child: const Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(Icons.menu_book_rounded, color: Colors.white, size: 32),
                                SizedBox(height: 2),
                                Text(
                                  'نور',
                                  style: TextStyle(
                                    color: Colors.white,
                                    fontSize: 16,
                                    fontWeight: FontWeight.w800,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: NoorTheme.spacingMd),
                          Text(
                            'مرحباً بك في منصة نور\nلإدارة حلقات تحفيظ القرآن الكريم',
                            style: Theme.of(context).textTheme.titleLarge,
                            textAlign: TextAlign.center,
                          ),
                          const SizedBox(height: NoorTheme.spacingMd),
                          TextField(
                            controller: _phoneController,
                            decoration: const InputDecoration(
                              labelText: 'رقم الهاتف',
                              hintText: 'أدخل رقم الهاتف',
                              prefixIcon: Icon(Icons.phone),
                            ),
                            keyboardType: TextInputType.phone,
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
                              prefixIcon: const Icon(Icons.lock),
                              suffixIcon: IconButton(
                                icon: Icon(
                                  _obscurePassword ? Icons.visibility : Icons.visibility_off,
                                ),
                                onPressed: () {
                                  setState(() => _obscurePassword = !_obscurePassword);
                                },
                              ),
                            ),
                          ),
                          const SizedBox(height: NoorTheme.spacingLg),
                          SizedBox(
                            width: double.infinity,
                            child: ElevatedButton(
                              onPressed: state is AuthLoading ? null : _submitLogin,
                              child: state is AuthLoading
                                  ? const CircularProgressIndicator(color: Colors.white)
                                  : const Text('دخول'),
                            ),
                          ),
                        ],
                      ),
                    ),
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
