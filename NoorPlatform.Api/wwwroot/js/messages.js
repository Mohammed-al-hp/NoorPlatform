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

        // إرسال رسالة جديدة متاح لولي الأمر فقط؛ المحفّظ والإدارة يردّون من صندوق الوارد
        if (btnNew) btnNew.style.display = isParent() ? 'inline-flex' : 'none';
        if (btnNew && !btnNew.textContent.includes('إرسال')) btnNew.innerHTML = (window.Icon ? window.Icon('send', {size:14}) : '') + ' إرسال رسالة';

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
                        ${renderReplies(m.replies)}
                    </div>`).join('');
            } else {
                list.innerHTML = data.map(m => `
                    <div style="padding:16px 20px;border-bottom:1px solid var(--border);${m.isRead ? '' : 'background:var(--green-light)'}" data-msg-id="${m.id}">
                        <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:6px">
                            <strong style="font-size:13px">من: ${esc(m.senderName)}</strong>
                            <span style="font-size:11px;color:var(--text-muted)">${utils().formatDateTimeEnGb(m.createdAt)}</span>
                        </div>
                        <p style="font-size:13px;color:var(--text)">${esc(m.content)}</p>
                        ${renderReplies(m.replies)}
                        <div style="display:flex;gap:6px;margin-top:8px;flex-wrap:wrap">
                            ${!m.isRead ? `<button class="btn btn-outline" style="padding:4px 10px;font-size:11px" onclick="NoorMessages.markAsRead(${Number(m.id)})">${window.Icon ? window.Icon('check', {size:12}) : ''} تعليم كمقروءة</button>` : ''}
                            <button class="btn btn-outline" style="padding:4px 10px;font-size:11px" onclick="NoorMessages.toggleReply(${Number(m.id)})">${window.Icon ? window.Icon('send', {size:12}) : ''} رد</button>
                        </div>
                        <div id="replyBox-${Number(m.id)}" style="display:none;margin-top:8px">
                            <textarea class="form-input" rows="2" maxlength="2000" placeholder="اكتب ردك لولي الأمر..." style="width:100%"></textarea>
                            <button class="btn btn-primary" style="margin-top:6px;padding:4px 14px;font-size:12px" onclick="NoorMessages.sendReply(${Number(m.id)}, this)">إرسال الرد</button>
                        </div>
                    </div>`).join('');
            }
        } catch (e) {
            list.innerHTML = '<div style="text-align:center;padding:40px;color:#ef4444">تعذر تحميل الرسائل</div>';
        }
    }

    function renderReplies(replies) {
        if (!Array.isArray(replies) || !replies.length) return '';
        return replies.map(r => `
            <div style="margin-top:8px;margin-inline-start:16px;padding:8px 12px;border-inline-start:3px solid var(--green);background:var(--bg-secondary);border-radius:8px">
                <div style="display:flex;justify-content:space-between;font-size:11px;color:var(--text-muted);margin-bottom:4px">
                    <strong>رد: ${esc(r.senderName)}</strong>
                    <span>${utils().formatDateTimeEnGb(r.createdAt)}</span>
                </div>
                <p style="font-size:13px;color:var(--text);margin:0">${esc(r.content)}</p>
            </div>`).join('');
    }

    function toggleReply(id) {
        const box = document.getElementById('replyBox-' + id);
        if (!box) return;
        box.style.display = box.style.display === 'none' ? 'block' : 'none';
        if (box.style.display === 'block') box.querySelector('textarea')?.focus();
    }

    async function sendReply(id, btn) {
        const box = document.getElementById('replyBox-' + id);
        const content = box?.querySelector('textarea')?.value?.trim();
        if (!content) {
            app().ui.showToast('اكتب نص الرد أولاً', 'error');
            return;
        }
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await apiFetch(`/messages/${id}/reply`, 'POST', { content });
            app().ui.showToast('تم إرسال الرد إلى ولي الأمر', 'success');
            fetchMessages();
        } catch (e) {
            app().api.handleApiError(e);
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
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
    global.NoorMessages = { fetchMessages, markAsRead, toggleReply, sendReply, openNewMessageModal, sendNewMessage, openChildDetails };
    global.openNewMessageModal = openNewMessageModal;
    global.sendNewMessage = sendNewMessage;
})(window);