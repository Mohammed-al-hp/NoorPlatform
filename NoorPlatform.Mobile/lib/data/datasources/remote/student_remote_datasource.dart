import 'package:dio/dio.dart';
import '../../../config/api_config.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/student_summary_model.dart';

abstract class StudentRemoteDataSource {
  Future<StudentSummaryModel> getStudentSummary();
}

class StudentRemoteDataSourceImpl implements StudentRemoteDataSource {
  final ApiClient apiClient;

  StudentRemoteDataSourceImpl(this.apiClient);

  @override
  Future<StudentSummaryModel> getStudentSummary() async {
    try {
      final response = await apiClient.get(ApiConfig.studentSummary);

      if (response.statusCode == 200) {
        return StudentSummaryModel.fromJson(response.data);
      } else {
        String msg = 'فشل تحميل بيانات الطالب';
        if (response.data is Map<String, dynamic>) {
          msg = response.data['message'] ?? msg;
        }
        throw ServerException(
          message: msg,
          statusCode: response.statusCode,
        );
      }
    } on DioException catch (e) {
      throw mapDioErrorToException(
        e,
        fallbackMessage: 'فشل تحميل بيانات الطالب',
      );
    }
  }
}
