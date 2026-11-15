import 'monthly_hifz_rating_model.dart';
// ── Safe type converters ──
int _toInt(dynamic v) {
  if (v == null) return 0;
  if (v is int) return v;
  if (v is double) return v.toInt();
  if (v is String) return int.tryParse(v) ?? 0;
  return 0;
}

double _toDouble(dynamic v) {
  if (v == null) return 0.0;
  if (v is double) return v;
  if (v is int) return v.toDouble();
  if (v is String) return double.tryParse(v) ?? 0.0;
  return 0.0;
}

/// Parent summary model — mirrors GET /api/dashboard/parent-summary response.
class ParentSummaryModel {
  final List<ChildDataModel> children;
  final List<PaymentAlert> alerts;

  const ParentSummaryModel({
    required this.children,
    required this.alerts,
  });

  factory ParentSummaryModel.fromJson(Map<String, dynamic> json) {
    return ParentSummaryModel(
      children: (json['children'] as List<dynamic>?)
              ?.map((e) => ChildDataModel.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
      alerts: (json['alerts'] as List<dynamic>?)
              ?.map((e) => PaymentAlert.fromJson(e as Map<String, dynamic>))
              .toList() ??
          [],
    );
  }

  Map<String, dynamic> toJson() => {
        'children': children.map((e) => e.toJson()).toList(),
        'alerts': alerts.map((e) => e.toJson()).toList(),
      };
}

class ChildDataModel {
  final int id;
  final String fullName;
  final int progress;
  final double attendance;
  final int points;
  final String? badges;
  final String lastNote;
  final MonthlyHifzRatingModel? monthlyHifzRating;

  const ChildDataModel({
    required this.id,
    required this.fullName,
    required this.progress,
    required this.attendance,
    required this.points,
    this.badges,
    required this.lastNote,
    this.monthlyHifzRating,
  });

  List<String> get badgeList =>
      badges?.split(',').where((b) => b.trim().isNotEmpty).toList() ?? [];

  factory ChildDataModel.fromJson(Map<String, dynamic> json) {
    return ChildDataModel(
      id: _toInt(json['id']),
      fullName: json['fullName']?.toString() ?? '',
      progress: _toInt(json['progress']),
      attendance: _toDouble(json['attendance']),
      points: _toInt(json['points']),
      badges: json['badges']?.toString(),
      lastNote: json['lastNote']?.toString() ?? 'لا توجد ملاحظات',
      monthlyHifzRating: MonthlyHifzRatingModel.tryParse(json['monthlyHifzRating']),
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'progress': progress,
        'attendance': attendance,
        'points': points,
        'badges': badges,
        'lastNote': lastNote,
        'monthlyHifzRating': monthlyHifzRating?.toJson(),
      };
}

class PaymentAlert {
  final String message;
  final int id;
  final double amount;

  const PaymentAlert({
    required this.message,
    required this.id,
    required this.amount,
  });

  factory PaymentAlert.fromJson(Map<String, dynamic> json) => PaymentAlert(
        message: json['message']?.toString() ?? '',
        id: _toInt(json['id']),
        amount: _toDouble(json['amount']),
      );

  Map<String, dynamic> toJson() => {
        'message': message,
        'id': id,
        'amount': amount,
      };
}

/// Payment model — mirrors GET /api/payments/parent response items.
class PaymentModel {
  final int id;
  final String studentName;
  final double amount;
  final String description;
  final String dueDate;
  final String? paidDate;
  final String status; // Pending, Paid, Overdue

  const PaymentModel({
    required this.id,
    required this.studentName,
    required this.amount,
    required this.description,
    required this.dueDate,
    this.paidDate,
    required this.status,
  });

  bool get isPaid => status == 'Paid';
  bool get isOverdue => status == 'Overdue';
  bool get isPending => status == 'Pending';

  factory PaymentModel.fromJson(Map<String, dynamic> json) => PaymentModel(
        id: _toInt(json['id']),
        studentName: json['studentName']?.toString() ?? '',
        amount: _toDouble(json['amount']),
        description: json['description']?.toString() ?? '',
        dueDate: json['dueDate']?.toString() ?? '',
        paidDate: json['paidDate']?.toString(),
        status: json['status']?.toString() ?? 'Pending',
      );

  Map<String, dynamic> toJson() => {
        'id': id,
        'studentName': studentName,
        'amount': amount,
        'description': description,
        'dueDate': dueDate,
        'paidDate': paidDate,
        'status': status,
      };
}
