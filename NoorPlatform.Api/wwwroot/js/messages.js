/**
 * منصة نور — نظام الرسائل (ولي الأمر ↔ المحفّظ/الإدارة)
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = (s) => utils().escapeHtml(s);

    function isParent() {
        return app().state.user?.role === 'Parent';
    }

    async function fetchMessages() {
        const list = document.getElementById('messagesList');
        const btnNew = document.getElementById('btnNewMessage');
        if (!list) return;

        if (btnNew) btnNew.style.display = isParent() ? 'inline-flex' : 'none';

        list.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)"><span class="spin-icon">' + (window.Icon ? window.Icon('loader', {size:14}) : '') + '</span> جاري التحميل...</div>';

        try {
            const endpoint = isParent() ? '/messages/sent' : '/messages/inbox';
            const data = await apiFetch(endpoint);

            if (!data.length) {
                list.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد رسائل بعد</div>';
                return;
            }

            if (isParent()) {
                list.innerHTML = data.map(m => `
                    <div style="padding:16px 20px;border-bottom:1px solid var(--border)">
                        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
                            <strong style="font-size:13px">إلى: ${esc(m.recipientName)}</strong>
                            <span style="font-size:11px;color:var(--text-muted)">${utils().formatDateTimeEnGb(m.createdAt)}</span>
                        </div>
                        <p style="font-size:13px;color:var(--text)">${esc(m.content)}</p>
                    </div>`).join('');
            } else {
                list.innerHTML = data.map(m => `
                    <div style="padding:16px 20px;border-bottom:1px solid var(--border);${m.isRead ? '' : 'background:var(--green-light)'}" data-msg-id="${m.id}">
                        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
                            <strong style="font-size:13px">من: ${esc(m.senderName)}</strong>
                            <span style="font-size:11px;color:var(--text-muted)">${utils().formatDateTimeEnGb(m.createdAt)}</span>
                        </div>
                        <p style="font-size:13px;color:var(--text)">${esc(m.content)}</p>
                        ${!m.isRead ? `<button class="btn btn-outline" style="margin-top:8px;padding:4px 10px;font-size:11px" onclick="NoorMessages.markAsRead(${m.id})">${window.Icon ? window.Icon('check', {size:12}) : ''} تعليم كمقروءة</button>` : ''}
                    </div>`).join('');
            }
        } catch (e) {
            list.innerHTML = '<div style="text-align:center;padding:40px;color:#ef4444">تعذر تحميل الرسائل</div>';
        }
    }

    async function markAsRead(id) {
        try {
            await apiFetch(`/messages/${id}/read`, 'PATCH');
            fetchMessages();
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    async function openNewMessageModal() {
        const sel = document.getElementById('msgRecipientSelect');
        if (sel) {
            sel.innerHTML = '<option value="Admin|">إدارة المركز</option>';
            try {
                const teachers = await apiFetch('/messages/available-recipients');
                teachers.forEach(t => {
                    sel.innerHTML += `<option value="Teacher|${t.id}">${esc(t.fullName)}</option>`;
                });
            } catch (e) { /* صامت — يبقى خيار الإدارة متاحًا على الأقل */ }
        }
        document.getElementById('msgContent').value = '';
        app().ui.openModal('newMessageModal');
    }

    async function sendNewMessage() {
        const sel = document.getElementById('msgRecipientSelect');
        const content = document.getElementById('msgContent')?.value?.trim();

        if (!content) {
            app().ui.showToast('نص الرسالة مطلوب', 'error');
            return;
        }

        const [recipientType, recipientTeacherId] = (sel.value || '').split('|');

        const btn = document.querySelector('#newMessageModal .btn-primary');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await apiFetch('/messages', 'POST', {
                recipientType,
                recipientTeacherId: recipientTeacherId ? parseInt(recipientTeacherId, 10) : null,
                content
            });
            app().ui.closeModal('newMessageModal');
            app().ui.showToast('تم إرسال الرسالة بنجاح', 'success');
            fetchMessages();
        } catch (e) {
            app().api.handleApiError(e);
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
        }
    }
    async function openChildDetails(studentId, fullName) {
        document.getElementById('childDetailsTitle').innerHTML = (window.Icon ? window.Icon('clipboard-list', {size:16}) : '') + ' تفاصيل: ' + fullName;
        app().ui.openModal('childDetailsModal');

        const attList = document.getElementById('childAttendanceList');
        const hifzList = document.getElementById('childHifzList');
        attList.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)"><span class="spin-icon">' + (window.Icon ? window.Icon('loader', {size:14}) : '') + '</span> جاري التحميل...</div>';
        hifzList.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)"><span class="spin-icon">' + (window.Icon ? window.Icon('loader', {size:14}) : '') + '</span> جاري التحميل...</div>';

        try {
            const records = await apiFetch(`/attendance/student/${studentId}`);
            if (!records.length) {
                attList.innerHTML = '<p style="text-align:center;padding:16px;color:var(--text-muted);font-size:13px">لا توجد سجلات حضور بعد</p>';
            } else {
                const ic = (n, s) => window.Icon ? window.Icon(n, {size: s || 12}) : '';
            const statusLabels = {
                Present: ic('check-circle') + ' حاضر',
                Late: ic('clock') + ' متأخر',
                ExcusedAbsence: ic('file-text') + ' غائب بإذن',
                UnexcusedAbsence: ic('x-circle') + ' غائب بدون إذن'
            };
                attList.innerHTML = records.map(r => `
                <div style="display:flex;justify-content:space-between;padding:8px 4px;border-bottom:1px solid var(--border);font-size:13px">
                    <span>${utils().formatDateEnGb(r.date)}</span>
                    <span>${statusLabels[r.status] || r.status}</span>
                </div>`).join('');
            }
        } catch (e) {
            attList.innerHTML = '<p style="text-align:center;padding:16px;color:#ef4444;font-size:13px">تعذر تحميل سجل الحضور</p>';
        }

        try {
            const records = await apiFetch(`/hifz/student/${studentId}`);
            if (!records.length) {
                hifzList.innerHTML = '<p style="text-align:center;padding:16px;color:var(--text-muted);font-size:13px">لا توجد سجلات تسميع بعد</p>';
            } else {
                hifzList.innerHTML = records.map(r => {
                    const typeLabel = r.type === 'Memorization' ? 'حفظ جديد' : 'مراجعة';
                    return `<div style="padding:10px 4px;border-bottom:1px solid var(--border);font-size:13px">
                    <div style="display:flex;justify-content:space-between;margin-bottom:2px">
                        <strong>${esc(r.surahName || '')} (${esc(r.verses || '')})</strong>
                        <span style="color:var(--text-muted);font-size:11px">${utils().formatDateEnGb(r.date)}</span>
                    </div>
                    <div style="color:var(--text-muted)">${typeLabel} — ${esc(r.evaluation || '—')}</div>
                </div>`;
                }).join('');
            }
        } catch (e) {
            hifzList.innerHTML = '<p style="text-align:center;padding:16px;color:#ef4444;font-size:13px">تعذر تحميل سجل التسميع</p>';
        }
    }
    global.NoorMessages = { fetchMessages, markAsRead, openNewMessageModal, sendNewMessage, openChildDetails };
    global.openNewMessageModal = openNewMessageModal;
    global.sendNewMessage = sendNewMessage;
})(window);