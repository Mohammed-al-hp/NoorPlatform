import 'package:flutter/material.dart';

import '../../config/theme.dart';
import '../../core/utils/formatters.dart';
import '../../data/datasources/remote/smart_remote_datasource.dart';
import '../../data/models/smart_models.dart';
import '../../di/injection_container.dart';

/// تدرج الخريطة الحرارية: الأخضر الزمردي الداكن (إتقان كامل ومراجعة حديثة)
/// يبهت تدريجياً حتى الذهبي الفاتح كلما طالت مدة الانقطاع عن المراجعة.
const List<(double, Color)> kHeatStops = [
  (0, Color(0xFFF6EBD0)),
  (40, Color(0xFFE8DCB5)),
  (60, Color(0xFFA8CDB6)),
  (80, Color(0xFF4C9A6E)),
  (100, Color(0xFF1A5C3A)),
];

Color heatColor(double? retention) {
  final v = (retention ?? 0).clamp(0, 100).toDouble();
  for (var i = 1; i < kHeatStops.length; i++) {
    final (b, cb) = kHeatStops[i];
    if (v <= b) {
      final (a, ca) = kHeatStops[i - 1];
      return Color.lerp(ca, cb, (v - a) / (b - a))!;
    }
  }
  return kHeatStops.last.$2;
}

Color heatTextColor(double? retention) =>
    (retention ?? 0) >= 62 ? Colors.white : const Color(0xFF3D3420);

/// بطاقة «مراجعة اليوم» — السور التي حان وقت مراجعتها مرتبة بالأولوية.
class ReviewTodayCard extends StatefulWidget {
  final int? studentId;
  final VoidCallback? onOpenMap;
  const ReviewTodayCard({super.key, this.studentId, this.onOpenMap});

  @override
  State<ReviewTodayCard> createState() => _ReviewTodayCardState();
}

class _ReviewTodayCardState extends State<ReviewTodayCard> {
  late final Future<ReviewScheduleModel> _future = _load();

  Future<ReviewScheduleModel> _load() =>
      sl<SmartRemoteDataSource>().getReviewSchedule(studentId: widget.studentId);

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return FutureBuilder<ReviewScheduleModel>(
      future: _future,
      builder: (context, snap) {
        if (snap.connectionState != ConnectionState.done) {
          return const Card(child: Padding(padding: EdgeInsets.all(20), child: Center(child: CircularProgressIndicator())));
        }
        if (snap.hasError || !snap.hasData) {
          return const SizedBox.shrink(); // ميزة مساندة: لا نشغل الشاشة بخطأ
        }
        final r = snap.data!;
        return Card(
          margin: const EdgeInsets.symmetric(vertical: 8),
          child: Padding(
            padding: const EdgeInsets.all(NoorTheme.spacingMd),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(children: [
                  const Icon(Icons.autorenew_rounded, color: NoorTheme.primaryGreen),
                  const SizedBox(width: 8),
                  Expanded(child: Text('مراجعة اليوم', style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w800))),
                  _Chip(text: '${Formatters.number(r.dueCount)} مستحقة', gold: true),
                ]),
                const SizedBox(height: 4),
                Text('مرتبة حسب الأولوية حتى لا يتفلت المحفوظ القديم', style: theme.textTheme.bodySmall),
                const SizedBox(height: 12),
                Row(children: [
                  _Stat(value: Formatters.number(r.totalMemorized), label: 'سورة محفوظة'),
                  _Stat(value: Formatters.number(r.dueCount), label: 'تحتاج مراجعة'),
                  _Stat(value: '${Formatters.number(r.averageRetention.round())}%', label: 'متوسط الثبات'),
                ]),
                const SizedBox(height: 12),
                if (r.due.isEmpty)
                  const Padding(
                    padding: EdgeInsets.symmetric(vertical: 12),
                    child: Text('ما شاء الله! لا توجد سور مستحقة للمراجعة اليوم',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: NoorTheme.primaryGreen, fontWeight: FontWeight.w700)),
                  )
                else
                  for (var i = 0; i < r.due.length && i < 6; i++) _ReviewRow(rank: i + 1, s: r.due[i]),
                if (r.upcoming.isNotEmpty) ...[
                  const SizedBox(height: 8),
                  Text('قريباً: ${r.upcoming.map((u) => u.name).join('، ')}', style: theme.textTheme.bodySmall),
                ],
                if (widget.onOpenMap != null)
                  Align(
                    alignment: AlignmentDirectional.centerEnd,
                    child: TextButton.icon(
                      onPressed: widget.onOpenMap,
                      icon: const Icon(Icons.grid_on_rounded, size: 18),
                      label: const Text('خريطة الإتقان'),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _ReviewRow extends StatelessWidget {
  final int rank;
  final SurahStrength s;
  const _ReviewRow({required this.rank, required this.s});

  @override
  Widget build(BuildContext context) {
    final retention = s.retention ?? 0;
    final when = s.overdueDays > 0 ? 'متأخرة ${Formatters.number(s.overdueDays)} يوم' : 'موعدها اليوم';
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).dividerColor.withValues(alpha: 0.4)),
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      ),
      child: Row(children: [
        CircleAvatar(
          radius: 14,
          backgroundColor: NoorTheme.goldSoft,
          child: Text(Formatters.number(rank),
              style: const TextStyle(color: NoorTheme.accentGold, fontWeight: FontWeight.w800, fontSize: 12)),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(s.name, style: const TextStyle(fontFamily: NoorTheme.quranFontFamily, fontSize: 18, fontWeight: FontWeight.w700)),
            Text('آخر مراجعة قبل ${Formatters.number(s.daysSinceReview ?? 0)} يوم · $when',
                style: Theme.of(context).textTheme.bodySmall),
          ]),
        ),
        SizedBox(
          width: 64,
          child: Column(children: [
            Text('${Formatters.number(retention.round())}%',
                style: TextStyle(fontWeight: FontWeight.w800, color: retention < 60 ? NoorTheme.accentGold : NoorTheme.primaryGreen)),
            const SizedBox(height: 4),
            ClipRRect(
              borderRadius: BorderRadius.circular(4),
              child: LinearProgressIndicator(
                value: retention / 100,
                minHeight: 6,
                color: heatColor(retention),
                backgroundColor: NoorTheme.borderLight,
              ),
            ),
          ]),
        ),
      ]),
    );
  }
}

