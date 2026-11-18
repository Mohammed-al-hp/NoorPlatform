import 'dart:convert';

import 'package:flutter/services.dart' show rootBundle;

import '../../config/quran_surahs.dart';

/// نص القرآن الكريم برواية قالون عن نافع — مضمّن في التطبيق ويعمل دون إنترنت.
/// المصدر: مجمع الملك فهد لطباعة المصحف الشريف (UthmanicQaloon v10)، بالعدّ المدني الأول (6214 آية)
/// المطابق لترقيم مصاحف قالون في ليبيا. يُعرض بخط «UthmanicQaloun».
class QuranText {
  QuranText._();

  static const fontFamily = 'UthmanicQaloun';
  static List<List<String>>? _surahs;
  static Future<List<List<String>>>? _loading;

  static Future<List<List<String>>> load() {
    if (_surahs != null) return Future.value(_surahs);
    return _loading ??= rootBundle
        .loadString('assets/quran/quran-qaloun.json')
        .then((raw) {
          final data = jsonDecode(raw) as Map<String, dynamic>;
          _surahs = (data['surahs'] as List)
              .map((s) => (s as List).map((v) => v.toString()).toList())
              .toList();
          return _surahs!;
        });
  }

  static int? surahNumber(String surahName) {
    for (final s in kQuranSurahs) {
      if (s.name == surahName) return s.number;
    }
    return null;
  }

  /// آيات السورة (الفهرس 0 = الآية 1).
  static Future<List<String>> versesOf(String surahName) async {
    final n = surahNumber(surahName);
    if (n == null) return const [];
    final all = await load();
    return all[n - 1];
  }

  static Future<String> verse(String surahName, int number) async {
    final verses = await versesOf(surahName);
    return number >= 1 && number <= verses.length ? verses[number - 1] : '';
  }

  /// أول كلمات الآية للعرض المختصر.
  static String lead(String text, [int words = 6]) {
    final parts = text.split(RegExp(r'\s+'));
    return parts.length <= words ? text : '${parts.take(words).join(' ')} …';
  }

  /// توحيد النص للبحث: إزالة التشكيل وعلامات الضبط وتوحيد الهمزات.
  static String normalize(String s) => s
      .replaceAll(RegExp('[ؐ-ًؚ-ٰٟۖ-ۭ࣓-ࣿـ]'), '')
      .replaceAll(RegExp('[إأآٱا]'), 'ا')
      .replaceAll(RegExp('[ىے]'), 'ي')
      .replaceAll('ة', 'ه')
      .replaceAll('ؤ', 'و')
      .replaceAll('ئ', 'ي')
      // الرسم العثماني يحذف الألف في كلمات كثيرة (الصرط، الكتب...)؛ نتجاهل الألف في الطرفين عند المطابقة
      .replaceAll('ا', '')
      .replaceAll(RegExp(r'\s+'), ' ')
      .trim();
}

/// فهرس صفحات «مصحف أمانة التعليم» (رواية قالون): صفحة بداية كل سورة (مُتحقَّق منها صفحةً صفحة).
class MushafIndex {
  final int totalPages;
  final int firstQuranPage;
  final int lastQuranPage;
  final List<int> surahStartPage;
  final List<int> verseCounts;

  const MushafIndex._(
    this.totalPages,
    this.firstQuranPage,
    this.lastQuranPage,
    this.surahStartPage,
    this.verseCounts,
  );

  static MushafIndex? _cached;

  static Future<MushafIndex> load() async {
    if (_cached != null) return _cached!;
    final data =
        jsonDecode(
              await rootBundle.loadString('assets/quran/mushaf-index.json'),
            )
            as Map<String, dynamic>;
    return _cached = MushafIndex._(
      data['totalPages'] as int,
      data['firstQuranPage'] as int,
      data['lastQuranPage'] as int,
      (data['surahStartPage'] as List).cast<int>(),
      (data['verseCounts'] as List).cast<int>(),
    );
  }

  /// الصفحة التي تقع فيها الآية (تقدير داخل السورة بنسبة موضع الآية).
  int pageFor(int surah, [int verse = 1]) {
    final n = surah.clamp(1, 114);
    final start = surahStartPage[n - 1];
    final end = n < 114 ? surahStartPage[n] : lastQuranPage;
    final count = verseCounts[n - 1];
    final v = verse.clamp(1, count);
    if (end <= start) return start;
    return (start + ((v - 1) / count * (end - start + 1)).floor()).clamp(
      start,
      end,
    );
  }

  /// السور الظاهرة في الصفحة (آخرها هي الأحدث بداية).
  List<int> surahsOnPage(int page) {
    final list = <int>[];
    for (var i = 0; i < 114; i++) {
      final start = surahStartPage[i];
      final end = i < 113 ? surahStartPage[i + 1] : lastQuranPage;
      if (start <= page &&
          page <= end &&
          (start == page || end > page || i == 113)) {
        list.add(i + 1);
      }
    }
    if (list.isEmpty) {
      for (var i = 113; i >= 0; i--) {
        if (surahStartPage[i] <= page) {
          list.add(i + 1);
          break;
        }
      }
    }
    return list;
  }
}
