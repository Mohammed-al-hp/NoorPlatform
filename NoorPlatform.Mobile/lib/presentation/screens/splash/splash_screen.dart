import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/storage/secure_storage.dart';
import '../../../di/injection_container.dart' as di;

class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen> {
  @override
  void initState() {
    super.initState();
    _navigateToNext();
  }

  Future<void> _navigateToNext() async {
    // نتحقق من الجلسة فعلياً بدل تأخير ثابت للجميع بغض النظر عن حالتهم.
    // 800ms هنا فقط لعرض الشعار بشكل مريح للعين، وليس وقت "تحميل" وهمياً.
    final secureStorage = di.sl<SecureStorage>();

    await Future<void>.delayed(const Duration(milliseconds: 800));

    final hasToken = await secureStorage.hasToken();
    final user = hasToken ? await secureStorage.getUserJson() : null;

    if (!mounted) return;

    if (user == null) {
      context.go('/login');
      return;
    }

    final role = user['role'] as String?;
    if (role == 'Student') {
      context.go('/student');
    } else if (role == 'Parent') {
      context.go('/parent');
    } else if (role == 'Teacher') {
      context.go('/teacher');
    } else {
      // دور غير معروف — نفس معالجة app_router.dart لأدوار غير صالحة.
      await secureStorage.clearAll();
      if (!mounted) return;
      context.go('/login');
    }
  }

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      backgroundColor: NoorTheme.primaryDark,
      body: Center(
        child: CircularProgressIndicator(color: NoorTheme.accentGold),
      ),
    );
  }
}
