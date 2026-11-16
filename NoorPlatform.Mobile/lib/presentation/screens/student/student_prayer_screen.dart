import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/pedagogical_models.dart';
import '../../../di/injection_container.dart';
import '../../blocs/prayer/prayer_cubit.dart';
import '../../blocs/reminder/reminder_cubit.dart';
import '../../widgets/async_body.dart';

/// تسجيل الصلاة اليومية صلاةً صلاة: صلّاها؟ في المسجد أم مكان آخر؟ في وقتها أم قضاءً؟
/// لا يمكن تسجيل صلاة لم يدخل وقت أذانها بعد في مصراتة (يتحقق الخادم أيضاً).
class StudentPrayerScreen extends StatelessWidget {
  const StudentPrayerScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => PrayerCubit(sl())..load(),
      child: const _PrayerView(),
    );
  }
}

class _PrayerView extends StatefulWidget {
  const _PrayerView();

  @override
  State<_PrayerView> createState() => _PrayerViewState();
}

class _PrayerViewState extends State<_PrayerView> {
  final _noteCtrl = TextEditingController();

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  Future<void> _pickDate(BuildContext context, PrayerState state) async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: DateTime.tryParse(state.date) ?? now,
      firstDate: now.subtract(const Duration(days: 60)),
      lastDate: now,
      helpText: 'تاريخ الصلاة',
    );
    if (picked != null && context.mounted) {
      context.read<PrayerCubit>().load(date: Formatters.ymd(picked));
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الصلاة اليومية')),
      body: BlocConsumer<PrayerCubit, PrayerState>(
        listenWhen: (a, b) =>
            a.successMessage != b.successMessage || a.error != b.error,
        listener: (context, state) {
          if (state.successMessage != null) {
            _noteCtrl.clear();
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(state.successMessage!)),
            );
          }
          if (state.error != null && state.status != PrayerStatus.error) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.error!),
                backgroundColor: NoorTheme.error,
              ),
            );
          }
        },
        builder: (context, state) {
          final times = state.times;
          final dayLog = state.dayLog;
          final legacyLocked =
              dayLog != null && dayLog.entries.isEmpty && dayLog.isLocked;
          final hasOpenPrayer = times?.prayers.any((p) =>
                  p.started && dayLog?.entryFor(p.prayer) == null) ??
              false;

          return AsyncBody(
            isLoading: state.status == PrayerStatus.loading ||
                state.status == PrayerStatus.initial,
            isEmpty: false,
            errorMessage:
                state.status == PrayerStatus.error ? state.error : null,
            onRetry: () => context.read<PrayerCubit>().load(),
            child: RefreshIndicator(
              onRefresh: () => context.read<PrayerCubit>().load(),
              child: ListView(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                children: [
                  const _HonestyBanner(),
                  const SizedBox(height: 12),
                  const _ReminderHint(),
                  SectionCard(
                    title: 'تسجيل صلاة اليوم',
                    icon: Icons.mosque_outlined,
                    actions: [
                      TextButton.icon(
                        onPressed: () => _pickDate(context, state),
                        icon: const Icon(Icons.calendar_month, size: 18),
                        label: Text(Formatters.displayDate(state.date)),
                      ),
                    ],
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text(
                          times?.next != null
                              ? 'الصلاة القادمة: ${times!.next!.name} ${times.next!.time} · بتوقيت ${times.city}'
                              : 'مواقيت الأذان بتوقيت ${times?.city ?? 'مصراتة'}',
                          style: const TextStyle(
                            fontSize: 12,
                            color: NoorTheme.textSecondary,
                          ),
                        ),
                        const SizedBox(height: 12),
                        if (times == null)
                          const Text('تعذر تحميل مواقيت الصلاة — اسحب للتحديث')
                        else
                          ...times.prayers.map((p) => Padding(
                                padding: const EdgeInsets.only(bottom: 10),
                                child: _PrayerRow(
                                  item: p,
                                  recorded: dayLog?.entryFor(p.prayer),
                                  legacyLocked: legacyLocked,
                                  choice: state.choices[p.prayer],
                                ),
                              )),
                        if (hasOpenPrayer && !legacyLocked) ...[
                          TextField(
                            controller: _noteCtrl,
                            decoration: const InputDecoration(
                              labelText: 'ملاحظة (اختياري)',
                            ),
                            maxLength: 200,
                          ),
                          const SizedBox(height: 4),
                          ElevatedButton.icon(
                            onPressed: state.submitting
                                ? null
                                : () => context.read<PrayerCubit>().submit(
                                      studentNote: _noteCtrl.text.trim().isEmpty
                                          ? null
                                          : _noteCtrl.text.trim(),
                                    ),
                            icon: state.submitting
                                ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                      color: Colors.white,
                                    ),
                                  )
                                : const Icon(Icons.check_circle_outline),
                            label: const Text('تسجيل الصلوات المحددة'),
                          ),
                          const SizedBox(height: 8),
                          const Text(
                            'كل صلاة تُقفل بعد تسجيلها ولا يعدّلها إلا المحفّظ.',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontSize: 12,
                              color: NoorTheme.textSecondary,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ),
                  SectionCard(
                    title: 'السجل السابق',
                    icon: Icons.history,
                    child: state.logs.isEmpty
                        ? const Text('لا توجد سجلات بعد')
                        : Column(
                            children: state.logs
                                .take(30)
                                .map((l) => _LogTile(log: l))
                                .toList(),
                          ),
                  ),
                ],
              ),
            ),
          );
        },
      ),
    );
  }
}

