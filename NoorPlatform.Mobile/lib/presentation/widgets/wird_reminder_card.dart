import 'package:flutter/material.dart';

import '../../config/theme.dart';
import '../../core/storage/app_preferences.dart';
import '../../core/utils/formatters.dart';
import '../../di/injection_container.dart';

/// بطاقة تذكير متحركة: «هل قرأت وردك اليوم؟» — تظهر مرة يومياً حتى يؤكد الطالب
/// قراءته أو يؤجّل التذكير، ثم تختفي بحركة ناعمة.
class WirdReminderCard extends StatefulWidget {
  const WirdReminderCard({super.key});

  @override
  State<WirdReminderCard> createState() => _WirdReminderCardState();
}

class _WirdReminderCardState extends State<WirdReminderCard>
    with SingleTickerProviderStateMixin {
  late final AnimationController _pulse = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 1600),
  )..repeat(reverse: true);
  bool _visible = false;
  bool _checked = false;

  AppPreferences get _prefs => sl<AppPreferences>();

  @override
  void initState() {
    super.initState();
    _check();
  }

  Future<void> _check() async {
    final show = await _prefs.shouldShowWirdReminder(Formatters.ymd());
    if (!mounted) return;
    setState(() {
      _checked = true;
      _visible = show;
    });
  }

  Future<void> _done() async {
    await _prefs.markWirdDone(Formatters.ymd());
    if (!mounted) return;
    setState(() => _visible = false);
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('بارك الله فيك وزادك حرصاً 🌿')),
    );
  }

  Future<void> _later() async {
    await _prefs.snoozeWird(const Duration(hours: 2));
    if (mounted) setState(() => _visible = false);
  }

  @override
  void dispose() {
    _pulse.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (!_checked) return const SizedBox.shrink();
    return AnimatedSize(
      duration: const Duration(milliseconds: 350),
      curve: Curves.easeOutCubic,
      child: AnimatedSwitcher(
        duration: const Duration(milliseconds: 400),
        transitionBuilder: (child, anim) => FadeTransition(
          opacity: anim,
          child: SlideTransition(
            position: Tween(begin: const Offset(0, -0.15), end: Offset.zero)
                .animate(anim),
            child: child,
          ),
        ),
        child: !_visible
            ? const SizedBox.shrink(key: ValueKey('hidden'))
            : Container(
                key: const ValueKey('wird'),
                margin: const EdgeInsets.only(bottom: NoorTheme.spacingMd),
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surface,
                  borderRadius: BorderRadius.circular(20),
                  border: const Border(
                    right: BorderSide(color: NoorTheme.accentGold, width: 5),
                  ),
                  boxShadow: NoorTheme.cardShadow,
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    ScaleTransition(
                      scale: Tween(begin: 1.0, end: 1.1).animate(
                        CurvedAnimation(parent: _pulse, curve: Curves.easeInOut),
                      ),
                      child: Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: NoorTheme.goldSoft,
                          borderRadius: BorderRadius.circular(14),
                        ),
                        child: const Icon(Icons.menu_book_rounded,
                            color: NoorTheme.accentGold),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            'هل قرأت وردك اليوم؟',
                            style: TextStyle(
                                fontSize: 16, fontWeight: FontWeight.w800),
                          ),
                          const SizedBox(height: 4),
                          const Text(
                            'حافظ على وردك اليومي ولو قليلاً — أحبّ الأعمال إلى الله أدومها.',
                            style: TextStyle(
                                fontSize: 12, color: NoorTheme.textSecondary),
                          ),
                          const SizedBox(height: 10),
                          Wrap(
                            spacing: 8,
                            runSpacing: 6,
                            children: [
                              FilledButton(
                                onPressed: _done,
                                child: const Text('نعم، قرأته'),
                              ),
                              OutlinedButton(
                                onPressed: _later,
                                child: const Text('ذكّرني لاحقاً'),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
      ),
    );
  }
}
