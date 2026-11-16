import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../data/models/message_models.dart';
import '../../../di/injection_container.dart';
import '../../blocs/portal/portal_cubits.dart';
import '../../widgets/async_body.dart';

class ParentMessagesScreen extends StatelessWidget {
  const ParentMessagesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => MessagesCubit(sl())..load(),
      child: const _MessagesView(),
    );
  }
}

class _MessagesView extends StatelessWidget {
  const _MessagesView();

  Future<void> _compose(BuildContext context) async {
    final cubit = context.read<MessagesCubit>();
    String recipientType = 'Admin';
    int? teacherId;
    final contentCtrl = TextEditingController();
    var submitting = false;

    await showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      isDismissible: !submitting,
      enableDrag: !submitting,
      builder: (ctx) {
        return StatefulBuilder(
          builder: (ctx, setModal) {
            return Padding(
              padding: EdgeInsets.only(
                left: 16,
                right: 16,
                top: 16,
                bottom: MediaQuery.of(ctx).viewInsets.bottom + 16,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    'رسالة جديدة',
                    style: Theme.of(ctx).textTheme.titleLarge,
                  ),
                  const SizedBox(height: 12),
                  DropdownButtonFormField<String>(
                    initialValue: recipientType,
                    decoration: const InputDecoration(labelText: 'المستلم'),
                    items: const [
                      DropdownMenuItem(
                        value: 'Admin',
                        child: Text('إدارة المركز'),
                      ),
                      DropdownMenuItem(
                        value: 'Teacher',
                        child: Text('محفّظ الابن'),
                      ),
                    ],
                    onChanged: submitting
                        ? null
                        : (v) => setModal(() => recipientType = v ?? 'Admin'),
                  ),
                  if (recipientType == 'Teacher') ...[
                    const SizedBox(height: 8),
                    DropdownButtonFormField<int>(
                       initialValue: teacherId,
                      decoration:
                          const InputDecoration(labelText: 'اختر المحفّظ'),
                      items: cubit.state.recipients
                          .map(
                            (r) => DropdownMenuItem(
                              value: r.id,
                              child: Text(r.fullName),
                            ),
                          )
                          .toList(),
                      onChanged: submitting
                          ? null
                          : (v) => setModal(() => teacherId = v),
                    ),
                  ],
                  const SizedBox(height: 8),
                  TextField(
                    controller: contentCtrl,
                    enabled: !submitting,
                    maxLines: 4,
                    decoration: const InputDecoration(
                      labelText: 'نص الرسالة',
                    ),
                  ),
                  const SizedBox(height: 12),
                  ElevatedButton(
                    onPressed: submitting
                        ? null
                        : () async {
                            if (contentCtrl.text.trim().isEmpty) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('يرجى كتابة نص الرسالة'),
                                ),
                              );
                              return;
                            }
                            if (recipientType == 'Teacher' &&
                                teacherId == null) {
                              ScaffoldMessenger.of(context).showSnackBar(
                                const SnackBar(
                                  content: Text('يرجى اختيار المحفّظ'),
                                ),
                              );
                              return;
                            }
                            setModal(() => submitting = true);
                            await cubit.send(
                              SendMessageRequest(
                                recipientType: recipientType,
                                recipientTeacherId: recipientType == 'Teacher'
                                    ? teacherId
                                    : null,
                                content: contentCtrl.text.trim(),
                              ),
                            );
                            if (ctx.mounted) Navigator.pop(ctx);
                          },
                    child: submitting
                        ? const SizedBox(
                            width: 22,
                            height: 22,
                            child: CircularProgressIndicator(
                              strokeWidth: 2,
                              color: Colors.white,
                            ),
                          )
                        : const Text('إرسال'),
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    contentCtrl.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('الرسائل')),
      floatingActionButton: BlocBuilder<MessagesCubit, MessagesState>(
        builder: (context, state) {
          return FloatingActionButton(
            onPressed: state.submitting ? null : () => _compose(context),
            child: const Icon(Icons.edit),
          );
        },
      ),
      body: BlocConsumer<MessagesCubit, MessagesState>(
        listener: (context, state) {
          if (state.successMessage != null) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(content: Text(state.successMessage!)),
            );
          }
          if (state.error != null &&
              state.status != PortalLoadStatus.error) {
            ScaffoldMessenger.of(context).showSnackBar(
              SnackBar(
                content: Text(state.error!),
                backgroundColor: NoorTheme.error,
              ),
            );
          }
        },
        builder: (context, state) {
          return AsyncBody(
              pullToRefresh: false,
            isLoading: state.status == PortalLoadStatus.loading ||
                state.status == PortalLoadStatus.initial,
            isEmpty: state.status == PortalLoadStatus.loaded &&
                state.messages.isEmpty,
            emptyTitle: 'لا توجد رسائل مرسلة',
            emptySubtitle: 'اضغط زر التحرير لإرسال رسالة',
            errorMessage:
                state.status == PortalLoadStatus.error ? state.error : null,
            onRetry: () => context.read<MessagesCubit>().load(),
            child: RefreshIndicator(
              onRefresh: () => context.read<MessagesCubit>().load(),
              child: ListView.builder(
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.messages.length,
                itemBuilder: (context, i) {
                  final m = state.messages[i];
                  return SectionCard(
                    title: m.recipientName,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(m.content),
                        const SizedBox(height: 8),
                        Row(
                          children: [
                            Text(
                              Formatters.displayDateTime(m.createdAt),
                              style: Theme.of(context).textTheme.bodySmall,
                            ),
                            const Spacer(),
                            if (m.replies.isEmpty)
                              Text(
                                m.isRead ? 'تمت القراءة' : 'بانتظار الرد',
                                style: Theme.of(context).textTheme.bodySmall,
                              ),
                          ],
                        ),
                        for (final r in m.replies) _ReplyBubble(reply: r),
                      ],
                    ),
                  );
                },
              ),
            ),
          );
        },
      ),
    );
  }
}

/// فقاعة الرد من المحفّظ/الإدارة أسفل رسالة ولي الأمر.
class _ReplyBubble extends StatelessWidget {
  final MessageReplyModel reply;
  const _ReplyBubble({required this.reply});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      width: double.infinity,
      margin: const EdgeInsetsDirectional.only(top: 10, start: 16),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: NoorTheme.primaryGreen.withValues(alpha: 0.07),
        borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
        // حدّ موحّد اللون: Flutter لا يسمح بـ borderRadius مع حدود غير موحّدة
        border: Border.all(color: NoorTheme.accentGold.withValues(alpha: 0.45)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.reply_rounded, size: 16, color: NoorTheme.primaryGreen),
              const SizedBox(width: 4),
              Expanded(
                child: Text('رد: ${reply.senderName}',
                    style: theme.textTheme.labelLarge?.copyWith(color: NoorTheme.primaryGreen)),
              ),
              Text(Formatters.displayDateTime(reply.createdAt), style: theme.textTheme.bodySmall),
            ],
          ),
          const SizedBox(height: 6),
          Text(reply.content),
        ],
      ),
    );
  }
}
