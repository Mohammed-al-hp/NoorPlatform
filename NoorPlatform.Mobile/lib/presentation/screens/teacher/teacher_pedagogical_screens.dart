import 'package:dartz/dartz.dart' hide State;
import 'package:flutter/material.dart';
import '../../../config/theme.dart';
import '../../../core/errors/failures.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/pedagogical_models.dart';
import '../../../data/models/teacher_models.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../widgets/async_body.dart';
import 'teacher_ui.dart';

PortalRepository get _portal => sl<PortalRepository>();

/// التقييم التربوي للمحفّظ — نفس تبويبات الويب: المتون، الأهداف الشهرية، فترات التقييم، اللباس، الصلاة.
class TeacherEvaluationScreen extends StatelessWidget {
  final int initialTab;
  final int? studentId;
  const TeacherEvaluationScreen({super.key, this.initialTab = 0, this.studentId});

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 5,
      initialIndex: initialTab.clamp(0, 4),
      child: Scaffold(
        appBar: AppBar(
          title: const Text('التقييم التربوي'),
          bottom: const TabBar(
            isScrollable: true,
            tabs: [
              Tab(text: 'المتون'),
              Tab(text: 'الأهداف الشهرية'),
              Tab(text: 'فترات التقييم'),
              Tab(text: 'اللباس'),
              Tab(text: 'الصلاة'),
            ],
          ),
        ),
        body: TabBarView(children: [
          _StudentScoped(studentId: studentId, builder: (id) => _MatnTab(studentId: id)),
          _StudentScoped(studentId: studentId, builder: (id) => _TargetsTab(studentId: id)),
          const _PeriodsTab(),
          const _DressTab(),
          _StudentScoped(studentId: studentId, builder: (id) => _PrayerTab(studentId: id)),
        ]),
      ),
    );
  }
}

/// تبويب مرتبط بطالب: يعرض منتقي الطالب ثم المحتوى.
class _StudentScoped extends StatefulWidget {
  final int? studentId;
  final Widget Function(int studentId) builder;
  const _StudentScoped({required this.studentId, required this.builder});

  @override
  State<_StudentScoped> createState() => _StudentScopedState();
}

class _StudentScopedState extends State<_StudentScoped> with AutomaticKeepAliveClientMixin {
  late int? _id = widget.studentId;

  @override
  bool get wantKeepAlive => true;

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Column(children: [
      Padding(
        padding: const EdgeInsets.all(12),
        child: StudentDropdown(value: _id, onChanged: (s) => setState(() => _id = s?.id)),
      ),
      Expanded(
        child: _id == null
            ? const EmptyState(title: 'اختر طالباً', icon: Icons.person_search)
            : KeyedSubtree(key: ValueKey(_id), child: widget.builder(_id!)),
      ),
    ]);
  }
}

// ───────────────────────── المتون ─────────────────────────
class _MatnTab extends StatefulWidget {
  final int studentId;
  const _MatnTab({required this.studentId});

  @override
  State<_MatnTab> createState() => _MatnTabState();
}

class _MatnTabState extends State<_MatnTab> {
  int _key = 0;

