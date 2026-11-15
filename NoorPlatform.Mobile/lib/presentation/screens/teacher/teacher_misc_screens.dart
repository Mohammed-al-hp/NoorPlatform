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

/// صندوق رسائل المحفّظ (من أولياء الأمور) مع تعليم المقروء.
class TeacherInboxScreen extends StatelessWidget {
  const TeacherInboxScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الرسائل الواردة')),
      body: LoadView<List<InboxMessageModel>>(
        loader: teacherRepo.getInbox,
        isEmpty: (l) => l.isEmpty,
        emptyTitle: 'لا توجد رسائل',
        emptyIcon: Icons.mail_outline,
        builder: (context, list, reload) => ListView.builder(
          physics: const AlwaysScrollableScrollPhysics(),
          itemCount: list.length,
          itemBuilder: (context, i) {
            final m = list[i];
            return ListTile(
              leading: Icon(m.isRead ? Icons.drafts_outlined : Icons.mark_email_unread,
                  color: m.isRead ? NoorTheme.textSecondary : NoorTheme.primaryGreen),
              title: Text(m.senderName,
                  style: TextStyle(fontWeight: m.isRead ? FontWeight.normal : FontWeight.w700)),
              subtitle: Text(m.content, maxLines: 2, overflow: TextOverflow.ellipsis),
              trailing: Text(Formatters.displayDate(m.createdAt), style: const TextStyle(fontSize: 11)),
              onTap: () async {
                await showDialog<void>(
                  context: context,
                  builder: (ctx) => AlertDialog(
                    title: Text(m.senderName),
                    content: SingleChildScrollView(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          SelectableText(m.content),
                          const SizedBox(height: 8),
                          Text(Formatters.displayDateTime(m.createdAt),
                              style: Theme.of(ctx).textTheme.bodySmall),
                        ],
                      ),
                    ),
                    actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('إغلاق'))],
                  ),
                );
                if (!m.isRead) {
                  await teacherRepo.markMessageRead(m.id);
                  reload();
                }
              },
            );
          },
        ),
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
        await FileOpenHelper.openResult(path);
        if (mounted) {
          showSnack(context, FileOpenHelper.isBrowserDownload(path) ? 'بدأ تنزيل الملف' : 'تم فتح الملف');
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
