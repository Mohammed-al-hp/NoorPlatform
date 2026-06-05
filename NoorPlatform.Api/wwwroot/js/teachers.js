// teachers.js - إدارة المحفظين وملفاتهم الشخصية
(function(global) {
    'use strict';

    async function saveTeacher() {
        const fullName = document.getElementById('teacherFullName')?.value?.trim() || '';
        const phone = document.getElementById('teacherPhone')?.value?.trim() || '';
        const qualification = document.getElementById('teacherQualification')?.value?.trim() || '';

        if (!fullName) {
            showToast('❌ يرجى إدخال اسم المحفظ');
            document.getElementById('teacherFullName')?.focus();
            return;
        }
        if (!isValidLibyanPhone(phone)) {
            showToast('❌ ' + (typeof libyanPhonePatternMsg === 'function' ? libyanPhonePatternMsg() : 'رقم الجوال يجب أن يبدأ بـ 09 ويتكون من 10 أرقام'));
            document.getElementById('teacherPhone')?.focus();
            return;
        }
        try {
            const data = await apiFetch('/teachers', 'POST', { fullName, phone, qualification });
            ['teacherFullName', 'teacherPhone', 'teacherQualification'].forEach(id => {
                const el = document.getElementById(id);
                if (el) el.value = '';
            });
            closeModal('addTeacherModal');
            showToast('✅ تم إضافة المحفظ بنجاح');
            if (data.credentials) {
                if (typeof showAccountCredentialsModal === 'function') {
                    showAccountCredentialsModal(data.credentials, phone);
                }
            }
            if (typeof fetchTeachers === 'function') fetchTeachers();
        } catch (e) {
            showToast('❌ ' + (e.message || 'تعذر الاتصال بالخادم'));
        }
    }

    async function viewTeacherProfile(teacherId) {
        openModal('teacherProfileModal');
        const titleEl = document.getElementById('profileModalTitle');
        if (titleEl) titleEl.textContent = '📋 ملف المحفظ';
        const body = document.getElementById('teacherProfileBody');
        if (!body) { showToast('❌ نافذة العرض غير متوفرة'); return; }
        body.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)">⏳ جاري التحميل...</div>';
        try {
            const t = await apiFetch('/teachers/' + teacherId);
            body.innerHTML = `
                    <div style="display:flex;align-items:center;gap:16px;margin-bottom:20px">
                        <div style="width:64px;height:64px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;color:#fff;font-weight:800;font-size:22px">${escapeHtml(t.fullName).slice(0, 2)}</div>
                        <div>
                            <h3 style="font-size:17px;font-weight:800">${escapeHtml(t.fullName)}</h3>
                            <p style="font-size:13px;color:var(--text-muted)">${escapeHtml(t.email)}</p>
                            <p style="font-size:12px;color:var(--text-muted);margin-top:2px">📚 ${escapeHtml(t.qualification || 'لم يحدد المؤهل')}</p>
                        </div>
                    </div>
                    <div style="background:var(--bg);border-radius:12px;padding:16px;margin-bottom:16px">
                        <p style="font-size:13px;font-weight:700;margin-bottom:12px">🔵 الحلقات المسندة (${t.circles?.length || 0})</p>
                        ${t.circles?.length ? t.circles.map(c => `
                            <div style="display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid var(--border);font-size:13px">
                                <span>${escapeHtml(c.name)}</span>
                                <span style="color:var(--text-muted)">${c.studentCount} طالب</span>
                            </div>`).join('') : '<p style="font-size:13px;color:var(--text-muted)">لا توجد حلقات مسندة</p>'}
                    </div>`;
        } catch {
            body.innerHTML = '<p style="color:var(--text-muted);text-align:center;padding:20px">تعذر تحميل بيانات المحفظ</p>';
        }
    }

    function messageTeacher(fullName, email) {
        if (email && email !== 'undefined') {
            window.location.href = `mailto:${email}?subject=رسالة من منصة نور&body=السلام عليكم أستاذ ${fullName}،`;
        } else {
            showToast('⚠️ لا يوجد بريد إلكتروني لهذا المحفظ');
        }
    }

    global.saveTeacher = saveTeacher;
    global.viewTeacherProfile = viewTeacherProfile;
    global.messageTeacher = messageTeacher;
})(window);
