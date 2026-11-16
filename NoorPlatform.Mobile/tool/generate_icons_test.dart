// أداة توليد أيقونات منصة نور (PNG) من الشعار المتجه assets/images/logo_mark.svg.
//
// التشغيل (من مجلد NoorPlatform.Mobile):
//   flutter test tool/generate_icons_test.dart
//
// تولّد:
//   - أيقونات أندرويد: android/app/src/main/res/mipmap-*/ic_launcher.png
//   - أيقونات الويب (PWA): ../NoorPlatform.Api/wwwroot/icon-192.png, icon-512.png, icon-maskable-512.png
import 'dart:io';
import 'dart:ui' as ui;

import 'package:flutter/painting.dart';
import 'package:flutter_svg/flutter_svg.dart';
import 'package:flutter_test/flutter_test.dart';

const _cream = Color(0xFFFFFDF5);
const _gold = Color(0xFFB8860B);

void main() {
  testWidgets('generate app icons from logo', (tester) async {
    await tester.runAsync(() async {
      final svg = File('assets/images/logo_mark.svg').readAsStringSync();
      final info = await vg.loadPicture(SvgStringLoader(svg), null);

      /// [markRatio]: نسبة عرض العلامة من الأيقونة. [rounded]: زوايا دائرية بإطار ذهبي (الأيقونة العادية)
      /// أو خلفية كاملة بدون إطار (maskable — المنصة تقصّها بنفسها، والعلامة داخل المنطقة الآمنة).
      Future<void> render(String path, int size, {double markRatio = 0.74, bool rounded = true}) async {
        final recorder = ui.PictureRecorder();
        final canvas = Canvas(recorder);
        final s = size.toDouble();
        final rect = Rect.fromLTWH(0, 0, s, s);

        if (rounded) {
          final r = RRect.fromRectAndRadius(rect.deflate(s * 0.02), Radius.circular(s * 0.22));
          canvas.drawRRect(r, Paint()..color = _cream);
          canvas.drawRRect(
            r.deflate(s * 0.012),
            Paint()
              ..style = PaintingStyle.stroke
              ..strokeWidth = s * 0.018
              ..color = _gold.withValues(alpha: 0.45),
          );
        } else {
          canvas.drawRect(rect, Paint()..color = _cream);
        }

        final markSize = s * markRatio;
        final scale = markSize / info.size.width;
        canvas.save();
        canvas.translate((s - markSize) / 2, (s - info.size.height * scale) / 2);
        canvas.scale(scale);
        canvas.drawPicture(info.picture);
        canvas.restore();

        final image = await recorder.endRecording().toImage(size, size);
        final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
        File(path)
          ..createSync(recursive: true)
          ..writeAsBytesSync(bytes!.buffer.asUint8List());
        // ignore: avoid_print
        print('✓ $path ($size×$size)');
      }

      const android = {
        'mdpi': 48,
        'hdpi': 72,
        'xhdpi': 96,
        'xxhdpi': 144,
        'xxxhdpi': 192,
      };
      for (final e in android.entries) {
        await render('android/app/src/main/res/mipmap-${e.key}/ic_launcher.png', e.value);
      }

      const web = '../NoorPlatform.Api/wwwroot';
      await render('$web/icon-192.png', 192);
      await render('$web/icon-512.png', 512);
      await render('$web/icon-maskable-512.png', 512, markRatio: 0.58, rounded: false);
    });
  });
}
