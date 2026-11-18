<div dir="rtl">

# خطة تحسين قاعدة البيانات — منصة نور

> **الحالة:** مرجع للتنفيذ لاحقاً — **لم يُنفَّذ أي تعديل على الجداول بعد.**
> **تاريخ التحليل:** 2026-11-17
> **الدافع:** ملاحظة لجنة المناقشة والمشرف بأن عدد الجداول كبير لمشروع تخرج، وأن بعضها يمكن حذفه أو دمجه.

---

## 1. الوضع الحالي

| الفئة | العدد | الجداول |
|---|---|---|
| جداول التطبيق | 29 | Students, Teachers, Parents, Circles, CircleEnrollments, Attendances, HifzRecords, MatnRecords, Matns, OralExamSessions, OralExamQuestions, StudentMonthlyTargets, EvaluationPeriods, StudentPeriodEvaluations, DressRecords, PrayerDailyLogs, PrayerEntries, ParentHomeFeedbacks, Payments, Expenses, LibraryItems, Announcements, Messages, UserNotifications, MonthlyParentReports, ActivityFeeds, AuditLogs, WaitingListEntries, PlatformSettings |
| جداول ASP.NET Identity | 7 | AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens |
| جداول نظامية | 2 | `__EFMigrationsHistory` (أداة الترحيل)، `sysdiagrams` (أُنشئ من SSMS — ليس من المشروع) |
| **المجموع الظاهر** | **38** | |

### 1.1 سلامة التصميم الحالي (لا أخطاء جسيمة)

- العلاقات (المفاتيح الأجنبية) مضبوطة، وسلوك الحذف مدروس: `Restrict` لحماية البيانات التاريخية، `Cascade` للسجلات التابعة للطالب، `SetNull` للحلقة والمحفّظ.
- قيود التفرد صحيحة: حضور واحد لكل (طالب، يوم، حلقة)، صلاة واحدة لكل (سجل يومي، صلاة)، هدف شهري واحد لكل (طالب، سنة، شهر)، تقييم فترة واحد لكل (فترة، طالب).
- الحذف الناعم (`IsDeleted`) مع فلتر عام على الطلاب والمحفّظين وأولياء الأمور.
- الفهارس تغطي الاستعلامات الشائعة (الطالب + التاريخ، الحلقة، الهاتف).
- سجل التدقيق (`AuditLogs`) يُملأ تلقائياً عبر Interceptor.

### 1.2 نقاط الضعف

1. **تكرار في السجلات اليومية:** خمسة جداول منفصلة (`HifzRecords`, `MatnRecords`, `OralExamSessions`, `DressRecords`, `ParentHomeFeedbacks`) كلها بالشكل نفسه تقريباً: *طالب + تاريخ + تقييم + ملاحظة + من سجّل*.
2. **جداول «سجل» متداخلة:** `ActivityFeeds` و `AuditLogs` و `UserNotifications` تحفظ كلها «ماذا حدث».
3. **أعمدة مكررة بين الجداول** (بيانات ولي الأمر داخل جدول الطالب، قيم مجمّعة محسوبة مخزّنة).
4. **جدول إعدادات عريض جداً:** `PlatformSettings` فيه 37 عموداً، وكل إعداد جديد يتطلب Migration.
5. **أعمدة غير مستخدمة** (`Teacher.AverageRating`).

---

## 2. خطة الدمج والحذف

### المرحلة (أ) — حذف آمن (أثر منخفض)

| # | الإجراء | المبرر | الأثر على الكود |
|---|---|---|---|
| A1 | **حذف `sysdiagrams`** | أنشأه SQL Server Management Studio عند فتح مخطط العلاقات، وليس جزءاً من المشروع | لا شيء |
| A2 | **حذف `ActivityFeeds`** | يكرر ما في `AuditLogs` و `UserNotifications`. «آخر النشاطات» في لوحة التحكم تُشتق من آخر سجلات `HifzRecords`/`Attendances` مباشرة | `DashboardController` + أماكن الإضافة في المتحكمات (8 ملفات) |
| A3 | **حذف عمود `Teacher.AverageRating`** | لا يُحسب ولا يُعرض فعلياً — يوهم بوجود تقييم للمحفّظين | `TeachersController` |
| A4 | **حذف `Student.Badges`** | الشارات يمكن اشتقاقها من `Points` ومن عدد الأجزاء المحفوظة عند العرض | 3 ملفات |

### المرحلة (ب) — دمج سجلات التسميع (الأثر الأكبر)