class _HonestyBanner extends StatelessWidget {
  const _HonestyBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [NoorTheme.goldSoft, Color(0xFFFFF8E6)],
        ),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: NoorTheme.accentGold, width: 2),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.warning_amber_rounded,
              color: NoorTheme.accentGold, size: 26),
          const SizedBox(width: 10),
          Flexible(
            child: Text(
              'اِنْتَبِهْ!! الْأَهَمُّ أَنْ تَكُونَ صَادِقاً.',
              textAlign: TextAlign.center,
              style: NoorTheme.quran(size: 22, color: const Color(0xFF8B6508))
                  .copyWith(fontWeight: FontWeight.w700, height: 1.6),
            ),
          ),
        ],
      ),
    );
  }
}

class _ReminderHint extends StatelessWidget {
  const _ReminderHint();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<ReminderCubit, ReminderState>(
      builder: (context, reminder) {
        if (reminder.enabled) return const SizedBox.shrink();
        return Padding(
          padding: const EdgeInsets.only(bottom: 12),
          child: Card(
            color: NoorTheme.primaryGreen.withValues(alpha: 0.08),
            child: ListTile(
              leading: const Icon(
                Icons.notifications_active_outlined,
                color: NoorTheme.primaryGreen,
              ),
              title: const Text('فعّل تذكير الصلاة والورد اليومي'),
              subtitle: Text(
                reminder.supported
                    ? 'تنبيه عند كل أذان وتذكير بقراءة الورد'
                    : 'التذكير يعمل على تطبيق الهاتف (ليس المتصفح)',
              ),
              trailing: const Icon(Icons.chevron_left),
              onTap: () => context.push('/student/settings'),
            ),
          ),
        );
      },
    );
  }
}

class _PrayerRow extends StatelessWidget {
  final PrayerTimeItem item;
  final PrayerEntryModel? recorded;
  final bool legacyLocked;
  final PrayerChoice? choice;

  const _PrayerRow({
    required this.item,
    required this.recorded,
    required this.legacyLocked,
    required this.choice,
  });

  @override
  Widget build(BuildContext context) {
    final cubit = context.read<PrayerCubit>();
    final locked = recorded != null || legacyLocked;
    final pending = !item.started && !locked;

    Color border = NoorTheme.borderLight;
    Color bg = Theme.of(context).cardColor;
    if (locked) {
      bg = NoorTheme.greenSoft;
      border = Colors.transparent;
    } else if (choice != null) {
      border = choice!.prayed ? NoorTheme.primaryGreen : NoorTheme.error;
    }

    return Opacity(
      opacity: pending ? 0.65 : 1,
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: bg,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: border, width: choice != null ? 1.6 : 1),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Text(item.name,
                    style: const TextStyle(
                        fontSize: 16, fontWeight: FontWeight.w800)),
                const SizedBox(width: 8),
                Text(item.time,
                    textDirection: TextDirection.ltr,
                    style: const TextStyle(
                        fontSize: 12, color: NoorTheme.textSecondary)),
                const Spacer(),
                if (locked)
                  const Row(children: [
                    Icon(Icons.lock, size: 14, color: NoorTheme.textSecondary),
                    SizedBox(width: 3),
                    Text('مقفلة',
                        style: TextStyle(
                            fontSize: 11, color: NoorTheme.textSecondary)),
                  ]),
              ],
            ),
            const SizedBox(height: 8),
            if (locked)
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: recorded != null
                    ? _entryTags(recorded!)
                    : [const _Tag('مسجّل سابقاً')],
              )
            else if (pending)
              _Tag('لم يحن وقتها بعد — يؤذَّن ${item.time}',
                  icon: Icons.schedule)
            else ...[
              _Seg(
                options: const ['صلّيتها', 'لم أصلّها'],
                selected: choice == null ? null : (choice!.prayed ? 0 : 1),
                activeColors: const [NoorTheme.primaryGreen, NoorTheme.error],
                onSelect: (i) => cubit.choose(
                  item.prayer,
                  (choice ?? const PrayerChoice(prayed: true))
                      .copyWith(prayed: i == 0),
                ),
              ),
              if (choice?.prayed == true) ...[
                const SizedBox(height: 8),
                _Seg(
                  options: const ['في المسجد', 'مكان آخر'],
                  selected: choice!.inMosque ? 0 : 1,
                  activeColors: const [
                    NoorTheme.primaryGreen,
                    NoorTheme.accentGold
                  ],
                  onSelect: (i) => cubit.choose(
                      item.prayer, choice!.copyWith(inMosque: i == 0)),
                ),
                const SizedBox(height: 8),
                _Seg(
                  options: const ['في وقتها', 'قضاءً'],
                  selected: choice!.onTime ? 0 : 1,
                  activeColors: const [
                    NoorTheme.primaryGreen,
                    NoorTheme.accentGold
                  ],
                  onSelect: (i) => cubit.choose(
                      item.prayer, choice!.copyWith(onTime: i == 0)),
                ),
              ],
            ],
          ],
        ),
      ),
    );
  }
}

