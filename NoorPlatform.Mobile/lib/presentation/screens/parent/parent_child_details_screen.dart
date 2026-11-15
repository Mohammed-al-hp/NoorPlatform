import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/attendance_repository.dart';
import '../../../data/models/attendance_models.dart';
import '../../blocs/hifz/hifz_cubit.dart';
import '../../blocs/portal/portal_cubits.dart';
import '../../widgets/async_body.dart';

class ParentChildDetailsScreen extends StatefulWidget {
  final int studentId;
  final String? studentName;

  const ParentChildDetailsScreen({
    super.key,
    required this.studentId,
    this.studentName,
  });

  @override
  State<ParentChildDetailsScreen> createState() =>
      _ParentChildDetailsScreenState();
}

class _ParentChildDetailsScreenState extends State<ParentChildDetailsScreen>
    with SingleTickerProviderStateMixin {
  late final TabController _tabs;

  @override
  void initState() {
    super.initState();
    _tabs = TabController(length: 3, vsync: this);
  }

  @override
  void dispose() {
    _tabs.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(widget.studentName ?? 'تفاصيل الابن'),
        bottom: TabBar(
          controller: _tabs,
          tabs: const [
            Tab(text: 'الحضور'),
            Tab(text: 'الحفظ والمراجعة'),
            Tab(text: 'الصلاة'),
          ],
        ),
        actions: [
          PopupMenuButton<String>(
            onSelected: (v) {
              switch (v) {
                case 'map':
                  context.push('/parent/child/${widget.studentId}/quran-map');
                  break;
                case 'exams':
                  context.push('/parent/child/${widget.studentId}/exams');
                  break;
                case 'matn':
                  context.push('/parent/child/${widget.studentId}/matn');
                  break;
                case 'monthly':
                  context.push(
                    '/parent/child/${widget.studentId}/monthly-targets',
                  );
                  break;
              }
            },
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'map', child: Text('خريطة الحفظ')),
              PopupMenuItem(value: 'exams', child: Text('الاختبارات')),
              PopupMenuItem(value: 'matn', child: Text('المتون')),
              PopupMenuItem(value: 'monthly', child: Text('الأهداف الشهرية')),
            ],
          ),
        ],
      ),
      body: TabBarView(
        controller: _tabs,
        children: [
          _ChildAttendanceTab(studentId: widget.studentId),
          BlocProvider(
            create: (_) => HifzCubit(sl())..loadForStudent(widget.studentId),
            child: _ChildHifzTab(studentId: widget.studentId),
          ),
          BlocProvider(
            create: (_) => ChildPrayerCubit(sl())..load(widget.studentId),
            child: _ChildPrayerTab(studentId: widget.studentId),
          ),
        ],
      ),
    );
  }
}

class _ChildAttendanceTab extends StatefulWidget {
  final int studentId;
  const _ChildAttendanceTab({required this.studentId});

  @override
  State<_ChildAttendanceTab> createState() => _ChildAttendanceTabState();
}

