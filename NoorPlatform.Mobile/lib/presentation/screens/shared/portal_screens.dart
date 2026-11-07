import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../core/utils/file_open_helper.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../blocs/evaluations/evaluations_cubit.dart';
import '../../blocs/portal/portal_cubits.dart';
import '../../widgets/async_body.dart';

class EvaluationsScreen extends StatelessWidget {
  final int? studentId;
  const EvaluationsScreen({super.key, this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => EvaluationsCubit(sl())..load(studentId: studentId),
      child: Scaffold(
        appBar: AppBar(title: const Text('التقييمات الدورية')),
        body: BlocBuilder<EvaluationsCubit, EvaluationsState>(
          builder: (context, state) {
            if (state.status == EvalStatus.hidden) {
              return const EmptyState(
                title: 'التقييمات غير متاحة حالياً',
                subtitle: 'المركز لم يفعّل إظهار التقييمات للطلاب وأولياء الأمور',
                icon: Icons.visibility_off_outlined,
              );
            }
            return AsyncBody(
              isLoading: state.status == EvalStatus.loading ||
                  state.status == EvalStatus.initial,
              isEmpty:
                  state.status == EvalStatus.loaded && state.evaluations.isEmpty,
              emptyTitle: 'لا توجد تقييمات بعد',
              errorMessage:
                  state.status == EvalStatus.error ? state.error : null,
              onRetry: () =>
                  context.read<EvaluationsCubit>().load(studentId: studentId),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.evaluations.length,
                itemBuilder: (context, i) {
                  final e = state.evaluations[i];
                  return SectionCard(
                    title: e.periodName,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${e.studentName} · الدرجة: ${Formatters.number(e.overallScore, fractionDigits: 1)} (${e.gradeLabel})',
                          style: Theme.of(context).textTheme.titleSmall,
                        ),
                        const SizedBox(height: 8),
                        Text(
                          'حضور ${Formatters.number(e.attendanceScore, fractionDigits: 1)} · حفظ ${Formatters.number(e.hifzScore, fractionDigits: 1)} · مراجعة ${Formatters.number(e.revisionScore, fractionDigits: 1)}',
                        ),
                        Text(
                          'تقدم ${Formatters.number(e.progressScore, fractionDigits: 1)} · متون ${Formatters.number(e.matnScore, fractionDigits: 1)} · مظهر ${Formatters.number(e.dressScore, fractionDigits: 1)}',
                        ),
                        if (e.sheikhNotes != null && e.sheikhNotes!.isNotEmpty)
                          Padding(
                            padding: const EdgeInsets.only(top: 8),
                            child: Text('ملاحظة الشيخ: ${e.sheikhNotes}'),
                          ),
                      ],
                    ),
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

class AnnouncementsScreen extends StatelessWidget {
  const AnnouncementsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => AnnouncementsCubit(sl())..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('الإعلانات')),
        body: BlocBuilder<AnnouncementsCubit, AnnouncementsState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == PortalLoadStatus.loading ||
                  state.status == PortalLoadStatus.initial,
              isEmpty:
                  state.status == PortalLoadStatus.loaded && state.items.isEmpty,
              emptyTitle: 'لا توجد إعلانات',
              errorMessage:
                  state.status == PortalLoadStatus.error ? state.error : null,
              onRetry: () => context.read<AnnouncementsCubit>().load(),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.items.length,
                itemBuilder: (context, i) {
                  final a = state.items[i];
                  return SectionCard(
                    title: a.title,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(a.content),
                        const SizedBox(height: 8),
                        Text(
                          Formatters.displayDateTime(a.createdAt),
                          style: Theme.of(context).textTheme.bodySmall,
                        ),
                      ],
                    ),
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

class LibraryScreen extends StatelessWidget {
  const LibraryScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => LibraryCubit(sl())..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('المكتبة الرقمية')),
        body: BlocConsumer<LibraryCubit, LibraryState>(
          listener: (context, state) {
            if (state.error != null) {
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text(state.error!),
                  backgroundColor: NoorTheme.error,
                ),
              );
            }
          },
          builder: (context, state) {
            return Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: TextField(
                    decoration: const InputDecoration(
                      hintText: 'بحث في المكتبة...',
                      prefixIcon: Icon(Icons.search),
                    ),
                    onSubmitted: (q) =>
                        context.read<LibraryCubit>().load(search: q),
                  ),
                ),
                Expanded(
                  child: AsyncBody(
                    isLoading: state.status == PortalLoadStatus.loading ||
                        state.status == PortalLoadStatus.initial,
                    isEmpty: state.status == PortalLoadStatus.loaded &&
                        state.items.isEmpty,
                    emptyTitle: 'لا توجد ملفات',
                    errorMessage: state.status == PortalLoadStatus.error
                        ? state.error
                        : null,
                    onRetry: () => context.read<LibraryCubit>().load(),
                    child: ListView.builder(
                      itemCount: state.items.length,
                      itemBuilder: (context, i) {
                        final item = state.items[i];
                        final downloading = state.downloadingId == item.id;
                        return ListTile(
                          leading: const Icon(
                            Icons.picture_as_pdf,
                            color: NoorTheme.error,
                          ),
                          title: Text(item.title),
                          subtitle: Text(
                            '${item.category} · ${Formatters.number(item.downloadCount)} تنزيل',
                          ),
                          trailing: downloading
                              ? const SizedBox(
                                  width: 24,
                                  height: 24,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : IconButton(
                                  icon: const Icon(Icons.download),
                                  onPressed: () async {
                                    final path = await context
                                        .read<LibraryCubit>()
                                        .download(item);
                                    if (path == null) return;
                                    await FileOpenHelper.openResult(path);
                                    if (!context.mounted) return;
                                    ScaffoldMessenger.of(context).showSnackBar(
                                      SnackBar(
                                        content: Text(
                                          FileOpenHelper.isBrowserDownload(path)
                                              ? 'بدأ تنزيل الملف'
                                              : 'تم فتح الملف',
                                        ),
                                      ),
                                    );
                                  },
                                ),
                        );
                      },
                    ),
                  ),
                ),
              ],
            );
          },
        ),
      ),
    );
  }
}

