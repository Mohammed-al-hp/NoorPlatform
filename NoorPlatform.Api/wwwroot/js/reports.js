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
            if (!res.ok) throw new Error('تعذر تحميل التقرير');
            
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
            if (!res.ok) throw new Error('تعذر تحميل الشهادة');
            
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
            if (!res.ok) throw new Error('تعذر منح الوسام');
            showToast('✅ تم منح وسام التميز بنجاح');
        } catch (err) {
            showToast('❌ ' + (err.message || 'فشل منح الوسام'));
        } finally {
            hideLoading();
        }
    }

    function exportStudentsCSV() {
        const students = window._students || window._allStudentsData || [];
        if (!students.length) {
            showToast('⚠️ لا توجد بيانات لتصديرها');
            return;
        }

        let csvContent = "data:text/csv;charset=utf-8,\uFEFF";
        csvContent += "الرقم,اسم الطالب,رقم ولي الأمر,الحلقة,المستوى\n";

        students.forEach(s => {
            const row = [
                s.id,
                `"${s.fullName}"`,
                `"${s.parentPhone || ''}"`,
                `"${s.circleName || 'غير محدد'}"`,
                `"${s.level || ''}"`
            ];
            csvContent += row.join(",") + "\n";
        });

        const encodedUri = encodeURI(csvContent);
        const link = document.createElement("a");
        link.setAttribute("href", encodedUri);
        link.setAttribute("download", `طلاب_نور_${new Date().toLocaleDateString('en-CA')}.csv`);
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        showToast('✅ تم التصدير بنجاح');
    }

    global.exportStudentPDF = exportStudentPDF;
    global.exportCertificatePDF = exportCertificatePDF;
    global.grantBadge = grantBadge;
    global.exportStudentsCSV = exportStudentsCSV;
    global.showLoading = showLoading;
    global.hideLoading = hideLoading;
})(window);
