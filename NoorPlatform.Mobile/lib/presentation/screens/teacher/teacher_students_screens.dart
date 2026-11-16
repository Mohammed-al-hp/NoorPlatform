import 'package:dartz/dartz.dart' hide State;
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/errors/failures.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/teacher_models.dart';
import '../../../core/utils/file_open_helper.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../widgets/async_body.dart';
import '../../widgets/noor_widgets.dart';
import 'teacher_ui.dart';

const _relationships = <String, String>{
  'Father': 'أب',
  'Mother': 'أم',
  'Uncle': 'عم',
  'Brother': 'أخ',
  'MaternalUncle': 'خال',
  'Other': 'أخرى',
};

const _waitingStatuses = <String, String>{
  'Pending': 'قيد الانتظار',
  'Contacted': 'تم التواصل',
  'Rejected': 'مرفوض',
};

final _libyanPhone = RegExp(r'^09\d{8}$');

String? _validatePhone(String? v, {required bool required}) {
  final s = (v ?? '').trim();
  if (s.isEmpty) return required ? 'رقم الهاتف مطلوب' : null;
  if (!_libyanPhone.hasMatch(s)) return 'رقم غير صالح: يبدأ بـ 09 ويتكون من 10 أرقام';
  return null;
}

/// إدارة الطلاب: قائمة طلاب المحفّظ + قائمة الانتظار (إضافة/تعديل/حذف — التحويل من صلاحيات الإدارة).
class TeacherStudentsScreen extends StatefulWidget {
  const TeacherStudentsScreen({super.key});

  @override
  State<TeacherStudentsScreen> createState() => _TeacherStudentsScreenState();
}

class _TeacherStudentsScreenState extends State<TeacherStudentsScreen> {
  String _query = '';
  int _reloadKey = 0;

  void _reload() => setState(() => _reloadKey++);

