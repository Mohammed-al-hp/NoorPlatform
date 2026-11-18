import 'package:flutter_test/flutter_test.dart';
import 'package:noor_mobile/config/quran_surahs.dart';
import 'package:noor_mobile/core/quran/quran_text.dart';

/// نص قالون المضمّن وفهرس «مصحف أمانة التعليم»: العدّ المدني الأول (6214 آية) وصفحات بدايات السور.
void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('عدد الآيات بالعدّ المدني الأول (قالون) = 6214', () {
    expect(kQuranSurahs.fold<int>(0, (a, s) => a + s.verseCount), 6214);
    expect(kQuranSurahs[1].verseCount, 285); // البقرة
    expect(kQuranSurahs[105].verseCount, 5); // قريش
  });

  test('نص قالون المضمّن يطابق عدد آيات الفهرس', () async {
    final surahs = await QuranText.load();
    expect(surahs.length, 114);
    for (var i = 0; i < 114; i++) {
      expect(
        surahs[i].length,
        kQuranSurahs[i].verseCount,
        reason: 'سورة ${i + 1}',
      );
    }
    expect(await QuranText.verse('قريش', 5), contains('خَوْف'));
  });

  test('البحث يتجاهل التشكيل', () async {
    final verses = await QuranText.versesOf('الفاتحة');
    final q = QuranText.normalize('الصراط المستقيم');
    expect(verses.indexWhere((v) => QuranText.normalize(v).contains(q)), 4);
  });

  test('فهرس صفحات المصحف', () async {
    final idx = await MushafIndex.load();
    expect(idx.totalPages, 576);
    expect(idx.pageFor(1), 3); // الفاتحة
    expect(idx.pageFor(2), 4); // البقرة
    expect(idx.pageFor(114), 573); // الناس
    expect(idx.surahsOnPage(570), [105, 106]); // الفيل وقريش
    final mid = idx.pageFor(2, 140);
    expect(mid, inInclusiveRange(4, 43));
  });
}