class _ChildAttendanceTabState extends State<_ChildAttendanceTab> {
  AttendanceModel? _data;
  String? _error;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final result =
        await sl<AttendanceRepository>().getStudentAttendance(widget.studentId);
    if (!mounted) return;
    result.fold(
      (f) => setState(() {
        _loading = false;
        _error = f.message;
      }),
      (d) => setState(() {
        _loading = false;
        _data = d;
      }),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AsyncBody(
      isLoading: _loading,
      isEmpty: !_loading && _error == null && (_data?.records.isEmpty ?? true),
      emptyTitle: 'لا توجد سجلات حضور',
      errorMessage: _error,
      onRetry: _load,
      child: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          if (_data != null) ...[
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                StatChip(
                  label: 'الحضور',
                  value: Formatters.number(_data!.summary.present),
                  icon: Icons.check_circle,
                  color: NoorTheme.success,
                ),
                StatChip(
                  label: 'التأخر',
                  value: Formatters.number(_data!.summary.late),
                  icon: Icons.schedule,
                  color: NoorTheme.warning,
                ),
                StatChip(
                  label: 'غائب بإذن',
                  value: Formatters.number(_data!.summary.excusedAbsence),
                  icon: Icons.event_busy,
                ),
                StatChip(
                  label: 'بدون إذن',
                  value: Formatters.number(_data!.summary.unexcusedAbsence),
                  icon: Icons.cancel,
                  color: NoorTheme.error,
                ),
                StatChip(
                  label: 'النسبة',
                  value: '${Formatters.number(_data!.summary.attendanceRate)}%',
                  icon: Icons.percent,
                ),
              ],
            ),
            const SizedBox(height: 16),
            ..._data!.records.map(
              (r) => ListTile(
                title: Text(Formatters.displayDate(r.date.toIso8601String())),
                subtitle: Text(r.hasNote ? r.note! : '—'),
                trailing: Text(_statusLabel(r.status)),
              ),
            ),
          ],
        ],
      ),
    );
  }

  String _statusLabel(AttendanceRecordStatus s) {
    switch (s) {
      case AttendanceRecordStatus.present:
        return 'حاضر';
      case AttendanceRecordStatus.late:
        return 'متأخر';
      case AttendanceRecordStatus.excusedAbsence:
        return 'غائب بإذن';
      case AttendanceRecordStatus.unexcusedAbsence:
        return 'غائب بدون إذن';
      case AttendanceRecordStatus.unknown:
        return '—';
    }
  }
}

class _ChildHifzTab extends StatelessWidget {
  final int studentId;
  const _ChildHifzTab({required this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<HifzCubit, HifzState>(
      builder: (context, state) {
        return AsyncBody(
          isLoading:
              state.status == HifzStatus.loading || state.status == HifzStatus.initial,
          isEmpty: state.status == HifzStatus.loaded && state.records.isEmpty,
          emptyTitle: 'لا توجد جلسات تسميع',
          errorMessage: state.status == HifzStatus.error ? state.error : null,
          onRetry: () => context.read<HifzCubit>().loadForStudent(studentId),
          child: ListView.builder(
            itemCount: state.records.length,
            itemBuilder: (context, i) {
              final r = state.records[i];
              return ListTile(
                title: Text(r.surahName),
                subtitle: Text(
                  '${Formatters.displayDate(r.date)} · ${r.isMemorization ? 'حفظ' : 'مراجعة'} · ${r.verses}',
                ),
                trailing: Text(r.evaluation),
              );
            },
          ),
        );
      },
    );
  }
}

class _ChildPrayerTab extends StatelessWidget {
  final int studentId;
  const _ChildPrayerTab({required this.studentId});

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<ChildPrayerCubit, ChildPrayerState>(
      builder: (context, state) {
        return AsyncBody(
          isLoading: state.status == PortalLoadStatus.loading ||
              state.status == PortalLoadStatus.initial,
          isEmpty:
              state.status == PortalLoadStatus.loaded && state.logs.isEmpty,
          emptyTitle: 'لا يوجد سجل صلاة',
          errorMessage:
              state.status == PortalLoadStatus.error ? state.error : null,
          onRetry: () => context.read<ChildPrayerCubit>().load(studentId),
          child: ListView.builder(
            itemCount: state.logs.length,
            itemBuilder: (context, i) {
              final l = state.logs[i];
              return ListTile(
                leading: Icon(
                  l.prayedInMosque ? Icons.mosque : Icons.home,
                  color: NoorTheme.primaryGreen,
                ),
                title: Text(Formatters.displayDate(l.date)),
                subtitle: Text(
                  '${l.prayedInMosque ? 'مسجد' : 'خارج'} · ${l.onTime ? 'في الوقت' : 'متأخر'} · ${Formatters.number(l.mosquePrayerCount)}',
                ),
              );
            },
          ),
        );
      },
    );
  }
}
