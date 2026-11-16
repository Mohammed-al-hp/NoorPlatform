import 'package:flutter/material.dart';
import '../../../config/theme.dart';

class AsyncBody extends StatelessWidget {
  final bool isLoading;
  final bool isEmpty;
  final String? errorMessage;
  final VoidCallback? onRetry;
  final String emptyTitle;
  final String emptySubtitle;
  final IconData emptyIcon;
  final Widget child;

  /// السحب للتحديث تلقائياً عند توفّر onRetry. يُعطَّل في الشاشات التي تضع RefreshIndicator خاصاً بها.
  final bool pullToRefresh;

  const AsyncBody({
    super.key,
    required this.isLoading,
    required this.isEmpty,
    required this.child,
    this.errorMessage,
    this.onRetry,
    this.emptyTitle = 'لا توجد بيانات',
    this.emptySubtitle = '',
    this.emptyIcon = Icons.inbox_outlined,
    this.pullToRefresh = true,
  });

  @override
  Widget build(BuildContext context) {
    if (isLoading) {
      return const Center(child: CircularProgressIndicator(strokeWidth: 3));
    }
    if (errorMessage != null && errorMessage!.isNotEmpty) {
      return ErrorState(message: errorMessage!, onRetry: onRetry);
    }
    final canRefresh = pullToRefresh && onRetry != null;
    if (isEmpty) {
      final empty = EmptyState(
        title: emptyTitle,
        subtitle: emptySubtitle,
        icon: emptyIcon,
      );
      if (!canRefresh) return empty;
      // الحالة الفارغة قابلة للسحب أيضاً (تحتاج عنصراً قابلاً للتمرير بارتفاع الشاشة)
      return RefreshIndicator(
        onRefresh: _refresh,
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            child: ConstrainedBox(
              constraints: BoxConstraints(minHeight: constraints.maxHeight),
              child: empty,
            ),
          ),
        ),
      );
    }
    if (canRefresh) {
      return RefreshIndicator(onRefresh: _refresh, child: child);
    }
    return child;
  }

  Future<void> _refresh() async {
    onRetry?.call();
    // مهلة قصيرة لإظهار مؤشر التحديث بينما يبدأ الـ Cubit التحميل
    await Future<void>.delayed(const Duration(milliseconds: 400));
  }
}

/// حالة فارغة: أيقونة داخل دائرة ملوّنة خفيفة + عنوان ووصف.
class EmptyState extends StatelessWidget {
  final String title;
  final String subtitle;
  final IconData icon;

  const EmptyState({
    super.key,
    required this.title,
    this.subtitle = '',
    this.icon = Icons.inbox_outlined,
  });

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(NoorTheme.spacingXl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 96,
              height: 96,
              decoration: BoxDecoration(color: scheme.primaryContainer, shape: BoxShape.circle),
              child: Icon(icon, size: 44, color: scheme.primary),
            ),
            const SizedBox(height: 20),
            Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleMedium),
            if (subtitle.isNotEmpty) ...[
              const SizedBox(height: 8),
              Text(subtitle, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
            ],
          ],
        ),
      ),
    );
  }
}

/// حالة خطأ برسالة عربية واضحة وزر إعادة المحاولة.
class ErrorState extends StatelessWidget {
  final String message;
  final VoidCallback? onRetry;

