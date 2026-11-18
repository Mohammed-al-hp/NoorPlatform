import '../../../core/quran/quran_text.dart';
import '../../widgets/verse_picker.dart';
import '../shared/mushaf_viewer_screen.dart';
import 'dart:convert';

import 'package:dartz/dartz.dart' show Either;
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../config/quran_surahs.dart';
import '../../../config/theme.dart';
import '../../../core/errors/failures.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/teacher_models.dart';
import 'teacher_ui.dart';
import '../../../data/repositories/teacher_repository_impl.dart';

/// صفحة "تقدم الحفظ والمراجعة" للمحفّظ: آخر الجلسات + تسجيل جلسة + التقييم الشهري.
class TeacherHifzHubScreen extends StatefulWidget {
  const TeacherHifzHubScreen({super.key});

  @override
  State<TeacherHifzHubScreen> createState() => _TeacherHifzHubScreenState();
}

class _TeacherHifzHubScreenState extends State<TeacherHifzHubScreen> {
  int _key = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('تقدم الحفظ والمراجعة'),
        actions: [
          IconButton(
            tooltip: 'التقييم الشهري للحفظ الجديد',
            icon: const Icon(Icons.workspace_premium_outlined),
            onPressed: () => context.push('/teacher/monthly-ratings'),
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        icon: const Icon(Icons.add),
        label: const Text('تسجيل جلسة'),
        onPressed: () async {
          await context.push('/teacher/hifz/new');
          setState(() => _key++);
        },
      ),
      body: LoadView<List<RecentHifzModel>>(
        key: ValueKey(_key),
        loader: () => teacherRepo.getRecentHifz(count: 30),
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد جلسات مسجلة بعد',
        builder: (context, list, reload) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.only(bottom: 80),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final r = list[i];
            final isMem = r.type == 'Memorization';
            return ListTile(
              leading: Icon(isMem ? Icons.auto_stories : Icons.replay,
                  color: isMem ? NoorTheme.primaryGreen : NoorTheme.info),
              title: Text(r.studentName),
              subtitle: Text(
                  '${isMem ? 'حفظ جديد' : 'مراجعة'} · ${r.surahName} (${r.verses}) · ${Formatters.displayDate(r.date)}'),
              trailing: PopupMenuButton<String>(
                child: Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(r.evaluation.isEmpty ? '—' : r.evaluation),
                ),
                onSelected: (v) async {
                  if (v == 'student') {
                    context.push('/teacher/student/${r.studentId}');
                  } else if (v == 'praise') {
                    await runAction(
                        context,
                        () => teacherRepo.sendHifzPraise({
                              'studentId': r.studentId,
                              'surahName': r.surahName,
                              'verses': r.verses,
                              'evaluation': r.evaluation,
                            }));
                  } else if (v == 'edit') {
                    final changed = await showModalBottomSheet<bool>(
                      context: context,
                      isScrollControlled: true,
                      showDragHandle: true,
                      builder: (_) => _EditHifzSheet(record: r),
                    );
                    if (changed == true) reload();
                  } else if (v == 'delete') {
                    if (!await confirmDialog(context, 'حذف سجل التسميع؟', confirmText: 'حذف')) return;
                    if (!context.mounted) return;
                    if (await runAction(context, () => teacherRepo.deleteHifzRecord(r.id))) reload();
                  }
                },
                itemBuilder: (_) => const [
                  PopupMenuItem(value: 'student', child: Text('ملف الطالب')),
                  PopupMenuItem(value: 'edit', child: Text('تعديل الجلسة')),
                  PopupMenuItem(value: 'praise', child: Text('إرسال رسالة مديح لولي الأمر')),
                  PopupMenuItem(value: 'delete', child: Text('حذف السجل')),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

/// نموذج تعديل جلسة تسميع (تصحيح خطأ إدخال) — الخادم يعيد حساب النقاط حسب التقييم الجديد.
class _EditHifzSheet extends StatefulWidget {
  final RecentHifzModel record;
  const _EditHifzSheet({required this.record});

  @override
  State<_EditHifzSheet> createState() => _EditHifzSheetState();
}

class _EditHifzSheetState extends State<_EditHifzSheet> {
  late String _type = widget.record.type == 'Revision' ? 'Revision' : 'Memorization';
  late final _surah = TextEditingController(text: widget.record.surahName);
  late final _toSurah = TextEditingController(text: widget.record.toSurahName ?? '');
  late final _verses = TextEditingController(text: widget.record.verses);
  late final _notes = TextEditingController(text: widget.record.notes);
  late String _evaluation = widget.record.evaluation;
  bool _saving = false;

  @override
  void dispose() {
    _surah.dispose();
    _toSurah.dispose();
    _verses.dispose();
    _notes.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (_surah.text.trim().isEmpty) {
      showSnack(context, 'اسم السورة مطلوب', error: true);
      return;
    }
    setState(() => _saving = true);
    final ok = await runAction(
      context,
      () => teacherRepo.updateHifzRecord(widget.record.id, {
        'type': _type,
        'surahName': _surah.text.trim(),
        'toSurahName': _toSurah.text.trim().isEmpty ? null : _toSurah.text.trim(),
        'verses': _verses.text.trim(),
        'evaluation': _evaluation,
        'notes': _notes.text.trim(),
      }),
    );
    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    final revision = _type == 'Revision';
    final evals = gradesFor(revision: revision);
    _evaluation = gradeForList(_evaluation, revision: revision);
    return Padding(
      padding: EdgeInsets.fromLTRB(20, 0, 20, MediaQuery.of(context).viewInsets.bottom + 20),
      child: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            Text('تعديل جلسة التسميع', style: Theme.of(context).textTheme.titleLarge),
            Text(widget.record.studentName, style: Theme.of(context).textTheme.bodyMedium),
            const SizedBox(height: 16),
            SegmentedButton<String>(
              segments: const [
                ButtonSegment(value: 'Memorization', label: Text('حفظ جديد'), icon: Icon(Icons.auto_stories_rounded)),
                ButtonSegment(value: 'Revision', label: Text('مراجعة'), icon: Icon(Icons.replay_rounded)),
              ],
              selected: {_type},
              onSelectionChanged: (v) => setState(() => _type = v.first),
            ),
            const SizedBox(height: 12),
            TextField(controller: _surah, decoration: const InputDecoration(labelText: 'السورة *')),
            const SizedBox(height: 12),
            TextField(controller: _toSurah, decoration: const InputDecoration(labelText: 'إلى سورة (اختياري)')),
            const SizedBox(height: 12),
            TextField(
              controller: _verses,
              textDirection: TextDirection.ltr,
              decoration: const InputDecoration(labelText: 'الآيات (مثال: 1-10)'),
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              key: ValueKey('edit-grade-$_type'),
              initialValue: _evaluation,
              decoration: const InputDecoration(labelText: 'التقدير *'),
              items: evals.map(gradeMenuItem).toList(),
              onChanged: (v) => setState(() => _evaluation = v ?? _evaluation),
            ),
            const SizedBox(height: 12),
            TextField(controller: _notes, maxLines: 2, decoration: const InputDecoration(labelText: 'ملاحظة')),
            const SizedBox(height: 20),
            FilledButton.icon(
              onPressed: _saving ? null : _save,
              icon: _saving
                  ? const SizedBox(width: 18, height: 18, child: CircularProgressIndicator(strokeWidth: 2, color: Colors.white))
                  : const Icon(Icons.check_rounded),
              label: const Text('حفظ التعديل'),
            ),
          ],
        ),
      ),
    );
  }
}

enum _SessionKind { memorization, sequential, questions }

class _QuestionRow {
  final label = TextEditingController();
  int hesitation = 0;
  int alert = 0;
  int opening = 0;
  final score = TextEditingController();
}

/// تسجيل جلسة: حفظ جديد / مراجعة تسلسلية / مراجعة بالأسئلة (اختبار شفهي) — مطابق لنموذج الويب.
class HifzSessionFormScreen extends StatefulWidget {
  final int? studentId;
  const HifzSessionFormScreen({super.key, this.studentId});

  @override
  State<HifzSessionFormScreen> createState() => _HifzSessionFormScreenState();
}

class _HifzSessionFormScreenState extends State<HifzSessionFormScreen> {
  TeacherStudentModel? _student;
  late int? _studentId = widget.studentId;
  _SessionKind _kind = _SessionKind.memorization;

  String _fromSurah = kQuranSurahs.first.name;
  String? _toSurah;
  final _fromVerse = TextEditingController(text: '1');
  final _toVerse = TextEditingController(text: '10');
  final _startText = TextEditingController();
  final _endText = TextEditingController();
  String _evaluation = kMemorizationGrades.first;
  final _notes = TextEditingController();

  bool _includeMatn = false;
  String? _matnName;
  final _matnPortion = TextEditingController();
  List<MatnOptionModel> _matns = const [];

  final _scope = TextEditingController();
  final _maxOpenings = TextEditingController(text: '1');
  final List<_QuestionRow> _questions = [_QuestionRow()];

  String? _positionHint;
  bool _saving = false;

  @override
  void initState() {
    super.initState();
    _fillVerseTexts();
    teacherRepo.getMatns().then((r) => r.fold((_) {}, (v) {
          if (mounted) setState(() => _matns = v);
        }));
    teacherRepo.getGradingDefaults().then((r) => r.fold((_) {}, (v) {
          if (mounted && v.oralMaxOpeningsBeforeFail > 0) {
            _maxOpenings.text = '${v.oralMaxOpeningsBeforeFail}';
          }
        }));
    if (_studentId != null) _prefill();
  }

  int _verseCount(String surah) =>
      kQuranSurahs.firstWhere((s) => s.name == surah, orElse: () => kQuranSurahs.first).verseCount;

  int get _fromVerseNo => (int.tryParse(_fromVerse.text) ?? 1).clamp(1, _verseCount(_fromSurah));
  String get _endSurah => _toSurah ?? _fromSurah;
  int get _toVerseNo => (int.tryParse(_toVerse.text) ?? _fromVerseNo).clamp(1, _verseCount(_endSurah));

  /// نص آيتي البداية والنهاية من مصحف قالون المضمّن (دون إنترنت).
  Future<void> _fillVerseTexts() async {
    final start = await QuranText.verse(_fromSurah, _fromVerseNo);
    final end = await QuranText.verse(_endSurah, _toVerseNo);
    if (!mounted) return;
    setState(() {
      _startText.text = start;
      _endText.text = end;
    });
  }

  void _setVerses({int? from, int? to}) {
    if (from != null) _fromVerse.text = '$from';
    if (to != null) _toVerse.text = '$to';
    // في السورة الواحدة: النهاية لا تسبق البداية
    if (_toSurah == null && _toVerseNo < _fromVerseNo) _toVerse.text = '$_fromVerseNo';
    setState(() {});
    _fillVerseTexts();
  }

  /// اقتراح نقطة البداية من آخر موضع (GET /api/hifz/last-position).
  Future<void> _prefill() async {
    final id = _studentId;
    if (id == null || _kind == _SessionKind.questions) return;
    final r = await teacherRepo.getLastPosition(id, sequential: _kind == _SessionKind.sequential);
    if (!mounted) return;
    r.fold((_) {}, (p) {
      if (!p.hasPrevious || p.surahName == null) {
        setState(() => _positionHint = 'لا توجد جلسات سابقة لهذا النوع');
        return;
      }
      final next = p.nextVerse;
      final max = _verseCount(p.surahName!);
      setState(() {
        _fromSurah = p.surahName!;
        _fromVerse.text = '$next';
        _toVerse.text = '${(next + 9).clamp(next, max)}';
        _positionHint =
            'آخر جلسة: ${p.lastSurahName} حتى الآية ${p.lastVerse} — المقترح: ${p.surahName} من الآية $next';
      });
      _fillVerseTexts();
    });
  }

  List<String> _surahRange(String from, String to) {
    final a = kQuranSurahs.indexWhere((s) => s.name == from);
    final b = kQuranSurahs.indexWhere((s) => s.name == to);
    if (a < 0 || b < 0) return [from];
    final lo = a < b ? a : b;
    final hi = a < b ? b : a;
    return kQuranSurahs.sublist(lo, hi + 1).map((s) => s.name).toList();
  }

  Future<void> _save() async {
    final id = _studentId;
    if (id == null) {
      showSnack(context, 'يرجى اختيار الطالب', error: true);
      return;
    }
    final notes = _notes.text.trim();
    setState(() => _saving = true);

    late final Future<Either<Failure, String>> Function() action;
    if (_kind == _SessionKind.questions) {
      final questions = _questions
          .where((q) => q.label.text.trim().isNotEmpty || q.score.text.trim().isNotEmpty)
          .map((q) => {
                'label': q.label.text.trim(),
                'hesitationCount': q.hesitation,
                'alertCount': q.alert,
                'openingCount': q.opening,
                'scorePercent': double.tryParse(q.score.text.trim()),
              })
          .toList();
      if (questions.isEmpty) {
        setState(() => _saving = false);
        showSnack(context, 'أضف سؤال مراجعة واحد على الأقل', error: true);
        return;
      }
      action = () => teacherRepo.createOralExam({
            'studentId': id,
            'kind': 'AthmanSampling',
            'scopeLabel': _scope.text.trim(),
            'maxOpeningsBeforeFail': int.tryParse(_maxOpenings.text.trim()) ?? 1,
            'circleId': _student?.circleId,
            'notes': notes.isEmpty ? null : notes,
            'questions': questions,
          });
    } else {
      final body = <String, dynamic>{
        'studentId': id,
        'type': _kind == _SessionKind.memorization ? 'Memorization' : 'Revision',
        'evaluation': gradeForList(_evaluation, revision: _kind != _SessionKind.memorization),
        'notes': notes,
        'date': libyaNowIso(),
        'surahName': _fromSurah,
        'verses': '$_fromVerseNo-$_toVerseNo',
        'startVerseText': _startText.text.trim(),
        'endVerseText': _endText.text.trim(),
      };
      if (_kind == _SessionKind.memorization) {
        body['toSurahName'] = _toSurah;
      } else {
        final to = _toSurah ?? _fromSurah;
        final range = _surahRange(_fromSurah, to);
        body['revisionMode'] = 'Sequential';
        body['toSurahName'] = to;
        body['sessionDetailsJson'] = jsonEncode({'surahs': range});
        body['notes'] = '${notes.isEmpty ? '' : '$notes\n'}سور المراجعة: ${range.join(' -> ')}';
      }
      action = () async {
        final r = await teacherRepo.addHifzRecord(body);
        // تسجيل متن اختياري بنفس الجلسة (حفظ جديد فقط) — كما في الويب
        // الجلسة المحفوظة دون اتصال: المتن يُسجَّل لاحقاً يدوياً (لا يُضاف للطابور)
        final savedOffline = r.fold((_) => false, (m) => m == TeacherRepositoryImpl.offlineSavedMessage);
        if (r.isRight() && !savedOffline && _kind == _SessionKind.memorization && _includeMatn && _matnName != null) {
          final m = await teacherRepo.addMatnRecord({
            'studentId': id,
            'date': body['date'],
            'matnName': _matnName,
            'portion': _matnPortion.text.trim(),
            'type': 'Memorization',
            'evaluation': _evaluation,
            'notes': null,
          });
          if (m.isLeft() && mounted) showSnack(context, 'تم حفظ التسميع، لكن تعذر حفظ سجل المتن', error: true);
        }
        return r;
      };
    }

    final ok = await runAction(context, action);
    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) Navigator.of(context).pop(true);
  }

  Widget _surahDropdown(String label, String? value, ValueChanged<String?> onChanged, {bool optional = false}) =>
      DropdownButtonFormField<String?>(
        initialValue: value,
        isExpanded: true,
        decoration: InputDecoration(labelText: label),
        items: [
          if (optional) const DropdownMenuItem<String?>(value: null, child: Text('— نفس السورة —')),
          ...kQuranSurahs.map((s) => DropdownMenuItem<String?>(
              value: s.name, child: Text('${s.number}. ${s.name} (${s.verseCount})'))),
        ],
        onChanged: onChanged,
      );

  Widget _counter(String label, int value, ValueChanged<int> onChanged) => Column(
        children: [
          Text(label, style: const TextStyle(fontSize: 11)),
          Row(mainAxisSize: MainAxisSize.min, children: [
            IconButton(
              visualDensity: VisualDensity.compact,
              icon: const Icon(Icons.remove_circle_outline, size: 20),
              onPressed: value > 0 ? () => onChanged(value - 1) : null,
            ),
            Text('$value'),
            IconButton(
              visualDensity: VisualDensity.compact,
              icon: const Icon(Icons.add_circle_outline, size: 20),
              onPressed: () => onChanged(value + 1),
            ),
          ]),
        ],
      );

  @override
  Widget build(BuildContext context) {
    final isQuestions = _kind == _SessionKind.questions;
    return Scaffold(
      appBar: AppBar(title: const Text('تسجيل جلسة تسميع')),
      body: ListView(
        padding: const EdgeInsets.all(NoorTheme.spacingMd),
        children: [
          StudentDropdown(
            value: _studentId,
            onChanged: (s) {
              setState(() {
                _student = s;
                _studentId = s?.id;
              });
              _prefill();
            },
          ),
          const SizedBox(height: 12),
          SegmentedButton<_SessionKind>(
            segments: const [
              ButtonSegment(value: _SessionKind.memorization, label: Text('حفظ جديد')),
              ButtonSegment(value: _SessionKind.sequential, label: Text('مراجعة تسلسلية')),
              ButtonSegment(value: _SessionKind.questions, label: Text('مراجعة بالأسئلة')),
            ],
            selected: {_kind},
            onSelectionChanged: (v) {
              setState(() => _kind = v.first);
              _prefill();
            },
          ),
          if (_positionHint != null && !isQuestions)
            Padding(
              padding: const EdgeInsets.only(top: 8),
              child: Text(_positionHint!, style: const TextStyle(fontSize: 12, color: NoorTheme.info)),
            ),
          const SizedBox(height: 12),
          if (!isQuestions) ...[
            _surahDropdown(_kind == _SessionKind.sequential ? 'من سورة' : 'السورة', _fromSurah, (v) {
              setState(() => _fromSurah = v ?? _fromSurah);
              _setVerses(from: 1, to: _toSurah == null ? 1 : null);
            }),
            _surahDropdown(_kind == _SessionKind.sequential ? 'إلى سورة' : 'إلى سورة', _toSurah, (v) {
              setState(() => _toSurah = v);
              _setVerses(to: v == null ? _fromVerseNo : 1);
            }, optional: true),
            const SizedBox(height: 8),
            // اختيار الآية بنصّها (رقمها وأول كلماتها) — دون حفظ الأرقام ودون إنترنت
            VersePickerField(
              label: 'من الآية',
              surahName: _fromSurah,
              verse: _fromVerseNo,
              onChanged: (n) => _setVerses(from: n),
            ),
            const SizedBox(height: 8),
            VersePickerField(
              label: 'إلى الآية (يبدأ ما بعدها في الجلسة القادمة)',
              surahName: _endSurah,
              verse: _toVerseNo,
              onChanged: (n) => _setVerses(to: n),
            ),
            Align(
              alignment: AlignmentDirectional.centerEnd,
              child: TextButton.icon(
                onPressed: () => MushafViewerScreen.open(
                  context,
                  surah: QuranText.surahNumber(_fromSurah),
                  verse: _fromVerseNo,
                ),
                icon: const Icon(Icons.menu_book_rounded),
                label: const Text('عرض في المصحف'),
              ),
            ),
            TextField(
              controller: _startText,
              style: const TextStyle(fontFamily: QuranText.fontFamily, fontSize: 18),
              decoration: const InputDecoration(labelText: 'نص آية البداية'),
            ),
            TextField(
              controller: _endText,
              style: const TextStyle(fontFamily: QuranText.fontFamily, fontSize: 18),
              decoration: const InputDecoration(labelText: 'نص آية النهاية'),
            ),
            DropdownButtonFormField<String>(
              key: ValueKey('grade-${_kind.name}'),
              initialValue: gradeForList(_evaluation, revision: _kind != _SessionKind.memorization),
              decoration: const InputDecoration(labelText: 'التقدير'),
              items: gradesFor(revision: _kind != _SessionKind.memorization).map(gradeMenuItem).toList(),
              onChanged: (v) => setState(() => _evaluation = v ?? _evaluation),
            ),
          ],
          if (_kind == _SessionKind.memorization) ...[
            SwitchListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('تسجيل متن مع الجلسة'),
              value: _includeMatn,
              onChanged: (v) => setState(() => _includeMatn = v),
            ),
            if (_includeMatn) ...[
              DropdownButtonFormField<String>(
                initialValue: _matnName,
                decoration: const InputDecoration(labelText: 'المتن'),
                items: _matns.map((m) => DropdownMenuItem(value: m.name, child: Text(m.name))).toList(),
                onChanged: (v) => setState(() => _matnName = v),
              ),
              TextField(controller: _matnPortion, decoration: const InputDecoration(labelText: 'المقدار المحفوظ')),
            ],
          ],
          if (isQuestions) ...[
            TextField(controller: _scope, decoration: const InputDecoration(labelText: 'النطاق / الوصف')),
            TextField(
              controller: _maxOpenings,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'أقصى فتحات مسموحة لكل سؤال'),
            ),
            const SizedBox(height: 12),
            ..._questions.asMap().entries.map((e) {
              final q = e.value;
              return Card(
                child: Padding(
                  padding: const EdgeInsets.all(8),
                  child: Column(children: [
                    Row(children: [
                      Expanded(
                        child: TextField(
                          controller: q.label,
                          decoration: InputDecoration(labelText: 'السؤال ${e.key + 1}'),
                        ),
                      ),
                      if (_questions.length > 1)
                        IconButton(
                          icon: const Icon(Icons.delete_outline),
                          onPressed: () => setState(() => _questions.removeAt(e.key)),
                        ),
                    ]),
                    Wrap(alignment: WrapAlignment.spaceAround, children: [
                      _counter('تردد', q.hesitation, (v) => setState(() => q.hesitation = v)),
                      _counter('تنبيه', q.alert, (v) => setState(() => q.alert = v)),
                      _counter('فتح', q.opening, (v) => setState(() => q.opening = v)),
                    ]),
                    TextField(
                      controller: q.score,
                      keyboardType: TextInputType.number,
                      decoration: const InputDecoration(labelText: 'الدرجة % (اختياري — تُحسب تلقائياً)'),
                    ),
                  ]),
                ),
              );
            }),
            TextButton.icon(
              onPressed: () => setState(() => _questions.add(_QuestionRow())),
              icon: const Icon(Icons.add),
              label: const Text('إضافة سؤال'),
            ),
          ],
          TextField(controller: _notes, maxLines: 2, decoration: const InputDecoration(labelText: 'ملاحظات')),
          const SizedBox(height: 24),
          FilledButton.icon(
            onPressed: _saving ? null : _save,
            icon: const Icon(Icons.save),
            label: Text(_saving ? 'جاري الحفظ...' : 'حفظ الجلسة'),
          ),
        ],
      ),
    );
  }
}

