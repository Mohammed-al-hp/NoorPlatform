import 'package:dio/dio.dart';

import '../../../core/network/api_client.dart';
import '../../../core/network/api_response_helper.dart';
import '../../../core/network/dio_error_mapper.dart';
import '../../models/smart_models.dart';

/// الميزات الذكية: مركز الإشعارات، جدول المراجعة المتباعدة، والخريطة الحرارية.
class SmartRemoteDataSource {
  final ApiClient api;
  SmartRemoteDataSource(this.api);

  Future<T> _guard<T>(Future<T> Function() run, String fallback) async {
    try {
      return await run();
    } on DioException catch (e) {
      throw mapDioErrorToException(e, fallbackMessage: fallback);
    }
  }

  Future<Map<String, dynamic>> _getMap(String path, String fallback, {Map<String, dynamic>? query}) =>
      _guard(() async {
        final res = await api.get(path, queryParameters: query);
        ApiResponseHelper.ensureSuccess(res, fallback: fallback);
        return Map<String, dynamic>.from(res.data as Map);
      }, fallback);

  // ── الإشعارات ──
  Future<({int unread, List<ServerNotification> items})> getInbox({int take = 40}) async {
    final data = await _getMap('/api/inbox', 'تعذر تحميل الإشعارات', query: {'take': take});
    final items = ApiResponseHelper.asMapList(data['items']).map(ServerNotification.fromJson).toList();
    return (unread: (data['unread'] as num?)?.toInt() ?? 0, items: items);
  }

  Future<int> getUnreadCount() async {
    final data = await _getMap('/api/inbox/unread-count', 'تعذر تحميل عدد الإشعارات');
    return (data['unread'] as num?)?.toInt() ?? 0;
  }

  Future<void> markRead(int id) => _guard(() async {
        final res = await api.post('/api/inbox/$id/read');
        ApiResponseHelper.ensureSuccess(res, fallback: 'تعذر تحديث الإشعار');
      }, 'تعذر تحديث الإشعار');

  Future<void> markAllRead() => _guard(() async {
        final res = await api.post('/api/inbox/read-all');
        ApiResponseHelper.ensureSuccess(res, fallback: 'تعذر تحديث الإشعارات');
      }, 'تعذر تحديث الإشعارات');

  // ── المراجعة والخريطة الحرارية ──
  Future<ReviewScheduleModel> getReviewSchedule({int? studentId}) async =>
      ReviewScheduleModel.fromJson(await _getMap(
        studentId == null ? '/api/hifz/review-schedule/my' : '/api/hifz/review-schedule/$studentId',
        'تعذر تحميل جدول المراجعة',
      ));

  Future<HeatmapModel> getHeatmap({int? studentId}) async => HeatmapModel.fromJson(await _getMap(
        studentId == null ? '/api/hifz/heatmap/my' : '/api/hifz/heatmap/$studentId',
        'تعذر تحميل خريطة الإتقان',
      ));
}