| # | الإجراء | التفصيل |
|---|---|---|
| B1 | **دمج `MatnRecords` في `HifzRecords`** | إضافة عمود `Subject` (`Quran` / `Matn`) وعمود `MatnId` (FK اختياري إلى `Matns`). الحقل `SurahName` يصبح «اسم السورة» للقرآن و`Portion` للمتن. **فائدة إضافية:** يحل مشكلة تكرار احتساب المتن من مكانين جذرياً (قيد تفرد واحد). |
| B2 | **دمج `OralExamSessions` + `OralExamQuestions` في `HifzRecords`** | الاختبار الشفوي هو «جلسة مراجعة بنمط الأسئلة». يُضاف `RevisionMode = Questions` (موجود أصلاً) وتُحفظ الأسئلة (تردد/تنبيه/فتح/درجة) في عمود JSON الموجود `SessionDetailsJson`، مع عمودي `ScorePercent` و `IsPassed`. |

**النتيجة:** 4 جداول → جدول واحد (`HifzRecords`)، مع بقاء `Matns` كجدول مرجعي.

### المرحلة (ج) — دمج التقييمات التربوية

| # | الإجراء | التفصيل |
|---|---|---|
| C1 | **دمج `PrayerDailyLogs` + `PrayerEntries` في جدول واحد `PrayerRecords`** | سجل لكل (طالب، تاريخ، صلاة) بقيد تفرد. الأعمدة المجمّعة (`PrayedCount`, `OnTimeCount`, `MosquePrayerCount`, `PrayedInMosque`, `OnTime`) **تُحذف** وتُحسب بالاستعلام عند الحاجة. |
| C2 | **دمج `DressRecords` + `ParentHomeFeedbacks` + `StudentMonthlyTargets` في جدول `StudentEvaluations`** | الأعمدة: `StudentId, Date, Kind (Dress/ParentHome/MonthlyTarget), Score, Target, Achieved, Notes, RecordedByUserId`. الثلاثة نفس الشكل: «طالب + فترة + درجة + ملاحظة». |
| C3 | (اختياري) **دمج `EvaluationPeriods` في `StudentPeriodEvaluations`** | بإضافة `PeriodName, StartDate, EndDate` مباشرة. يُفضَّل إبقاؤهما إن كانت الفترة مشتركة بين طلاب كثيرين (وهو الحال) — **التوصية: الإبقاء**. |

### المرحلة (د) — تنظيف الأعمدة المكررة

| # | الجدول | العمود | الإجراء |
|---|---|---|---|
| D1 | `Students` | `ParentPhone`, `GuardianName` | حذفهما — المرجع هو `Parents.Phone` و `AspNetUsers.FullName` لولي الأمر عبر `ParentId`. (تُنقل `GuardianRelationship` فقط للطالب لأنها تخص العلاقة.) |
| D2 | `Students` | `CreatedAt` | حذفه — `RegistrationDate` يكفي، و`AuditLogs` يسجّل وقت الإنشاء. |
| D3 | `Students` | `StudentPhone` | حذفه — رقم الطالب هو `AspNetUsers.PhoneNumber` لحسابه. |
| D4 | `Parents` | `Phone` | حذفه — يكرر `AspNetUsers.PhoneNumber`. (بعد D4 يصبح `Parents` جدول ربط فقط؛ يمكن الإبقاء عليه لوضوح العلاقة.) |
| D5 | `AspNetUsers` | `Email`, `NormalizedEmail`, `EmailConfirmed` | **نُفّذ جزئياً (الدفعة الثالثة):** البريد أُزيل من الواجهات والـ API، وتُفرَّغ قيمه عند التشغيل. الأعمدة تبقى لأنها جزء من Identity. |
| D5b | `PlatformSettings` | `Email` (بريد المركز)، `HifzRatingPassingEvaluations` | غير مستخدمين بعد الدفعتين الثانية والثالثة — يُحذفان مع تحويل الإعدادات لجدول مفتاح/قيمة (E1). |
| D6 | `ActivityFeeds` | `UserName` | (يُحذف مع الجدول في A2) — كان يكرر `AspNetUsers.FullName`. |
| D7 | `MonthlyParentReports` | `Phone`, `Content` | `Phone` يُشتق من ولي الأمر؛ `Content` يمكن إعادة توليده — يكفي حفظ (طالب، شهر، حالة الإرسال). |

### المرحلة (هـ) — جدول الإعدادات

| # | الإجراء | التفصيل |
|---|---|---|
| E1 | **تحويل `PlatformSettings` (37 عموداً) إلى جدول مفتاح/قيمة** | `Settings(Key nvarchar(100) PK, Value nvarchar(max), UpdatedAt)`. خدمة واحدة `SettingsService` تقرأ القيم وتعطي الافتراضي عند غيابها. **الفائدة:** لا Migration لكل إعداد جديد (مثل نقاط التقديرات في الدفعة الثانية). |

### مراجعة جداول أخرى (التوصية: الإبقاء)

