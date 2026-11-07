import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/storage/app_preferences.dart';
import '../../../data/models/announcement_models.dart';
import '../../../domain/repositories/parent_repository.dart';
import '../../../domain/repositories/portal_repository.dart';

enum InboxKind { announcement, payment }

class InboxItem extends Equatable {
  final String id;
  final InboxKind kind;
  final String title;
  final String body;
  final String? date;
  final bool isRead;
  final String? actionRoute;

  const InboxItem({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    this.date,
    required this.isRead,
    this.actionRoute,
  });

  InboxItem copyWith({bool? isRead}) => InboxItem(
        id: id,
        kind: kind,
        title: title,
        body: body,
        date: date,
        isRead: isRead ?? this.isRead,
        actionRoute: actionRoute,
      );

  @override
  List<Object?> get props => [id, kind, title, body, date, isRead, actionRoute];
}

enum NotificationsStatus { initial, loading, loaded, error }

class NotificationsState extends Equatable {
  final NotificationsStatus status;
  final List<InboxItem> items;
  final String? error;
  final bool isParent;

  const NotificationsState({
    this.status = NotificationsStatus.initial,
    this.items = const [],
    this.error,
    this.isParent = false,
  });

  int get unreadCount => items.where((e) => !e.isRead).length;

  NotificationsState copyWith({
    NotificationsStatus? status,
    List<InboxItem>? items,
    String? error,
    bool? isParent,
    bool clearError = false,
  }) =>
      NotificationsState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
        isParent: isParent ?? this.isParent,
      );

  @override
  List<Object?> get props => [status, items, error, isParent];
}

class NotificationsCubit extends Cubit<NotificationsState> {
  final PortalRepository portalRepository;
  final ParentRepository parentRepository;
  final AppPreferences preferences;

  NotificationsCubit({
    required this.portalRepository,
    required this.parentRepository,
    required this.preferences,
  }) : super(const NotificationsState());

  Future<void> refresh({required bool isParent}) async {
    emit(state.copyWith(
      status: NotificationsStatus.loading,
      isParent: isParent,
      clearError: true,
    ));

    final readIds = await preferences.getReadInboxIds();
    final items = <InboxItem>[];

    final announcements = await portalRepository.getAnnouncements();
    String? error;
    announcements.fold(
      (f) => error = f.message,
      (list) {
        for (final a in list.where((e) => e.isActive)) {
          items.add(_fromAnnouncement(a, readIds, isParent));
        }
      },
    );

    if (isParent) {
      final summary = await parentRepository.getParentSummary();
      summary.fold(
        (f) => error ??= f.message,
        (data) {
          for (final alert in data.alerts) {
            final id = 'payment:${alert.id}';
            items.add(
              InboxItem(
                id: id,
                kind: InboxKind.payment,
                title: 'تنبيه فاتورة',
                body: alert.message.isNotEmpty
                    ? alert.message
                    : 'لديك فاتورة مستحقة بمبلغ ${alert.amount}',
                isRead: readIds.contains(id),
                actionRoute: '/parent/fees',
              ),
            );
          }
        },
      );
    }

    // Newest first when date available; payments first among undated.
    items.sort((a, b) {
      if (a.isRead != b.isRead) return a.isRead ? 1 : -1;
      final ad = a.date ?? '';
      final bd = b.date ?? '';
      return bd.compareTo(ad);
    });

    if (error != null && items.isEmpty) {
      emit(state.copyWith(status: NotificationsStatus.error, error: error));
      return;
    }

    emit(state.copyWith(
      status: NotificationsStatus.loaded,
      items: items,
      clearError: true,
    ));
  }

  InboxItem _fromAnnouncement(
    AnnouncementModel a,
    Set<String> readIds,
    bool isParent,
  ) {
    final id = 'announcement:${a.id}';
    return InboxItem(
      id: id,
      kind: InboxKind.announcement,
      title: a.title,
      body: a.content,
      date: a.createdAt,
      isRead: readIds.contains(id),
      actionRoute: isParent ? '/parent/announcements' : '/student/announcements',
    );
  }

  Future<void> markRead(String id) async {
    await preferences.markInboxIdsRead([id]);
    final updated = state.items
        .map((e) => e.id == id ? e.copyWith(isRead: true) : e)
        .toList();
    emit(state.copyWith(items: updated));
  }

  Future<void> markAllRead() async {
    final ids = state.items.map((e) => e.id);
    await preferences.markInboxIdsRead(ids);
    emit(
      state.copyWith(
        items: state.items.map((e) => e.copyWith(isRead: true)).toList(),
      ),
    );
  }
}
