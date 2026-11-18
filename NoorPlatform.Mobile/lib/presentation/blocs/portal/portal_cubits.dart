import 'package:flutter_bloc/flutter_bloc.dart';
import '../../../domain/repositories/portal_repository.dart';
import '../../../data/models/message_models.dart';
import '../../../data/models/parent_models.dart';
import '../../../data/models/pedagogical_models.dart';
import '../../../data/models/library_models.dart';
import '../../../data/models/announcement_models.dart';
import '../../../data/models/oral_exam_models.dart';

/// Generic async list status for portal feature cubits.
enum PortalLoadStatus { initial, loading, loaded, error }

class MessagesCubit extends Cubit<MessagesState> {
  final PortalRepository repository;
  MessagesCubit(this.repository) : super(const MessagesState());

  Future<void> load() async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final sent = await repository.getSentMessages();
    final recipients = await repository.getMessageRecipients();
    String? error;
    List<SentMessageModel> messages = const [];
    List<MessageRecipientModel> recs = const [];
    sent.fold((f) => error = f.message, (v) => messages = v);
    recipients.fold((f) => error ??= f.message, (v) => recs = v);
    if (error != null) {
      emit(state.copyWith(status: PortalLoadStatus.error, error: error));
    } else {
      emit(state.copyWith(
        status: PortalLoadStatus.loaded,
        messages: messages,
        recipients: recs,
      ));
    }
  }

  Future<void> send(SendMessageRequest request) async {
    emit(state.copyWith(submitting: true, clearError: true, clearMsg: true));
    final result = await repository.sendMessage(request);
    result.fold(
      (f) => emit(state.copyWith(submitting: false, error: f.message)),
      (_) async {
        emit(state.copyWith(
          submitting: false,
          successMessage: 'تم إرسال الرسالة بنجاح',
        ));
        await load();
      },
    );
  }

  /// رد ولي الأمر داخل محادثة (يُوجَّه تلقائياً للمحفّظ أو الإدارة).
  Future<bool> reply(int threadId, String content) async {
    emit(state.copyWith(submitting: true, clearError: true, clearMsg: true));
    final result = await repository.replyToThread(threadId, content);
    final error = result.fold((f) => f.message, (_) => null);
    if (error != null) {
      emit(state.copyWith(submitting: false, error: error));
      return false;
    }
    emit(state.copyWith(submitting: false, successMessage: 'تم إرسال الرد'));
    await load();
    return true;
  }
}

class MessagesState {
  final PortalLoadStatus status;
  final List<SentMessageModel> messages;
  final List<MessageRecipientModel> recipients;
  final String? error;
  final bool submitting;
  final String? successMessage;

  const MessagesState({
    this.status = PortalLoadStatus.initial,
    this.messages = const [],
    this.recipients = const [],
    this.error,
    this.submitting = false,
    this.successMessage,
  });

  MessagesState copyWith({
    PortalLoadStatus? status,
    List<SentMessageModel>? messages,
    List<MessageRecipientModel>? recipients,
    String? error,
    bool? submitting,
    String? successMessage,
    bool clearError = false,
    bool clearMsg = false,
  }) =>
      MessagesState(
        status: status ?? this.status,
        messages: messages ?? this.messages,
        recipients: recipients ?? this.recipients,
        error: clearError ? null : (error ?? this.error),
        submitting: submitting ?? this.submitting,
        successMessage:
            clearMsg ? null : (successMessage ?? this.successMessage),
      );
}

class PaymentsCubit extends Cubit<PaymentsState> {
  final PortalRepository repository;
  PaymentsCubit(this.repository) : super(const PaymentsState());

  Future<void> load() async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getParentPayments();
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) => emit(
        state.copyWith(status: PortalLoadStatus.loaded, payments: list),
      ),
    );
  }
}

class PaymentsState {
  final PortalLoadStatus status;
  final List<PaymentModel> payments;
  final String? error;

  const PaymentsState({
    this.status = PortalLoadStatus.initial,
    this.payments = const [],
    this.error,
  });

