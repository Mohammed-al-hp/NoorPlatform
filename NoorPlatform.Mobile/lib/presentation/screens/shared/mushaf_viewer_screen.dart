import 'dart:io';

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:path_provider/path_provider.dart';

import '../../../config/api_config.dart';
import '../../../config/quran_surahs.dart';
import '../../../config/theme.dart';
import '../../../core/quran/quran_text.dart';

/// عارض «مصحف أمانة التعليم» (رواية قالون) — صور الصفحات من الخادم (/mushaf/{n}.jpg) بإذن الجهة الناشرة.
/// كل صفحة تُحفظ على الجهاز بعد أول عرض فتعمل لاحقاً دون إنترنت. التقليب بالاتجاه العربي (التالية من اليسار).
class MushafViewerScreen extends StatefulWidget {
  final int? surah;
  final int? verse;
  final int? page;

  const MushafViewerScreen({super.key, this.surah, this.verse, this.page});

  static Future<void> open(
    BuildContext context, {
    int? surah,
    int? verse,
    int? page,
  }) => Navigator.of(context).push(
    MaterialPageRoute(
      builder: (_) =>
          MushafViewerScreen(surah: surah, verse: verse, page: page),
    ),
  );

  @override
  State<MushafViewerScreen> createState() => _MushafViewerScreenState();
}

class _MushafViewerScreenState extends State<MushafViewerScreen> {
  MushafIndex? _index;
  PageController? _controller;
  int _page = 3;

  @override
  void initState() {
    super.initState();
    MushafIndex.load().then((idx) {
      if (!mounted) return;
      final start =
          widget.page ??
          (widget.surah != null
              ? idx.pageFor(widget.surah!, widget.verse ?? 1)
              : idx.firstQuranPage);
      setState(() {
        _index = idx;
        _page = start.clamp(1, idx.totalPages);
        _controller = PageController(initialPage: _page - 1);
      });
    });
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  String _surahName(int n) => kQuranSurahs[n - 1].name;

  Future<void> _jumpToSurah() async {
    final idx = _index;
    if (idx == null) return;
    final picked = await showModalBottomSheet<int>(
      context: context,
      useSafeArea: true,
      isScrollControlled: true,
      builder: (ctx) => SizedBox(
        height: MediaQuery.of(ctx).size.height * 0.75,
        child: ListView.builder(
          itemCount: 114,
          itemBuilder: (ctx, i) => ListTile(
            leading: CircleAvatar(
              radius: 16,
              child: Text('${i + 1}', style: const TextStyle(fontSize: 12)),
            ),
            title: Text('سورة ${kQuranSurahs[i].name}'),
            trailing: Text('ص ${idx.surahStartPage[i]}'),
            onTap: () => Navigator.pop(ctx, i + 1),
          ),
        ),
      ),
    );
    if (picked != null) {
      _controller?.jumpToPage(idx.surahStartPage[picked - 1] - 1);
    }
  }

  Future<void> _jumpToPage() async {
    final idx = _index;
    if (idx == null) return;
    final ctrl = TextEditingController(text: '$_page');
    final p = await showDialog<int>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('الانتقال إلى صفحة'),
        content: TextField(
          controller: ctrl,
          keyboardType: TextInputType.number,
          autofocus: true,
          decoration: InputDecoration(hintText: '1 – ${idx.totalPages}'),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('إلغاء'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, int.tryParse(ctrl.text)),
            child: const Text('انتقال'),
          ),
        ],
      ),
    );
    ctrl.dispose();
    if (p != null) _controller?.jumpToPage(p.clamp(1, idx.totalPages) - 1);
  }

  @override
  Widget build(BuildContext context) {
    final idx = _index;
    final names =
        idx == null || _page < idx.firstQuranPage || _page > idx.lastQuranPage
        ? const <String>[]
        : idx.surahsOnPage(_page).map(_surahName).toList();
    return Scaffold(
      backgroundColor: const Color(0xFF1E2622),
      appBar: AppBar(
        title: const Text('المصحف — رواية قالون'),
        actions: [
          IconButton(
            tooltip: 'الانتقال إلى سورة',
            icon: const Icon(Icons.menu_book_rounded),
            onPressed: _jumpToSurah,
          ),
          IconButton(
            tooltip: 'الانتقال إلى صفحة',
            icon: const Icon(Icons.numbers_rounded),
            onPressed: _jumpToPage,
          ),
        ],
      ),
      body: idx == null || _controller == null
          ? const Center(child: CircularProgressIndicator())
          : Column(
              children: [
                Expanded(
                  // في الواجهة العربية (RTL) تأتي الصفحة التالية من اليسار كالمصحف الورقي
                  child: PageView.builder(
                    controller: _controller,
                    itemCount: idx.totalPages,
                    onPageChanged: (i) => setState(() => _page = i + 1),
                    itemBuilder: (context, i) => _MushafPage(page: i + 1),
                  ),
                ),
                Container(
                  width: double.infinity,
                  padding: const EdgeInsets.symmetric(
                    vertical: 10,
                    horizontal: 16,
                  ),
                  color: NoorTheme.primaryGreen,
                  child: Text(
                    '${names.isEmpty ? '' : 'سورة ${names.join(' • ')} — '}صفحة $_page من ${idx.totalPages}',
                    textAlign: TextAlign.center,
                    style: const TextStyle(
                      color: Colors.white,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
    );
  }
}

/// صفحة واحدة: من ذاكرة الجهاز إن وُجدت، وإلا تُنزَّل من الخادم وتُحفظ.
class _MushafPage extends StatefulWidget {
  final int page;
  const _MushafPage({required this.page});

  @override
  State<_MushafPage> createState() => _MushafPageState();
}

class _MushafPageState extends State<_MushafPage> {
  late Future<File> _file = _load();

  static final Dio _dio = Dio(
    BaseOptions(
      connectTimeout: ApiConfig.connectTimeout,
      receiveTimeout: ApiConfig.receiveTimeout,
    ),
  );

  Future<File> _load() async {
    final dir = await getApplicationDocumentsDirectory();
    final file = File('${dir.path}/mushaf/${widget.page}.jpg');
    if (await file.exists() && await file.length() > 0) return file;
    await file.parent.create(recursive: true);
    final tmp = File('${file.path}.part');
    await _dio.download(
      '${ApiConfig.baseUrl}/mushaf/${widget.page}.jpg',
      tmp.path,
    );
    return tmp.rename(file.path);
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<File>(
      future: _file,
      builder: (context, snap) {
        if (snap.hasError) {
          return Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Icon(
                  Icons.wifi_off_rounded,
                  color: Colors.white70,
                  size: 40,
                ),
                const SizedBox(height: 8),
                const Text(
                  'تعذر تحميل الصفحة — تحقق من الاتصال',
                  style: TextStyle(color: Colors.white70),
                ),
                TextButton(
                  onPressed: () => setState(() => _file = _load()),
                  child: const Text('إعادة المحاولة'),
                ),
              ],
            ),
          );
        }
        if (!snap.hasData) {
          return const Center(child: CircularProgressIndicator());
        }
        return InteractiveViewer(
          minScale: 1,
          maxScale: 4,
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(8),
              child: Image.file(snap.data!, fit: BoxFit.contain),
            ),
          ),
        );
      },
    );
  }
}
