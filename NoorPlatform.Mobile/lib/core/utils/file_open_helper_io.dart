import 'dart:io';
import 'dart:typed_data';

import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';

import 'file_open_helper.dart';

Future<String> savePdf(Uint8List bytes, String filename) async {
  final dir = await getTemporaryDirectory();
  final path = '${dir.path}/$filename';
  final file = File(path);
  await file.writeAsBytes(bytes, flush: true);
  return path;
}

Future<void> openResult(String result) async {
  if (FileOpenHelper.isBrowserDownload(result)) return;
  await OpenFilex.open(result);
}