class MatnLogScreen extends StatelessWidget {
  final int studentId;
  const MatnLogScreen({super.key, required this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => MatnCubit(sl())..load(studentId),
      child: Scaffold(
        appBar: AppBar(title: const Text('سجل المتون')),
        body: BlocBuilder<MatnCubit, MatnState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == PortalLoadStatus.loading ||
                  state.status == PortalLoadStatus.initial,
              isEmpty:
                  state.status == PortalLoadStatus.loaded && state.items.isEmpty,
              emptyTitle: 'لا توجد سجلات متون',
              errorMessage:
                  state.status == PortalLoadStatus.error ? state.error : null,
              onRetry: () => context.read<MatnCubit>().load(studentId),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.items.length,
                itemBuilder: (context, i) {
                  final m = state.items[i];
                  return ListTile(
                    leading: const Icon(Icons.auto_stories),
                    title: Text(m.matnName),
                    subtitle: Text(
                      '${Formatters.displayDate(m.date)} · ${m.portion} · ${m.type}',
                    ),
                    trailing: Text(m.evaluation),
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

class MonthlyTargetsScreen extends StatelessWidget {
  final int studentId;
  const MonthlyTargetsScreen({super.key, required this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => MonthlyTargetsCubit(sl())..load(studentId),
      child: Scaffold(
        appBar: AppBar(title: const Text('الأهداف الشهرية')),
        body: BlocBuilder<MonthlyTargetsCubit, MonthlyTargetsState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == PortalLoadStatus.loading ||
                  state.status == PortalLoadStatus.initial,
              isEmpty:
                  state.status == PortalLoadStatus.loaded && state.items.isEmpty,
              emptyTitle: 'لا توجد أهداف شهرية',
              errorMessage:
                  state.status == PortalLoadStatus.error ? state.error : null,
              onRetry: () =>
                  context.read<MonthlyTargetsCubit>().load(studentId),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.items.length,
                itemBuilder: (context, i) {
                  final t = state.items[i];
                  return SectionCard(
                    title:
                        '${Formatters.number(t.month)} / ${Formatters.number(t.year)}',
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'المطلوب: ${Formatters.number(t.targetAthmanCount)} ثمن',
                        ),
                        Text(
                          'المنجز: ${Formatters.number(t.achievedAthmanCount)} ثمن',
                        ),
                        Text(
                          'الدرجة: ${Formatters.number(t.progressScoreOutOf10, fractionDigits: 1)} / 10',
                        ),
                        if (t.notes != null && t.notes!.isNotEmpty)
                          Text('ملاحظة: ${t.notes}'),
                      ],
                    ),
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

class OralExamsScreen extends StatelessWidget {
  final int studentId;
  const OralExamsScreen({super.key, required this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => OralExamsCubit(sl())..load(studentId),
      child: Scaffold(
        appBar: AppBar(title: const Text('الاختبارات والنتائج')),
        body: BlocBuilder<OralExamsCubit, OralExamsState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == PortalLoadStatus.loading ||
                  state.status == PortalLoadStatus.initial,
              isEmpty:
                  state.status == PortalLoadStatus.loaded && state.items.isEmpty,
              emptyTitle: 'لا توجد اختبارات',
              errorMessage:
                  state.status == PortalLoadStatus.error ? state.error : null,
              onRetry: () => context.read<OralExamsCubit>().load(studentId),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.items.length,
                itemBuilder: (context, i) {
                  final e = state.items[i];
                  return SectionCard(
                    title: e.scopeLabel.isEmpty ? e.kind : e.scopeLabel,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(Formatters.displayDate(e.date)),
                        Text(
                          'النتيجة: ${Formatters.number(e.overallPercent, fractionDigits: 1)}% · ${e.overallGrade}',
                        ),
                        Text(
                          e.isConsideredMemorized
                              ? 'يُعتبر محفوظاً'
                              : 'لم يُعتبر محفوظاً بعد',
                        ),
                        if (e.notes != null && e.notes!.isNotEmpty)
                          Text('ملاحظات: ${e.notes}'),
                      ],
                    ),
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
