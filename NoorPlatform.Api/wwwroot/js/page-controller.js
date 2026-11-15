/**
 * منصة نور — دوال التحكم بالصفحات والمتبقي من index.html
 */
(function (global) {
    'use strict';

    // ─── إعداد حساب جديد ──────────────────────────────
    let _lastAccountCredentials = null;
    function showAccountCredentialsModal(credentials, whatsappPhone) {
        _lastAccountCredentials = { ...credentials, whatsappPhone: whatsappPhone || credentials.displayPhone || credentials.phone };
        const body = document.getElementById('accountCredentialsBody');
        if (!body) return;
        body.innerHTML = `
            <p><strong>الاسم:</strong> ${escapeHtml(credentials.fullName)}</p>
            <p><strong>رقم الهاتف (تسجيل الدخول):</strong> <code>${escapeHtml(credentials.displayPhone || credentials.phone)}</code></p>
            <p><strong>كلمة المرور المؤقتة:</strong> <code>${escapeHtml(credentials.tempPassword)}</code></p>
            <p style="color:var(--text-muted);font-size:13px">يجب تغيير كلمة المرور بعد أول تسجيل دخول.</p>`;
        openModal('accountCredentialsModal');
    }

    function copyAccountCredentials() {
        if (!_lastAccountCredentials) return;
        const c = _lastAccountCredentials;
        const text = `منصة نور\nالاسم: ${c.fullName}\nرقم الهاتف: ${c.displayPhone || c.phone}\nكلمة المرور: ${c.tempPassword}`;
        navigator.clipboard.writeText(text).then(() => showToast('تم النسخ', 'success'));
    }

    function printAccountCredentials() {
        if (!_lastAccountCredentials) return;
        const c = _lastAccountCredentials;
        const w = window.open('', '_blank');
        if (!w) return;
        const d = w.document;
        d.open();
        d.write('<!DOCTYPE html><html dir="rtl"><head><meta charset="utf-8"></head>');
        d.write('<body style="font-family:Tajawal,sans-serif;padding:24px">');
        d.write('<h2>بيانات الدخول — منصة نور</h2>');
        d.write('<p>الاسم: ' + escapeHtml(c.fullName) + '</p>');
        d.write('<p>رقم الهاتف: ' + escapeHtml(c.displayPhone || c.phone) + '</p>');
        d.write('<p>كلمة المرور: ' + escapeHtml(c.tempPassword) + '</p>');
        d.write('</body></html>');
        d.close();
        w.print();
    }

    function sendAccountCredentialsWhatsApp() {
        if (!_lastAccountCredentials) return;
        const c = _lastAccountCredentials;
        const phone = toWhatsAppLibyanPhone(c.whatsappPhone || c.displayPhone || '');
        const msg = `السلام عليكم ورحمة الله وبركاته\n\nتم إنشاء حسابكم في منصة نور لتحفيظ القرآن الكريم.\n\nبيانات الدخول:\n\nرقم الهاتف: ${c.displayPhone || c.phone}\nكلمة المرور المؤقتة: ${c.tempPassword}\n\nيرجى تغيير كلمة المرور بعد أول تسجيل دخول.\n\nبارك الله فيكم.`;
        window.open(`https://wa.me/${phone}?text=${encodeURIComponent(msg)}`, '_blank');
    }

    // ─── إدارة التوجيه بين الصفحات ────────────────────────
    global._onNavigatePage = function (page) {
        if (page === 'students') {
            if (typeof setStudentFilter === 'function') {
                setStudentFilter('all', document.querySelector('.filter-chips .filter-chip'));
            }
        }
        if (page === 'attendance') {
            if (typeof renderAttendanceCircleChips === 'function') renderAttendanceCircleChips();
            if (typeof fetchAttendanceForDate === 'function') fetchAttendanceForDate();
        }
        if (page === 'memorization' && typeof fetchMemorizationData === 'function') fetchMemorizationData();
        if (page === 'memorization') global.NoorMonthlyRating?.initReport();
        if (page === 'library' && typeof fetchLibraryItems === 'function') fetchLibraryItems();
        if (page === 'evaluation' && typeof fetchPedagogicalPage === 'function') fetchPedagogicalPage('matn');
        if (page === 'myPrayer' && typeof fetchPedagogicalPage === 'function') fetchPedagogicalPage('prayer');
        if (page === 'parentHome' && typeof fetchPedagogicalPage === 'function') fetchPedagogicalPage('parentHome');
        if (page === 'myEvaluations' && typeof fetchPedagogicalPage === 'function') fetchPedagogicalPage('myEvaluations');
        if (page === 'studentView' && typeof fetchStudentView === 'function') fetchStudentView();
        if (page === 'parentView' && typeof fetchParentView === 'function') fetchParentView();
        if (page === 'payments' && typeof fetchPayments === 'function') fetchPayments();
        if (page === 'parentFees' && typeof fetchParentFees === 'function') fetchParentFees();
        if (page === 'parents' && global.USER?.role === 'Admin' && global.NoorParents) global.NoorParents.fetchParents();
        if (page === 'messages' && global.NoorMessages) global.NoorMessages.fetchMessages();
        if (page === 'users' && global.USER?.role === 'Admin' && global.NoorUsers) global.NoorUsers.fetchUsers();
        if (page === 'settings' && global.NoorDashboard) global.NoorDashboard.fetchSettings();
        if (page === 'expenses' && global.NoorExpenses) global.NoorExpenses.fetchExpenses();
        if (page === 'announcements' && global.NoorDashboard) global.NoorDashboard.fetchAnnouncements();
        if (page === 'reports') {
            if (typeof initReportDates === 'function') initReportDates();
            if (typeof loadReportData === 'function') loadReportData();
        }
    };

    window.addEventListener('popstate', function (e) {
        if (e.state && e.state.page) {
            var navEl = document.querySelector('[onclick*="navigate(\'' + e.state.page + '\'"]');
            if (typeof navigate === 'function') navigate(e.state.page, navEl);
        }
    });
    // ─── الطلاب (CRUD) ────────────────────────
    async function viewStudentDetails(id) {
        try {
            const [s, lastPos, oralSessions] = await Promise.all([
                apiFetch(`/students/${id}`),
                apiFetch(`/hifz/last-position/${id}`).catch(() => null),
                apiFetch(`/oral-exams/student/${id}`).catch(() => [])
            ]);
            const lastPosHtml = lastPos && lastPos.hasPrevious
                ? `<div style="text-align:center;padding:10px 14px;background:var(--bg);border-radius:10px;margin-bottom:16px">
                   <span style="font-size:12px;color:var(--text-muted)">آخر سورة جديدة وصل إليها</span>
                   <div style="font-weight:800;font-size:16px;color:var(--green-dark,#16a34a);margin-top:2px">${escapeHtml(lastPos.lastSurahName || lastPos.surahName || '—')}</div>
               </div>`
                : '';
            const hifzHtml = s.recentHifz && s.recentHifz.length > 0
                ? s.recentHifz.map(h => `<div style="display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid var(--border)">
                <span>${escapeHtml(h.surahName)} (${escapeHtml(h.verses)})</span><span class="status-badge status-excellent">${escapeHtml(h.evaluation)}</span>
               </div>`).join('')
                : '<p style="color:var(--text-muted)">لا توجد سجلات تسميع</p>';

            // آخر 8 جلسات أسئلة شفوية، بترتيب زمني تصاعدي للرسم (الأقدم أولاً)
            const sessions = (oralSessions || [])
                .slice(0, 8)
                .sort((a, b) => new Date(a.date) - new Date(b.date));

            const progressChartHtml = sessions.length >= 2
                ? `<h4 style="margin-bottom:8px">تطور الأداء في اختبارات الأسئلة</h4>
               <div style="margin-bottom:16px"><canvas id="studentProgressChart" height="90"></canvas></div>`
                : '';

            const progressListHtml = sessions.length > 0
                ? `<h4 style="margin-bottom:8px">${sessions.length >= 2 ? '' : 'اختبارات الأسئلة'}</h4>
                              ${sessions.slice().reverse().map(sess => `
                   <div style="display:flex;justify-content:space-between;padding:6px 0;border-bottom:1px solid var(--border);font-size:13px">
                       <span>${escapeHtml(formatDateEnGb ? formatDateEnGb(sess.date) : sess.date)}</span>
                       <span class="status-badge ${sess.isConsideredMemorized ? 'status-excellent' : ''}" style="${sess.isConsideredMemorized ? '' : 'background:#fee2e2;color:#dc2626'}">${sess.overallPercent}% — ${escapeHtml(sess.overallGrade || '')}</span>
                   </div>`).join('')}`
                : '';

            const html = `
            <div style="text-align:center;margin-bottom:20px">
                <div class="student-avatar-lg" style="background:var(--gradient);width:64px;height:64px;font-size:24px;margin:0 auto 10px">${escapeHtml((s.fullName || '').slice(0, 2))}</div>
                <h3>${escapeHtml(s.fullName)}</h3>
            </div>
            <div style="display:grid;gap:8px;margin-bottom:16px;font-size:13px;text-align:right">
                <p><strong>الحلقة:</strong> ${escapeHtml(s.circleName || 'بدون حلقة')}</p>
                <p><strong>المحفظ:</strong> ${escapeHtml(s.teacherName || '—')}</p>
                <p><strong>هاتف الطالب:</strong> <span dir="ltr">${escapeHtml(s.studentPhone || '—')}</span></p>
                <p><strong>ولي الأمر:</strong> ${escapeHtml(s.parentName || '—')} — <span dir="ltr">${escapeHtml(s.parentPhone || '')}</span></p>
                <p><strong>بريد حساب الدخول:</strong> ${escapeHtml(s.email || 'لا يوجد — الدخول برقم الهاتف')}</p>
            </div>
                        <div class="student-card-stats" style="margin-bottom:16px">
                <div class="mini-stat"><label>الحفظ</label><p>${s.progress ?? 0}%</p></div>
                <div class="mini-stat"><label>الحضور</label><p>${s.attendance ?? 0}%</p></div>
                <div class="mini-stat"><label>المستوى</label><p>${escapeHtml(s.level || '—')}</p></div>
            </div>
            ${lastPosHtml}
            ${progressChartHtml}
            ${progressListHtml}
            <h4 style="margin-bottom:8px;margin-top:16px">آخر سجلات التسميع</h4>
            ${hifzHtml}`;
            if (setModalBody('teacherProfileBody', html, (window.Icon ? window.Icon('user', { size: 16 }) : '') + ' بيانات الطالب')) {
                openModal('teacherProfileModal');
                if (sessions.length >= 2) renderStudentProgressChart(sessions);
            }
        } catch (err) { showToast(err.message, 'error'); }
    }

    function renderStudentProgressChart(sessions) {
        const canvas = document.getElementById('studentProgressChart');
        if (!canvas || typeof Chart === 'undefined') return;
        const ctx = canvas.getContext('2d');
        new Chart(ctx, {
            type: 'line',
            data: {
                labels: sessions.map(s => formatDateEnGb ? formatDateEnGb(s.date) : s.date),
                datasets: [{
                    label: 'نسبة الأداء',
                    data: sessions.map(s => s.overallPercent),
                    borderColor: '#16a34a',
                    backgroundColor: 'rgba(22,163,74,0.1)',
                    tension: 0.3,
                    fill: true,
                    pointRadius: 4,
                    pointBackgroundColor: sessions.map(s => s.isConsideredMemorized ? '#16a34a' : '#dc2626')
                }]
            },
            options: {
                responsive: true,
                plugins: { legend: { display: false } },
                scales: { y: { min: 0, max: 100, ticks: { stepSize: 25 } } }
            }
        });
    }

    async function editStudent(id) {
        try {
            const s = await apiFetch(`/students/${id}`);
            document.getElementById('editStudentId').value = id;
            document.getElementById('editStudentName').value = s.fullName || '';
            document.getElementById('editStudentBirthDate').value = s.dateOfBirth || '';
            document.getElementById('editStudentRegistrationDate').value = s.registrationDate || '';
            document.getElementById('editStudentPhone').value = s.studentPhone || '';
            document.getElementById('editStudentResidence').value = s.residence || '';
            document.getElementById('editGuardianName').value = s.parentName || '';
            document.getElementById('editParentPhone').value = s.parentPhone || '';
            document.getElementById('editGuardianRelationship').value = s.guardianRelationship || 'Father';
            document.getElementById('editStudentLevel').value = s.level || 'مبتدئ';
            const circleSel = document.getElementById('editStudentCircle');
            if (circleSel) {
                circleSel.innerHTML = '<option value="">— بدون حلقة —</option>' + (window._circles || []).map(c =>
                    `<option value="${c.id}">${escapeHtml(c.name)}</option>`
                ).join('');
                circleSel.value = s.circleId ? String(s.circleId) : '';
            }
            openModal('editStudentModal');
        } catch (err) {
            showToast(err.message || 'تعذر تحميل بيانات الطالب', 'error');
        }
    }

    async function submitEditStudent() {
        const id = document.getElementById('editStudentId').value;
        const payload = {
            fullName: document.getElementById('editStudentName').value.trim(),
            dateOfBirth: document.getElementById('editStudentBirthDate').value || null,
            registrationDate: document.getElementById('editStudentRegistrationDate').value || null,
            studentPhone: document.getElementById('editStudentPhone').value.trim() || null,
            residence: document.getElementById('editStudentResidence').value.trim() || null,
            guardianName: document.getElementById('editGuardianName').value.trim(),
            parentPhone: document.getElementById('editParentPhone').value.trim(),
            guardianRelationship: document.getElementById('editGuardianRelationship').value,
            level: document.getElementById('editStudentLevel').value,
            circleId: parseInt(document.getElementById('editStudentCircle').value, 10) || null
        };
        if (!payload.fullName) { showToast('الاسم الثلاثي مطلوب', 'warning'); return; }
        try {
            await apiFetch(`/students/${id}`, 'PUT', payload);
            closeModal('editStudentModal');
            showToast('تم تحديث بيانات الطالب', 'success');
            if (typeof fetchStudents === 'function') fetchStudents();
        } catch (err) { showToast(err.message, 'error'); }
    }

    /**
     * نافذة "هذا طالب سابق — استعادة بياناته؟" — تعيد 'restore' أو 'new' أو null (إلغاء).
     * تُنشأ ديناميكياً فوق النافذة الحالية.
     */
    function promptArchivedStudent(data) {
        return new Promise(resolve => {
            const s = data.archivedStudent || {};
            const overlay = document.createElement('div');
            overlay.className = 'modal-overlay open';
            overlay.style.zIndex = '1200';
            overlay.innerHTML = `
                <div class="modal" style="max-width:440px" role="dialog" aria-modal="true">
                    <div class="modal-header"><h3>${global.Icon ? global.Icon('refresh-cw', { size: 18 }) : ''} طالب سابق</h3></div>
                    <div class="modal-body">
                        <p style="font-weight:700;margin-bottom:10px">${escapeHtml(data.message || 'هذا طالب سابق — استعادة بياناته؟')}</p>
                        <p><strong>الاسم:</strong> ${escapeHtml(s.fullName || '—')}</p>
                        <p><strong>اسم الدخول:</strong> <code>${escapeHtml(s.displayPhone || '—')}</code></p>
                        <p><strong>الحلقة السابقة:</strong> ${escapeHtml(s.circleName || '—')} · <strong>النقاط:</strong> ${escapeHtml(String(s.points ?? 0))}</p>
                        <p style="font-size:12px;color:var(--text-muted);margin-top:10px">الاستعادة تعيد تفعيل حسابه نفسه مع سجلات الحفظ والحضور والنقاط، وتُحدّث بياناته بما أدخلته الآن، وتُولّد كلمة مرور مؤقتة جديدة.</p>
                    </div>
                    <div class="modal-footer" style="display:flex;gap:8px;flex-wrap:wrap">
                        ${data.canRestore ? '<button type="button" class="btn btn-primary" data-act="restore">استعادة بياناته</button>' : ''}
                        ${data.canRestore ? '<button type="button" class="btn btn-outline" data-act="new">طالب مختلف — إنشاء جديد</button>' : ''}
                        <button type="button" class="btn btn-outline" data-act="cancel">${data.canRestore ? 'إلغاء' : 'حسناً'}</button>
                    </div>
                </div>`;
            overlay.addEventListener('click', (ev) => {
                const act = ev.target.closest('[data-act]')?.dataset.act;
                if (!act && ev.target !== overlay) return;
                overlay.remove();
                resolve(act === 'restore' || act === 'new' ? act : null);
            });
            document.body.appendChild(overlay);
            overlay.querySelector('[data-act]')?.focus();
        });
    }

    /** يرسل الطلب، وعند اكتشاف طالب سابق يسأل الأدمن ثم يعيد الإرسال بالقرار. */
    async function submitWithArchivedCheck(submit) {
        try {
            return await submit();
        } catch (err) {
            if (err.status !== 409 || err.data?.code !== 'ARCHIVED_STUDENT_FOUND') throw err;
            const choice = await promptArchivedStudent(err.data);
            if (!choice) return null;
            return await submit(choice === 'restore'
                ? { restoreArchivedStudentId: err.data.archivedStudent.id }
                : { ignoreArchivedMatch: true });
        }
    }
    global.submitWithArchivedCheck = submitWithArchivedCheck;

    async function saveStudent() {
        const modal = document.getElementById('addStudentModal');
        const fullName = modal.querySelector('#studentFullName')?.value.trim() || '';
        const existingParentId = modal.querySelector('#existingParentId')?.value || '';
        const parentPhone = modal.querySelector('#parentPhone')?.value.trim() || '';
        const phone = modal.querySelector('#phone')?.value.trim() || '';
        const birthDate = modal.querySelector('#birthDate')?.value || '';
        const registrationDate = modal.querySelector('#registrationDate')?.value || '';
        const halaqa = modal.querySelector('#halaqa')?.value || '';
        const level = modal.querySelector('#level')?.value || '';
        const guardianName = modal.querySelector('#guardianName')?.value.trim() || '';
        const guardianRelationship = modal.querySelector('#guardianRelationship')?.value || '';
        const residence = modal.querySelector('#residence')?.value.trim() || '';

        // إن اختار المستخدم ولي أمر موجوداً مسبقاً، لا حاجة لاسم/هاتف/صلة قرابة جديدة
        if (!fullName || !birthDate || !registrationDate) {
            showToast('يرجى تعبئة جميع الحقول الإلزامية', 'warning');
            return;
        }
        if (!existingParentId && (!parentPhone || !guardianName || !guardianRelationship)) {
            showToast('اختر ولي أمر موجوداً أو أدخل بيانات ولي أمر جديد كاملة', 'warning');
            return;
        }

        showToast('جاري حفظ البيانات...', 'info');
        const submit = (extra) => apiFetch('/students', 'POST', {
                ...(extra || {}),
                fullName,
                phone: phone || null,
                parentId: existingParentId ? parseInt(existingParentId, 10) : null,
                parentPhone: existingParentId ? null : parentPhone,
                guardianName: existingParentId ? null : guardianName,
                guardianRelationship: existingParentId ? null : guardianRelationship,
                dateOfBirth: birthDate,
                registrationDate,
                residence: residence || null,
                circleId: parseInt(halaqa, 10) || null,
                level: level || null
            });
        try {
            const data = await submitWithArchivedCheck(submit);
            if (!data) return; // أُلغيت العملية من نافذة "طالب سابق"
            closeModal('addStudentModal');
            showToast(data.restored ? data.message : 'تم إضافة الطالب بنجاح', 'success');
            if (data.credentials) showAccountCredentialsModal(data.credentials, parentPhone);
            const pc = data.parentCredentials;
            const credBody = document.getElementById('accountCredentialsBody');
            if (pc && credBody) {
                credBody.insertAdjacentHTML('beforeend',
                    `<hr style="margin:12px 0">
         <p><strong>حساب ولي الأمر الجديد:</strong> ${escapeHtml(pc.fullName)}</p>
         <p><strong>رقم الهاتف:</strong> <code>${escapeHtml(pc.displayPhone)}</code></p>
         <p><strong>كلمة المرور المؤقتة:</strong> <code>${escapeHtml(pc.tempPassword)}</code></p>`);
            }
            if (typeof fetchStudents === 'function') fetchStudents();
        } catch (error) {
            showToast(error.message || 'تعذر الاتصال بالخادم', 'error');
        }
    }

    // ─── ملاحظة: دوال المحفظين والحلقات (fetchTeachers, editTeacher, submitEditTeacher,
    //      deleteTeacher, fetchCircles, editCircle, submitEditCircle, deleteCircle)
    //      موجودة في teachers.js — تم إزالة النسخ المكررة هنا ───

    // ─── الحفظ والاختبارات ────────────────────────
    function deleteHifzRecord(id) {
        global.confirmDelete('حذف سجل التسميع؟', async () => {
            try {
                await apiFetch(`/hifz/${id}`, 'DELETE');
                showToast('تم حذف السجل', 'success');
                if (typeof fetchMemorizationData === 'function') fetchMemorizationData();
            } catch (err) { showToast(err.message, 'error'); }
        });
    }

    async function sendBulkAbsenceNotifs(circleId) {
        const cid = parseInt(circleId, 10);
        if (!cid || isNaN(cid)) {
            showToast('اختر حلقة أولاً من القائمة أعلاه', 'warning');
            return;
        }

        global.confirmDelete('إرسال إشعارات غياب لجميع أولياء الأمور عبر واتساب؟', async () => {
            const btns = document.querySelectorAll('button');
            let targetBtn = null;
            for (const b of btns) { if (b.textContent.includes('إرسال إشعارات للغائبين')) { targetBtn = b; break; } }
            try {
                if (global.setBtnLoading && targetBtn) global.setBtnLoading(targetBtn, true);
                const res = await apiFetch('/notifications/bulk-absence', 'POST', { circleId: cid });
                showToast(`${res.message}`, 'success');
            } catch (err) { showToast(err.message, 'error'); }
            finally {
                if (global.setBtnLoading && targetBtn) global.setBtnLoading(targetBtn, false, targetBtn.dataset.origText || 'إرسال إشعارات للغائبين');
            }
        });
    }

    async function sendHifzPraise(studentId, surahName, verses, evaluation) {
        try {
            await apiFetch('/notifications/hifz-praise', 'POST', { studentId, surahName, verses, evaluation });
            showToast('تم إرسال رسالة المديح لولي الأمر', 'success');
        } catch (err) { showToast(err.message, 'error'); }
    }

    async function saveAnn() {
        const title = document.getElementById('annTitle')?.value.trim();
        const content = document.getElementById('annContent')?.value.trim();
        const target = document.getElementById('annTarget')?.value || 'الجميع';
        const btn = document.querySelector('#addAnnModal .btn-primary');
        if (!title || !content) {
            showToast('العنوان ونص الإعلان مطلوبان', 'warning');
            return;
        }
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await apiFetch('/announcements', 'POST', { title, content, target });
            closeModal('addAnnModal');
            showToast('تم نشر الإعلان بنجاح', 'success');
            global.NoorDashboard?.fetchAnnouncements?.();
        } catch (err) {
            showToast(err.message || 'حدث خطأ أثناء النشر', 'error');
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
        }
    }

    async function fetchMemorizationData() {
        const data = await apiFetch('/students');
        window._students = data;
        if (window.NoorHifz) {
            NoorHifz.populateMemorizationFilter();
            NoorHifz.initHifzModal();
        }

        const panel = document.getElementById('recentSessions');
        if (panel && data.length > 0) {
            try {
                const records = await apiFetch('/hifz/recent?count=8');
                panel.innerHTML = records.length ? records.map(r => `
            <div style="display:flex;justify-content:space-between;align-items:center;padding:10px 0;border-bottom:1px solid var(--border)">
              <div>
                <p style="font-weight:700;font-size:13px">${escapeHtml(r.studentName)} — ${escapeHtml(r.surahName)} (${escapeHtml(r.verses)})</p>
                <p style="font-size:11px;color:var(--text-muted)">${formatDateEnGb(r.date)} · ${r.type === 'Memorization' ? 'حفظ جديد' : 'مراجعة'}</p>
              </div>
              <div style="display:flex;align-items:center;gap:8px">
                  <span class="status-badge ${r.evaluation === 'ممتاز' ? 'status-excellent' : 'status-good'}">${escapeHtml(r.evaluation)}</span>
                  <button class="btn btn-outline" style="padding:4px 8px;font-size:11px;color:#10b981;border-color:#10b981" onclick="sendHifzPraise(${r.studentId}, ${jsArg(r.surahName)}, ${jsArg(r.verses)}, ${jsArg(r.evaluation)})" title="إرسال رسالة مديح لولي الأمر عبر واتساب">${window.Icon ? window.Icon('message-circle', { size: 12 }) : ''} مديح</button>
                  <button class="btn btn-outline" style="padding:4px 8px;font-size:11px;color:#ef4444;border-color:#ef4444" onclick="deleteHifzRecord(${r.id})" title="حذف السجل">${window.Icon ? window.Icon('trash-2', { size: 12 }) : ''}</button>
              </div>
            </div>`).join('') : '<p style="color:var(--text-muted);font-size:13px">لا توجد جلسات بعد</p>';
            } catch { panel.innerHTML = '<p style="color:var(--text-muted);font-size:13px">لا توجد جلسات بعد</p>'; }
        }
    }

    // ─── المكتبة ────────────────────────
    async function fetchLibraryItems() {
        try {
            const search = document.getElementById('librarySearchInput')?.value || '';
            const category = document.getElementById('libraryCategoryFilter')?.value || '';
            let url = '/library?';
            if (search) url += `search=${encodeURIComponent(search)}&`;
            if (category) url += `category=${encodeURIComponent(category)}`;

            const data = await apiFetch(url);
            window._libraryItems = data;
            renderLibraryItems();
        } catch (err) {
            console.error(err);
            showToast('حدث خطأ في تحميل المكتبة', 'error');
        }
    }

    function filterLibraryItems() {
        fetchLibraryItems();
    }

    function renderLibraryItems() {
        const container = document.getElementById('libraryCards');
        const emptyState = document.getElementById('libraryEmptyState');
        if (!container || !emptyState) return;
        const data = window._libraryItems || [];
        const canUpload = global.USER?.role === 'Admin' || global.USER?.role === 'Teacher';
        const emptyBtn = document.getElementById('btnAddLibraryFileEmpty');
        if (emptyBtn) emptyBtn.style.display = canUpload ? 'inline-flex' : 'none';

        if (data.length === 0) {
            container.style.display = 'none';
            emptyState.style.display = 'block';
        } else {
            container.style.display = 'grid';
            emptyState.style.display = 'none';
            container.innerHTML = data.map(item => `
                <div class="student-card">
                    <div style="display:flex; justify-content:space-between; align-items:flex-start">
                        <h3 style="font-size:16px; font-weight:700; margin-bottom:4px">${escapeHtml(item.title)}</h3>
                        <span class="status-badge status-excellent" style="font-size:10px">${escapeHtml(item.category)}</span>
                    </div>
                    <p style="color:var(--text-muted); font-size:12px; margin-bottom:12px; height:36px; overflow:hidden">${escapeHtml(item.description)}</p>
                    <div class="stats-row" style="margin-bottom:16px">
                        <div class="stat-item"><div class="stat-value" style="font-size:12px">${formatDateEnGb(item.createdAt)}</div><div class="stat-label">تاريخ الرفع</div></div>
                        <div class="stat-item"><div class="stat-value" style="font-size:12px">${item.downloadCount}</div><div class="stat-label">تنزيلات</div></div>
                    </div>
                    <div style="font-size:11px; color:var(--text-muted); margin-bottom:12px">رافع الملف: ${escapeHtml(item.uploadedBy)}</div>
                    <div class="actions" style="margin-top:auto; display:flex; gap:8px">
                        <button type="button" class="btn btn-primary btn-view-pdf" style="flex:1; padding:8px" data-library-id="${item.id}">${window.Icon ? window.Icon('eye', { size: 12 }) : ''} عرض</button>
                        ${canUpload ? `<button class="btn btn-outline" style="padding:8px 12px; color:#ef4444; border-color:#ef4444" onclick="deleteLibraryItem(${item.id})">${window.Icon ? window.Icon('trash-2', { size: 12 }) : ''}</button>` : ''}
                    </div>
                </div>
            `).join('');
        }
    }

    if (!window._libraryViewBound) {
        window._libraryViewBound = true;
        document.getElementById('libraryCards')?.addEventListener('click', e => {
            const btn = e.target.closest('.btn-view-pdf');
            if (!btn) return;
            e.preventDefault();
            e.stopPropagation();
            const id = parseInt(btn.getAttribute('data-library-id'), 10);
            if (!id) return;
            const item = (window._libraryItems || []).find(x => x.id === id);
            viewLibraryPdf(id, item?.title || 'ملف');
        });
    }

    function openUploadLibraryModal() {
        if (!global.TOKEN) { showToast('يرجى تسجيل الدخول أولاً', 'warning'); return; }
        if (global.USER?.role !== 'Admin' && global.USER?.role !== 'Teacher') { showToast('غير مصرح لك بإضافة ملفات', 'warning'); return; }
        document.getElementById('uploadLibraryForm')?.reset();
        openModal('uploadLibraryModal');
    }

    function closeUploadLibraryModal() {
        closeModal('uploadLibraryModal');
    }

    async function handleUploadLibrary(e) {
        e.preventDefault();
        const form = e.target;
        const formData = new FormData(form);
        const btn = document.getElementById('btnUploadLibrary');
        try {
            btn.disabled = true;
            btn.innerHTML = 'جاري الرفع... ' + (window.Icon ? '<span class="spin-icon">' + window.Icon('loader', { size: 14 }) + '</span>' : '');
            const fileInput = form.querySelector('input[name="file"]');
            if (fileInput?.files[0] && fileInput.files[0].size > 50 * 1024 * 1024) throw new Error('حجم الملف يتجاوز 50 ميجابايت');

            const response = await fetch(API_URL + '/library/upload', {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}` },
                body: formData
            });
            let data = {};
            const ct = response.headers.get('content-type') || '';
            if (ct.includes('application/json')) data = await response.json();
            if (!response.ok) throw new Error(data.message || (response.status === 413 ? 'حجم الملف كبير جداً — الحد الأقصى 50MB' : 'فشل الرفع — تحقق من الاتصال وحجم الملف'));

            showToast(data.message, 'success');
            closeUploadLibraryModal();
            fetchLibraryItems();
        } catch (err) { showToast(err.message, 'error'); }
        finally { btn.disabled = false; btn.innerHTML = 'رفع الملف ' + (window.Icon ? window.Icon('upload', { size: 14 }) : ''); }
    }

    function deleteLibraryItem(id) {
        global.confirmDelete('هل أنت متأكد من حذف هذا الملف نهائياً؟', async () => {
            try {
                await apiFetch(`/library/${id}`, 'DELETE');
                showToast('تم حذف الملف بنجاح', 'success');
                fetchLibraryItems();
            } catch (err) { showToast(err.message, 'error'); }
        });
    }

    async function viewLibraryPdf(id, title) {
        if (!global.TOKEN) { showToast('يرجى تسجيل الدخول أولاً', 'warning'); return; }
        const iframe = document.getElementById('pdfViewerIframe');
        const titleEl = document.getElementById('pdfViewerTitle');
        const downBtn = document.getElementById('pdfViewerDownloadBtn');
        if (!iframe || !titleEl || !downBtn) { showToast('عارض الملف غير متوفر', 'error'); return; }

        titleEl.textContent = title || 'عرض الملف';
        iframe.src = '';
        openModal('pdfViewerModal');
        try {
            const res = await fetch(`${API_URL}/library/${id}/file`, { headers: { 'Authorization': `Bearer ${TOKEN}` } });
            if (res.status === 401) { if (typeof logout === 'function') logout(); return; }
            if (!res.ok) { const err = await res.json().catch(() => ({})); throw new Error(err.message || 'تعذر تحميل الملف'); }
            const blob = await res.blob();
            if (!blob.size) throw new Error('الملف فارغ أو تالف');
            const blobUrl = URL.createObjectURL(blob);
            if (iframe._blobUrl) URL.revokeObjectURL(iframe._blobUrl);
            iframe._blobUrl = blobUrl;
            iframe.src = blobUrl;
            downBtn.href = blobUrl;
            downBtn.download = (title || 'document').replace(/[^\w\u0600-\u06FF\s.-]/g, '') + '.pdf';
        } catch (err) {
            showToast(err.message || 'فشل عرض الملف', 'error');
            closePdfViewerModal();
        }
    }

    function closePdfViewerModal() {
        closeModal('pdfViewerModal');
        const iframe = document.getElementById('pdfViewerIframe');
        if (iframe && iframe._blobUrl) {
            URL.revokeObjectURL(iframe._blobUrl);
            iframe._blobUrl = null;
        }
        if (iframe) iframe.src = '';
    }

    // تصدير للدوال
    global.showAccountCredentialsModal = showAccountCredentialsModal;
    global.copyAccountCredentials = copyAccountCredentials;
    global.printAccountCredentials = printAccountCredentials;
    global.sendAccountCredentialsWhatsApp = sendAccountCredentialsWhatsApp;

    global.viewStudentDetails = viewStudentDetails;
    global.editStudent = editStudent;
    global.submitEditStudent = submitEditStudent;
    global.saveStudent = saveStudent;

    // ─── تصدير المحفظين/الحلقات تم نقله إلى teachers.js ───

    global.deleteHifzRecord = deleteHifzRecord;
    global.sendBulkAbsenceNotifs = sendBulkAbsenceNotifs;
    global.sendHifzPraise = sendHifzPraise;
    global.saveAnn = saveAnn;
    global.fetchMemorizationData = fetchMemorizationData;

    global.fetchLibraryItems = fetchLibraryItems;
    global.filterLibraryItems = filterLibraryItems;
    global.renderLibraryItems = renderLibraryItems;
    global.openUploadLibraryModal = openUploadLibraryModal;
    global.closeUploadLibraryModal = closeUploadLibraryModal;
    global.handleUploadLibrary = handleUploadLibrary;
    global.deleteLibraryItem = deleteLibraryItem;
    global.viewLibraryPdf = viewLibraryPdf;
    global.closePdfViewerModal = closePdfViewerModal;

    // تهيئة الأحداث الإضافية
    window.addEventListener('DOMContentLoaded', () => {
        ['btnAddLibraryFile', 'btnAddLibraryFileEmpty'].forEach(id => {
            const btn = document.getElementById(id);
            if (btn) btn.addEventListener('click', e => { e.preventDefault(); openUploadLibraryModal(); });
        });
        const logoutBtn = document.querySelector('.logout-btn');
        if (logoutBtn) logoutBtn.addEventListener('click', global.logout);
        if (typeof global.checkAuth === 'function') global.checkAuth();
    });

})(window);