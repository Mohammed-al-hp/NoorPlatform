import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/parent_models.dart';
import '../../../data/models/pedagogical_models.dart';
import '../../../di/injection_container.dart';
import '../../blocs/parent_dashboard/parent_dashboard_bloc.dart';
import '../../blocs/parent_dashboard/parent_dashboard_event.dart';
import '../../blocs/parent_dashboard/parent_dashboard_state.dart';
import '../../blocs/portal/portal_cubits.dart';
import '../../widgets/async_body.dart';

class ParentHomeNoteScreen extends StatelessWidget {
  const ParentHomeNoteScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: [
        BlocProvider(
          create: (_) => sl<ParentDashboardBloc>()..add(LoadParentDashboard()),
        ),
        BlocProvider(create: (_) => ParentHomeCubit(sl())),
      ],
      child: const _HomeNoteView(),
    );
  }
}

class _HomeNoteView extends StatefulWidget {
  const _HomeNoteView();

  @override
  State<_HomeNoteView> createState() => _HomeNoteViewState();
}

class _HomeNoteViewState extends State<_HomeNoteView> {
  int? _studentId;
  int _rating = 5;
  final _notes = TextEditingController();
  bool _initializedChild = false;
  String? _prefilledKey;

  static const ratings = {
    5: 'ممتاز',
    4: 'جيد جداً',
    3: 'جيد',
    2: 'مقبول',
    1: 'ضعيف',
  };

  @override
  void dispose() {
    _notes.dispose();
    super.dispose();
  }

  void _selectChild(int id) {
    setState(() {
      _studentId = id;
      _prefilledKey = null;
      _rating = 5;
      _notes.clear();
    });
    context.read<ParentHomeCubit>().load(id);
  }

  ParentHomeFeedbackModel? _weekEntry(List<ParentHomeFeedbackModel> feedbacks) {
    final week = Formatters.weekStartYmd();
    for (final f in feedbacks) {
      final raw = f.weekStartDate;
      if (raw.startsWith(week)) return f;
      final parsed = DateTime.tryParse(raw);
      if (parsed != null && Formatters.ymd(parsed) == week) return f;
    }
    return null;
  }

  int _ratingFromEntry(ParentHomeFeedbackModel entry) {
    final parsed = int.tryParse(entry.rating);
    if (parsed != null && ratings.containsKey(parsed)) return parsed;
    for (final e in ratings.entries) {
      if (e.value == entry.ratingLabel || e.value == entry.rating) {
        return e.key;
      }
    }
    return 5;
  }

  void _prefillIfNeeded(ParentHomeState state) {
    if (state.status != PortalLoadStatus.loaded || _studentId == null) return;
    final week = Formatters.weekStartYmd();
    final key = '$_studentId|$week';
    if (_prefilledKey == key) return;
    _prefilledKey = key;
    final existing = _weekEntry(state.feedbacks);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      setState(() {
        if (existing != null) {
          _rating = _ratingFromEntry(existing);
          _notes.text = existing.notes ?? '';
        } else {
          _rating = 5;
          _notes.clear();
        }
      });
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('المتابعة المنزلية')),
      body: BlocBuilder<ParentDashboardBloc, ParentDashboardState>(
        builder: (context, dashState) {
          if (dashState is ParentDashboardLoading ||
              dashState is ParentDashboardInitial) {
            return const Center(child: CircularProgressIndicator());
          }
          if (dashState is ParentDashboardError) {
            return ErrorState(
              message: dashState.message,
              onRetry: () => context
                  .read<ParentDashboardBloc>()
                  .add(LoadParentDashboard()),
            );
          }
          final children = dashState is ParentDashboardLoaded
              ? dashState.summary.children
              : <ChildDataModel>[];
          if (children.isEmpty) {
            return const EmptyState(title: 'لا يوجد أبناء');
          }
          if (!_initializedChild) {
            _initializedChild = true;
            WidgetsBinding.instance.addPostFrameCallback((_) {
              _selectChild(children.first.id);
            });
          }

          return BlocConsumer<ParentHomeCubit, ParentHomeState>(
            listener: (context, state) {
              if (state.successMessage != null) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(content: Text(state.successMessage!)),
                );
              }
              if (state.error != null &&
                  state.status != PortalLoadStatus.error) {
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text(state.error!),
                    backgroundColor: NoorTheme.error,
                  ),
                );
              }
            },
            builder: (context, state) {
              _prefillIfNeeded(state);
              final hasThisWeek = _weekEntry(state.feedbacks) != null;
              return ListView(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                children: [
                  DropdownButtonFormField<int>(
                    value: _studentId ?? children.first.id,
                    decoration: const InputDecoration(labelText: 'الابن'),
                    items: children
                        .map(
                          (c) => DropdownMenuItem(
                            value: c.id,
                            child: Text(c.fullName),
                          ),
                        )
                        .toList(),
                    onChanged: (v) {
                      if (v != null) _selectChild(v);
                    },
                  ),
                  const SizedBox(height: 12),
                  SectionCard(
                    title: 'تقييم هذا الأسبوع',
                    child: Column(
                      children: [
                        DropdownButtonFormField<int>(
                          value: _rating,
                          decoration:
                              const InputDecoration(labelText: 'التقييم'),
                          items: ratings.entries
                              .map(
                                (e) => DropdownMenuItem(
                                  value: e.key,
                                  child: Text(
                                    '${Formatters.number(e.key)} — ${e.value}',
                                  ),
                                ),
                              )
                              .toList(),
                          onChanged: (v) => setState(() => _rating = v ?? 5),
                        ),
                        const SizedBox(height: 8),
                        TextField(
                          controller: _notes,
                          maxLines: 3,
                          decoration:
                              const InputDecoration(labelText: 'ملاحظات'),
                        ),
                        const SizedBox(height: 12),
                        ElevatedButton(
                          onPressed: state.submitting || _studentId == null
                              ? null
                              : () => context.read<ParentHomeCubit>().submit(
                                    studentId: _studentId!,
                                    weekStartDate: Formatters.weekStartYmd(),
                                    rating: _rating,
                                    notes: _notes.text.trim().isEmpty
                                        ? null
                                        : _notes.text.trim(),
                                  ),
                          child: state.submitting
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : Text(
                                  hasThisWeek
                                      ? 'تحديث ملاحظة هذا الأسبوع'
                                      : 'حفظ ملاحظة هذا الأسبوع',
                                ),
                        ),
                      ],
                    ),
                  ),
                  SectionCard(
                    title: 'السجل',
                    child: state.feedbacks.isEmpty
                        ? const Text('لا توجد ملاحظات سابقة')
                        : Column(
                            children: state.feedbacks
                                .map(
                                  (f) => ListTile(
                                    contentPadding: EdgeInsets.zero,
                                    title: Text(f.ratingLabel),
                                    subtitle: Text(
                                      '${Formatters.displayDate(f.weekStartDate)}${f.notes != null ? ' · ${f.notes}' : ''}',
                                    ),
                                  ),
                                )
                                .toList(),
                          ),
                  ),
                ],
              );
            },
          );
        },
      ),
    );
  }
}
