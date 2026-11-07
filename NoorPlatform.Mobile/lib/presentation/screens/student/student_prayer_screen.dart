import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../blocs/prayer/prayer_cubit.dart';
import '../../blocs/reminder/reminder_cubit.dart';
import '../../widgets/async_body.dart';

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
  bool _inMosque = true;
  bool _onTime = true;
  int _count = 5;
  final _noteCtrl = TextEditingController();
  late String _date;

  @override
  void initState() {
    super.initState();
    _date = Formatters.ymd();
  }

  @override
  void dispose() {
    _noteCtrl.dispose();
    super.dispose();
  }

  bool _isTodayLocked(PrayerState state) {
    return state.logs.any((l) => l.date.startsWith(_date) && l.isLocked);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الصلاة اليومية')),
      body: BlocConsumer<PrayerCubit, PrayerState>(
        listener: (context, state) {
          if (state.successMessage != null) {
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
          final locked = _isTodayLocked(state);
          return AsyncBody(
            isLoading: state.status == PrayerStatus.loading ||
                state.status == PrayerStatus.initial,
            isEmpty: false,
            errorMessage:
                state.status == PrayerStatus.error ? state.error : null,
            onRetry: () => context.read<PrayerCubit>().load(),
            child: ListView(
              padding: const EdgeInsets.all(NoorTheme.spacingMd),
              children: [
                BlocBuilder<ReminderCubit, ReminderState>(
                  builder: (context, reminder) {
                    if (reminder.enabled) return const SizedBox.shrink();
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: Card(
                        color: NoorTheme.primaryGreen.withValues(alpha: 0.1),
                        child: ListTile(
                          leading: const Icon(
                            Icons.notifications_active_outlined,
                            color: NoorTheme.primaryGreen,
                          ),
                          title: const Text('فعّل تذكير تسجيل الصلاة'),
                          subtitle: Text(
                            reminder.supported
                                ? 'تذكير يومي حتى لا يُقفل اليوم دون تسجيل'
                                : 'التذكير يعمل على تطبيق الهاتف (ليس المتصفح)',
                          ),
                          trailing: const Icon(Icons.chevron_left),
                          onTap: () => context.push('/student/settings'),
                        ),
                      ),
                    );
                  },
                ),
                SectionCard(
                  title: 'تسجيل صلاة اليوم (${Formatters.displayDate(_date)})',
                  child: locked
                      ? const ListTile(
                          leading: Icon(Icons.lock, color: NoorTheme.warning),
                          title: Text('تم إرسال سجل اليوم'),
                          subtitle: Text(
                            'السجل مقفل — لا يمكن تعديله إلا بواسطة المحفّظ',
                          ),
                        )
                      : Column(
                          children: [
                            SwitchListTile(
                              title: const Text('صليت في المسجد'),
                              value: _inMosque,
                              onChanged: (v) => setState(() {
                                _inMosque = v;
                                if (v && _count < 1) _count = 5;
                              }),
                            ),
                            SwitchListTile(
                              title: const Text('في الوقت'),
                              value: _onTime,
                              onChanged: (v) => setState(() => _onTime = v),
                            ),
                            if (_inMosque)
                              ListTile(
                                title: const Text('عدد الصلوات في المسجد'),
                                trailing: DropdownButton<int>(
                                  value: _count.clamp(1, 5),
                                  items: List.generate(
                                    5,
                                    (i) {
                                      final n = i + 1;
                                      return DropdownMenuItem(
                                        value: n,
                                        child: Text(Formatters.number(n)),
                                      );
                                    },
                                  ),
                                  onChanged: (v) =>
                                      setState(() => _count = v ?? 5),
                                ),
                              ),
                            TextField(
                              controller: _noteCtrl,
                              decoration: const InputDecoration(
                                labelText: 'ملاحظة (اختياري)',
                              ),
                              maxLines: 2,
                            ),
                            const SizedBox(height: 12),
                            ElevatedButton(
                              onPressed: state.submitting
                                  ? null
                                  : () => context.read<PrayerCubit>().submit(
                                        date: _date,
                                        prayedInMosque: _inMosque,
                                        onTime: _onTime,
                                        mosquePrayerCount:
                                            _inMosque ? _count.clamp(1, 5) : 0,
                                        studentNote:
                                            _noteCtrl.text.trim().isEmpty
                                                ? null
                                                : _noteCtrl.text.trim(),
                                      ),
                              child: state.submitting
                                  ? const SizedBox(
                                      height: 22,
                                      width: 22,
                                      child: CircularProgressIndicator(
                                        strokeWidth: 2,
                                        color: Colors.white,
                                      ),
                                    )
                                  : const Text('إرسال وتقفيل السجل'),
                            ),
                          ],
                        ),
                ),
                SectionCard(
                  title: 'السجل السابق',
                  child: state.logs.isEmpty
                      ? const Text('لا توجد سجلات بعد')
                      : Column(
                          children: state.logs
                              .map(
                                (l) => ListTile(
                                  contentPadding: EdgeInsets.zero,
                                  leading: Icon(
                                    l.isLocked ? Icons.lock : Icons.lock_open,
                                    color: l.prayedInMosque
                                        ? NoorTheme.success
                                        : NoorTheme.warning,
                                  ),
                                  title: Text(Formatters.displayDate(l.date)),
                                  subtitle: Text(
                                    '${l.prayedInMosque ? 'في المسجد' : 'خارج المسجد'}'
                                    ' · ${l.onTime ? 'في الوقت' : 'متأخر'}'
                                    ' · ${Formatters.number(l.mosquePrayerCount)} صلوات',
                                  ),
                                ),
                              )
                              .toList(),
                        ),
                ),
              ],
            ),
          );
        },
      ),
    );
  }
}
