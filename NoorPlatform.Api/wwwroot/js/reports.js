// reports.js - تصدير التقارير والشهادات (PDF, CSV)
(function(global) {
    'use strict';

    function showLoading(text) {
        const el = document.getElementById('globalLoading');
        const label = document.getElementById('loadingText');
        if (label) label.textContent = text || 'جاري التحميل...';
        if (el) el.classList.add('show');
    }
    
    function hideLoading() {
        const el = document.getElementById('globalLoading');
        if (el) el.classList.remove('show');
    }

    async function exportStudentPDF(studentId) {
        showLoading('جاري إنشاء التقرير...');
        try {
            const res = await fetch(`${API_URL}/reports/monthly/${studentId}`, {
                headers: { 'Authorization': `Bearer ${TOKEN}` }
            });
            if (res.status === 401) {
                if (typeof logout === 'function') logout();
                throw new Error('انتهت الجلسة، يرجى تسجيل الدخول مجدداً');
            }
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || 'تعذر تحميل التقرير');
            }

            const blob = await res.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `تقرير_طالب_${studentId}.pdf`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);

            showToast('✅ تم تصدير التقرير بنجاح');
        } catch (err) {
            showToast('❌ ' + (err.message || 'فشل تصدير PDF'));
        } finally {
            hideLoading();
        }
    }

    async function exportCertificatePDF(studentId) {
        showLoading('جاري إنشاء الشهادة...');
        try {
            const res = await fetch(`${API_URL}/reports/certificate/${studentId}`, {
                headers: { 'Authorization': `Bearer ${TOKEN}` }
            });
            if (res.status === 401) {
                if (typeof logout === 'function') logout();
                throw new Error('انتهت الجلسة، يرجى تسجيل الدخول مجدداً');
            }
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || 'تعذر تحميل الشهادة');
            }

            const blob = await res.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `شهادة_${studentId}.pdf`;
            document.body.appendChild(a);
            a.click();
            a.remove();
            window.URL.revokeObjectURL(url);

            showToast('✅ تم تصدير الشهادة بنجاح');
        } catch (err) {
            showToast('❌ ' + (err.message || 'فشل تصدير الشهادة'));
        } finally {
            hideLoading();
        }
    }

    async function grantBadge(studentId) {
        showLoading('جاري منح الوسام...');
        try {
            const res = await fetch(`${API_URL}/reports/certificate/${studentId}/grant-badge`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}` }
            });
            if (res.status === 401) {
                if (typeof logout === 'function') logout();
                throw new Error('انتهت الجلسة، يرجى تسجيل الدخول مجدداً');
            }
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || 'تعذر منح الوسام');
            }
            showToast('✅ تم منح وسام التميز بنجاح');
        } catch (err) {
            showToast('❌ ' + (err.message || 'فشل منح الوسام'));
        } finally {
            hideLoading();
        }
    }

    function exportStudentsExcel() {
        const students = window._students || window._allStudentsData || [];
        if (!students.length) {
            showToast('⚠️ لا توجد بيانات لتصديرها');
            return;
        }

        let tableHtml = `
            <html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:x="urn:schemas-microsoft-com:office:excel" xmlns="http://www.w3.org/TR/REC-html40">
            <head>
                <meta charset="utf-8" />
                <style>
                    table { border-collapse: collapse; width: 100%; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; }
                    th, td { border: 1px solid #dddddd; text-align: right; padding: 8px; }
                    th { background-color: #f2f2f2; font-weight: bold; }
                </style>
            </head>
            <body dir="rtl">
                <table>
                    <thead>
                        <tr>
                            <th>الرقم</th>
                            <th>اسم الطالب</th>
                            <th>رقم ولي الأمر</th>
                            <th>الحلقة</th>
                            <th>المحفظ المسؤول</th>
                            <th>المستوى</th>
                        </tr>
                    </thead>
                    <tbody>
        `;

        students.forEach(s => {
            tableHtml += `
                <tr>
                    <td>${s.id}</td>
                    <td>${s.fullName || ''}</td>
                    <td style="mso-number-format:'\@';">${s.parentPhone || ''}</td>
                    <td>${s.circleName || 'بدون حلقة'}</td>
                    <td>${s.teacherName || '—'}</td>
                    <td>${s.level || ''}</td>
                </tr>
            `;
        });

        tableHtml += `
                    </tbody>
                </table>
            </body>
            </html>
        `;

        const blob = new Blob([tableHtml], { type: 'application/vnd.ms-excel;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `قائمة_الطلاب_${new Date().toLocaleDateString('en-CA')}.xls`;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
        
        showToast('✅ تم التصدير بنجاح كملف Excel');
    }

    global.exportStudentPDF = exportStudentPDF;
    global.exportCertificatePDF = exportCertificatePDF;
    global.grantBadge = grantBadge;
    global.exportStudentsExcel = exportStudentsExcel;
    global.showLoading = showLoading;
    global.hideLoading = hideLoading;
})(window);
