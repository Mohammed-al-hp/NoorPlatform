// نماذج بيانات الحضور — مطابقة لبنية استجابة GET /api/Attendance/my الفعلية
// (تم التحقق منها مباشرة عبر Swagger، وليست بنية مُخمَّنة).

class AttendanceSummaryModel {
  final int present;
  final int late;
  final int excusedAbsence;
  final int unexcusedAbsence;
  final int total;
  final int attendanceRate; // نسبة مئوية جاهزة (0-100)، وليست عشرية

  const AttendanceSummaryModel({
    required this.present,
    required this.late,
    required this.excusedAbsence,
    required this.unexcusedAbsence,
    required this.total,
    required this.attendanceRate,
  });

  factory AttendanceSummaryModel.fromJson(Map<String, dynamic> json) {
    // attendanceRate قد يأتي كـ double (مثل 98.4) أو int من الـ API
    final rawRate = json['attendanceRate'];
    final rate = rawRate is double
        ? rawRate.round()
        : (rawRate as int? ?? 0);

    // باقي الحقول قد تأتي أيضاً كـ double من بعض الـ APIs
    int safeInt(dynamic v) {
      if (v is int) return v;
      if (v is double) return v.round();
      return 0;
    }

    return AttendanceSummaryModel(
      present: safeInt(json['present']),
      late: safeInt(json['late']),
      excusedAbsence: safeInt(json['excusedAbsence']),
      unexcusedAbsence: safeInt(json['unexcusedAbsence']),
      total: safeInt(json['total']),
      attendanceRate: rate,
    );
  }

  Map<String, dynamic> toJson() => {
        'present': present,
        'late': late,
        'excusedAbsence': excusedAbsence,
        'unexcusedAbsence': unexcusedAbsence,
        'total': total,
        'attendanceRate': attendanceRate,
      };
}

/// حالات الحضور — تطابق enum AttendanceStatus في الـ Backend حرفياً.
enum AttendanceRecordStatus {
  present,
  late,
  excusedAbsence,
  unexcusedAbsence,
  unknown; // احتياطي لأي قيمة مستقبلية غير متوقعة من الـ API

  static AttendanceRecordStatus fromApiString(String value) {
    switch (value) {
      case 'Present':
        return AttendanceRecordStatus.present;
      case 'Late':
        return AttendanceRecordStatus.late;
      case 'ExcusedAbsence':
        return AttendanceRecordStatus.excusedAbsence;
      case 'UnexcusedAbsence':
        return AttendanceRecordStatus.unexcusedAbsence;
      default:
        return AttendanceRecordStatus.unknown;
    }
  }
}

class AttendanceRecordModel {
  final DateTime date;
  final AttendanceRecordStatus status;
  final String? note; // الـ API يُرجع أحياناً "" وأحياناً null — كلاهما "لا ملاحظة"

  const AttendanceRecordModel({
    required this.date,
    required this.status,
    this.note,
  });

  /// ملاحظة فعلية بعد استبعاد النص الفارغ — للاستخدام في الواجهة
  /// بدل تكرار فحص isEmpty في كل مكان.
  bool get hasNote => note != null && note!.trim().isNotEmpty;

  factory AttendanceRecordModel.fromJson(Map<String, dynamic> json) {
    return AttendanceRecordModel(
      date: DateTime.parse(json['date'] as String),
      status: AttendanceRecordStatus.fromApiString(json['status'] as String? ?? ''),
      note: json['note'] as String?,
    );
  }

  Map<String, dynamic> toJson() => {
        'date': '${date.year.toString().padLeft(4, '0')}-'
            '${date.month.toString().padLeft(2, '0')}-'
            '${date.day.toString().padLeft(2, '0')}',
        'status': status.name,
        'note': note,
      };
}

class AttendanceModel {
  final AttendanceSummaryModel summary;
  final List<AttendanceRecordModel> records;

  const AttendanceModel({required this.summary, required this.records});

  factory AttendanceModel.fromJson(Map<String, dynamic> json) {
    final recordsJson = json['records'] as List<dynamic>? ?? [];
    return AttendanceModel(
      summary: AttendanceSummaryModel.fromJson(
        json['summary'] as Map<String, dynamic>? ?? {},
      ),
      records: recordsJson
          .map((r) => AttendanceRecordModel.fromJson(r as Map<String, dynamic>))
          .toList(),
    );
  }

  /// Builds summary client-side for GET /api/attendance/student/{id}
  /// which returns a flat list (no summary object).
  factory AttendanceModel.fromFlatList(List<dynamic> raw) {
    final records = raw
        .whereType<Map>()
        .map((e) => AttendanceRecordModel.fromJson(Map<String, dynamic>.from(e)))
        .toList();
    var present = 0, late = 0, excused = 0, unexcused = 0;
    for (final r in records) {
      switch (r.status) {
        case AttendanceRecordStatus.present:
          present++;
          break;
        case AttendanceRecordStatus.late:
          late++;
          break;
        case AttendanceRecordStatus.excusedAbsence:
          excused++;
          break;
        case AttendanceRecordStatus.unexcusedAbsence:
          unexcused++;
          break;
        case AttendanceRecordStatus.unknown:
          break;
      }
    }
    final total = records.length;
    final attended = present + late;
    final rate = total == 0 ? 0 : ((attended / total) * 100).round();
    return AttendanceModel(
      summary: AttendanceSummaryModel(
        present: present,
        late: late,
        excusedAbsence: excused,
        unexcusedAbsence: unexcused,
        total: total,
        attendanceRate: rate,
      ),
      records: records,
    );
  }

  Map<String, dynamic> toJson() => {
        'summary': summary.toJson(),
        'records': records.map((r) => r.toJson()).toList(),
      };
}
