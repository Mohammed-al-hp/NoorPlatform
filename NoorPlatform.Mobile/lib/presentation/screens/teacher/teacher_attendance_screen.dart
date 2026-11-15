import 'package:flutter/material.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/teacher_models.dart';
import '../../widgets/async_body.dart';
import 'teacher_ui.dart';

/// تسجيل الحضور اليومي لحلقة: نفس سلوك الويب (الافتراضي "حاضر" لليوم الحالي،
/// حفظ جماعي عبر POST /api/attendance/bulk، وإشعارات الغياب لأولياء الأمور).
class TeacherAttendanceScreen extends StatefulWidget {
  const TeacherAttendanceScreen({super.key});

  @override
  State<TeacherAttendanceScreen> createState() => _TeacherAttendanceScreenState();
}

class _TeacherAttendanceScreenState extends State<TeacherAttendanceScreen> {
  List<CircleModel> _circles = const [];
  CircleModel? _circle;
  DateTime _date = DateTime.now();
  List<AttendanceRosterItem> _roster = const [];
  final Map<int, String> _status = {};
  final Map<int, String> _notes = {};
  bool _loading = true;
  bool _saving = false;
  String? _error;

  bool get _isToday => Formatters.ymd(_date) == Formatters.ymd();

  @override
  void initState() {
    super.initState();
    _loadCircles();
  }

