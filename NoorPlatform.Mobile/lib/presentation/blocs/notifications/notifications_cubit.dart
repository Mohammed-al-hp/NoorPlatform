import 'dart:async';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../../core/storage/app_preferences.dart';
import '../../../data/datasources/remote/smart_remote_datasource.dart';
import '../../../data/models/announcement_models.dart';
import '../../../data/models/smart_models.dart';
import '../../../domain/repositories/parent_repository.dart';
import '../../../domain/repositories/portal_repository.dart';

/// announcement/payment تُبنى محلياً، وserver من مركز الإشعارات في الخادم (/api/inbox).
enum InboxKind { announcement, payment, server }

class InboxItem extends Equatable {
  final String id;
  final InboxKind kind;
  final String title;
  final String body;
  final String? date;
  final bool isRead;
  final String? actionRoute;

  /// نوع إشعار الخادم: Hifz | Absence | Announcement | Message | MessageReply | MonthlyReport
  final String? serverKind;
  final int? serverId;

  const InboxItem({
    required this.id,
    required this.kind,
    required this.title,
    required this.body,
    this.date,
    required this.isRead,
    this.actionRoute,
    this.serverKind,
    this.serverId,
  });

  InboxItem copyWith({bool? isRead}) => InboxItem(
        id: id,
        kind: kind,
        title: title,
        body: body,
        date: date,
        isRead: isRead ?? this.isRead,
        actionRoute: actionRoute,
        serverKind: serverKind,
        serverId: serverId,
      );

  @override
  List<Object?> get props => [id, kind, title, body, date, isRead, actionRoute, serverKind, serverId];
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
  final SmartRemoteDataSource? smart;

  Timer? _poll;
  bool _isTeacher = false;

  NotificationsCubit({
    required this.portalRepository,
    required this.parentRepository,
    required this.preferences,
    this.smart,
  }) : super(const NotificationsState());

  /// سحب دوري خفيف كل دقيقة (الإشعارات الفورية داخل التطبيق دون خدمة دفع خارجية).
  void startPolling({required bool isParent, bool isTeacher = false}) {
    _isTeacher = isTeacher;
    _poll?.cancel();
    unawaited(refresh(isParent: isParent, isTeacher: isTeacher));
    _poll = Timer.periodic(const Duration(seconds: 60), (_) {
      unawaited(refresh(isParent: isParent, isTeacher: isTeacher, silent: true));
    });
  }

  void stopPolling() {
    _poll?.cancel();
    _poll = null;
  }

  @override
  Future<void> close() {
    stopPolling();
    return super.close();
  }

  Future<void> refresh({required bool isParent, bool? isTeacher, bool silent = false}) async {
    if (isTeacher != null) _isTeacher = isTeacher;
    if (!silent || state.status != NotificationsStatus.loaded) {
      emit(state.copyWith(
        status: NotificationsStatus.loading,
        isParent: isParent,
        clearError: true,
      ));
    }

    final readIds = await preferences.getReadInboxIds();
    final items = <InboxItem>[];

    // ── إشعارات الخادم (التسميع، الغياب، الإعلانات، الردود، التقرير الشهري) ──
    var hasServerAnnouncements = false;
    if (smart != null) {
      try {
        final inbox = await smart!.getInbox();
        for (final n in inbox.items) {
          if (n.kind == 'Announcement') hasServerAnnouncements = true;
          items.add(_fromServer(n, isParent));
        }
      } catch (_) {/* بلا اتصال: نكمل بالمصادر الأخرى */}
    }

    String? error;
    // الإعلانات القديمة (قبل مركز الإشعارات) تُعرض محلياً فقط إن لم تصل من الخادم
    if (!hasServerAnnouncements && !_isTeacher) {
      final announcements = await portalRepository.getAnnouncements();
      announcements.fold(
        (f) => error = f.message,
        (list) {
          for (final a in list.where((e) => e.isActive)) {
            items.add(_fromAnnouncement(a, readIds, isParent));
          }
        },
      );
    }

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

  InboxItem _fromServer(ServerNotification n, bool isParent) {
    String? route;
    if (n.kind == 'Announcement' && !_isTeacher) {
      route = isParent ? '/parent/announcements' : '/student/announcements';
    } else if (n.kind == 'Hifz' || n.kind == 'Absence') {
      if (isParent && n.studentId != null) route = '/parent/child/${n.studentId}';
      if (!isParent && !_isTeacher) route = '/student/quran-map';
    }
    return InboxItem(
      id: 'srv:${n.id}',
      kind: InboxKind.server,
      title: n.title,
      body: n.body,
      date: n.createdAt,
      isRead: n.isRead,
      actionRoute: route,
      serverKind: n.kind,
      serverId: n.id,
    );
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
    final item = state.items.where((e) => e.id == id).firstOrNull;
    if (item?.serverId != null && !item!.isRead) {
      try { await smart?.markRead(item.serverId!); } catch (_) {}
    }
    await preferences.markInboxIdsRead([id]);
    final updated = state.items
        .map((e) => e.id == id ? e.copyWith(isRead: true) : e)
        .toList();
    emit(state.copyWith(items: updated));
  }

  Future<void> markAllRead() async {
    if (state.items.any((e) => e.serverId != null && !e.isRead)) {
      try { await smart?.markAllRead(); } catch (_) {}
    }
    final ids = state.items.map((e) => e.id);
    await preferences.markInboxIdsRead(ids);
    emit(
      state.copyWith(
        items: state.items.map((e) => e.copyWith(isRead: true)).toList(),
      ),
    );
  }
}
