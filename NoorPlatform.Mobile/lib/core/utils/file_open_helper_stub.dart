import 'dart:typed_data';

/// Stub used so web never links path_provider / dart:io helpers,
/// and IO never links dart:html helpers.
Future<String> savePdf(Uint8List bytes, String filename) async {
  throw UnsupportedError('FileOpenHelper: wrong platform stub for savePdf');
}

Future<void> openResult(String result) async {}
