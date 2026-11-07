import 'package:dio/dio.dart';
import '../../../config/api_config.dart';
import '../../../core/errors/exceptions.dart';
import '../../../core/network/api_client.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/parent_models.dart';

abstract class ParentRemoteDataSource {
  Future<ParentSummaryModel> getParentSummary();
}

class ParentRemoteDataSourceImpl implements ParentRemoteDataSource {
  final ApiClient apiClient;

  ParentRemoteDataSourceImpl(this.apiClient);

  @override
  Future<ParentSummaryModel> getParentSummary() async {
    try {
      final response = await apiClient.get(ApiConfig.parentSummary);

      if (response.statusCode == 200) {
        return ParentSummaryModel.fromJson(response.data);
      } else {
        String msg = 'فشل تحميل بيانات ولي الأمر';
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
        fallbackMessage: 'فشل تحميل بيانات ولي الأمر',
      );
    }
  }
}