  Future<void> _add() async {
    final matns = (await teacherRepo.getMatns()).getOrElse(() => const []);
    if (!mounted) return;
    if (matns.isEmpty) {
      showSnack(context, 'لا توجد متون مفعّلة — يضيفها مدير المنصة من الإعدادات', error: true);
      return;
    }
    String matn = matns.first.name;
    String type = 'Memorization';
    String evaluation = kMemorizationGrades.first;
    final portion = TextEditingController();
    final notes = TextEditingController();
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setD) => AlertDialog(
          title: const Text('تسجيل متن'),
          content: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              DropdownButtonFormField<String>(
                initialValue: matn,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'المتن'),
                items: matns.map((m) => DropdownMenuItem(value: m.name, child: Text(m.name))).toList(),
                onChanged: (v) => matn = v ?? matn,
              ),
              TextField(controller: portion, decoration: const InputDecoration(labelText: 'المقدار')),
              DropdownButtonFormField<String>(
                initialValue: type,
                decoration: const InputDecoration(labelText: 'النوع'),
                items: const [
                  DropdownMenuItem(value: 'Memorization', child: Text('حفظ')),
                  DropdownMenuItem(value: 'Revision', child: Text('مراجعة')),
                ],
                onChanged: (v) => setD(() {
                  type = v ?? type;
                  evaluation = gradeForList(evaluation, revision: type == 'Revision');
                }),
              ),
              DropdownButtonFormField<String>(
                key: ValueKey('matn-grade-$type'),
                initialValue: evaluation,
                decoration: const InputDecoration(labelText: 'التقدير'),
                items: gradesFor(revision: type == 'Revision').map(gradeMenuItem).toList(),
                onChanged: (v) => evaluation = v ?? evaluation,
              ),
              TextField(controller: notes, decoration: const InputDecoration(labelText: 'ملاحظات')),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
            FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حفظ')),
          ],
        ),
      ),
    );
    if (ok != true || !mounted) return;
    final saved = await runAction(
        context,
        () => teacherRepo.addMatnRecord({
              'studentId': widget.studentId,
              'date': libyaNowIso(),
              'matnName': matn,
              'portion': portion.text.trim(),
              'type': type,
              'evaluation': evaluation,
              'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
            }));
    if (saved) setState(() => _key++);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton(onPressed: _add, child: const Icon(Icons.add)),
      body: LoadView<List<MatnRecordModel>>(
        key: ValueKey(_key),
        loader: () => _portal.getMatn(widget.studentId),
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد سجلات متون',
        builder: (context, list, reload) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final m = list[i];
            return ListTile(
              title: Text('${m.matnName} — ${m.portion}'),
              subtitle: Text(
                  '${m.type == 'Revision' ? 'مراجعة' : 'حفظ'} · ${m.evaluation} · ${Formatters.displayDate(m.date)}'),
              trailing: IconButton(
                icon: const Icon(Icons.delete_outline),
                onPressed: () async {
                  if (!await confirmDialog(context, 'حذف سجل المتن؟', confirmText: 'حذف')) return;
                  if (!context.mounted) return;
                  if (await runAction(context, () => teacherRepo.deleteMatnRecord(m.id))) reload();
                },
              ),
            );
          },
        ),
      ),
    );
  }
}

// ───────────────────────── الأهداف الشهرية ─────────────────────────
class _TargetsTab extends StatefulWidget {
  final int studentId;
  const _TargetsTab({required this.studentId});

  @override
  State<_TargetsTab> createState() => _TargetsTabState();
}

class _TargetsTabState extends State<_TargetsTab> {
  int _key = 0;