/// تقرير التقييم الشهري للحفظ الجديد مع اختيار الشهر والحلقة (طلاب المحفّظ فقط).
class MonthlyRatingsReportScreen extends StatefulWidget {
  const MonthlyRatingsReportScreen({super.key});

  @override
  State<MonthlyRatingsReportScreen> createState() => _MonthlyRatingsReportScreenState();
}

class _MonthlyRatingsReportScreenState extends State<MonthlyRatingsReportScreen> {
  DateTime _month = DateTime(DateTime.now().year, DateTime.now().month);
  int? _circleId;

  void _shift(int delta) => setState(() => _month = DateTime(_month.year, _month.month + delta));

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('التقييم الشهري للحفظ الجديد')),
      body: Column(
        children: [
          Row(children: [
            IconButton(icon: const Icon(Icons.chevron_right), onPressed: () => _shift(-1)),
            Expanded(
              child: Text('${_month.month}/${_month.year}',
                  textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
            ),
            IconButton(
              icon: const Icon(Icons.chevron_left),
              onPressed: DateTime(_month.year, _month.month + 1).isAfter(DateTime.now()) ? null : () => _shift(1),
            ),
          ]),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: CircleDropdown(
              value: _circleId,
              allowNone: true,
              noneLabel: 'كل الحلقات',
              onChanged: (c) => setState(() => _circleId = c?.id),
            ),
          ),
          Expanded(
            child: LoadView<MonthlyRatingsReportModel>(
              key: ValueKey('${_month.year}-${_month.month}-$_circleId'),
              loader: () =>
                  teacherRepo.getMonthlyRatings(year: _month.year, month: _month.month, circleId: _circleId),
              isEmpty: (d) => d.items.isEmpty,
              emptyTitle: 'لا يوجد طلاب',
              builder: (context, d, _) => ListView(
                physics: const AlwaysScrollableScrollPhysics(),
                children: [
                  Padding(
                    padding: const EdgeInsets.all(12),
                    child: Text('التقييمات الناجحة: ${d.passingEvaluations.join('، ')}',
                        style: const TextStyle(fontSize: 12, color: NoorTheme.textSecondary)),
                  ),
                  ...d.items.map((i) {
                    final r = i.rating;
                    return ListTile(
                      title: Text(i.fullName),
                      subtitle: Text(r == null
                          ? i.circleName
                          : '${i.circleName} · العمر ${r.ageYears ?? '—'} · ${r.successfulSessions}/${r.target} ناجحة من ${r.totalMemorizationSessions}${r.warning != null ? ' ⚠' : ''}'),
                      trailing: Text(
                        r == null ? '—' : '${formatScore(r.score)}/10',
                        style: TextStyle(
                            fontWeight: FontWeight.w800,
                            color: r == null ? null : scoreColor(r.score)),
                      ),
                      onTap: () => context.push('/teacher/student/${i.studentId}'),
                    );
                  }),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }
}
