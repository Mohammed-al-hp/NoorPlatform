import 'package:equatable/equatable.dart';

/// Failure types for the domain layer (used with Either&lt;Failure, T&gt;).

abstract class Failure extends Equatable {
  final String message;
  const Failure(this.message);

  @override
  List<Object> get props => [message];
}

class ServerFailure extends Failure {
  const ServerFailure(super.message);
}

class CacheFailure extends Failure {
  const CacheFailure(super.message);
}

class AuthFailure extends Failure {
  const AuthFailure(super.message);
}

class NetworkFailure extends Failure {
  const NetworkFailure([super.message = 'لا يوجد اتصال بالإنترنت']);
}

/// إضافة طالب سابق مؤرشف — الاستعادة من صلاحيات الإدارة (409 ARCHIVED_STUDENT_FOUND).
class ArchivedStudentFailure extends Failure {
  final bool canRestore;
  final int archivedStudentId;
  final String archivedName;

  const ArchivedStudentFailure(
    super.message, {
    required this.canRestore,
    required this.archivedStudentId,
    required this.archivedName,
  });
}
