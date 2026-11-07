import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/storage/secure_storage.dart';
import '../../../di/injection_container.dart';
import '../../blocs/auth/auth_bloc.dart';
import '../../blocs/auth/auth_event.dart';
import '../../blocs/auth/auth_state.dart';

class ChangePasswordScreen extends StatefulWidget {
  final bool forced;
  const ChangePasswordScreen({super.key, this.forced = false});

  @override
  State<ChangePasswordScreen> createState() => _ChangePasswordScreenState();
}

class _ChangePasswordScreenState extends State<ChangePasswordScreen> {
  final _current = TextEditingController();
  final _next = TextEditingController();
  final _confirm = TextEditingController();
  bool _obscure = true;

  @override
  void dispose() {
    _current.dispose();
    _next.dispose();
    _confirm.dispose();
    super.dispose();
  }

  void _submit() {
    if (_next.text != _confirm.text) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('كلمات المرور غير متطابقة')),
      );
      return;
    }
    if (_next.text.length < 6) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('كلمة المرور يجب أن تكون 6 أحرف على الأقل'),
        ),
      );
      return;
    }
    context.read<AuthBloc>().add(
          ChangePasswordRequested(
            currentPassword: _current.text,
            newPassword: _next.text,
          ),
        );
  }

  Future<void> _goHome() async {
    final user = await sl<SecureStorage>().getUserJson();
    final role = user?['role'] as String?;
    if (!mounted) return;
    if (role == 'Student') {
      context.go('/student');
    } else if (role == 'Parent') {
      context.go('/parent');
    } else {
      context.go('/login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !widget.forced,
      child: Scaffold(
        appBar: AppBar(
          title: Text(
            widget.forced ? 'تغيير كلمة المرور مطلوب' : 'تغيير كلمة المرور',
          ),
          automaticallyImplyLeading: !widget.forced,
        ),
        body: BlocConsumer<AuthBloc, AuthState>(
          listener: (context, state) async {
            if (state is AuthPasswordChanged) {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('تم تغيير كلمة المرور بنجاح')),
              );
              await _goHome();
            } else if (state is AuthPasswordChangeError) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(state.message),
                  backgroundColor: NoorTheme.error,
                ),
              );
            }
          },
          builder: (context, state) {
            final loading = state is AuthPasswordChanging;
            return ListView(
              padding: const EdgeInsets.all(NoorTheme.spacingLg),
              children: [
                if (widget.forced)
                  const Padding(
                    padding: EdgeInsets.only(bottom: 16),
                    child: Text(
                      'يجب تغيير كلمة المرور قبل متابعة استخدام التطبيق.',
                    ),
                  ),
                TextField(
                  controller: _current,
                  obscureText: _obscure,
                  decoration: const InputDecoration(
                    labelText: 'كلمة المرور الحالية',
                    prefixIcon: Icon(Icons.lock_outline),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _next,
                  obscureText: _obscure,
                  decoration: const InputDecoration(
                    labelText: 'كلمة المرور الجديدة',
                    prefixIcon: Icon(Icons.lock),
                  ),
                ),
                const SizedBox(height: 12),
                TextField(
                  controller: _confirm,
                  obscureText: _obscure,
                  decoration: InputDecoration(
                    labelText: 'تأكيد كلمة المرور',
                    prefixIcon: const Icon(Icons.lock),
                    suffixIcon: IconButton(
                      icon: Icon(
                        _obscure ? Icons.visibility : Icons.visibility_off,
                      ),
                      onPressed: () => setState(() => _obscure = !_obscure),
                    ),
                  ),
                ),
                const SizedBox(height: 24),
                ElevatedButton(
                  onPressed: loading ? null : _submit,
                  child: loading
                      ? const SizedBox(
                          height: 22,
                          width: 22,
                          child: CircularProgressIndicator(
                            strokeWidth: 2,
                            color: Colors.white,
                          ),
                        )
                      : const Text('حفظ'),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}
