import 'dart:io';
import 'dart:typed_data';

import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import 'file_open_helper.dart';

/// يحفظ الملف في مجلد مستندات التطبيق (يبقى متاحاً بعد إغلاق التطبيق، بخلاف المجلد المؤقت)
/// باسم آمن، ثم يُفتح بعارض PDF المثبت على الجهاز.
Future<String> savePdf(Uint8List bytes, String filename) async {
  if (bytes.isEmpty) {
    throw const FileSystemException('الملف المستلم فارغ');
  }
  Directory dir;
  try {
    dir = await getApplicationDocumentsDirectory();
  } catch (_) {
    dir = await getTemporaryDirectory();
  }
  final folder = Directory('${dir.path}/noor_files');
  if (!await folder.exists()) await folder.create(recursive: true);
  final safeName = filename.replaceAll(RegExp(r'[\\/:*?"<>|]'), '_');
  final file = File('${folder.path}/$safeName');
  await file.writeAsBytes(bytes, flush: true);
  return file.path;
}

Future<String?> openResult(String result) async {
  if (FileOpenHelper.isBrowserDownload(result)) return null;
  final r = await OpenFilex.open(result, type: 'application/pdf');
  switch (r.type) {
    case ResultType.done:
      return null;
    case ResultType.noAppToOpen:
      return 'تم حفظ الملف، لكن لا يوجد تطبيق لعرض ملفات PDF على الجهاز. ثبّت عارض PDF ثم أعد المحاولة.';
    case ResultType.fileNotFound:
      return 'تعذر العثور على الملف بعد تنزيله، حاول مرة أخرى.';
    case ResultType.permissionDenied:
      return 'لا توجد صلاحية لفتح الملف على هذا الجهاز.';
    default:
      return 'تعذر فتح الملف: ${r.message}';
  }
}
