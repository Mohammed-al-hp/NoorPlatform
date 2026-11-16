import 'package:flutter/material.dart';

import '../../config/theme.dart';
import '../../core/offline/offline_queue.dart';
import '../../core/offline/sync_service.dart';
import '../../core/utils/formatters.dart';
import '../../di/injection_container.dart';

/// شارة «بانتظار المزامنة» في شريط المحفّظ: تظهر فقط عند وجود سجلات محفوظة على الهاتف لم تُرفع.
class SyncStatusBadge extends StatefulWidget {
  const SyncStatusBadge({super.key});

  @override
  State<SyncStatusBadge> createState() => _SyncStatusBadgeState();
}

class _SyncStatusBadgeState extends State<SyncStatusBadge> {
  final _queue = sl<OfflineQueue>();
  final _sync = sl<SyncService>();

  @override
  void initState() {
    super.initState();
    _sync.lastMessage.addListener(_onMessage);
  }

  @override
  void dispose() {
    _sync.lastMessage.removeListener(_onMessage);
    super.dispose();
  }

  void _onMessage() {
    final msg = _sync.lastMessage.value;
    if (msg == null || !mounted) return;
    ScaffoldMessenger.maybeOf(context)?.showSnackBar(SnackBar(content: Text(msg)));
    _sync.lastMessage.value = null;
  }

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<int>(
      valueListenable: _queue.pendingCount,
      builder: (context, count, _) {
        final hasRejected = _queue.items.any((o) => o.rejected);
        if (count == 0 && !hasRejected) return const SizedBox.shrink();
        return ValueListenableBuilder<bool>(
          valueListenable: _queue.syncing,
          builder: (context, syncing, _) => Padding(
            padding: const EdgeInsetsDirectional.only(end: 4),
            child: ActionChip(
              avatar: syncing
                  ? const SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2))
                  : Icon(hasRejected && count == 0 ? Icons.error_outline : Icons.cloud_off_rounded,
                      size: 18, color: NoorTheme.accentGold),
              label: Text(count > 0 ? '${Formatters.number(count)} بانتظار المزامنة' : 'سجلات مرفوضة',
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
              backgroundColor: NoorTheme.goldSoft,
              side: const BorderSide(color: NoorTheme.accentGold),
              onPressed: () => showPendingSyncSheet(context),
            ),
          ),
        );
      },
    );
  }
}

Future<void> showPendingSyncSheet(BuildContext context) {
  final queue = sl<OfflineQueue>();
  final sync = sl<SyncService>();
  return showModalBottomSheet<void>(
    context: context,
    isScrollControlled: true,
    builder: (ctx) => StatefulBuilder(
      builder: (ctx, setSheet) {
        final items = queue.items;
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
            child: Column(mainAxisSize: MainAxisSize.min, crossAxisAlignment: CrossAxisAlignment.stretch, children: [
              Row(children: [
                const Icon(Icons.cloud_sync_rounded, color: NoorTheme.primaryGreen),
                const SizedBox(width: 8),
                Expanded(child: Text('سجلات محفوظة على الهاتف', style: Theme.of(ctx).textTheme.titleMedium)),
              ]),
              const SizedBox(height: 4),
              Text('تُرفع تلقائياً عند عودة الإنترنت. يمكنك المزامنة الآن يدوياً.',
                  style: Theme.of(ctx).textTheme.bodySmall),
              const SizedBox(height: 12),
              ConstrainedBox(
                constraints: BoxConstraints(maxHeight: MediaQuery.of(ctx).size.height * 0.5),
                child: items.isEmpty
                    ? const Padding(padding: EdgeInsets.all(24), child: Text('لا توجد سجلات معلّقة', textAlign: TextAlign.center))
                    : ListView.separated(
                        shrinkWrap: true,
                        itemCount: items.length,
                        separatorBuilder: (_, __) => const Divider(height: 1),
                        itemBuilder: (_, i) {
                          final op = items[i];
                          return ListTile(
                            dense: true,
                            leading: Icon(
                              op.kind == PendingKind.attendanceBulk ? Icons.fact_check_outlined : Icons.menu_book_outlined,
                              color: op.rejected ? NoorTheme.error : NoorTheme.primaryGreen,
                            ),
                            title: Text(op.label),
                            subtitle: Text(op.rejected
                                ? 'رفضه الخادم: ${op.lastError ?? ''}'
                                : 'سُجّل ${Formatters.displayDateTime(op.createdAt.toIso8601String())}'),
                            trailing: op.rejected
                                ? IconButton(
                                    tooltip: 'حذف',
                                    icon: const Icon(Icons.delete_outline),
                                    onPressed: () async {
                                      await queue.remove(op.id);
                                      setSheet(() {});
                                    },
                                  )
                                : null,
                          );
                        },
                      ),
              ),
              const SizedBox(height: 12),
              ValueListenableBuilder<bool>(
                valueListenable: queue.syncing,
                builder: (_, syncing, __) => ElevatedButton.icon(
                  onPressed: syncing || queue.pendingCount.value == 0
                      ? null
                      : () async {
                          final r = await sync.syncNow();
                          setSheet(() {});
                          if (r == null && ctx.mounted) {
                            ScaffoldMessenger.maybeOf(ctx)?.showSnackBar(
                              const SnackBar(content: Text('لا يوجد اتصال بالإنترنت حالياً')),
                            );
                          }
                        },
                  icon: const Icon(Icons.sync_rounded),
                  label: Text(syncing ? 'جارٍ المزامنة...' : 'مزامنة الآن'),
                ),
              ),
            ]),
          ),
        );
      },
    ),
  );
}
