import 'package:dio/dio.dart';
import '../../../config/api_config.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/api_response_helper.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/attendance_models.dart';

abstract class AttendanceRemoteDataSource {
  Future<AttendanceModel> getMyAttendance({int? days});
  Future<AttendanceModel> getStudentAttendance(int studentId);
}

class AttendanceRemoteDataSourceImpl implements AttendanceRemoteDataSource {
  final ApiClient apiClient;

  AttendanceRemoteDataSourceImpl(this.apiClient);

  @override
  Future<AttendanceModel> getMyAttendance({int? days}) async {
    try {
      final response = await apiClient.get(
        ApiConfig.myAttendance,
        queryParameters: days != null ? {'days': days} : null,
      );

      if (response.statusCode == 200) {
        return AttendanceModel.fromJson(
          Map<String, dynamic>.from(response.data as Map),
        );
      }

      throw mapDioErrorToException(
        DioException(
          requestOptions: response.requestOptions,
          response: response,
        ),
        fallbackMessage: 'فشل تحميل سجل الحضور',
      );
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: 'فشل تحميل سجل الحضور');
    }
  }

  @override
  Future<AttendanceModel> getStudentAttendance(int studentId) async {
    try {
      final response =
          await apiClient.get(ApiConfig.studentAttendance(studentId));
      ApiResponseHelper.ensureSuccess(
        response,
        fallback: 'فشل تحميل سجل الحضور',
      );
      if (response.data is List) {
        return AttendanceModel.fromFlatList(response.data as List);
      }
      return AttendanceModel.fromJson(
        Map<String, dynamic>.from(response.data as Map),
      );
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: 'فشل تحميل سجل الحضور');
    }
  }
}