class _Stat extends StatelessWidget {
  final String value;
  final String label;
  const _Stat({required this.value, required this.label});

  @override
  Widget build(BuildContext context) => Expanded(
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 3),
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            color: NoorTheme.primaryGreen.withValues(alpha: 0.07),
            borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
          ),
          child: Column(children: [
            Text(value, style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: NoorTheme.primaryGreen)),
            Text(label, style: Theme.of(context).textTheme.bodySmall),
          ]),
        ),
      );
}

class _Chip extends StatelessWidget {
  final String text;
  final bool gold;
  const _Chip({required this.text, this.gold = false});

  @override
  Widget build(BuildContext context) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
        decoration: BoxDecoration(
          color: gold ? NoorTheme.goldSoft : NoorTheme.greenSoft,
          borderRadius: BorderRadius.circular(999),
        ),
        child: Text(text,
            style: TextStyle(
                fontSize: 12,
                fontWeight: FontWeight.w700,
                color: gold ? NoorTheme.accentGold : NoorTheme.primaryGreen)),
      );
}

/// الخريطة الحرارية: شريط الأجزاء الثلاثين (للتصفية) ثم شبكة السور الـ114.
class MasteryHeatmapView extends StatefulWidget {
  final HeatmapModel data;
  const MasteryHeatmapView({super.key, required this.data});

  @override
  State<MasteryHeatmapView> createState() => _MasteryHeatmapViewState();
}

class _MasteryHeatmapViewState extends State<MasteryHeatmapView> {
  int? _juz;

  /// نفس قاعدة الخادم: السورة تمتد حتى جزء بداية السورة التالية.
  bool _covers(SurahStrength s, int juz) {
    final all = widget.data.surahs;
    final next = s.number < all.length ? all[s.number] : null;
    final startsJuz = next != null && const [46, 58, 67, 78].contains(next.number);
    final end = next == null ? 30 : (startsJuz ? next.juz - 1 : next.juz).clamp(s.juz, 30);
    return juz >= s.juz && juz <= end;
  }

