# منصة نور — تطبيق الهاتف (NoorPlatform.Mobile)

تطبيق Flutter للمحفّظ والطالب وولي الأمر، مرتبط بخادم `NoorPlatform.Api`.
حساب المشرف يرى شاشة توضح أن لوحة التحكم متاحة عبر منصة الويب فقط.

## عنوان الخادم

العنوان لا يُكتب في الكود؛ يُمرَّر عند التشغيل أو البناء:

```bash
# تطوير على الشبكة المحلية (HTTP مسموح في نسخ debug/profile فقط)
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5032

# نسخة الإصدار — تتطلب HTTPS (أندرويد 9+ يمنع HTTP غير المشفّر)
flutter build apk --release --dart-define=API_BASE_URL=https://noor.example.com
```

القيمة الافتراضية (عند عدم التمرير) موجودة في `lib/config/api_config.dart` وتصلح للتطوير فقط.

## الفحص

```bash
flutter analyze
flutter test
```
