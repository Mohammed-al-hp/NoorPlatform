import 'package:flutter/material.dart';
import '../../config/theme.dart';
import '../../data/models/monthly_hifz_rating_model.dart';
import 'async_body.dart';

String formatScore(double v) =>
    v == v.roundToDouble() ? v.toStringAsFixed(0) : v.toStringAsFixed(1);

Color scoreColor(double score) {
  if (score >= 8) return NoorTheme.success;
  if (score >= 5) return NoorTheme.warning;
  return NoorTheme.error;
}

/// بطاقة التقييم الشهري للحفظ الجديد (من 10).
class MonthlyRatingCard extends StatelessWidget {
  final MonthlyHifzRatingModel rating;
  final String? title;

  const MonthlyRatingCard({super.key, required this.rating, this.title});

  @override
  Widget build(BuildContext context) {
    final color = scoreColor(rating.score);
    return SectionCard(
      title: title ?? 'تقييم الحفظ الجديد — ${rating.month}/${rating.year}',
      actions: [
        Text(
          '${formatScore(rating.score)} / 10',
          style: Theme.of(context).textTheme.titleLarge?.copyWith(color: color, fontWeight: FontWeight.w800),
        ),
      ],
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          LinearProgressIndicator(
            value: (rating.score / 10).clamp(0, 1),
            color: color,
            minHeight: 8,
            borderRadius: BorderRadius.circular(8),
          ),
          const SizedBox(height: 8),
          Text(
            '${rating.successfulSessions} جلسة حفظ جديد ناجحة من أصل ${rating.target} مطلوبة (شريحة العمر ${rating.ageBand})',
            style: Theme.of(context).textTheme.bodySmall,
          ),
          if (rating.warning != null) ...[
            const SizedBox(height: 4),
            Text('⚠ ${rating.warning}',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(color: NoorTheme.warning)),
          ],
        ],
      ),
    );
  }
}
