/**
 * منصة نور — تفعيل تقويم عربي حديث (ميلادي، أرقام إنجليزية) على كل حقول التاريخ
 */
(function (global) {
    'use strict';

    const arabicLocale = {
        weekdays: {
            shorthand: ['أحد', 'اثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة', 'سبت'],
            longhand: ['الأحد', 'الاثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت']
        },
        months: {
            shorthand: ['ينا', 'فبر', 'مار', 'أبر', 'ماي', 'يون', 'يول', 'أغس', 'سبت', 'أكت', 'نوف', 'ديس'],
            longhand: ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر']
        },
        rangeSeparator: ' إلى ',
        weekAbbreviation: 'أسبوع',
        scrollTitle: 'مرر للتغيير',
        toggleTitle: 'اضغط للتبديل',
        firstDayOfWeek: 0,
        ordinal: () => ''
    };

    function initDatePickers(root) {
        const scope = root || document;
        const inputs = scope.querySelectorAll('input[type="date"]');

        inputs.forEach(input => {
            // تجنب التفعيل المزدوج لنفس الحقل
            if (input.dataset.flatpickrInit === '1') return;
            input.dataset.flatpickrInit = '1';

            global.flatpickr(input, {
                locale: arabicLocale,
                dateFormat: 'Y-m-d',   // القيمة الفعلية المُرسلة للسيرفر (تبقى كما هي)
                altInput: true,
                altFormat: 'd/m/Y',    // الشكل المعروض للمستخدم — أرقام إنجليزية دائمًا
                allowInput: true,
                disableMobile: true    // فرض تقويم flatpickr حتى على الجوال بدل التقويم الأصلي للنظام
            });
        });
    }

    // تشغيل أولي عند تحميل الصفحة
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => initDatePickers(document));
    } else {
        initDatePickers(document);
    }

    // إتاحة استدعائها يدويًا لاحقًا (مثلًا بعد إضافة عناصر ديناميكية جديدة)
    global.NoorDatePickers = { init: initDatePickers };
})(window);