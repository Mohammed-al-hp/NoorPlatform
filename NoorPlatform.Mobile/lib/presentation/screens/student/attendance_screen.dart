import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../data/models/attendance_models.dart';
import '../../blocs/attendance/attendance_bloc.dart';
import '../../blocs/attendance/attendance_event.dart';
import '../../blocs/attendance/attendance_state.dart';

class AttendanceScreen extends StatefulWidget {
  const AttendanceScreen({super.key});

  @override
  State<AttendanceScreen> createState() => _AttendanceScreenState();
}

class _AttendanceScreenState extends State<AttendanceScreen> {
  @override
  void initState() {
    super.initState();
    context.read<AttendanceBloc>().add(const LoadAttendance());
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('سجل الحضور'),
        actions: [
          IconButton(
            icon: const Icon(Icons.refresh),
            onPressed: () {
              context.read<AttendanceBloc>().add(const LoadAttendance());
            },
          ),
        ],
      ),
      body: BlocBuilder<AttendanceBloc, AttendanceState>(
        builder: (context, state) {
          if (state is AttendanceLoading || state is AttendanceInitial) {
            return const Center(child: CircularProgressIndicator());
          } else if (state is AttendanceError) {
            return Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Text(state.message, style: const TextStyle(color: NoorTheme.error)),
                  const SizedBox(height: 16),
                  ElevatedButton(
                    onPressed: () {
                      context.read<AttendanceBloc>().add(const LoadAttendance());
                    },
                    child: const Text('إعادة المحاولة'),
                  ),
                ],
              ),
            );
          } else if (state is AttendanceLoaded) {
            return _buildContent(context, state.data);
          }
          return const SizedBox.shrink();
        },
      ),
    );
  }

  Widget _buildContent(BuildContext context, AttendanceModel data) {
    return RefreshIndicator(
      onRefresh: () async {
        context.read<AttendanceBloc>().add(const LoadAttendance());
        // ننتظر حتى يخرج الـ Bloc من حالة Loading قبل إيقاف مؤشر السحب،
        // وإلا سيتوقف المؤشر فوراً قبل اكتمال الطلب فعلياً.
        await context.read<AttendanceBloc>().stream.firstWhere(
              (s) => s is! AttendanceLoading,
            );
      },
      child: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          _SummaryCard(summary: data.summary),
          const SizedBox(height: NoorTheme.spacingLg),
          Text('السجل التفصيلي', style: Theme.of(context).textTheme.titleLarge),
          const SizedBox(height: NoorTheme.spacingSm),
          if (data.records.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(vertical: NoorTheme.spacingLg),
              child: Center(child: Text('لا توجد سجلات حضور بعد')),
            )
          else
            ...data.records.map((r) => _AttendanceRecordTile(record: r)),
        ],
      ),
    );
  }
}

class _SummaryCard extends StatelessWidget {
  final AttendanceSummaryModel summary;
  const _SummaryCard({required this.summary});

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        child: Column(
          children: [
            Stack(
              alignment: Alignment.center,
              children: [
                SizedBox(
                  width: 110,
                  height: 110,
                  child: CircularProgressIndicator(
                    value: summary.attendanceRate / 100,
                    strokeWidth: 10,
                    backgroundColor: Colors.grey.shade200,
                    color: NoorTheme.primaryGreen,
                  ),
                ),
                Text(
                  '${summary.attendanceRate}%',
                  style: Theme.of(context).textTheme.headlineMedium?.copyWith(
                        color: NoorTheme.primaryGreen,
                      ),
                ),
              ],
            ),
            const SizedBox(height: NoorTheme.spacingMd),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                _StatChip(label: 'حاضر', value: summary.present, color: NoorTheme.success),
                _StatChip(label: 'متأخر', value: summary.late, color: NoorTheme.accentGold),
                _StatChip(
                  label: 'غياب بعذر',
                  value: summary.excusedAbsence,
                  color: NoorTheme.info,
                ),
                _StatChip(
                  label: 'غياب بلا عذر',
                  value: summary.unexcusedAbsence,
                  color: NoorTheme.error,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _StatChip extends StatelessWidget {
  final String label;
  final int value;
  final Color color;

  const _StatChip({required this.label, required this.value, required this.color});

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Text(
          '$value',
          style: Theme.of(context).textTheme.titleLarge?.copyWith(color: color),
        ),
        Text(label, style: Theme.of(context).textTheme.bodySmall),
      ],
    );
  }
}

class _AttendanceRecordTile extends StatelessWidget {
  final AttendanceRecordModel record;
  const _AttendanceRecordTile({required this.record});

  ({IconData icon, Color color, String label}) get _statusVisual {
    switch (record.status) {
      case AttendanceRecordStatus.present:
        return (icon: Icons.check_circle, color: NoorTheme.success, label: 'حاضر');
      case AttendanceRecordStatus.late:
        return (icon: Icons.watch_later, color: NoorTheme.accentGold, label: 'متأخر');
      case AttendanceRecordStatus.excusedAbsence:
        return (icon: Icons.event_busy, color: NoorTheme.info, label: 'غياب بعذر');
      case AttendanceRecordStatus.unexcusedAbsence:
        return (icon: Icons.cancel, color: NoorTheme.error, label: 'غياب بلا عذر');
      case AttendanceRecordStatus.unknown:
        return (icon: Icons.help_outline, color: Colors.grey, label: 'غير معروف');
    }
  }

  @override
  Widget build(BuildContext context) {
    final visual = _statusVisual;
    final dateLabel = '${record.date.year}-'
        '${record.date.month.toString().padLeft(2, '0')}-'
        '${record.date.day.toString().padLeft(2, '0')}';

    return Card(
      child: ListTile(
        leading: Icon(visual.icon, color: visual.color),
        title: Text(dateLabel),
        subtitle: record.hasNote ? Text(record.note!) : null,
        trailing: Chip(
          label: Text(visual.label, style: const TextStyle(fontSize: 11)),
          backgroundColor: visual.color.withValues(alpha: 0.1),
        ),
      ),
    );
  }
}
