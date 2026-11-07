import 'dart:html' as html;
import 'dart:typed_data';

import 'file_open_helper.dart';

Future<String> savePdf(Uint8List bytes, String filename) async {
  final blob = html.Blob([bytes], 'application/pdf');
  final url = html.Url.createObjectUrlFromBlob(blob);
  final anchor = html.AnchorElement(href: url)
    ..setAttribute('download', filename)
    ..style.display = 'none';
  html.document.body?.children.add(anchor);
  anchor.click();
  anchor.remove();
  html.Url.revokeObjectUrl(url);
  return '${FileOpenHelper.browserPrefix}$filename';
}

Future<void> openResult(String result) async {
  // Browser download already triggered in [savePdf].
}