  PaymentsState copyWith({
    PortalLoadStatus? status,
    List<PaymentModel>? payments,
    String? error,
    bool clearError = false,
  }) =>
      PaymentsState(
        status: status ?? this.status,
        payments: payments ?? this.payments,
        error: clearError ? null : (error ?? this.error),
      );
}

class ParentHomeCubit extends Cubit<ParentHomeState> {
  final PortalRepository repository;
  ParentHomeCubit(this.repository) : super(const ParentHomeState());

  Future<void> load(int studentId) async {
    emit(state.copyWith(
      status: PortalLoadStatus.loading,
      studentId: studentId,
      clearError: true,
    ));
    final result = await repository.getParentHome(studentId);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) => emit(
        state.copyWith(status: PortalLoadStatus.loaded, feedbacks: list),
      ),
    );
  }

  Future<void> submit({
    required int studentId,
    required String weekStartDate,
    required int rating,
    String? notes,
  }) async {
    emit(state.copyWith(submitting: true, clearError: true, clearMsg: true));
    final result = await repository.submitParentHome(
      studentId: studentId,
      weekStartDate: weekStartDate,
      rating: rating,
      notes: notes,
    );
    result.fold(
      (f) => emit(state.copyWith(submitting: false, error: f.message)),
      (_) async {
        emit(state.copyWith(
          submitting: false,
          successMessage: 'تم حفظ المتابعة المنزلية',
        ));
        await load(studentId);
      },
    );
  }
}

class ParentHomeState {
  final PortalLoadStatus status;
  final int? studentId;
  final List<ParentHomeFeedbackModel> feedbacks;
  final String? error;
  final bool submitting;
  final String? successMessage;

  const ParentHomeState({
    this.status = PortalLoadStatus.initial,
    this.studentId,
    this.feedbacks = const [],
    this.error,
    this.submitting = false,
    this.successMessage,
  });

  ParentHomeState copyWith({
    PortalLoadStatus? status,
    int? studentId,
    List<ParentHomeFeedbackModel>? feedbacks,
    String? error,
    bool? submitting,
    String? successMessage,
    bool clearError = false,
    bool clearMsg = false,
  }) =>
      ParentHomeState(
        status: status ?? this.status,
        studentId: studentId ?? this.studentId,
        feedbacks: feedbacks ?? this.feedbacks,
        error: clearError ? null : (error ?? this.error),
        submitting: submitting ?? this.submitting,
        successMessage:
            clearMsg ? null : (successMessage ?? this.successMessage),
      );
}

class LibraryCubit extends Cubit<LibraryState> {
  final PortalRepository repository;
  LibraryCubit(this.repository) : super(const LibraryState());

  Future<void> load({String? search}) async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getLibrary(search: search);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) =>
          emit(state.copyWith(status: PortalLoadStatus.loaded, items: list)),
    );
  }

  Future<String?> download(LibraryItemModel item) async {
    emit(state.copyWith(downloadingId: item.id, clearError: true));
    final result = await repository.downloadLibraryFile(item.id, item.title);
    String? path;
    result.fold(
      (f) => emit(state.copyWith(downloadingId: null, error: f.message)),
      (p) {
        path = p;
        emit(state.copyWith(downloadingId: null));
      },
    );
    return path;
  }
}

class LibraryState {
  final PortalLoadStatus status;
  final List<LibraryItemModel> items;
  final String? error;
  final int? downloadingId;

  const LibraryState({
    this.status = PortalLoadStatus.initial,
    this.items = const [],
    this.error,
    this.downloadingId,
  });

  LibraryState copyWith({
    PortalLoadStatus? status,
    List<LibraryItemModel>? items,
    String? error,
    int? downloadingId,
    bool clearError = false,
  }) =>
      LibraryState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
        downloadingId: downloadingId,
      );
}

class AnnouncementsCubit extends Cubit<AnnouncementsState> {
  final PortalRepository repository;
  AnnouncementsCubit(this.repository) : super(const AnnouncementsState());

  Future<void> load() async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getAnnouncements();
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) => emit(
        state.copyWith(status: PortalLoadStatus.loaded, items: list),
      ),
    );
  }
}

