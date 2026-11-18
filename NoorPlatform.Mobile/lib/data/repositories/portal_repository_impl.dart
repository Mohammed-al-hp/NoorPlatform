import 'package:flutter/foundation.dart';
import 'package:dartz/dartz.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/errors/failures.dart';
import '../../../core/network/network_info.dart';
import '../datasources/remote/portal_remote_datasource.dart';
import '../models/announcement_models.dart';
import '../models/hifz_models.dart';
import '../models/library_models.dart';
import '../models/message_models.dart';
import '../models/oral_exam_models.dart';
import '../models/pedagogical_models.dart';
import '../models/parent_models.dart';
import '../../../domain/repositories/portal_repository.dart';

class PortalRepositoryImpl implements PortalRepository {
  final PortalRemoteDataSource remote;
  final NetworkInfo networkInfo;

  PortalRepositoryImpl({required this.remote, required this.networkInfo});

  Future<Either<Failure, T>> _run<T>(Future<T> Function() action) async {
    if (!await networkInfo.isConnected) {
      return const Left(NetworkFailure());
    }
    try {
      return Right(await action());
    } on ServerException catch (e) {
      return Left(ServerFailure(e.message));
    } on NetworkException catch (e) {
      return Left(NetworkFailure(e.message));
    } catch (e) {
      debugPrint('repository error: $e');
      return const Left(ServerFailure('حدث خطأ غير متوقع، حاول مرة أخرى'));
    }
  }

  @override
  Future<Either<Failure, PortalConfigModel>> getPortalConfig() =>
      _run(remote.getPortalConfig);

  @override
  Future<Either<Failure, List<EvaluationModel>>> getMyEvaluations({
    int? studentId,
  }) =>
      _run(() => remote.getMyEvaluations(studentId: studentId));

  @override
  Future<Either<Failure, List<PrayerLogModel>>> getMyPrayerLogs() =>
      _run(remote.getMyPrayerLogs);

  @override
  Future<Either<Failure, String?>> submitMyPrayer({
    required String date,
    required List<PrayerEntryModel> prayers,
    String? studentNote,
  }) =>
      _run(() => remote.submitMyPrayer(
            date: date,
            prayers: prayers,
            studentNote: studentNote,
          ));

  @override
  Future<Either<Failure, PrayerTimesModel>> getPrayerTimes({String? date}) =>
      _run(() => remote.getPrayerTimes(date: date));

  @override
  Future<Either<Failure, List<PrayerLogModel>>> getPrayerByStudent(
    int studentId,
  ) =>
      _run(() => remote.getPrayerByStudent(studentId));

  @override
  Future<Either<Failure, List<ParentHomeFeedbackModel>>> getParentHome(
    int studentId,
  ) =>
      _run(() => remote.getParentHome(studentId));

  @override
  Future<Either<Failure, Unit>> submitParentHome({
    required int studentId,
    required String weekStartDate,
    required int rating,
    String? notes,
  }) =>
      _run(() async {
        await remote.submitParentHome(
          studentId: studentId,
          weekStartDate: weekStartDate,
          rating: rating,
          notes: notes,
        );
        return unit;
      });

  @override
  Future<Either<Failure, List<MatnRecordModel>>> getMatn(int studentId) =>
      _run(() => remote.getMatn(studentId));

  @override
  Future<Either<Failure, List<MonthlyTargetModel>>> getMonthlyTargets(
    int studentId,
  ) =>
      _run(() => remote.getMonthlyTargets(studentId));

  @override
  Future<Either<Failure, MyHifzResponse>> getMyHifz() => _run(remote.getMyHifz);

  @override
  Future<Either<Failure, List<HifzRecordModel>>> getStudentHifz(
    int studentId,
  ) =>
      _run(() => remote.getStudentHifz(studentId));

  @override
  Future<Either<Failure, List<SentMessageModel>>> getSentMessages() =>
      _run(remote.getSentMessages);

  @override
  Future<Either<Failure, List<MessageRecipientModel>>> getMessageRecipients() =>
      _run(remote.getMessageRecipients);

  @override
  Future<Either<Failure, Unit>> sendMessage(SendMessageRequest request) =>
      _run(() async {
        await remote.sendMessage(request);
        return unit;
      });

  @override
  Future<Either<Failure, Unit>> replyToThread(int threadId, String content) =>
      _run(() async {
        await remote.replyToThread(threadId, content);
        return unit;
      });

  @override
  Future<Either<Failure, List<PaymentModel>>> getParentPayments() =>
      _run(remote.getParentPayments);

  @override
  Future<Either<Failure, List<LibraryItemModel>>> getLibrary({
    String? search,
    String? category,
  }) =>
      _run(() => remote.getLibrary(search: search, category: category));

  @override
  Future<Either<Failure, String>> downloadLibraryFile(int id, String title) =>
      _run(() => remote.downloadLibraryFile(id, title));

  @override
  Future<Either<Failure, String>> downloadCertificate(int studentId) =>
      _run(() => remote.downloadCertificate(studentId));

  @override
  Future<Either<Failure, String>> downloadMonthlyReport(int studentId, int year, int month) =>
      _run(() => remote.downloadMonthlyReport(studentId, year, month));

  @override
  Future<Either<Failure, List<AnnouncementModel>>> getAnnouncements() =>
      _run(remote.getAnnouncements);

  @override
  Future<Either<Failure, List<OralExamModel>>> getOralExams(int studentId) =>
      _run(() => remote.getOralExams(studentId));
}