  Future<void> _loadCircles() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final r = await teacherRepo.getCircles();
    if (!mounted) return;
    r.fold(
      (f) => setState(() {
        _error = f.message;
        _loading = false;
      }),
      (list) {
        _circles = list;
        _circle = list.isEmpty ? null : list.first;
        _loadRoster();
      },
    );
  }

  Future<void> _loadRoster() async {
    final c = _circle;
    if (c == null) {
      setState(() => _loading = false);
      return;
    }
    setState(() {
      _loading = true;
      _error = null;
    });
    final r = await teacherRepo.getAttendanceRoster(c.id, Formatters.ymd(_date));
    if (!mounted) return;
    r.fold(
      (f) => setState(() {
        _error = f.message;
        _loading = false;
      }),
      (list) => setState(() {
        _roster = list;
        _status.clear();
        _notes.clear();
        for (final s in list) {
          // البند 8 في الويب: غير المسجّلين اليوم يُفترض حضورهم
          _status[s.studentId] =
              s.status == 'NotRecorded' ? (_isToday ? 'Present' : 'NotRecorded') : s.status;
          if (s.note != null) _notes[s.studentId] = s.note!;
        }
        _loading = false;
      }),
    );
  }

  Future<void> _pickDate() async {
    final d = await showDatePicker(
      context: context,
      initialDate: _date,
      firstDate: DateTime.now().subtract(const Duration(days: 365)),
      lastDate: DateTime.now(),
    );
    if (d != null) {
      setState(() => _date = d);
      _loadRoster();
    }
  }

  Future<void> _save() async {
    final c = _circle;
    if (c == null) return;
    final records = _roster
        .where((s) => (_status[s.studentId] ?? 'NotRecorded') != 'NotRecorded')
        .map((s) => {
              'studentId': s.studentId,
              'status': _status[s.studentId],
              'note': _notes[s.studentId] ?? '',
            })
        .toList();
    if (records.isEmpty) {
      showSnack(context, 'لا توجد سجلات للحفظ');
      return;
    }
    if (!await confirmDialog(context, 'سيتم حفظ ${records.length} سجل حضور. متابعة؟', confirmText: 'حفظ')) {
      return;
    }
    if (!mounted) return;
    setState(() => _saving = true);
    final ok = await runAction(
      context,
      () => teacherRepo.saveBulkAttendance(date: Formatters.ymd(_date), circleId: c.id, records: records),
    );
    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) _loadRoster();
  }

  Future<void> _notifyAbsent() async {
    final c = _circle;
    if (c == null) return;
    if (!await confirmDialog(context, 'إرسال إشعارات غياب لأولياء أمور الغائبين اليوم عبر واتساب؟',
        confirmText: 'إرسال')) {
      return;
    }
    if (mounted) await runAction(context, () => teacherRepo.sendBulkAbsence(c.id));
  }

  Future<void> _editNote(AttendanceRosterItem s) async {
    final ctrl = TextEditingController(text: _notes[s.studentId]);
    final v = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('ملاحظة: ${s.fullName}'),
        content: TextField(controller: ctrl, maxLines: 2),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إلغاء')),
          FilledButton(onPressed: () => Navigator.pop(ctx, ctrl.text.trim()), child: const Text('حفظ')),
        ],
      ),
    );
    if (v != null) setState(() => _notes[s.studentId] = v);
  }

  @override
  Widget build(BuildContext context) {
    final counts = <String, int>{};
    for (final v in _status.values) {
      counts[v] = (counts[v] ?? 0) + 1;
    }
    return Scaffold(
      appBar: AppBar(
        title: const Text('الحضور والغياب'),
        actions: [
          IconButton(
            tooltip: 'إرسال إشعارات للغائبين',
            icon: const Icon(Icons.notifications_active_outlined),
            onPressed: _circle == null ? null : _notifyAbsent,
          ),
        ],
      ),
      bottomNavigationBar: _roster.isEmpty
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton.icon(
                  onPressed: _saving ? null : _save,
                  icon: const Icon(Icons.save),
                  label: Text(_saving ? 'جاري الحفظ...' : 'حفظ الحضور'),
                ),
              ),
            ),
      body: Column(
        children: [
          SizedBox(
            height: 56,
            child: ListView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              children: _circles
                  .map((c) => Padding(
                        padding: const EdgeInsetsDirectional.only(end: 8),
                        child: ChoiceChip(
                          label: Text(c.isExtra ? '${c.name} (إضافية)' : c.name),
                          selected: _circle?.id == c.id,
                          onSelected: (_) {
                            setState(() => _circle = c);
                            _loadRoster();
                          },
                        ),
                      ))
                  .toList(),
            ),
          ),
          ListTile(
            leading: const Icon(Icons.calendar_today),
            title: Text(Formatters.ymd(_date)),
            subtitle: Text(
                'حاضر ${counts['Present'] ?? 0} · متأخر ${counts['Late'] ?? 0} · بعذر ${counts['ExcusedAbsence'] ?? 0} · غائب ${counts['UnexcusedAbsence'] ?? 0}'),
            trailing: TextButton(
              onPressed: _roster.isEmpty
                  ? null
                  : () => setState(() {
                        for (final s in _roster) {
                          _status[s.studentId] = 'Present';
                        }
                      }),
              child: const Text('الجميع حاضر'),
            ),
            onTap: _pickDate,
          ),
          const Divider(height: 1),
          Expanded(
            child: AsyncBody(
              isLoading: _loading,
              errorMessage: _error,
              onRetry: _circles.isEmpty ? _loadCircles : _loadRoster,
              isEmpty: _circle == null || _roster.isEmpty,
              emptyTitle: _circle == null ? 'لا توجد حلقات مسندة إليك' : 'لا يوجد طلاب في هذه الحلقة',
              child: ListView.builder(
                itemCount: _roster.length,
                itemBuilder: (context, i) {
                  final s = _roster[i];
                  final current = _status[s.studentId] ?? 'NotRecorded';
                  return Card(
                    margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                    child: Padding(
                      padding: const EdgeInsets.all(8),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Row(children: [
                            Expanded(
                              child: Text(s.fullName, style: Theme.of(context).textTheme.titleSmall),
                            ),
                            IconButton(
                              tooltip: 'ملاحظة',
                              icon: Icon(
                                (_notes[s.studentId] ?? '').isEmpty ? Icons.note_add_outlined : Icons.sticky_note_2,
                                size: 20,
                              ),
                              onPressed: () => _editNote(s),
                            ),
                          ]),
                          Wrap(
                            spacing: 6,
                            children: kAttendanceStatuses.entries
                                .map((e) => ChoiceChip(
                                      label: Text(e.value),
                                      selected: current == e.key,
                                      selectedColor: attendanceColor(e.key).withValues(alpha: 0.25),
                                      onSelected: (_) => setState(() => _status[s.studentId] = e.key),
                                    ))
                                .toList(),
                          ),
                          if ((_notes[s.studentId] ?? '').isNotEmpty)
                            Text(_notes[s.studentId]!,
                                style: const TextStyle(fontSize: 12, color: NoorTheme.textSecondary)),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
          ),
        ],
      ),
    );
  }
}
