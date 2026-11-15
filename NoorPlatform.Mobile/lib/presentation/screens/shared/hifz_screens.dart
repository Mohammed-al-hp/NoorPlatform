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
              child: ListView.separated(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.records.length,
                separatorBuilder: (_, __) => const Divider(height: 1),
                itemBuilder: (context, i) {
                  final r = state.records[i];
                  final typeLabel =
                      r.isMemorization ? 'حفظ جديد' : 'مراجعة';
                  return ListTile(
                    leading: Icon(
                      r.isMemorization ? Icons.fiber_new : Icons.replay,
                      color: NoorTheme.primaryGreen,
                    ),
                    title: Text(
                      r.toSurahName != null && r.toSurahName!.isNotEmpty
                          ? '${r.surahName} ← ${r.toSurahName}'
                          : r.surahName,
                    ),
                    subtitle: Text(
                      '${Formatters.displayDate(r.date)} · $typeLabel · ${r.verses}',
                    ),
                    trailing: Text(r.evaluation.isEmpty ? '—' : r.evaluation),
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