  const ErrorState({super.key, required this.message, this.onRetry});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(NoorTheme.spacingXl),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Container(
              width: 96,
              height: 96,
              decoration: BoxDecoration(color: NoorTheme.error.withValues(alpha: 0.08), shape: BoxShape.circle),
              child: const Icon(Icons.cloud_off_rounded, size: 44, color: NoorTheme.error),
            ),
            const SizedBox(height: 20),
            Text('تعذّر تحميل البيانات', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            Text(message, textAlign: TextAlign.center, style: Theme.of(context).textTheme.bodyMedium),
            if (onRetry != null) ...[
              const SizedBox(height: 20),
              FilledButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh_rounded),
                label: const Text('إعادة المحاولة'),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// بطاقة قسم: زوايا دائرية، حدّ خفيف وظل ناعم، وعنوان بعلامة ذهبية.
class SectionCard extends StatelessWidget {
  final String? title;
  final Widget child;
  final EdgeInsetsGeometry padding;
  final List<Widget>? actions;
  final IconData? icon;

  const SectionCard({
    super.key,
    this.title,
    required this.child,
    this.padding = const EdgeInsets.all(NoorTheme.spacingMd + 2),
    this.actions,
    this.icon,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: NoorTheme.spacingMd),
      padding: padding,
      decoration: BoxDecoration(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(NoorTheme.radiusLg + 4),
        border: Border.all(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.7)),
        boxShadow: NoorTheme.cardShadow,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null) ...[
            Row(
              children: [
                Container(
                  width: 4,
                  height: 18,
                  decoration: BoxDecoration(
                    gradient: NoorTheme.goldGradient,
                    borderRadius: BorderRadius.circular(4),
                  ),
                ),
                const SizedBox(width: 10),
                if (icon != null) ...[
                  Icon(icon, size: 20, color: theme.colorScheme.primary),
                  const SizedBox(width: 6),
                ],
                Expanded(child: Text(title!, style: theme.textTheme.titleMedium)),
                ...?actions,
              ],
            ),
            const SizedBox(height: NoorTheme.spacingMd - 4),
          ],
          child,
        ],
      ),
    );
  }
}

/// بطاقة إحصائية: أيقونة داخل مربع ملوّن خفيف + قيمة بارزة + وصف.
class StatChip extends StatelessWidget {
  final String label;
  final String value;
  final IconData icon;
  final Color? color;

  const StatChip({
    super.key,
    required this.label,
    required this.value,
    required this.icon,
    this.color,
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = color ?? theme.colorScheme.primary;
    return ConstrainedBox(
      constraints: const BoxConstraints(minWidth: 148),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: theme.colorScheme.surface,
          borderRadius: BorderRadius.circular(NoorTheme.radiusLg),
          border: Border.all(color: c.withValues(alpha: 0.18)),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Container(
              width: 40,
              height: 40,
              decoration: BoxDecoration(
                color: c.withValues(alpha: 0.12),
                borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
              ),
              child: Icon(icon, size: 22, color: c),
            ),
            const SizedBox(width: 10),
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  value,
                  style: theme.textTheme.titleLarge?.copyWith(
                    color: c,
                    fontWeight: FontWeight.w800,
                    height: 1.1,
                    fontFeatures: const [FontFeature.tabularFigures()],
                  ),
                ),
                const SizedBox(height: 2),
                Text(label, style: theme.textTheme.bodySmall),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

/// عنصر قائمة «المزيد»: بطاقة بأيقونة داخل مربع ملوّن وسهم.
class MoreMenuTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String? subtitle;
  final VoidCallback onTap;
  final int badgeCount;
  final Color? color;
  final EdgeInsetsGeometry margin;

  const MoreMenuTile({
    super.key,
    required this.icon,
    required this.title,
    required this.onTap,
    this.subtitle,
    this.badgeCount = 0,
    this.color,
    this.margin = const EdgeInsets.symmetric(horizontal: NoorTheme.spacingMd, vertical: 5),
  });

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final c = color ?? theme.colorScheme.primary;
    return Padding(
      padding: margin,
      child: Material(
        color: theme.colorScheme.surface,
        borderRadius: BorderRadius.circular(NoorTheme.radiusLg),
        child: InkWell(
          borderRadius: BorderRadius.circular(NoorTheme.radiusLg),
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(NoorTheme.radiusLg),
              border: Border.all(color: theme.colorScheme.outlineVariant.withValues(alpha: 0.7)),
            ),
            child: Row(
              children: [
                Badge(
                  isLabelVisible: badgeCount > 0,
                  label: Text(badgeCount > 99 ? '99+' : '$badgeCount'),
                  backgroundColor: NoorTheme.accentGold,
                  child: Container(
                    width: 42,
                    height: 42,
                    decoration: BoxDecoration(
                      color: c.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(NoorTheme.radiusMd),
                    ),
                    child: Icon(icon, color: c, size: 22),
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title, style: theme.textTheme.titleSmall),
                      if (subtitle != null) ...[
                        const SizedBox(height: 2),
                        Text(subtitle!, style: theme.textTheme.bodySmall),
                      ],
                    ],
                  ),
                ),
                Icon(Icons.chevron_left_rounded, color: theme.colorScheme.outline),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
