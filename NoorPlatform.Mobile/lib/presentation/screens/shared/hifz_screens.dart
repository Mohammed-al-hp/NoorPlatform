import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/quran_surahs.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../../data/models/hifz_models.dart';
import '../../blocs/hifz/hifz_cubit.dart';
import '../../widgets/async_body.dart';

class HifzLogScreen extends StatelessWidget {
  final int? studentId;
  final bool isMine;

  const HifzLogScreen({super.key, this.studentId, this.isMine = true});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) {
        final cubit = HifzCubit(sl());
        if (isMine || studentId == null) {
          cubit.loadMine();
        } else {
          cubit.loadForStudent(studentId!);
        }
        return cubit;
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('تقدم الحفظ والمراجعة')),
        body: BlocBuilder<HifzCubit, HifzState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == HifzStatus.loading ||
                  state.status == HifzStatus.initial,
              isEmpty: state.status == HifzStatus.loaded && state.records.isEmpty,
              emptyTitle: 'لا توجد جلسات تسميع بعد',
              emptyIcon: Icons.menu_book_outlined,
              errorMessage:
                  state.status == HifzStatus.error ? state.error : null,
              onRetry: () {
                if (isMine || studentId == null) {
                  context.read<HifzCubit>().loadMine();
                } else {
                  context.read<HifzCubit>().loadForStudent(studentId!);
                }
              },
              child: ListView(
                padding: const EdgeInsets.fromLTRB(NoorTheme.spacingMd, 4, NoorTheme.spacingMd, NoorTheme.spacingXl),
                children: [
                  _HifzSummary(records: state.records),
                  for (final r in state.records) _HifzSessionCard(record: r),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

class QuranMapScreen extends StatelessWidget {
  final int? studentId;
  final bool isMine;

  const QuranMapScreen({super.key, this.studentId, this.isMine = true});

  Map<String, int> _memorizedVerses(List<HifzRecordModel> records) {
    final map = <String, int>{};
    for (final r in records) {
      if (!r.isMemorization) continue;
      final key = r.surahName;
      final count = r.verseCount > 0 ? r.verseCount : 0;
      map[key] = (map[key] ?? 0) + count;
    }
    return map;
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) {
        final cubit = HifzCubit(sl());
        if (isMine || studentId == null) {
          cubit.loadMine();
        } else {
          cubit.loadForStudent(studentId!);
        }
        return cubit;
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('خريطة حفظ القرآن')),
        body: BlocBuilder<HifzCubit, HifzState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == HifzStatus.loading ||
                  state.status == HifzStatus.initial,
              isEmpty: false,
              errorMessage:
                  state.status == HifzStatus.error ? state.error : null,
              onRetry: () {
                if (isMine || studentId == null) {
                  context.read<HifzCubit>().loadMine();
                } else {
                  context.read<HifzCubit>().loadForStudent(studentId!);
                }
              },
              child: Builder(
                builder: (context) {
                  final memorized = _memorizedVerses(state.records);
                  return Column(
                    children: [
                      Padding(
                        padding: const EdgeInsets.all(12),
                        child: Wrap(
                          spacing: 12,
                          children: const [
                            _Legend(color: Color(0xFFE5E7EB), label: 'لم يُحفظ'),
                            _Legend(color: Color(0xFFFBBF24), label: 'جزئي'),
                            _Legend(color: Color(0xFF16A34A), label: 'مكتمل'),
                          ],
                        ),
                      ),
                      Expanded(
                        child: GridView.builder(
                          padding: const EdgeInsets.all(12),
                          gridDelegate:
                              const SliverGridDelegateWithFixedCrossAxisCount(
                            crossAxisCount: 3,
                            mainAxisSpacing: 8,
                            crossAxisSpacing: 8,
                            childAspectRatio: 1.1,
                          ),
                          itemCount: kQuranSurahs.length,
                          itemBuilder: (context, i) {
                            final s = kQuranSurahs[i];
                            final done = memorized[s.name] ?? 0;
                            final pct = done <= 0
                                ? 0
                                : ((done / s.verseCount) * 100)
                                    .clamp(0, 100)
                                    .round();
                            Color bg;
                            if (done >= s.verseCount) {
                              bg = const Color(0xFF16A34A);
                            } else if (done > 0) {
                              bg = const Color(0xFFFBBF24);
                            } else {
                              bg = const Color(0xFFE5E7EB);
                            }
                            final fg = done > 0 ? Colors.white : NoorTheme.textPrimary;
                            return Container(
                              decoration: BoxDecoration(
                                color: bg,
                                borderRadius:
                                    BorderRadius.circular(NoorTheme.radiusMd),
                              ),
                              padding: const EdgeInsets.all(8),
                              child: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Text(
                                    s.name,
                                    textAlign: TextAlign.center,
                                    style: TextStyle(
                                      color: fg,
                                      fontWeight: FontWeight.w700,
                                      fontSize: 13,
                                    ),
                                  ),
                                  Text(
                                     Formatters.number(s.number),
                                    style: TextStyle(color: fg, fontSize: 11),
                                  ),
                                  if (pct > 0)
                                    Text(
                                      '${Formatters.number(pct)}%',
                                      style: TextStyle(color: fg, fontSize: 11),
                                    ),
                                ],
                              ),
                            );
                          },
                        ),
                      ),
                    ],
                  );
                },
              ),
            );
          },
        ),
      ),
    );
  }
}

