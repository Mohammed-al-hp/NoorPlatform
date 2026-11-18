import 'package:flutter/material.dart';

import '../../config/theme.dart';
import '../../core/quran/quran_text.dart';

/// حقل اختيار آية بنصّها: يعرض «رقم — أول كلمات الآية»، وعند الضغط تُفتح قائمة آيات السورة مع البحث بكلمة.
class VersePickerField extends StatelessWidget {
  final String label;
  final String surahName;
  final int verse;
  final ValueChanged<int> onChanged;

  const VersePickerField({
    super.key,
    required this.label,
    required this.surahName,
    required this.verse,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<String>(
      future: QuranText.verse(surahName, verse),
      builder: (context, snap) {
        final text = snap.data ?? '';
        return InkWell(
          borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
          onTap: () async {
            final picked = await showVersePicker(
              context,
              surahName: surahName,
              selected: verse,
              title: label,
            );
            if (picked != null) onChanged(picked);
          },
          child: InputDecorator(
            decoration: InputDecoration(
              labelText: label,
              suffixIcon: const Icon(Icons.unfold_more_rounded),
            ),
            child: Row(
              children: [
                _VerseNumber(verse),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    text.isEmpty ? 'آية $verse' : QuranText.lead(text, 5),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontFamily: QuranText.fontFamily,
                      fontSize: 17,
                    ),
                  ),
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

/// قائمة آيات السورة للاختيار (مع البحث بكلمة دون اعتبار للتشكيل). تعيد رقم الآية المختارة.
Future<int?> showVersePicker(
  BuildContext context, {
  required String surahName,
  int? selected,
  String title = 'اختر الآية',
}) {
  return showModalBottomSheet<int>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (ctx) => _VersePickerSheet(
      surahName: surahName,
      selected: selected,
      title: title,
    ),
  );
}

class _VersePickerSheet extends StatefulWidget {
  final String surahName;
  final int? selected;
  final String title;
  const _VersePickerSheet({
    required this.surahName,
    this.selected,
    required this.title,
  });

  @override
  State<_VersePickerSheet> createState() => _VersePickerSheetState();
}

class _VersePickerSheetState extends State<_VersePickerSheet> {
  List<String> _verses = const [];
  String _query = '';
  final _scroll = ScrollController();

  @override
  void initState() {
    super.initState();
    QuranText.versesOf(widget.surahName).then((v) {
      if (!mounted) return;
      setState(() => _verses = v);
      // الانتقال للآية المختارة حالياً
      final sel = widget.selected;
      if (sel != null && sel > 3) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (_scroll.hasClients) {
            _scroll.jumpTo(
              ((sel - 3) * 72.0).clamp(0, _scroll.position.maxScrollExtent),
            );
          }
        });
      }
    });
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final q = QuranText.normalize(_query);
    final items = <int>[
      for (var i = 0; i < _verses.length; i++)
        if (q.isEmpty || QuranText.normalize(_verses[i]).contains(q)) i + 1,
    ];
    return SizedBox(
      height: MediaQuery.of(context).size.height * 0.85,
      child: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  '${widget.title} — سورة ${widget.surahName}',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                TextField(
                  decoration: const InputDecoration(
                    prefixIcon: Icon(Icons.search_rounded),
                    hintText: 'ابحث بكلمة من الآية...',
                  ),
                  onChanged: (v) => setState(() => _query = v),
                ),
              ],
            ),
          ),
          Expanded(
            child: _verses.isEmpty
                ? const Center(child: CircularProgressIndicator())
                : items.isEmpty
                ? const Center(child: Text('لا توجد آية بهذه الكلمة في السورة'))
                : ListView.separated(
                    controller: _scroll,
                    itemCount: items.length,
                    separatorBuilder: (_, __) => const Divider(height: 1),
                    itemBuilder: (context, i) {
                      final n = items[i];
                      final selected = n == widget.selected;
                      return ListTile(
                        selected: selected,
                        leading: _VerseNumber(n),
                        title: Text(
                          _verses[n - 1],
                          style: const TextStyle(
                            fontFamily: QuranText.fontFamily,
                            fontSize: 19,
                            height: 1.9,
                          ),
                        ),
                        onTap: () => Navigator.pop(context, n),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}

class _VerseNumber extends StatelessWidget {
  final int number;
  const _VerseNumber(this.number);

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 34,
      height: 34,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        color: NoorTheme.primaryGreen.withValues(alpha: 0.1),
        border: Border.all(color: NoorTheme.accentGold.withValues(alpha: 0.6)),
      ),
      child: Text(
        '$number',
        style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13),
      ),
    );
  }
}