  Future<void> _edit([MonthlyTargetModel? t]) async {
    final now = DateTime.now();
    final defaults = (await teacherRepo.getGradingDefaults()).fold((_) => null, (v) => v);
    if (!mounted) return;
    final year = TextEditingController(text: '${t?.year ?? now.year}');
    final month = TextEditingController(text: '${t?.month ?? now.month}');
    final target = TextEditingController(
        text: '${t?.targetAthmanCount ?? defaults?.defaultMonthlyAthmanTarget ?? 8}');
    final achieved = TextEditingController(text: t == null ? '' : '${t.achievedAthmanCount}');
    final specialNote = TextEditingController(text: t?.specialModeNote);
    final notes = TextEditingController(text: t?.notes);
    bool special = t?.isSpecialMode ?? false;

    final action = await showDialog<String>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setD) => AlertDialog(
          title: const Text('الهدف الشهري (بالأثمان)'),
          content: SingleChildScrollView(
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              Row(children: [
                Expanded(
                    child: TextField(
                        controller: month,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(labelText: 'الشهر'))),
                const SizedBox(width: 8),
                Expanded(
                    child: TextField(
                        controller: year,
                        keyboardType: TextInputType.number,
                        decoration: const InputDecoration(labelText: 'السنة'))),
              ]),
              TextField(
                  controller: target,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'الهدف (عدد الأثمان)')),
              TextField(
                  controller: achieved,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(labelText: 'المنجز (اختياري)')),
              SwitchListTile(
                contentPadding: EdgeInsets.zero,
                title: const Text('وضع خاص (لوحة مراجعة/مسابقة)'),
                value: special,
                onChanged: (v) => setD(() => special = v),
              ),
              if (special)
                TextField(controller: specialNote, decoration: const InputDecoration(labelText: 'ملاحظة الوضع الخاص')),
              TextField(controller: notes, decoration: const InputDecoration(labelText: 'ملاحظات')),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إلغاء')),
            OutlinedButton(onPressed: () => Navigator.pop(ctx, 'sync'), child: const Text('مزامنة من الاختبارات')),
            FilledButton(onPressed: () => Navigator.pop(ctx, 'save'), child: const Text('حفظ')),
          ],
        ),
      ),
    );
    if (action == null || !mounted) return;
    final body = {
      'studentId': widget.studentId,
      'year': int.tryParse(year.text) ?? now.year,
      'month': int.tryParse(month.text) ?? now.month,
      'targetAthmanCount': int.tryParse(target.text) ?? 8,
      'isSpecialMode': special,
      'specialModeNote': specialNote.text.trim().isEmpty ? null : specialNote.text.trim(),
      'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
    };
    final ok = await runAction(
        context,
        () => action == 'sync'
            ? teacherRepo.syncMonthlyTargetFromOral(body)
            : teacherRepo.upsertMonthlyTarget({
                ...body,
                'achievedAthmanCount': int.tryParse(achieved.text.trim()),
              }));
    if (ok) setState(() => _key++);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton(onPressed: () => _edit(), child: const Icon(Icons.add)),
      body: LoadView<List<MonthlyTargetModel>>(
        key: ValueKey(_key),
        loader: () => _portal.getMonthlyTargets(widget.studentId),
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد أهداف شهرية',
        builder: (context, list, _) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final t = list[i];
            return ListTile(
              title: Text('${t.month}/${t.year} — ${t.achievedAthmanCount} من ${t.targetAthmanCount} ثمن'),
              subtitle: Text([
                'الدرجة ${formatScore(t.progressScoreOutOf10)}/10',
                if (t.isSpecialMode) 'وضع خاص${t.specialModeNote != null ? ': ${t.specialModeNote}' : ''}',
                if ((t.notes ?? '').isNotEmpty) t.notes!,
              ].join(' · ')),
              trailing: const Icon(Icons.edit_outlined),
              onTap: () => _edit(t),
            );
          },
        ),
      ),
    );
  }
}

// ───────────────────────── فترات التقييم ─────────────────────────
class _PeriodsTab extends StatefulWidget {
  const _PeriodsTab();

  @override
  State<_PeriodsTab> createState() => _PeriodsTabState();
}

class _PeriodsTabState extends State<_PeriodsTab> {
  int _key = 0;

