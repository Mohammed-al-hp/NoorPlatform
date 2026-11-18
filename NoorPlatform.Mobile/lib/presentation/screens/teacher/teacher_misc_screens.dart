import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import '../../../config/theme.dart';
import '../../../core/utils/file_open_helper.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/announcement_models.dart';
import '../../../data/models/library_models.dart';
import '../../../data/models/teacher_models.dart';
import '../../../di/injection_container.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../widgets/async_body.dart';
import 'teacher_ui.dart';

/// رسائل المحفّظ: محادثات مع أولياء الأمور (واردة أو بدأها المحفّظ)، مع الرد وبدء رسالة جديدة.
class TeacherInboxScreen extends StatefulWidget {
  const TeacherInboxScreen({super.key});

  @override
  State<TeacherInboxScreen> createState() => _TeacherInboxScreenState();
}

class _TeacherInboxScreenState extends State<TeacherInboxScreen> {
  int _reloadKey = 0;

  void _refresh() => setState(() => _reloadKey++);

  Future<void> _compose() async {
    final recipients = await teacherRepo.getParentRecipients();
    if (!mounted) return;
    final parents = recipients.fold((f) {
      showSnack(context, f.message, error: true);
      return <ParentContactModel>[];
    }, (v) => v);
    if (parents.isEmpty) {
      showSnack(context, 'لا يوجد أولياء أمور مرتبطون بطلابك');
      return;
    }
    int? selected;
    final ctrl = TextEditingController();
    final sent = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setModal) => Padding(
          padding: EdgeInsets.only(
            left: 16,
            right: 16,
            top: 16,
            bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text('رسالة إلى ولي أمر', style: Theme.of(ctx).textTheme.titleLarge),
              const SizedBox(height: 12),
              DropdownButtonFormField<int>(
                initialValue: selected,
                isExpanded: true,
                decoration: const InputDecoration(labelText: 'ولي الأمر'),
                items: parents
                    .map((p) => DropdownMenuItem(
                          value: p.userId,
                          child: Text(p.label, overflow: TextOverflow.ellipsis),
                        ))
                    .toList(),
                onChanged: (v) => setModal(() => selected = v),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: ctrl,
                maxLines: 4,
                maxLength: 2000,
                decoration: const InputDecoration(labelText: 'نص الرسالة'),
              ),
              const SizedBox(height: 8),
              ElevatedButton.icon(
                onPressed: () async {
                  if (selected == null || ctrl.text.trim().isEmpty) {
                    showSnack(ctx, 'اختر ولي الأمر واكتب نص الرسالة', error: true);
                    return;
                  }
                  final res = await teacherRepo.sendToParent(selected!, ctrl.text.trim());
                  if (!ctx.mounted) return;
                  res.fold(
                    (f) => showSnack(ctx, f.message, error: true),
                    (_) => Navigator.pop(ctx, true),
                  );
                },
                icon: const Icon(Icons.send_rounded),
                label: const Text('إرسال'),
              ),
            ],
          ),
        ),
      ),
    );
    ctrl.dispose();
    if (sent == true && mounted) {
      showSnack(context, 'تم إرسال الرسالة');
      _refresh();
    }
  }

  Future<void> _openThread(InboxMessageModel m) async {
    if (!m.isRead) await teacherRepo.markMessageRead(m.id);
    if (!mounted) return;
    final ctrl = TextEditingController();
    final replied = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      builder: (ctx) => Padding(
        padding: EdgeInsets.only(
          left: 16,
          right: 16,
          top: 16,
          bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
        ),
        child: ConstrainedBox(
          constraints: BoxConstraints(maxHeight: MediaQuery.of(ctx).size.height * 0.85),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                m.isOutgoing ? 'إلى: ${m.counterpartName}' : 'من: ${m.counterpartName}',
                style: Theme.of(ctx).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              Flexible(
                child: ListView(
                  shrinkWrap: true,
                  children: [
                    _ThreadBubble(
                      author: m.isOutgoing ? m.senderName : m.counterpartName,
                      content: m.content,
                      createdAt: m.createdAt,
                      fromParent: !m.isOutgoing,
                    ),
                    for (final r in m.replies)
                      _ThreadBubble(
                        author: r.senderName,
                        content: r.content,
                        createdAt: r.createdAt,
                        fromParent: r.fromParent,
                      ),
                  ],
                ),
              ),
              const SizedBox(height: 8),
              TextField(
                controller: ctrl,
                maxLines: 3,
                maxLength: 2000,
                decoration: const InputDecoration(labelText: 'اكتب ردك لولي الأمر'),
              ),
              ElevatedButton.icon(
                onPressed: () async {
                  final text = ctrl.text.trim();
                  if (text.isEmpty) return;
                  final res = await teacherRepo.replyToMessage(m.id, text);
                  if (!ctx.mounted) return;
                  res.fold(
                    (f) => showSnack(ctx, f.message, error: true),
                    (_) => Navigator.pop(ctx, true),
                  );
                },
                icon: const Icon(Icons.reply_rounded),
                label: const Text('إرسال الرد'),
              ),
            ],
          ),
        ),
      ),
    );
    ctrl.dispose();
    if (!mounted) return;
    if (replied == true) showSnack(context, 'تم إرسال الرد');
    _refresh();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الرسائل')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _compose,
        icon: const Icon(Icons.edit_rounded),
        label: const Text('رسالة جديدة'),
      ),
      body: LoadView<List<InboxMessageModel>>(
        key: ValueKey(_reloadKey),
        loader: teacherRepo.getInbox,
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد رسائل',
        emptyIcon: Icons.mail_outline,
        builder: (context, list, reload) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.only(bottom: 88),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final m = list[i];
            final last = m.replies.isNotEmpty ? m.replies.last.content : m.content;
            return ListTile(
              leading: Icon(
                m.isRead
                    ? (m.isOutgoing ? Icons.outbox_outlined : Icons.drafts_outlined)
                    : Icons.mark_email_unread,
                color: m.isRead ? NoorTheme.textSecondary : NoorTheme.primaryGreen,
              ),
              title: Text(
                m.isOutgoing ? 'إلى: ${m.counterpartName}' : m.counterpartName,
                style: TextStyle(fontWeight: m.isRead ? FontWeight.normal : FontWeight.w700),
              ),
              subtitle: Text(last, maxLines: 2, overflow: TextOverflow.ellipsis),
              trailing: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(Formatters.displayDate(m.createdAt), style: const TextStyle(fontSize: 11)),
                  if (m.replies.isNotEmpty)
                    Text('${m.replies.length} رد', style: const TextStyle(fontSize: 11)),
                ],
              ),
              onTap: () => _openThread(m),
            );
          },
        ),
      ),
    );
  }
}