List<Widget> _entryTags(PrayerEntryModel e) {
  if (!e.prayed) return const [_Tag('لم يصلّها', color: NoorTheme.error)];
  return [
    _Tag(e.inMosque ? 'في المسجد' : 'مكان آخر',
        color: e.inMosque ? NoorTheme.primaryGreen : NoorTheme.accentGold),
    _Tag(e.onTime ? 'في وقتها' : 'قضاءً',
        color: e.onTime ? NoorTheme.primaryGreen : NoorTheme.accentGold),
  ];
}

class _Seg extends StatelessWidget {
  final List<String> options;
  final int? selected;
  final List<Color> activeColors;
  final ValueChanged<int> onSelect;

  const _Seg({
    required this.options,
    required this.selected,
    required this.activeColors,
    required this.onSelect,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(3),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: NoorTheme.borderLight),
      ),
      child: Row(
        children: List.generate(options.length, (i) {
          final active = selected == i;
          return Expanded(
            child: InkWell(
              borderRadius: BorderRadius.circular(9),
              onTap: () => onSelect(i),
              child: AnimatedContainer(
                duration: const Duration(milliseconds: 180),
                padding: const EdgeInsets.symmetric(vertical: 9),
                decoration: BoxDecoration(
                  color: active ? activeColors[i] : Colors.transparent,
                  borderRadius: BorderRadius.circular(9),
                ),
                alignment: Alignment.center,
                child: Text(
                  options[i],
                  style: TextStyle(
                    fontWeight: FontWeight.w700,
                    fontSize: 13,
                    color: active ? Colors.white : NoorTheme.textSecondary,
                  ),
                ),
              ),
            ),
          );
        }),
      ),
    );
  }
}

class _Tag extends StatelessWidget {
  final String text;
  final Color? color;
  final IconData? icon;

  const _Tag(this.text, {this.color, this.icon});

  @override
  Widget build(BuildContext context) {
    final c = color ?? NoorTheme.textSecondary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: c.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 13, color: c),
            const SizedBox(width: 4),
          ],
          Flexible(
            child: Text(text,
                style: TextStyle(
                    fontSize: 11, fontWeight: FontWeight.w700, color: c)),
          ),
        ],
      ),
    );
  }
}

class _LogTile extends StatelessWidget {
  final PrayerLogModel log;
  const _LogTile({required this.log});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Text(Formatters.displayDate(log.date),
                  style: const TextStyle(fontWeight: FontWeight.w800)),
              const Spacer(),
              if (log.prayedCount != null)
                _Tag(
                    'صلّى ${Formatters.number(log.prayedCount!)}/٥ · جماعة ${Formatters.number(log.mosquePrayerCount)}'),
            ],
          ),
          const SizedBox(height: 6),
          if (log.entries.isNotEmpty)
            ...log.entries.map((e) => Padding(
                  padding: const EdgeInsets.only(bottom: 4),
                  child: Row(
                    children: [
                      SizedBox(
                        width: 56,
                        child: Text(e.arabicName,
                            style:
                                const TextStyle(fontWeight: FontWeight.w700)),
                      ),
                      Expanded(
                        child: Wrap(
                            spacing: 6, runSpacing: 4, children: _entryTags(e)),
                      ),
                    ],
                  ),
                ))
          else
            Text(
              '${log.prayedInMosque ? 'في المسجد' : 'خارج المسجد'}'
              ' · ${log.onTime ? 'في الوقت' : 'متأخر'}'
              ' · ${Formatters.number(log.mosquePrayerCount)} صلوات',
              style: const TextStyle(color: NoorTheme.textSecondary),
            ),
          if (log.sheikhOverrideNote != null &&
              log.sheikhOverrideNote!.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text('ملاحظة المحفّظ: ${log.sheikhOverrideNote}',
                  style: const TextStyle(
                      fontSize: 12, color: NoorTheme.textSecondary)),
            ),
          const Divider(height: 16),
        ],
      ),
    );
  }
}
