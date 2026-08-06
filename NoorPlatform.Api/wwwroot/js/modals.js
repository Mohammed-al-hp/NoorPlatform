/**
 * منصة نور — التنقل بين الصفحات (shell)
 */
(function (global) {
    'use strict';

    const PAGE_TITLES = {
        dashboard: ['لوحة التحكم', 'نظرة عامة على المركز'],
        students: ['إدارة الطلاب', 'الطلاب المسجلون'],
        teachers: ['إدارة المحفظين', 'فريق التحفيظ'],
        circles: ['الحلقات الدراسية', 'جداول الحلقات'],
        attendance: ['الحضور والغياب', 'تسجيل يومي'],
        memorization: ['الحفظ والتسميع', 'سجلات التسميع'],
        exams: ['الاختبارات والتقييمات', 'نتائج الاختبارات'],
        announcements: ['الإعلانات', 'آخر الأخبار'],
        studentView: ['بوابة الطالب', 'متابعة تقدمك'],
        parentView: ['بوابة ولي الأمر', 'متابعة أبنائك'],
        payments: ['إدارة المدفوعات', 'الفواتير والرسوم'],
        parentFees: ['الرسوم الدراسية', 'فواتير الأبناء'],
        library: ['المكتبة الرقمية', 'ملفات ومراجع التحفيظ'],
        parents: ['إدارة أولياء الأمور', 'ربط الأبناء بالحسابات'],
        users: ['إدارة المستخدمين', 'جميع حسابات المنصة'],
        reports: ['التقارير', 'تقارير الأداء والحضور والحفظ'],
        settings: ['الإعدادات', 'إعدادات المنصة والنظام'],
    };

    function navigate(page, el) {
        document.querySelectorAll('.content').forEach(c => c.classList.remove('active'));
        document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
        const target = document.getElementById('page-' + page);
        if (target) target.classList.add('active');
        if (el) el.classList.add('active');

        const titles = PAGE_TITLES[page];
        if (titles) {
            const pt = document.getElementById('pageTitle');
            const ps = document.getElementById('pageSubtitle');
            if (pt) pt.textContent = titles[0];
            if (ps) ps.textContent = titles[1];
        }

        if (typeof global._onNavigatePage === 'function') global._onNavigatePage(page);
        if (history.pushState) history.pushState({ page }, '', '#' + page);
        global.NoorApp?.ui?.closeSidebar();
    }

    function navBottom(page, el) {
        document.querySelectorAll('.bottom-nav-item').forEach(i => i.classList.remove('active'));
        if (el) el.classList.add('active');
        const navEl = document.querySelector('[onclick*="navigate(\'' + page + '\'"]');
        navigate(page, navEl);
    }

    global.NoorApp = global.NoorApp || {};
    global.NoorApp.navigate = navigate;
    global.navigate = navigate;
    global.navBottom = navBottom;
})(window);