  Future<void> _create() async {
    final name = TextEditingController();
    final notes = TextEditingController();
    DateTime start = DateTime(DateTime.now().year, DateTime.now().month, 1);
    DateTime end = DateTime(start.year, start.month + 1, 0);
    int? circleId;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setD) {
          Future<void> pick(bool isStart) async {
            final d = await showDatePicker(
              context: ctx,
              initialDate: isStart ? start : end,
              firstDate: DateTime(2020),
              lastDate: DateTime(2100),
            );
            if (d != null) setD(() => isStart ? start = d : end = d);
          }

          return AlertDialog(
            title: const Text('فترة تقييم جديدة'),
            content: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                TextField(controller: name, decoration: const InputDecoration(labelText: 'اسم الفترة')),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('من'),
                  subtitle: Text(Formatters.ymd(start)),
                  onTap: () => pick(true),
                ),
                ListTile(
                  contentPadding: EdgeInsets.zero,
                  title: const Text('إلى'),
                  subtitle: Text(Formatters.ymd(end)),
                  onTap: () => pick(false),
                ),
                CircleDropdown(
                  value: circleId,
                  allowNone: true,
                  noneLabel: 'كل حلقاتي',
                  onChanged: (c) => circleId = c?.id,
                ),
                TextField(controller: notes, decoration: const InputDecoration(labelText: 'ملاحظات')),
              ]),
            ),
            actions: [
              TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
              FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('إنشاء')),
            ],
          );
        },
      ),
    );
    if (ok != true || !mounted) return;
    if (name.text.trim().isEmpty) {
      showSnack(context, 'اسم الفترة مطلوب', error: true);
      return;
    }
    final created = await runAction(
        context,
        () => teacherRepo.createPeriod({
              'name': name.text.trim(),
              'startDate': Formatters.ymd(start),
              'endDate': Formatters.ymd(end),
              'circleId': circleId,
              'notes': notes.text.trim().isEmpty ? null : notes.text.trim(),
            }));
    if (created) setState(() => _key++);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      floatingActionButton: FloatingActionButton(onPressed: _create, child: const Icon(Icons.add)),
      body: LoadView<List<EvaluationPeriodModel>>(
        key: ValueKey(_key),
        loader: teacherRepo.getPeriods,
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد فترات تقييم',
        builder: (context, list, reload) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final p = list[i];
            return ListTile(
              leading: const Icon(Icons.date_range, color: NoorTheme.primaryGreen),
              title: Text(p.name),
              subtitle: Text(
                  '${Formatters.displayDate(p.startDate)} ← ${Formatters.displayDate(p.endDate)} · ${p.circleName ?? 'كل الحلقات'} · ${p.evaluationsCount} تقييم'),
              trailing: const Icon(Icons.chevron_left),
              onTap: () async {
                await Navigator.of(context)
                    .push(MaterialPageRoute(builder: (_) => PeriodEvaluationsScreen(period: p)));
                reload();
              },
            );
          },
        ),
      ),
    );
  }
}

/// تقييمات فترة: قائمة طلاب المحفّظ مع التقييم المحفوظ، وتحرير/مسودة تلقائية لكل طالب.
class PeriodEvaluationsScreen extends StatelessWidget {
  final EvaluationPeriodModel period;
  const PeriodEvaluationsScreen({super.key, required this.period});

  Future<Either<Failure, (List<TeacherStudentModel>, List<PeriodEvaluationDraft>)>> _load() async {
    final students = await teacherRepo.getStudents();
    final evals = await teacherRepo.getPeriodEvaluations(period.id);
    return students.flatMap((s) => evals.map((e) => (
          period.circleId == null ? s : s.where((x) => x.circleId == period.circleId).toList(),
          e,
        )));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('تقييمات: ${period.name}')),
      body: LoadView<(List<TeacherStudentModel>, List<PeriodEvaluationDraft>)>(
        loader: _load,
        isEmpty: (d) => d.$1.isEmpty,
        emptyTitle: 'لا يوجد طلاب في نطاق هذه الفترة',
        builder: (context, d, reload) {
          final (students, evals) = d;
          return ListView.builder(
            physics: const AlwaysScrollableScrollPhysics(),
            itemCount: students.length,
            itemBuilder: (context, i) {
              final s = students[i];
              final saved = evals.where((e) => e.studentId == s.id).firstOrNull;
              return ListTile(
                title: Text(s.fullName),
                subtitle: Text(saved == null
                    ? 'لم يُقيّم بعد'
                    : '${formatScore(saved.overallScore)}% — ${saved.gradeLabel}'),
                trailing: Icon(saved == null ? Icons.edit_note : Icons.check_circle,
                    color: saved == null ? null : NoorTheme.success),
                onTap: () async {
                  final changed = await Navigator.of(context).push<bool>(MaterialPageRoute(
                    builder: (_) => _EvaluationForm(
                      period: period,
                      studentId: s.id,
                      studentName: s.fullName,
                      saved: saved,
                    ),
                  ));
                  if (changed == true) reload();
                },
              );
            },
          );
        },
      ),
    );
  }
}

class _EvaluationForm extends StatefulWidget {
  final EvaluationPeriodModel period;
  final int studentId;
  final String studentName;
  final PeriodEvaluationDraft? saved;

  const _EvaluationForm({
    required this.period,
    required this.studentId,
    required this.studentName,
    this.saved,
  });

  @override
  State<_EvaluationForm> createState() => _EvaluationFormState();
}