  @override
  Widget build(BuildContext context) {
    return DefaultTabController(
      length: 2,
      child: Scaffold(
        appBar: AppBar(
          title: const Text('إدارة الطلاب'),
          bottom: const TabBar(tabs: [Tab(text: 'طلابي'), Tab(text: 'قائمة الانتظار')]),
        ),
        floatingActionButton: Builder(
          builder: (ctx) => FloatingActionButton.extended(
            icon: const Icon(Icons.add),
            label: const Text('إضافة'),
            onPressed: () async {
              final tab = DefaultTabController.of(ctx).index;
              final Future<bool?> pending = tab == 0
                  ? Navigator.of(context).push<bool>(
                      MaterialPageRoute(builder: (_) => const StudentFormScreen()))
                  : showWaitingEntryForm(ctx);
              final changed = await pending;
              if (changed == true) _reload();
            },
          ),
        ),
        body: TabBarView(
          children: [
            Column(
              children: [
                Padding(
                  padding: const EdgeInsets.all(12),
                  child: TextField(
                    decoration: const InputDecoration(
                      hintText: 'بحث بالاسم أو الحلقة...',
                      prefixIcon: Icon(Icons.search),
                    ),
                    onChanged: (v) => setState(() => _query = v.trim()),
                  ),
                ),
                Expanded(
                  child: LoadView<List<TeacherStudentModel>>(
                    key: ValueKey('students-$_reloadKey'),
                    loader: teacherRepo.getStudents,
                    isEmpty: (l) => l.isEmpty,
                    emptyTitle: 'لا يوجد طلاب في حلقاتك',
                    builder: (context, list, reload) {
                      final filtered = _query.isEmpty
                          ? list
                          : list
                              .where((s) => s.fullName.contains(_query) || s.circleName.contains(_query))
                              .toList();
                      return ListView.builder(
                        physics: const AlwaysScrollableScrollPhysics(),
                        itemCount: filtered.length,
                        itemBuilder: (context, i) {
                          final s = filtered[i];
                          return ListTile(
                            leading: CircleAvatar(
                              backgroundColor: NoorTheme.primaryGreen.withValues(alpha: 0.12),
                              child: Text(s.fullName.isEmpty ? '؟' : s.fullName.substring(0, 1)),
                            ),
                            title: Text(s.fullName),
                            subtitle: Text(
                                '${s.circleName} · ${s.level} · حضور ${s.attendance}% · حفظ ${Formatters.number(s.progress, fractionDigits: 1)}%'),
                            trailing: const Icon(Icons.chevron_left),
                            onTap: () async {
                              await context.push('/teacher/student/${s.id}');
                              reload();
                            },
                          );
                        },
                      );
                    },
                  ),
                ),
              ],
            ),
            LoadView<List<WaitingEntryModel>>(
              key: ValueKey('waiting-$_reloadKey'),
              loader: teacherRepo.getWaitingList,
              isEmpty: (l) => l.isEmpty,
              emptyTitle: 'قائمة الانتظار فارغة',
              builder: (context, list, reload) => ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                itemCount: list.length,
                itemBuilder: (context, i) {
                  final w = list[i];
                  return ListTile(
                    leading: const Icon(Icons.hourglass_top, color: NoorTheme.warning),
                    title: Text(w.fullName),
                    subtitle: Text([
                      if (w.displayPhone.isNotEmpty) 'هاتف: ${w.displayPhone}',
                      if (w.parentName.isNotEmpty) 'ولي الأمر: ${w.parentName}',
                      _waitingStatuses[w.status] ?? w.status,
                    ].join(' · ')),
                    trailing: PopupMenuButton<String>(
                      onSelected: (v) async {
                        if (v == 'edit') {
                          if (await showWaitingEntryForm(context, entry: w) == true) reload();
                        } else if (v == 'delete') {
                          if (!await confirmDialog(context, 'حذف هذا السجل من قائمة الانتظار؟',
                              confirmText: 'حذف')) {
                            return;
                          }
                          if (!context.mounted) return;
                          if (await runAction(context, () => teacherRepo.deleteWaitingEntry(w.id))) {
                            reload();
                          }
                        }
                      },
                      itemBuilder: (_) => const [
                        PopupMenuItem(value: 'edit', child: Text('تعديل')),
                        PopupMenuItem(value: 'delete', child: Text('حذف')),
                      ],
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// نموذج إضافة/تعديل سجل قائمة الانتظار — هاتف الطالب اختياري، هاتف ولي الأمر إلزامي.
Future<bool?> showWaitingEntryForm(BuildContext context, {WaitingEntryModel? entry}) {
  return showModalBottomSheet<bool>(
    context: context,
    isScrollControlled: true,
    builder: (_) => Padding(
      padding: EdgeInsets.only(bottom: MediaQuery.of(context).viewInsets.bottom),
      child: _WaitingEntryForm(entry: entry),
    ),
  );
}

class _WaitingEntryForm extends StatefulWidget {
  final WaitingEntryModel? entry;
  const _WaitingEntryForm({this.entry});

  @override
  State<_WaitingEntryForm> createState() => _WaitingEntryFormState();
}

class _WaitingEntryFormState extends State<_WaitingEntryForm> {
  final _form = GlobalKey<FormState>();
  late final _name = TextEditingController(text: widget.entry?.fullName);
  late final _phone = TextEditingController(text: widget.entry?.displayPhone);
  late final _age = TextEditingController(text: widget.entry?.age?.toString());
  late final _parentName = TextEditingController(text: widget.entry?.parentName);
  late final _parentPhone = TextEditingController(text: widget.entry?.displayParentPhone);
  late final _time = TextEditingController(text: widget.entry?.preferredTime);
  late final _notes = TextEditingController(text: widget.entry?.notes);
  late String _level = widget.entry?.requestedLevel ?? 'مبتدئ';
  late String _status = widget.entry?.status ?? 'Pending';
  bool _saving = false;

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    setState(() => _saving = true);
    final body = {
      'fullName': _name.text.trim(),
      'phone': _phone.text.trim(),
      'parentName': _parentName.text.trim(),
      'parentPhone': _parentPhone.text.trim(),
      'age': int.tryParse(_age.text.trim()),
      'requestedLevel': _level,
      'preferredTime': _time.text.trim(),
      'notes': _notes.text.trim(),
      if (widget.entry != null) 'status': _status,
    };
    final ok = await runAction(context, () => teacherRepo.saveWaitingEntry(widget.entry?.id, body));
    if (!mounted) return;
    setState(() => _saving = false);
    if (ok) Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(NoorTheme.spacingMd),
      child: Form(
        key: _form,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(widget.entry == null ? 'إضافة لقائمة الانتظار' : 'تعديل قائمة الانتظار',
                style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            TextFormField(
              controller: _name,
              decoration: const InputDecoration(labelText: 'اسم الطالب *'),
              validator: (v) => (v ?? '').trim().length < 2 ? 'اسم الطالب مطلوب' : null,
            ),
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(labelText: 'هاتف الطالب (اختياري)', hintText: '09xxxxxxxx'),
              validator: (v) => _validatePhone(v, required: false),
            ),
            TextFormField(
              controller: _age,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'العمر'),
            ),
            TextFormField(controller: _parentName, decoration: const InputDecoration(labelText: 'اسم ولي الأمر')),
            TextFormField(
              controller: _parentPhone,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(labelText: 'هاتف ولي الأمر *', hintText: '09xxxxxxxx'),
              validator: (v) => widget.entry != null && (v ?? '').trim().isEmpty
                  ? null
                  : _validatePhone(v, required: true),
            ),
            DropdownButtonFormField<String>(
              initialValue: kLevels.contains(_level) ? _level : 'مبتدئ',
              decoration: const InputDecoration(labelText: 'المستوى المطلوب'),
              items: kLevels.map((l) => DropdownMenuItem(value: l, child: Text(l))).toList(),
              onChanged: (v) => _level = v ?? _level,
            ),
            TextFormField(controller: _time, decoration: const InputDecoration(labelText: 'الوقت المفضل')),
            if (widget.entry != null)
              DropdownButtonFormField<String>(
                initialValue: _waitingStatuses.containsKey(_status) ? _status : 'Pending',
                decoration: const InputDecoration(labelText: 'الحالة'),
                items: _waitingStatuses.entries
                    .map((e) => DropdownMenuItem(value: e.key, child: Text(e.value)))
                    .toList(),
                onChanged: (v) => _status = v ?? _status,
              ),
            TextFormField(controller: _notes, maxLines: 2, decoration: const InputDecoration(labelText: 'ملاحظات')),
            const SizedBox(height: 16),
            FilledButton(
              onPressed: _saving ? null : _save,
              child: _saving ? const CircularProgressIndicator() : const Text('حفظ'),
            ),
          ],
        ),
      ),
    );
  }
}

/// إضافة طالب أو تعديله — نفس حقول وتحقق نموذج الويب.
class StudentFormScreen extends StatefulWidget {
  final StudentDetailsModel? student;
  const StudentFormScreen({super.key, this.student});

  @override
  State<StudentFormScreen> createState() => _StudentFormScreenState();
}

class _StudentFormScreenState extends State<StudentFormScreen> {
  final _form = GlobalKey<FormState>();
  late final _s = widget.student;
  late final _name = TextEditingController(text: _s?.fullName);
  late final _phone = TextEditingController(text: _display(_s?.studentPhone));
  late final _residence = TextEditingController(text: _s?.residence);
  late final _guardianName = TextEditingController(text: _s?.parentName == '—' ? '' : _s?.parentName);
  late final _parentPhone = TextEditingController(text: _display(_s?.parentPhone));
  late String _relationship = _s?.guardianRelationship ?? 'Father';
  late String _level = _s?.level.isNotEmpty == true ? _s!.level : 'مبتدئ';
  late int? _circleId = _s?.circleId;
  late DateTime? _dob = DateTime.tryParse(_s?.dateOfBirth ?? '');
  late DateTime _registration = DateTime.tryParse(_s?.registrationDate ?? '') ?? DateTime.now();
  int? _existingParentId;
  bool _saving = false;

  bool get _isEdit => _s != null;

  static String _display(String? phone) {
    final p = (phone ?? '').replaceAll(RegExp(r'\D'), '');
    if (p.startsWith('218') && p.length >= 12) return '0${p.substring(3)}';
    return p;
  }

  Future<void> _pickDate(bool dob) async {
    final initial = dob ? (_dob ?? DateTime(DateTime.now().year - 8)) : _registration;
    final d = await showDatePicker(
      context: context,
      initialDate: initial,
      firstDate: DateTime(1950),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (d != null) setState(() => dob ? _dob = d : _registration = d);
  }

  Map<String, dynamic> _body() => {
        'fullName': _name.text.trim(),
        'dateOfBirth': _dob == null ? null : Formatters.ymd(_dob),
        'registrationDate': Formatters.ymd(_registration),
        'residence': _residence.text.trim().isEmpty ? null : _residence.text.trim(),
        'level': _level,
        'circleId': _circleId,
        if (_isEdit) ...{
          'studentPhone': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
          'guardianName': _guardianName.text.trim(),
          'parentPhone': _parentPhone.text.trim(),
          'guardianRelationship': _relationship,
        } else ...{
          'phone': _phone.text.trim().isEmpty ? null : _phone.text.trim(),
          'parentId': _existingParentId,
          'parentPhone': _existingParentId != null ? null : _parentPhone.text.trim(),
          'guardianName': _existingParentId != null ? null : _guardianName.text.trim(),
          'guardianRelationship': _existingParentId != null ? null : _relationship,
        },
      };

  Future<void> _save() async {
    if (!_form.currentState!.validate()) return;
    if (_dob == null) {
      showSnack(context, 'تاريخ الميلاد مطلوب', error: true);
      return;
    }
    setState(() => _saving = true);
    if (_isEdit) {
      final ok = await runAction(context, () => teacherRepo.updateStudent(_s!.id, _body()));
      if (!mounted) return;
      setState(() => _saving = false);
      if (ok) Navigator.pop(context, true);
      return;
    }

    final Either<Failure, CreateStudentResult> result = await teacherRepo.createStudent(_body());
    if (!mounted) return;
    setState(() => _saving = false);
    result.fold(
      (f) {
        if (f is ArchivedStudentFailure) {
          // المحفّظ لا يملك صلاحية الاستعادة — نفس سلوك الويب
          showDialog<void>(
            context: context,
            builder: (ctx) => AlertDialog(
              title: const Text('طالب سابق'),
              content: Text('${f.message}\n\nالاسم: ${f.archivedName}'),
              actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('حسناً'))],
            ),
          );
        } else {
          showSnack(context, f.message, error: true);
        }
      },
      (r) async {
        await _showCredentials(r);
        if (mounted) Navigator.pop(context, true);
      },
    );
  }

  Future<void> _showCredentials(CreateStudentResult r) {
    Widget block(String title, Map<String, dynamic> c) => Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
            SelectableText('الاسم: ${c['fullName'] ?? ''}'),
            SelectableText('رقم الدخول: ${c['displayPhone'] ?? c['phone'] ?? ''}'),
            SelectableText('كلمة المرور المؤقتة: ${c['tempPassword'] ?? ''}'),
            const SizedBox(height: 8),
          ],
        );
    return showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        title: Text(r.message),
        content: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              if (r.credentials != null) block('حساب الطالب', r.credentials!),
              if (r.parentCredentials != null) block('حساب ولي الأمر الجديد', r.parentCredentials!),
              const Text('سلّم هذه البيانات لصاحبها — لن تظهر مرة أخرى.',
                  style: TextStyle(fontSize: 12, color: NoorTheme.textSecondary)),
            ],
          ),
        ),
        actions: [FilledButton(onPressed: () => Navigator.pop(ctx), child: const Text('تم'))],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final newParent = _existingParentId == null;
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'تعديل بيانات الطالب' : 'إضافة طالب جديد')),
      body: Form(
        key: _form,
        child: ListView(
          padding: const EdgeInsets.all(NoorTheme.spacingMd),
          children: [
            TextFormField(
              controller: _name,
              decoration: const InputDecoration(labelText: 'الاسم الثلاثي *'),
              validator: (v) => (v ?? '').trim().split(RegExp(r'\s+')).where((e) => e.isNotEmpty).length < 3
                  ? 'يرجى إدخال الاسم الثلاثي كاملاً'
                  : null,
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.cake_outlined),
              title: const Text('تاريخ الميلاد *'),
              subtitle: Text(_dob == null ? 'اختر التاريخ' : Formatters.ymd(_dob)),
              onTap: () => _pickDate(true),
            ),
            ListTile(
              contentPadding: EdgeInsets.zero,
              leading: const Icon(Icons.event_outlined),
              title: const Text('تاريخ التسجيل *'),
              subtitle: Text(Formatters.ymd(_registration)),
              onTap: () => _pickDate(false),
            ),
            TextFormField(
              controller: _phone,
              keyboardType: TextInputType.phone,
              decoration: const InputDecoration(labelText: 'رقم هاتف الطالب (اختياري)', hintText: '09xxxxxxxx'),
              validator: (v) => _validatePhone(v, required: false),
            ),
            TextFormField(controller: _residence, decoration: const InputDecoration(labelText: 'السكن (اختياري)')),
            CircleDropdown(
              value: _circleId,
              allowNone: true,
              onChanged: (c) => _circleId = c?.id,
            ),
            DropdownButtonFormField<String>(
              initialValue: kLevels.contains(_level) ? _level : 'مبتدئ',
              decoration: const InputDecoration(labelText: 'المستوى'),
              items: kLevels.map((l) => DropdownMenuItem(value: l, child: Text(l))).toList(),
              onChanged: (v) => _level = v ?? _level,
            ),
            const Divider(height: 32),
            if (!_isEdit)
              FutureBuilder<Either<Failure, List<ParentLookupModel>>>(
                future: _parentsFuture,
                builder: (context, snap) {
                  final parents = snap.data?.getOrElse(() => const []) ?? const <ParentLookupModel>[];
                  return DropdownButtonFormField<int?>(
                    initialValue: _existingParentId,
                    isExpanded: true,
                    decoration: const InputDecoration(labelText: 'ولي أمر موجود مسبقاً (اختياري)'),
                    items: [
                      const DropdownMenuItem<int?>(value: null, child: Text('— ولي أمر جديد —')),
                      ...parents.map((p) => DropdownMenuItem<int?>(
                            value: p.id,
                            child: Text('${p.fullName} (${p.phone})', overflow: TextOverflow.ellipsis),
                          )),
                    ],
                    onChanged: (v) => setState(() => _existingParentId = v),
                  );
                },
              ),
            if (newParent || _isEdit) ...[
              TextFormField(
                controller: _guardianName,
                decoration: const InputDecoration(labelText: 'اسم ولي الأمر *'),
                validator: (v) => (v ?? '').trim().isEmpty ? 'اسم ولي الأمر مطلوب' : null,
              ),
              TextFormField(
                controller: _parentPhone,
                keyboardType: TextInputType.phone,
                decoration: const InputDecoration(labelText: 'رقم هاتف ولي الأمر *', hintText: '09xxxxxxxx'),
                validator: (v) => _validatePhone(v, required: true),
              ),
              DropdownButtonFormField<String>(
                initialValue: _relationships.containsKey(_relationship) ? _relationship : 'Father',
                decoration: const InputDecoration(labelText: 'صلة القرابة *'),
                items: _relationships.entries
                    .map((e) => DropdownMenuItem(value: e.key, child: Text(e.value)))
                    .toList(),
                onChanged: (v) => _relationship = v ?? _relationship,
              ),
            ],
            const SizedBox(height: 24),
            FilledButton.icon(
              onPressed: _saving ? null : _save,
              icon: const Icon(Icons.save),
              label: Text(_saving ? 'جاري الحفظ...' : 'حفظ'),
            ),
          ],
        ),
      ),
    );
  }

  late final Future<Either<Failure, List<ParentLookupModel>>> _parentsFuture = teacherRepo.getParentsLookup();
}