class AnnouncementsState {
  final PortalLoadStatus status;
  final List<AnnouncementModel> items;
  final String? error;

  const AnnouncementsState({
    this.status = PortalLoadStatus.initial,
    this.items = const [],
    this.error,
  });

  AnnouncementsState copyWith({
    PortalLoadStatus? status,
    List<AnnouncementModel>? items,
    String? error,
    bool clearError = false,
  }) =>
      AnnouncementsState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
      );
}

class MatnCubit extends Cubit<MatnState> {
  final PortalRepository repository;
  MatnCubit(this.repository) : super(const MatnState());

  Future<void> load(int studentId) async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getMatn(studentId);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) =>
          emit(state.copyWith(status: PortalLoadStatus.loaded, items: list)),
    );
  }
}

class MatnState {
  final PortalLoadStatus status;
  final List<MatnRecordModel> items;
  final String? error;

  const MatnState({
    this.status = PortalLoadStatus.initial,
    this.items = const [],
    this.error,
  });

  MatnState copyWith({
    PortalLoadStatus? status,
    List<MatnRecordModel>? items,
    String? error,
    bool clearError = false,
  }) =>
      MatnState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
      );
}

class MonthlyTargetsCubit extends Cubit<MonthlyTargetsState> {
  final PortalRepository repository;
  MonthlyTargetsCubit(this.repository) : super(const MonthlyTargetsState());

  Future<void> load(int studentId) async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getMonthlyTargets(studentId);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) =>
          emit(state.copyWith(status: PortalLoadStatus.loaded, items: list)),
    );
  }
}

class MonthlyTargetsState {
  final PortalLoadStatus status;
  final List<MonthlyTargetModel> items;
  final String? error;

  const MonthlyTargetsState({
    this.status = PortalLoadStatus.initial,
    this.items = const [],
    this.error,
  });

  MonthlyTargetsState copyWith({
    PortalLoadStatus? status,
    List<MonthlyTargetModel>? items,
    String? error,
    bool clearError = false,
  }) =>
      MonthlyTargetsState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
      );
}

class OralExamsCubit extends Cubit<OralExamsState> {
  final PortalRepository repository;
  OralExamsCubit(this.repository) : super(const OralExamsState());

  Future<void> load(int studentId) async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getOralExams(studentId);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) =>
          emit(state.copyWith(status: PortalLoadStatus.loaded, items: list)),
    );
  }
}

class OralExamsState {
  final PortalLoadStatus status;
  final List<OralExamModel> items;
  final String? error;

  const OralExamsState({
    this.status = PortalLoadStatus.initial,
    this.items = const [],
    this.error,
  });

  OralExamsState copyWith({
    PortalLoadStatus? status,
    List<OralExamModel>? items,
    String? error,
    bool clearError = false,
  }) =>
      OralExamsState(
        status: status ?? this.status,
        items: items ?? this.items,
        error: clearError ? null : (error ?? this.error),
      );
}

class ChildPrayerCubit extends Cubit<ChildPrayerState> {
  final PortalRepository repository;
  ChildPrayerCubit(this.repository) : super(const ChildPrayerState());

  Future<void> load(int studentId) async {
    emit(state.copyWith(status: PortalLoadStatus.loading, clearError: true));
    final result = await repository.getPrayerByStudent(studentId);
    result.fold(
      (f) =>
          emit(state.copyWith(status: PortalLoadStatus.error, error: f.message)),
      (list) =>
          emit(state.copyWith(status: PortalLoadStatus.loaded, logs: list)),
    );
  }
}

class ChildPrayerState {
  final PortalLoadStatus status;
  final List<PrayerLogModel> logs;
  final String? error;

  const ChildPrayerState({
    this.status = PortalLoadStatus.initial,
    this.logs = const [],
    this.error,
  });

  ChildPrayerState copyWith({
    PortalLoadStatus? status,
    List<PrayerLogModel>? logs,
    String? error,
    bool clearError = false,
  }) =>
      ChildPrayerState(
        status: status ?? this.status,
        logs: logs ?? this.logs,
        error: clearError ? null : (error ?? this.error),
      );
}