/// فقاعة رسالة داخل المحادثة: ولي الأمر بلون، والمحفّظ/الإدارة بلون آخر.
class _ThreadBubble extends StatelessWidget {
  final String author;
  final String content;
  final String createdAt;
  final bool fromParent;

  const _ThreadBubble({
    required this.author,
    required this.content,
    required this.createdAt,
    required this.fromParent,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      margin: EdgeInsetsDirectional.only(top: 8, start: fromParent ? 0 : 24, end: fromParent ? 24 : 0),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: fromParent
            ? NoorTheme.accentGold.withValues(alpha: 0.08)
            : NoorTheme.primaryGreen.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Expanded(child: Text(author, style: theme.textTheme.labelLarge)),
              Text(Formatters.displayDateTime(createdAt), style: theme.textTheme.bodySmall),
            ],
          ),
          const SizedBox(height: 4),
          SelectableText(content),
        ],
      ),
    );
  }
}

/// الإعلانات: عرض + نشر إعلان جديد (المحفّظ: الجميع/الطلاب/أولياء الأمور).
class TeacherAnnouncementsScreen extends StatefulWidget {
  const TeacherAnnouncementsScreen({super.key});

  @override
  State<TeacherAnnouncementsScreen> createState() => _TeacherAnnouncementsScreenState();
}

class _TeacherAnnouncementsScreenState extends State<TeacherAnnouncementsScreen> {
  int _key = 0;

  Future<void> _create() async {
    final title = TextEditingController();
    final content = TextEditingController();
    String target = 'الجميع';
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('إعلان جديد'),
        content: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(controller: title, decoration: const InputDecoration(labelText: 'العنوان')),
            TextField(controller: content, maxLines: 4, decoration: const InputDecoration(labelText: 'نص الإعلان')),
            DropdownButtonFormField<String>(
              initialValue: target,
              decoration: const InputDecoration(labelText: 'الفئة المستهدفة'),
              items: const ['الجميع', 'الطلاب', 'أولياء الأمور']
                  .map((t) => DropdownMenuItem(value: t, child: Text(t)))
                  .toList(),
              onChanged: (v) => target = v ?? target,
            ),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('نشر')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    if (title.text.trim().isEmpty || content.text.trim().isEmpty) {
      showSnack(context, 'العنوان ونص الإعلان مطلوبان', error: true);
      return;
    }
    final saved = await runAction(
      context,
      () => teacherRepo.createAnnouncement(
          title: title.text.trim(), content: content.text.trim(), target: target),
    );
    if (saved) setState(() => _key++);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الإعلانات')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _create,
        icon: const Icon(Icons.campaign),
        label: const Text('إعلان جديد'),
      ),
      body: LoadView<List<AnnouncementModel>>(
        key: ValueKey(_key),
        loader: sl<PortalRepository>().getAnnouncements,
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد إعلانات',
        builder: (context, list, _) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 88),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final a = list[i];
            return SectionCard(
              title: a.title,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(a.content),
                  const SizedBox(height: 8),
                  Text('${a.target} · ${Formatters.displayDateTime(a.createdAt)}',
                      style: Theme.of(context).textTheme.bodySmall),
                ],
              ),
            );
          },
        ),
      ),
    );
  }
}