/// ملف الطالب للمحفّظ: البيانات، التقييم الشهري، آخر موضع، والوصول لكل سجلاته.
class TeacherStudentDetailsScreen extends StatelessWidget {
  final int studentId;
  const TeacherStudentDetailsScreen({super.key, required this.studentId});

  Future<Either<Failure, (StudentDetailsModel, LastPositionModel?, MonthlyHifzRatingModel?)>> _load() async {
    final details = await teacherRepo.getStudent(studentId);
    if (details.isLeft()) return Left(details.fold((f) => f, (_) => const ServerFailure('')));
    final pos = await teacherRepo.getLastPosition(studentId);
    final rating = await teacherRepo.getStudentMonthlyRating(studentId);
    return Right((
      details.getOrElse(() => throw StateError('unreachable')),
      pos.fold((_) => null, (v) => v),
      rating.fold((_) => null, (v) => v),
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('ملف الطالب')),
      body: LoadView<(StudentDetailsModel, LastPositionModel?, MonthlyHifzRatingModel?)>(
        loader: _load,
        builder: (context, data, reload) {
          final (s, pos, rating) = data;
          Widget row(String k, String? v) => Padding(
                padding: const EdgeInsets.symmetric(vertical: 3),
                child: Row(children: [
                  SizedBox(width: 110, child: Text(k, style: const TextStyle(color: NoorTheme.textSecondary))),
                  Expanded(child: Text(v == null || v.isEmpty ? '—' : v)),
                ]),
              );
          return ListView(
            padding: const EdgeInsets.fromLTRB(NoorTheme.spacingMd, 4, NoorTheme.spacingMd, NoorTheme.spacingXl),
            children: [
              // ── بطاقة الطالب: الاسم والحلقة والمستوى وأبرز الأرقام ──
              GradientHeader(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.all(3),
                          decoration: const BoxDecoration(shape: BoxShape.circle, gradient: NoorTheme.goldGradient),
                          child: InitialsAvatar(name: s.fullName, radius: 28),
                        ),
                        const SizedBox(width: 14),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(s.fullName,
                                  style: const TextStyle(color: Colors.white, fontSize: 19, fontWeight: FontWeight.w800)),
                              const SizedBox(height: 6),
                              Wrap(spacing: 6, runSpacing: 6, children: [
                                HeaderPill(icon: Icons.mosque_outlined, text: s.circleName),
                                HeaderPill(icon: Icons.school_outlined, text: s.level),
                              ]),
                            ],
                          ),
                        ),
                        IconButton.filledTonal(
                          tooltip: 'تعديل البيانات',
                          style: IconButton.styleFrom(backgroundColor: Colors.white24, foregroundColor: Colors.white),
                          icon: const Icon(Icons.edit_rounded),
                          onPressed: () async {
                            final changed = await Navigator.of(context).push<bool>(
                                MaterialPageRoute(builder: (_) => StudentFormScreen(student: s)));
                            if (changed == true) reload();
                          },
                        ),
                      ],
                    ),
                    const SizedBox(height: 16),
                    Row(
                      children: [
                        Expanded(child: _ProfileMetric(label: 'الحضور', value: '${s.attendance}%', progress: s.attendance / 100)),
                        const SizedBox(width: 12),
                        Expanded(
                          child: _ProfileMetric(
                            label: 'نسبة الحفظ',
                            value: '${Formatters.number(s.progress, fractionDigits: 1)}%',
                            progress: s.progress / 100,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              SectionCard(
                title: 'البيانات الأساسية',
                icon: Icons.badge_outlined,
                child: Column(children: [
                  row('الحلقة', s.circleName),
                  row('المستوى', s.level),
                  row('ولي الأمر', s.parentName),
                  row('هاتف ولي الأمر', _StudentFormScreenState._display(s.parentPhone)),
                  row('صلة القرابة', _relationships[s.guardianRelationship] ?? s.guardianRelationship),
                  row('تاريخ الميلاد', s.dateOfBirth),
                  row('تاريخ التسجيل', s.registrationDate),
                  row('هاتف الطالب', _StudentFormScreenState._display(s.studentPhone)),
                  row('السكن', s.residence),
                ]),
              ),
              if (rating != null) MonthlyRatingCard(rating: rating),
              if (pos != null)
                SectionCard(
                  title: 'آخر موضع حفظ',
                  child: Text(pos.hasPrevious
                      ? 'آخر حفظ: ${pos.lastSurahName ?? '—'} حتى الآية ${pos.lastVerse} (${Formatters.displayDate(pos.lastSessionDate)})\nالمقترح: ${pos.surahName ?? '—'} من الآية ${pos.nextVerse}'
                      : 'لا توجد جلسات حفظ سابقة'),
                ),
              const SectionTitle('متابعة الطالب'),
              Column(children: [
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.add_task,
                    title: 'تسجيل جلسة حفظ/مراجعة',
                    onTap: () async {
                      await context.push('/teacher/hifz/new?studentId=${s.id}');
                      reload();
                    },
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.history_edu,
                    title: 'تقدم الحفظ والمراجعة',
                    onTap: () => context.push('/teacher/student/${s.id}/hifz'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.map_outlined,
                    title: 'خريطة الحفظ',
                    onTap: () => context.push('/teacher/student/${s.id}/quran-map'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.quiz_outlined,
                    title: 'الاختبارات والنتائج',
                    onTap: () => context.push('/teacher/student/${s.id}/exams'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.auto_stories_outlined,
                    title: 'سجل المتون',
                    onTap: () => context.push('/teacher/evaluation?tab=0&studentId=${s.id}'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.flag_outlined,
                    title: 'الأهداف الشهرية',
                    onTap: () => context.push('/teacher/evaluation?tab=1&studentId=${s.id}'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.mosque_outlined,
                    title: 'سجل الصلاة',
                    onTap: () => context.push('/teacher/evaluation?tab=4&studentId=${s.id}'),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.picture_as_pdf_outlined,
                    title: 'التقرير الشهري (PDF)',
                    onTap: () => _downloadMonthlyReport(context, s.id),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.event_available,
                    title: 'سجل الحضور (30 يوماً)',
                    onTap: () => Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => _StudentAttendanceHistory(studentId: s.id, name: s.fullName))),
                  ),
                  MoreMenuTile(
                    margin: const EdgeInsets.symmetric(vertical: 5),
                    icon: Icons.notifications_active_outlined,
                    title: 'إرسال إشعار غياب لولي الأمر',
                    onTap: () async {
                      if (!await confirmDialog(context, 'إرسال إشعار غياب لولي الأمر عبر واتساب؟')) return;
                      if (context.mounted) {
                        await runAction(context, () => teacherRepo.sendAbsenceNotification(s.id));
                      }
                    },
                  ),
                ]),
            ],
          );
        },
      ),
    );
  }
}

/// رقم بارز مع شريط تقدم داخل بطاقة الطالب.
class _ProfileMetric extends StatelessWidget {
  final String label;
  final String value;
  final double progress;
  const _ProfileMetric({required this.label, required this.value, required this.progress});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: Colors.white.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(color: Colors.white70, fontSize: 12)),
          const SizedBox(height: 2),
          Text(value, style: const TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.w800)),
          const SizedBox(height: 8),
          LinearProgressIndicator(
            value: progress.clamp(0, 1).toDouble(),
            minHeight: 6,
            borderRadius: BorderRadius.circular(6),
            color: NoorTheme.accentGoldLight,
            backgroundColor: Colors.white24,
          ),
        ],
      ),
    );
  }
}

