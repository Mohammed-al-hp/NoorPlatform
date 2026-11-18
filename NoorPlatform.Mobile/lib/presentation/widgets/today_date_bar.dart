import 'dart:async';

import 'package:flutter/material.dart';
import 'package:hijri/hijri_calendar.dart';

import '../../config/theme.dart';

/// شريط تاريخ اليوم لكل الأدوار: اليوم، والتاريخ الميلادي، والهجري (تقويم أم القرى — مثل واجهة الويب).
/// يتحدّث تلقائياً عند منتصف الليل.
class TodayDateBar extends StatefulWidget {
  const TodayDateBar({super.key});

  @override
  State<TodayDateBar> createState() => _TodayDateBarState();
}

class _TodayDateBarState extends State<TodayDateBar> {
  static const _days = [
    'الاثنين',
    'الثلاثاء',
    'الأربعاء',
    'الخميس',
    'الجمعة',
    'السبت',
    'الأحد',
  ];
  static const _months = [
    'يناير',
    'فبراير',
    'مارس',
    'أبريل',
    'مايو',
    'يونيو',
    'يوليو',
    'أغسطس',
    'سبتمبر',
    'أكتوبر',
    'نوفمبر',
    'ديسمبر',
  ];
  static const _hijriMonths = [
    'محرم',
    'صفر',
    'ربيع الأول',
    'ربيع الآخر',
    'جمادى الأولى',
    'جمادى الآخرة',
    'رجب',
    'شعبان',
    'رمضان',
    'شوال',
    'ذو القعدة',
    'ذو الحجة',
  ];

  Timer? _timer;
  DateTime _now = DateTime.now();

  @override
  void initState() {
    super.initState();
    _scheduleMidnight();
  }

  void _scheduleMidnight() {
    final now = DateTime.now();
    final next = DateTime(now.year, now.month, now.day + 1, 0, 0, 5);
    _timer = Timer(next.difference(now), () {
      if (!mounted) return;
      setState(() => _now = DateTime.now());
      _scheduleMidnight();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final d = _now;
    final h = HijriCalendar.fromDate(d);
    final gregorian = '${d.day} ${_months[d.month - 1]} ${d.year}م';
    final hijri = '${h.hDay} ${_hijriMonths[h.hMonth - 1]} ${h.hYear}هـ';
    final theme = Theme.of(context);
    return Material(
      color: NoorTheme.primaryGreen.withValues(alpha: 0.06),
      child: Container(
        width: double.infinity,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(
              color: NoorTheme.accentGold.withValues(alpha: 0.35),
            ),
          ),
        ),
        child: Wrap(
          alignment: WrapAlignment.center,
          crossAxisAlignment: WrapCrossAlignment.center,
          spacing: 8,
          children: [
            Icon(
              Icons.calendar_today_rounded,
              size: 14,
              color: theme.colorScheme.primary,
            ),
            Text(
              _days[d.weekday - 1],
              style: theme.textTheme.labelLarge?.copyWith(
                fontWeight: FontWeight.w800,
              ),
            ),
            Text(gregorian, style: theme.textTheme.labelMedium),
            Text('•', style: theme.textTheme.labelMedium),
            Text(
              hijri,
              style: theme.textTheme.labelMedium?.copyWith(
                color: theme.colorScheme.primary,
              ),
            ),
          ],
        ),
      ),
    );
  }
}
