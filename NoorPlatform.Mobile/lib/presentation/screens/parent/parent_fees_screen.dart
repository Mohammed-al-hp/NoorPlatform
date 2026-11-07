import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../config/theme.dart';
import '../../../core/utils/formatters.dart';
import '../../../di/injection_container.dart';
import '../../blocs/portal/portal_cubits.dart';
import '../../widgets/async_body.dart';

class ParentFeesScreen extends StatelessWidget {
  const ParentFeesScreen({super.key});

  Color _statusColor(String status) {
    switch (status) {
      case 'Paid':
        return NoorTheme.success;
      case 'Overdue':
        return NoorTheme.error;
      default:
        return NoorTheme.warning;
    }
  }

  String _statusLabel(String status) {
    switch (status) {
      case 'Paid':
        return 'مدفوعة';
      case 'Overdue':
        return 'متأخرة';
      case 'Pending':
        return 'بانتظار الدفع';
      default:
        return status;
    }
  }

  @override
  Widget build(BuildContext context) {
    return BlocProvider(
      create: (_) => PaymentsCubit(sl())..load(),
      child: Scaffold(
        appBar: AppBar(title: const Text('الرسوم الدراسية')),
        body: BlocBuilder<PaymentsCubit, PaymentsState>(
          builder: (context, state) {
            return AsyncBody(
              isLoading: state.status == PortalLoadStatus.loading ||
                  state.status == PortalLoadStatus.initial,
              isEmpty: state.status == PortalLoadStatus.loaded &&
                  state.payments.isEmpty,
              emptyTitle: 'لا توجد فواتير',
              errorMessage:
                  state.status == PortalLoadStatus.error ? state.error : null,
              onRetry: () => context.read<PaymentsCubit>().load(),
              child: ListView.builder(
                padding: const EdgeInsets.all(NoorTheme.spacingMd),
                itemCount: state.payments.length,
                itemBuilder: (context, i) {
                  final p = state.payments[i];
                  return SectionCard(
                    title: p.studentName,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(p.description),
                        const SizedBox(height: 6),
                        Text(
                          'المبلغ: ${Formatters.number(p.amount, fractionDigits: 2)} د.ل',
                        ),
                        Text('الاستحقاق: ${Formatters.displayDate(p.dueDate)}'),
                        const SizedBox(height: 8),
                        Chip(
                          label: Text(_statusLabel(p.status)),
                          backgroundColor:
                              _statusColor(p.status).withValues(alpha: 0.15),
                          labelStyle: TextStyle(color: _statusColor(p.status)),
                        ),
                        if (!p.isPaid) ...[
                          const SizedBox(height: 10),
                          Container(
                            width: double.infinity,
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                              color: NoorTheme.warning.withValues(alpha: 0.08),
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: const Text(
                              'السداد يتم في المركز، وتُحدَّث الحالة بعد استلام الإدارة.',
                              style: TextStyle(fontSize: 12),
                            ),
                          ),
                        ],
                      ],
                    ),
                  );
                },
              ),
            );
          },
        ),
      ),
    );
  }
}