class _EvaluationFormState extends State<_EvaluationForm> {
  final _c = <String, TextEditingController>{
    'attendanceScore': TextEditingController(),
    'hifzScore': TextEditingController(),
    'revisionScore': TextEditingController(),
    'progressScore': TextEditingController(),
    'matnScore': TextEditingController(),
    'dressScore': TextEditingController(),
    'prayerAdvisoryScore': TextEditingController(),
    'parentHomeAdvisoryScore': TextEditingController(),
  };
  final _notes = TextEditingController();
  bool _includeAdvisory = false;
  bool _busy = false;
  String? _draftInfo;

  static const _labels = {
    'attendanceScore': 'الحضور',
    'hifzScore': 'الحفظ',
    'revisionScore': 'المراجعة',
    'progressScore': 'التقدم الشهري',
    'matnScore': 'المتون',
    'dressScore': 'اللباس',
    'prayerAdvisoryScore': 'الصلاة (استرشادي)',
    'parentHomeAdvisoryScore': 'متابعة البيت (استرشادي)',
  };

  @override
  void initState() {
    super.initState();
    if (widget.saved != null) _fill(widget.saved!);
  }

  String _fmt(double? v) => v == null ? '' : formatScore(v);

  void _fill(PeriodEvaluationDraft d) {
    _c['attendanceScore']!.text = _fmt(d.attendanceScore);
    _c['hifzScore']!.text = _fmt(d.hifzScore);
    _c['revisionScore']!.text = _fmt(d.revisionScore);
    _c['progressScore']!.text = _fmt(d.progressScore);
    _c['matnScore']!.text = _fmt(d.matnScore);
    _c['dressScore']!.text = _fmt(d.dressScore);
    _c['prayerAdvisoryScore']!.text = _fmt(d.prayerAdvisoryScore);
    _c['parentHomeAdvisoryScore']!.text = _fmt(d.parentHomeAdvisoryScore);
    _notes.text = d.sheikhNotes ?? '';
    _includeAdvisory = d.includeAdvisoryInOverall;
  }

  Future<void> _autoDraft() async {
    setState(() => _busy = true);
    final r = await teacherRepo.autoDraftEvaluation(widget.period.id, widget.studentId, widget.studentName);
    if (!mounted) return;
    setState(() => _busy = false);
    r.fold((f) => showSnack(context, f.message, error: true), (d) {
      setState(() {
        final keepNotes = _notes.text;
        _fill(d);
        if (keepNotes.isNotEmpty) _notes.text = keepNotes;
        _draftInfo = 'مسودة تلقائية: ${formatScore(d.overallScore)}% — ${d.gradeLabel} (راجعها قبل الحفظ)';
      });
    });
  }

  Future<void> _save() async {
    double v(String k) => double.tryParse(_c[k]!.text.trim()) ?? 0;
    double? opt(String k) => double.tryParse(_c[k]!.text.trim());
    setState(() => _busy = true);
    final ok = await runAction(
        context,
        () => teacherRepo.savePeriodEvaluation(widget.period.id, {
              'studentId': widget.studentId,
              'attendanceScore': v('attendanceScore'),
              'hifzScore': v('hifzScore'),
              'revisionScore': v('revisionScore'),
              'progressScore': v('progressScore'),
              'matnScore': v('matnScore'),
              'dressScore': v('dressScore'),
              'prayerAdvisoryScore': opt('prayerAdvisoryScore'),
              'parentHomeAdvisoryScore': opt('parentHomeAdvisoryScore'),
              'includeAdvisoryInOverall': _includeAdvisory,
              'sheikhNotes': _notes.text.trim().isEmpty ? null : _notes.text.trim(),
            }));
    if (!mounted) return;
    setState(() => _busy = false);
    if (ok) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.studentName)),
      body: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          OutlinedButton.icon(
            onPressed: _busy ? null : _autoDraft,
            icon: const Icon(Icons.auto_fix_high),
            label: const Text('مسودة تلقائية من سجلات الفترة'),
          ),
          if (_draftInfo != null)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Text(_draftInfo!, style: const TextStyle(color: NoorTheme.info)),
            ),
          const SizedBox(height: 8),
          ..._labels.entries.map((e) => TextField(
                controller: _c[e.key],
                keyboardType: const TextInputType.numberWithOptions(decimal: true),
                decoration: InputDecoration(labelText: '${e.value} (من 100)'),
              )),
          SwitchListTile(
            contentPadding: EdgeInsets.zero,
            title: const Text('احتساب الصلاة ومتابعة البيت في الدرجة الكلية'),
            value: _includeAdvisory,
            onChanged: (v) => setState(() => _includeAdvisory = v),
          ),
          TextField(controller: _notes, maxLines: 3, decoration: const InputDecoration(labelText: 'ملاحظات الشيخ')),
          const SizedBox(height: 24),
          FilledButton.icon(
            onPressed: _busy ? null : _save,
            icon: const Icon(Icons.save),
            label: const Text('حفظ التقييم'),
          ),
        ],
      ),
    );
  }
}