  void _showSurah(SurahStrength s) {
    showModalBottomSheet<void>(
      context: context,
      builder: (ctx) => Padding(
        padding: const EdgeInsets.fromLTRB(20, 20, 20, 28),
        child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Text('سورة ${s.name}',
              textAlign: TextAlign.center,
              style: const TextStyle(fontFamily: NoorTheme.quranFontFamily, fontSize: 24, fontWeight: FontWeight.w700)),
          const SizedBox(height: 12),
          if (!s.memorized)
            const Text('لم تُحفظ بعد', textAlign: TextAlign.center)
          else ...[
            LinearProgressIndicator(value: (s.retention ?? 0) / 100, minHeight: 10, color: heatColor(s.retention)),
            const SizedBox(height: 10),
            Text('نسبة الإتقان: ${Formatters.number((s.retention ?? 0).round())}%', textAlign: TextAlign.center),
            Text('آخر مراجعة قبل ${Formatters.number(s.daysSinceReview ?? 0)} يوم · آخر تقييم: ${s.lastEvaluation ?? '—'}',
                textAlign: TextAlign.center),
            if (s.isDue)
              const Padding(
                padding: EdgeInsets.only(top: 8),
                child: Text('حان موعد مراجعتها', textAlign: TextAlign.center,
                    style: TextStyle(color: NoorTheme.accentGold, fontWeight: FontWeight.w800)),
              ),
          ],
        ]),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final d = widget.data;
    final theme = Theme.of(context);
    return Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
      Wrap(spacing: 8, runSpacing: 8, children: [
        _Chip(text: '${Formatters.number(d.memorizedCount)} سورة محفوظة'),
        _Chip(text: 'متوسط الإتقان ${Formatters.number(d.averageRetention.round())}%'),
        if (d.dueCount > 0) _Chip(text: '${Formatters.number(d.dueCount)} مستحقة للمراجعة', gold: true),
      ]),
      const SizedBox(height: 12),
      Row(children: [
        Text('متقن', style: theme.textTheme.bodySmall),
        const SizedBox(width: 6),
        Expanded(
          child: Container(
            height: 10,
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(6),
              gradient: LinearGradient(colors: kHeatStops.reversed.map((e) => e.$2).toList()),
            ),
          ),
        ),
        const SizedBox(width: 6),
        Text('بهت بالانقطاع', style: theme.textTheme.bodySmall),
      ]),
      const SizedBox(height: 12),
      Text('الأجزاء', style: theme.textTheme.titleSmall),
      const SizedBox(height: 6),
      GridView.count(
        crossAxisCount: 10,
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        mainAxisSpacing: 4,
        crossAxisSpacing: 4,
        children: [
          for (final j in d.juz)
            Semantics(
              button: true,
              label: 'الجزء ${j.juz}',
              child: InkWell(
                onTap: () => setState(() => _juz = _juz == j.juz ? null : j.juz),
                borderRadius: BorderRadius.circular(6),
                child: Container(
                  alignment: Alignment.center,
                  decoration: BoxDecoration(
                    color: j.retention == null ? NoorTheme.borderLight.withValues(alpha: 0.6) : heatColor(j.retention),
                    borderRadius: BorderRadius.circular(6),
                    border: _juz == j.juz ? Border.all(color: NoorTheme.accentGold, width: 2) : null,
                  ),
                  child: Text(Formatters.number(j.juz),
                      style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w700,
                          color: j.retention == null ? NoorTheme.textSecondary : heatTextColor(j.retention))),
                ),
              ),
            ),
        ],
      ),
      const SizedBox(height: 14),
      GridView.builder(
        shrinkWrap: true,
        physics: const NeverScrollableScrollPhysics(),
        gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
          maxCrossAxisExtent: 110,
          mainAxisSpacing: 6,
          crossAxisSpacing: 6,
          childAspectRatio: 1.35,
        ),
        itemCount: d.surahs.length,
        itemBuilder: (context, i) {
          final s = d.surahs[i];
          final dim = _juz != null && !_covers(s, _juz!);
          final bg = s.memorized ? heatColor(s.retention) : theme.colorScheme.surface;
          final fg = s.memorized ? heatTextColor(s.retention) : NoorTheme.textSecondary;
          return Opacity(
            opacity: dim ? 0.25 : 1,
            child: InkWell(
              onTap: () => _showSurah(s),
              borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
              child: Container(
                padding: const EdgeInsets.all(4),
                decoration: BoxDecoration(
                  color: bg,
                  borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
                  border: s.memorized ? null : Border.all(color: NoorTheme.borderLight),
                ),
                child: Stack(children: [
                  Center(
                    child: Column(mainAxisSize: MainAxisSize.min, children: [
                      Text(s.name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: TextStyle(fontFamily: NoorTheme.quranFontFamily, color: fg, fontSize: 15, fontWeight: FontWeight.w700)),
                      Text(
                        s.memorized ? '${Formatters.number((s.retention ?? 0).round())}%' : Formatters.number(s.number),
                        style: TextStyle(color: fg, fontSize: 10),
                      ),
                    ]),
                  ),
                  if (s.isDue)
                    const Positioned(
                      top: 2,
                      left: 2,
                      child: CircleAvatar(radius: 4, backgroundColor: NoorTheme.accentGold),
                    ),
                ]),
              ),
            ),
          );
        },
      ),
    ]);
  }
}