class _StudentAttendanceHistory extends StatelessWidget {
  final int studentId;
  final String name;
  const _StudentAttendanceHistory({required this.studentId, required this.name});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('حضور: $name')),
      body: LoadView<List<Map<String, dynamic>>>(
        loader: () => teacherRepo.getStudentAttendance(studentId),
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد سجلات حضور خلال 30 يوماً',
        builder: (context, list, _) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final r = list[i];
            final status = '${r['status'] ?? ''}';
            return ListTile(
              leading: Icon(Icons.circle, size: 14, color: attendanceColor(status)),
              title: Text(kAttendanceStatuses[status] ?? status),
              subtitle: (r['note'] ?? '').toString().isEmpty ? null : Text('${r['note']}'),
              trailing: Text(Formatters.displayDate('${r['date']}')),
            );
          },
        ),
      ),
    );
  }
}

const _arabicMonths = [
  'يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
  'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر',
];

/// اختيار الشهر (آخر 6 أشهر) ثم تنزيل التقرير الشهري للطالب وفتحه.
Future<void> _downloadMonthlyReport(BuildContext context, int studentId) async {
  final now = DateTime.now();
  final months = List.generate(6, (i) => DateTime(now.year, now.month - i));
  final picked = await showModalBottomSheet<DateTime>(
    context: context,
    showDragHandle: true,
    builder: (ctx) => SafeArea(
      child: ListView(
        shrinkWrap: true,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(20, 0, 20, 8),
            child: Text('اختر شهر التقرير', style: TextStyle(fontWeight: FontWeight.w700, fontSize: 16)),
          ),
          for (final m in months)
            ListTile(
              leading: const Icon(Icons.calendar_month_outlined),
              title: Text('${_arabicMonths[m.month - 1]} ${m.year}'),
              onTap: () => Navigator.pop(ctx, m),
            ),
        ],
      ),
    ),
  );
  if (picked == null || !context.mounted) return;

  final messenger = ScaffoldMessenger.of(context);
  messenger.showSnackBar(const SnackBar(content: Text('جارٍ تجهيز التقرير...')));
  final result = await sl<PortalRepository>().downloadMonthlyReport(studentId, picked.year, picked.month);
  messenger.hideCurrentSnackBar();
  await result.fold(
    (f) async => messenger.showSnackBar(SnackBar(content: Text(f.message), backgroundColor: NoorTheme.error)),
    (path) async {
      await FileOpenHelper.openResult(path);
      messenger.showSnackBar(SnackBar(
        content: Text(FileOpenHelper.isBrowserDownload(path) ? 'بدأ تنزيل التقرير' : 'تم فتح التقرير'),
      ));
    },
  );
}