// ───────────────────────── اللباس ─────────────────────────
class _DressTab extends StatefulWidget {
  const _DressTab();

  @override
  State<_DressTab> createState() => _DressTabState();
}

class _DressRow {
  bool compliant = true;
  final score = TextEditingController();
  final note = TextEditingController();
}

class _DressTabState extends State<_DressTab> with AutomaticKeepAliveClientMixin {
  int? _circleId;
  DateTime _date = DateTime.now();
  List<CircleStudentModel> _students = const [];
  final Map<int, _DressRow> _rows = {};
  bool _loading = false;
  bool _saving = false;

  @override
  bool get wantKeepAlive => true;

  Future<void> _load() async {
    final cid = _circleId;
    if (cid == null) return;
    setState(() => _loading = true);
    final students = await teacherRepo.getCircleStudents(cid);
    final existing = await teacherRepo.getDress(Formatters.ymd(_date));
    if (!mounted) return;
    setState(() {
      _loading = false;
      _rows.clear();
      _students = students.getOrElse(() => const []);
      final byStudent = {for (final d in existing.getOrElse(() => const [])) d.studentId: d};
      for (final s in _students) {
        final row = _DressRow();
        final e = byStudent[s.id];
        if (e != null) {
          row.compliant = e.isCompliant;
          row.score.text = e.scoreOutOf10 == null ? '' : formatScore(e.scoreOutOf10!);
          row.note.text = e.note ?? '';
        }
        _rows[s.id] = row;
      }
    });
    students.fold((f) => showSnack(context, f.message, error: true), (_) {});
  }

  Future<void> _save() async {
    if (_students.isEmpty) return;
    setState(() => _saving = true);
    final records = _students.map((s) {
      final r = _rows[s.id]!;
      return {
        'studentId': s.id,
        'isCompliant': r.compliant,
        'scoreOutOf10': double.tryParse(r.score.text.trim()),
        'note': r.note.text.trim().isEmpty ? null : r.note.text.trim(),
      };
    }).toList();
    await runAction(context, () => teacherRepo.saveDressBulk(Formatters.ymd(_date), records));
    if (mounted) setState(() => _saving = false);
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    return Scaffold(
      bottomNavigationBar: _students.isEmpty
          ? null
          : SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: FilledButton.icon(
                  onPressed: _saving ? null : _save,
                  icon: const Icon(Icons.save),
                  label: const Text('حفظ سجل اللباس'),
                ),
              ),
            ),
      body: Column(children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: CircleDropdown(
            value: _circleId,
            onChanged: (c) {
              _circleId = c?.id;
              _load();
            },
          ),
        ),
        ListTile(
          leading: const Icon(Icons.calendar_today),
          title: Text(Formatters.ymd(_date)),
          onTap: () async {
            final d = await showDatePicker(
              context: context,
              initialDate: _date,
              firstDate: DateTime.now().subtract(const Duration(days: 365)),
              lastDate: DateTime.now(),
            );
            if (d != null) {
              _date = d;
              _load();
            }
          },
        ),
        Expanded(
          child: AsyncBody(
            isLoading: _loading,
            isEmpty: _circleId == null || _students.isEmpty,
            emptyTitle: _circleId == null ? 'اختر حلقة' : 'لا يوجد طلاب في هذه الحلقة',
            child: ListView.builder(
              itemCount: _students.length,
              itemBuilder: (context, i) {
                final s = _students[i];
                final r = _rows[s.id]!;
                return Card(
                  margin: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                  child: Padding(
                    padding: const EdgeInsets.all(8),
                    child: Column(children: [
                      SwitchListTile(
                        contentPadding: EdgeInsets.zero,
                        title: Text(s.fullName),
                        subtitle: Text(r.compliant ? 'ملتزم باللباس' : 'غير ملتزم'),
                        value: r.compliant,
                        onChanged: (v) => setState(() => r.compliant = v),
                      ),
                      Row(children: [
                        SizedBox(
                          width: 110,
                          child: TextField(
                            controller: r.score,
                            keyboardType: const TextInputType.numberWithOptions(decimal: true),
                            decoration: const InputDecoration(labelText: 'الدرجة /10'),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: TextField(
                            controller: r.note,
                            decoration: const InputDecoration(labelText: 'ملاحظة'),
                          ),
                        ),
                      ]),
                    ]),
                  ),
                );
              },
            ),
          ),
        ),
      ]),
    );
  }
}