/// مكتبة الطلاب للمحفّظ: عرض/تنزيل + رفع ملف PDF + حذف.
class TeacherLibraryScreen extends StatefulWidget {
  const TeacherLibraryScreen({super.key});

  @override
  State<TeacherLibraryScreen> createState() => _TeacherLibraryScreenState();
}

class _TeacherLibraryScreenState extends State<TeacherLibraryScreen> {
  int _key = 0;
  String _search = '';
  int? _downloading;

  Future<void> _upload() async {
    final picked = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: const ['pdf'],
      withData: true,
    );
    final file = picked?.files.single;
    if (file == null || !mounted) return;
    if (file.size > 50 * 1024 * 1024) {
      showSnack(context, 'حجم الملف يتجاوز الحد المسموح (50MB)', error: true);
      return;
    }

    final title = TextEditingController(text: file.name.replaceAll(RegExp(r'\.pdf$', caseSensitive: false), ''));
    final description = TextEditingController();
    final category = TextEditingController(text: 'عام');
    int? circleId;
    final ok = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('رفع ملف للمكتبة'),
        content: SingleChildScrollView(
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            Text(file.name, style: const TextStyle(fontSize: 12, color: NoorTheme.textSecondary)),
            TextField(controller: title, decoration: const InputDecoration(labelText: 'العنوان')),
            TextField(controller: category, decoration: const InputDecoration(labelText: 'التصنيف')),
            TextField(controller: description, maxLines: 2, decoration: const InputDecoration(labelText: 'الوصف')),
            CircleDropdown(
              value: null,
              allowNone: true,
              noneLabel: 'كل الطلاب',
              onChanged: (c) => circleId = c?.id,
            ),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('إلغاء')),
          FilledButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('رفع')),
        ],
      ),
    );
    if (ok != true || !mounted) return;
    if (title.text.trim().isEmpty) {
      showSnack(context, 'العنوان مطلوب', error: true);
      return;
    }
    showSnack(context, 'جاري رفع الملف...');
    final saved = await runAction(
      context,
      () => teacherRepo.uploadLibraryFile(
        title: title.text.trim(),
        description: description.text.trim(),
        category: category.text.trim().isEmpty ? 'عام' : category.text.trim(),
        circleId: circleId,
        fileName: file.name,
        bytes: file.bytes,
        filePath: file.bytes == null ? file.path : null,
      ),
    );
    if (saved) setState(() => _key++);
  }

  Future<void> _download(LibraryItemModel item) async {
    setState(() => _downloading = item.id);
    final r = await sl<PortalRepository>().downloadLibraryFile(item.id, item.title);
    if (!mounted) return;
    setState(() => _downloading = null);
    await r.fold(
      (f) async => showSnack(context, f.message, error: true),
      (path) async {
        final openError = await FileOpenHelper.openResult(path);
        if (mounted) {
          showSnack(context, openError ?? (FileOpenHelper.isBrowserDownload(path) ? 'بدأ تنزيل الملف' : 'تم فتح الملف'), error: openError != null);
        }
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('مكتبة الطلاب')),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _upload,
        icon: const Icon(Icons.upload_file),
        label: const Text('رفع PDF'),
      ),
      body: Column(children: [
        Padding(
          padding: const EdgeInsets.all(12),
          child: TextField(
            decoration: const InputDecoration(hintText: 'بحث في المكتبة...', prefixIcon: Icon(Icons.search)),
            onSubmitted: (q) => setState(() {
              _search = q.trim();
              _key++;
            }),
          ),
        ),
        Expanded(
          child: LoadView<List<LibraryItemModel>>(
            key: ValueKey('$_key-$_search'),
            loader: () => sl<PortalRepository>().getLibrary(search: _search),
            isEmpty: (l) => l.isEmpty,
            emptyTitle: 'لا توجد ملفات',
            builder: (context, list, reload) => ListView.builder(
              physics: const AlwaysScrollableScrollPhysics(),
              padding: const EdgeInsets.only(bottom: 88),
              itemCount: list.length,
              itemBuilder: (context, i) {
                final item = list[i];
                return ListTile(
                  leading: const Icon(Icons.picture_as_pdf, color: NoorTheme.error),
                  title: Text(item.title),
                  subtitle: Text('${item.category} · ${Formatters.number(item.downloadCount)} تنزيل · ${item.uploadedBy}'),
                  trailing: Row(mainAxisSize: MainAxisSize.min, children: [
                    _downloading == item.id
                        ? const SizedBox(width: 24, height: 24, child: CircularProgressIndicator(strokeWidth: 2))
                        : IconButton(icon: const Icon(Icons.download), onPressed: () => _download(item)),
                    IconButton(
                      icon: const Icon(Icons.delete_outline),
                      onPressed: () async {
                        if (!await confirmDialog(context, 'حذف "${item.title}" من المكتبة؟', confirmText: 'حذف')) {
                          return;
                        }
                        if (!context.mounted) return;
                        if (await runAction(context, () => teacherRepo.deleteLibraryItem(item.id))) reload();
                      },
                    ),
                  ]),
                );
              },
            ),
          ),
        ),
      ]),
    );
  }
}