| الجدول | سبب الإبقاء |
|---|---|
| `CircleEnrollments` | ضروري لميزة «الحلقات الإضافية» (علاقة كثير-لكثير). يمكن حذفه **فقط** إن أُلغيت الميزة. |
| `MonthlyParentReports` | يمنع إرسال التقرير الشهري مرتين لنفس الطالب — ضروري للخدمة المجدولة (مع تقليص أعمدته D7). |
| `WaitingListEntries` | ميزة مستقلة واضحة (قائمة الانتظار). |
| `AuditLogs` | متطلب أمني (سجل التدقيق) — نقطة قوة عند المناقشة. |
| `Messages`, `UserNotifications`, `Announcements` | ثلاث وظائف مختلفة: محادثة ثنائية، إشعار شخصي، إعلان عام. |
| `Payments`, `Expenses`, `LibraryItems` | ميزات مستقلة. |
| جداول Identity السبعة | جزء من إطار ASP.NET Identity — **لا تُحتسب عادةً** ضمن تصميم الطالب، ويُفضَّل ذكر ذلك صراحة للجنة. الجداول غير المستخدمة منها (`UserClaims`, `RoleClaims`, `UserLogins`, `UserTokens` — كلها فارغة) يمكن استبعادها بتخصيص `IdentityDbContext`، لكن **لا يُنصح** بذلك لأنه يعقّد الكود دون فائدة. |

---

## 3. النتيجة المتوقعة

| | قبل | بعد |
|---|---|---|
| جداول التطبيق | 29 | **22** (أو **18** مع التوصيات الاختيارية) |
| جداول Identity | 7 | 7 (تُعرض منفصلة كجزء من الإطار) |
| `sysdiagrams` | 1 | 0 |

**جداول التطبيق بعد التحسين (22):**
`Students, Teachers, Parents, Circles, CircleEnrollments, Attendances, HifzRecords, Matns, PrayerRecords, StudentEvaluations, EvaluationPeriods, StudentPeriodEvaluations, Payments, Expenses, LibraryItems, Announcements, Messages, UserNotifications, MonthlyParentReports, AuditLogs, WaitingListEntries, Settings`

> الوصول إلى **18** يتم بتطبيق التوصيات الاختيارية: دمج `EvaluationPeriods` (C3)، حذف `MonthlyParentReports` لصالح عمود `LastMonthlyReportMonth` في `Students`، حذف `CircleEnrollments` إن أُلغيت الحلقات الإضافية، ودمج `Announcements` في `UserNotifications` (إعلان = إشعار لمجموعة).

**طريقة العرض المقترحة للجنة:** تقسيم المخطط إلى 4 وحدات بدل مخطط واحد كبير:
1. **المستخدمون والحلقات:** Users, Students, Teachers, Parents, Circles, CircleEnrollments
2. **المتابعة التعليمية:** Attendances, HifzRecords, Matns, PrayerRecords, StudentEvaluations, EvaluationPeriods, StudentPeriodEvaluations
3. **التواصل:** Messages, UserNotifications, Announcements, MonthlyParentReports
4. **الإدارة والمالية:** Payments, Expenses, LibraryItems, WaitingListEntries, Settings, AuditLogs

---

## 4. ترتيب التنفيذ المقترح (عند البدء)

1. **نسخة احتياطية كاملة** لقاعدة البيانات قبل أي خطوة.
2. المرحلة (أ) — حذف آمن (Migration واحدة).
3. المرحلة (هـ) — جدول الإعدادات (يسهّل كل ما بعده).
4. المرحلة (ب) — دمج سجلات التسميع: Migration تنشئ الأعمدة الجديدة، ثم **سكربت SQL ينقل البيانات** من `MatnRecords`/`OralExamSessions` إلى `HifzRecords` (مع نقل `PointsAwarded`)، ثم تحديث المتحكمات والواجهات، ثم Migration أخيرة تحذف الجداول القديمة.
5. المرحلة (ج) — بالطريقة نفسها (إنشاء ← نقل ← تحديث الكود ← حذف).
6. المرحلة (د) — تنظيف الأعمدة بعد التأكد أن كل الواجهات تقرأ من المصدر الموحد.
7. بعد كل مرحلة: `dotnet test` + تجربة الأدوار الأربعة يدوياً + تحديث تطبيق الهاتف (النماذج في `lib/data/models`).

### المخاطر
- **تطبيق الهاتف** يعتمد على أشكال الاستجابات الحالية — يجب الحفاظ على نفس أسماء الحقول في الـ API أو تحديث التطبيق معه.
- **البيانات القائمة** — النقل يجب أن يحفظ `PointsAwarded` حتى لا يتغير رصيد نقاط الطلاب.
- **الاختبارات** (179 اختباراً) تعتمد على الكيانات الحالية — تُحدَّث في كل مرحلة.

### التقدير
| المرحلة | الجهد التقريبي |
|---|---|
| أ | ساعتان |
| هـ | نصف يوم |
| ب | يوم إلى يومين |
| ج | يوم |
| د | نصف يوم |

</div>
