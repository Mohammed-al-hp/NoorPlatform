import 'package:flutter/foundation.dart';

import '../../config/api_config.dart';
import 'api_client.dart';

/// معلومات المركز العامة من الخادم (اسم المركز المسجل في الإعدادات، وقنوات رمز التحقق).
/// تُحمَّل عند بدء التطبيق دون تسجيل دخول، وتُعرض تحت «منصة نور» بدل النص الثابت.
class CenterInfo {
  static const String tagline = 'لإدارة مركز وحلقات تحفيظ القرآن الكريم';
  static const String fallbackCenterName = 'مركز تحفيظ القرآن الكريم';

  final ApiClient apiClient;

  /// اسم المركز الفعلي (null حتى يُحمَّل).
  final ValueNotifier<String?> centerName = ValueNotifier<String?>(null);

  /// قنوات رمز التحقق المتاحة: WhatsApp و/أو Sms.
  final ValueNotifier<List<String>> otpChannels =
      ValueNotifier<List<String>>(const ['WhatsApp']);

  CenterInfo(this.apiClient);

  Future<void> load() async {
    try {
      final res = await apiClient.get(ApiConfig.publicSettings);
      final data = res.data;
      if (data is Map) {
        final name = data['centerName']?.toString().trim();
        if (name != null && name.isNotEmpty) centerName.value = name;
        final channels = data['otpChannels'];
        if (channels is List && channels.isNotEmpty) {
          otpChannels.value = channels.map((e) => e.toString()).toList();
        }
      }
    } catch (_) {
      // بلا اتصال: يبقى النص الافتراضي
    }
  }
}
