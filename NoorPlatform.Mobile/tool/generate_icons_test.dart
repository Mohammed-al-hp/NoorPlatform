// أداة توليد أيقونات منصة نور (PNG) من الشعار المتجه assets/images/logo_mark.svg.
//
// التشغيل (من مجلد NoorPlatform.Mobile):
//   flutter test tool/generate_icons_test.dart
//
// تولّد:
//   - أيقونات أندرويد: android/app/src/main/res/mipmap-*/ic_launcher.png
//   - أيقونات الويب (PWA): ../NoorPlatform.Api/wwwroot/icon-192.png, icon-512.png, icon-maskable-512.png
//   - أيقونات نسخة الويب من تطبيق الهاتف: web/favicon.png, web/icons/*
//   - أيقونات تطبيق iOS: ios/Runner/Assets.xcassets/AppIcon.appiconset/*
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
      // iOS يقصّ الزوايا بنفسه ويملأ الشفافية بالأسود، لذا الخلفية مربعة كاملة
      await render('$web/apple-touch-icon.png', 180, markRatio: 0.7, rounded: false);

      // نسخة الويب من تطبيق الهاتف (flutter run -d web-server / flutter build web)
      await render('web/favicon.png', 32);
      await render('web/icons/Icon-192.png', 192);
      await render('web/icons/Icon-512.png', 512);
      await render('web/icons/Icon-maskable-192.png', 192, markRatio: 0.58, rounded: false);
      await render('web/icons/Icon-maskable-512.png', 512, markRatio: 0.58, rounded: false);
      // iOS يقصّ الزوايا بنفسه، لذا الخلفية مربعة كاملة بدون إطار
      await render('web/icons/apple-touch-icon.png', 180, markRatio: 0.7, rounded: false);

      // أيقونات تطبيق iOS (iOS يقصّ الزوايا بنفسه)
      const ios = 'ios/Runner/Assets.xcassets/AppIcon.appiconset';
      const iosIcons = {
        '20x20@1x': 20, '20x20@2x': 40, '20x20@3x': 60,
        '29x29@1x': 29, '29x29@2x': 58, '29x29@3x': 87,
        '40x40@1x': 40, '40x40@2x': 80, '40x40@3x': 120,
        '60x60@2x': 120, '60x60@3x': 180,
        '76x76@1x': 76, '76x76@2x': 152, '83.5x83.5@2x': 167,
        '1024x1024@1x': 1024,
      };
      for (final e in iosIcons.entries) {
        await render('$ios/Icon-App-${e.key}.png', e.value, markRatio: 0.7, rounded: false);
      }
    });
  });
}