class _Legend extends StatelessWidget {
  final Color color;
  final String label;
  const _Legend({required this.color, required this.label});

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(width: 14, height: 14, color: color),
        const SizedBox(width: 6),
        Text(label, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}


/// ملخص أعلى سجل التسميع: عدد جلسات الحفظ والمراجعة ومجموع الآيات.
class _HifzSummary extends StatelessWidget {
  final List<HifzRecordModel> records;
  const _HifzSummary({required this.records});

  @override
  Widget build(BuildContext context) {
    final mem = records.where((r) => r.isMemorization).toList();
    final verses = mem.fold<int>(0, (sum, r) => sum + (r.verseCount > 0 ? r.verseCount : 0));
    Widget metric(String label, String value) => Expanded(
          child: Column(
            children: [
              Text(value, style: const TextStyle(color: Colors.white, fontSize: 22, fontWeight: FontWeight.w800)),
              Text(label, style: const TextStyle(color: Colors.white70, fontSize: 12)),
            ],
          ),
        );
    return Container(
      margin: const EdgeInsets.only(bottom: NoorTheme.spacingMd),
      padding: const EdgeInsets.symmetric(vertical: 18, horizontal: 12),
      decoration: BoxDecoration(
        gradient: NoorTheme.headerGradient,
        borderRadius: BorderRadius.circular(NoorTheme.radiusXl),
        boxShadow: NoorTheme.elevatedShadow,
      ),
      child: Row(
        children: [
          metric('جلسات حفظ', '${mem.length}'),
          Container(width: 1, height: 34, color: Colors.white24),
          metric('جلسات مراجعة', '${records.length - mem.length}'),
          Container(width: 1, height: 34, color: Colors.white24),
          metric('آيات محفوظة', '$verses'),
        ],
      ),
    );
  }
}

/// بطاقة جلسة تسميع: نوع الجلسة بلون مميز، اسم السورة بخط Amiri، التاريخ والآيات والتقييم.
class _HifzSessionCard extends StatelessWidget {
  final HifzRecordModel record;
  const _HifzSessionCard({required this.record});

  Color _evalColor(String e) {
    if (e.contains('ممتاز')) return NoorTheme.success;
    if (e.contains('جيد')) return NoorTheme.primaryGreenLight;
    if (e.contains('ضعيف')) return NoorTheme.warning;
    if (e.isEmpty) return NoorTheme.textSecondary;
    return NoorTheme.error;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final r = record;
    final isMem = r.isMemorization;
    final typeColor = isMem ? theme.colorScheme.primary : NoorTheme.accentGold;
    final title = r.toSurahName != null && r.toSurahName!.isNotEmpty && r.toSurahName != r.surahName
        ? '${r.surahName} ← ${r.toSurahName}'
        : r.surahName;
    final evalColor = _evalColor(r.evaluation);

    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(NoorTheme.radiusLg + 2),
        border: Border.all(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.7)),
        boxShadow: NoorTheme.cardShadow,
      ),
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: typeColor.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
              ),
              child: Icon(isMem ? Icons.auto_stories_rounded : Icons.replay_rounded, color: typeColor),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(title,
                            style: NoorTheme.quran(size: 19, color: theme.colorScheme.onSurface).copyWith(height: 1.3)),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: evalColor.withValues(alpha: 0.1),
                          borderRadius: BorderRadius.circular(NoorTheme.radiusFull),
                        ),
                        child: Text(r.evaluation.isEmpty ? '—' : r.evaluation,
                            style: TextStyle(color: evalColor, fontSize: 12, fontWeight: FontWeight.w700)),
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 12,
                    runSpacing: 4,
                    children: [
                      _Meta(icon: Icons.label_outline_rounded, text: isMem ? 'حفظ جديد' : 'مراجعة', color: typeColor),
                      if (r.verses.isNotEmpty) _Meta(icon: Icons.format_list_numbered_rtl_rounded, text: 'الآيات ${r.verses}'),
                      _Meta(icon: Icons.calendar_today_rounded, text: Formatters.displayDate(r.date)),
                    ],
                  ),
                  if (r.notes != null && r.notes!.trim().isNotEmpty) ...[
                    const SizedBox(height: 8),
                    Text(r.notes!, style: theme.textTheme.bodySmall),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Meta extends StatelessWidget {
  final IconData icon;
  final String text;
  final Color? color;
  const _Meta({required this.icon, required this.text, this.color});

  @override
  Widget build(BuildContext context) {
    final c = color ?? Theme.of(context).textTheme.bodySmall?.color;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 14, color: c),
        const SizedBox(width: 4),
        Text(text, style: TextStyle(fontSize: 12, color: c, fontWeight: FontWeight.w500)),
      ],
    );
  }
}
