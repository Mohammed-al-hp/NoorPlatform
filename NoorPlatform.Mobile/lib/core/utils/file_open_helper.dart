import 'dart:typed_data';

import 'package:flutter/foundation.dart' show kIsWeb;

import 'file_open_helper_io.dart' if (dart.library.html) 'file_open_helper_stub.dart'
    as io_impl;
import 'file_open_helper_web.dart' if (dart.library.io) 'file_open_helper_stub.dart'
    as web_impl;

/// Saves PDF bytes and opens/downloads them in a platform-safe way.
///
/// Returns a local file path on mobile/desktop, or `browser:<filename>` on web
/// after triggering a browser download.
class FileOpenHelper {
  FileOpenHelper._();

  static const browserPrefix = 'browser:';

  static bool isBrowserDownload(String result) =>
      result.startsWith(browserPrefix);

  static Future<String> savePdf(Uint8List bytes, String filename) {
    if (kIsWeb) {
      return web_impl.savePdf(bytes, filename);
    }
    return io_impl.savePdf(bytes, filename);
  }

  /// يعيد null عند النجاح، أو رسالة خطأ عربية إن تعذّر فتح الملف.
  static Future<String?> openResult(String result) {
    if (kIsWeb || isBrowserDownload(result)) {
      return web_impl.openResult(result);
    }
    return io_impl.openResult(result);
  }
}