// ───────────────────────── الصلاة ─────────────────────────
class _PrayerTab extends StatefulWidget {
  final int studentId;
  const _PrayerTab({required this.studentId});

  @override
  State<_PrayerTab> createState() => _PrayerTabState();
}

class _PrayerTabState extends State<_PrayerTab> {
  int _key = 0;

  Future<void> _override(PrayerLogModel p) async {
    bool mosque = p.prayedInMosque;
    bool onTime = p.onTime;
    final count = TextEditingController(text: '${p.mosquePrayerCount}');
    final note = TextEditingController(text: p.sheikhOverrideNote);
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setD) => AlertDialog(
          title: Text('تعديل صلاة ${Formatters.displayDate(p.date)}'),
          content: Column(mainAxisSize: MainAxisSize.min, children: [
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('صلّى في المسجد'),
              value: mosque,
              onChanged: (v) => setD(() => mosque = v),
            ),
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('في الوقت'),
              value: onTime,
              onChanged: (v) => setD(() => onTime = v),
            ),
            TextField(
              controller: count,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'عدد الصلوات في المسجد (0–5)'),
            ),
            TextField(controller: note, decoration: const InputDecoration(labelText: 'ملاحظة الشيخ')),
          ]),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
            FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('حفظ')),
          ],
        ),
      ),
    );
    if (ok != true || !mounted) return;
    final saved = await runAction(
        context,
        () => teacherRepo.overridePrayer(p.id, {
              'prayedInMosque': mosque,
              'onTime': onTime,
              'mosquePrayerCount': int.tryParse(count.text.trim()),
              'sheikhOverrideNote': note.text.trim().isEmpty ? null : note.text.trim(),
            }));
    if (saved) setState(() => _key++);
  }

  @override
  Widget build(BuildContext context) {
    return LoadView<List<PrayerLogModel>>(
      key: ValueKey(_key),
      loader: () => _portal.getPrayerByStudent(widget.studentId),
      isEmpty: (l) => l.isEmpty,
      emptyTitle: 'لا توجد سجلات صلاة',
      builder: (context, list, _) => ListView.builder(
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: list.length,
        itemBuilder: (context, i) {
          final p = list[i];
          return ListTile(
            leading: Icon(p.prayedInMosque ? Icons.mosque : Icons.home_outlined,
                color: p.prayedInMosque ? NoorTheme.success : NoorTheme.textSecondary),
            title: Text(Formatters.displayDate(p.date)),
            subtitle: Text([
              p.prayedInMosque ? 'في المسجد (${p.mosquePrayerCount})' : 'ليس في المسجد',
              p.onTime ? 'في الوقت' : 'متأخر',
              if ((p.studentNote ?? '').isNotEmpty) 'الطالب: ${p.studentNote}',
              if ((p.sheikhOverrideNote ?? '').isNotEmpty) 'الشيخ: ${p.sheikhOverrideNote}',
            ].join(' · ')),
            trailing: const Icon(Icons.edit_outlined),
            onTap: () => _override(p),
          );
        },
      ),
    );
  }
}
