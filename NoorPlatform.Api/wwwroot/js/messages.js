/**
 * منصة نور — نظام الرسائل (محادثات بين ولي الأمر والمحفّظ/الإدارة)
 * ولي الأمر: يبدأ رسالة لمحفّظ أبنائه أو للإدارة، ويرد داخل أي محادثة.
 * المحفّظ/الإدارة: يبدأ رسالة لولي أمر (أولياء طلابه للمحفّظ)، ويرد داخل المحادثة.
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = (s) => utils().escapeHtml(s);
    const ic = (n, s) => (window.Icon ? window.Icon(n, { size: s || 12 }) : '');

    function isParent() {
        return app().state.user?.role === 'Parent';
    }

    async function fetchMessages() {
        const list = document.getElementById('messagesList');
        const btnNew = document.getElementById('btnNewMessage');
        if (!list) return;

        if (btnNew) {
            btnNew.style.display = 'inline-flex';
            btnNew.innerHTML = ic('send', 14) + ' إرسال رسالة';
        }
        const subtitle = document.getElementById('messagesSubtitle');
        if (subtitle) {
            subtitle.textContent = isParent()
                ? 'التواصل مع الإدارة ومحفّظي أبنائك'
                : 'التواصل مع أولياء الأمور — ابدأ رسالة جديدة أو رد على المحادثات';
        }

        list.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)"><span class="spin-icon">' + ic('loader', 14) + '</span> جاري التحميل...</div>';

        try {
            const data = await apiFetch(isParent() ? '/messages/threads' : '/messages/inbox');
            if (!data || !data.length) {
                list.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد رسائل بعد — اضغط «إرسال رسالة» لبدء محادثة</div>';
                return;
            }
            list.innerHTML = data.map(renderThread).join('');
        } catch (e) {
            list.innerHTML = '<div style="text-align:center;padding:40px;color:#ef4444">تعذر تحميل الرسائل</div>';
        }
    }

    function renderThread(m) {
        const id = Number(m.id);
        const parent = isParent();
        const outgoing = m.direction === 'out';
        const counterpart = m.counterpartName || m.recipientName || m.senderName || '';
        const unread = parent ? !!m.hasUnread : m.isRead === false;
        const header = outgoing ? 'إلى: ' + esc(counterpart) : 'من: ' + esc(counterpart);
        const placeholder = parent ? 'اكتب ردك...' : 'اكتب ردك لولي الأمر...';
        return `
            <div class="msg-thread${unread ? ' msg-thread-unread' : ''}" data-msg-id="${id}">
                <div class="msg-thread-head">
                    <strong>${header}</strong>
                    <span>${utils().formatDateTimeEnGb(m.createdAt)}</span>
                </div>
                <p class="msg-thread-body">${esc(m.content)}</p>
                ${renderReplies(m.replies, parent)}
                <div class="msg-thread-actions">
                    ${!parent && unread ? `<button class="btn btn-outline" onclick="NoorMessages.markAsRead(${id})">${ic('check')} تعليم كمقروءة</button>` : ''}
                    <button class="btn btn-outline" onclick="NoorMessages.toggleReply(${id})">${ic('message-circle')} رد</button>
                </div>
                <div id="replyBox-${id}" class="msg-reply-box" style="display:none">
                    <textarea class="form-input" rows="2" maxlength="2000" placeholder="${placeholder}"></textarea>
                    <button class="btn btn-primary" onclick="NoorMessages.sendReply(${id}, this)">${ic('send')} إرسال الرد</button>
                </div>
            </div>`;
    }

    function renderReplies(replies, parent) {
        if (!Array.isArray(replies) || !replies.length) return '';
        return replies.map(r => {
            // «رد مني» يُميَّز بلون مختلف ليسهل تتبع المحادثة
            const mine = parent ? !!r.isMine : r.fromParent === false;
            return `
            <div class="msg-reply${mine ? ' msg-reply-mine' : ''}">
                <div class="msg-reply-head">
                    <strong>${esc(r.senderName)}</strong>
                    <span>${utils().formatDateTimeEnGb(r.createdAt)}</span>
                </div>
                <p>${esc(r.content)}</p>
            </div>`;
        }).join('');
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
            const endpoint = isParent() ? `/messages/${id}/parent-reply` : `/messages/${id}/reply`;
            await apiFetch(endpoint, 'POST', { content });
            app().ui.showToast('تم إرسال الرد', 'success');
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
            if (isParent()) {
                sel.innerHTML = '<option value="Admin|">إدارة المركز</option>';
                try {
                    const teachers = await apiFetch('/messages/available-recipients');
                    (teachers || []).forEach(t => {
                        sel.innerHTML += `<option value="Teacher|${Number(t.id)}">${esc(t.fullName)}</option>`;
                    });
                } catch (e) { /* يبقى خيار الإدارة متاحًا على الأقل */ }
            } else {
                sel.innerHTML = '<option value="">— اختر ولي الأمر —</option>';
                try {
                    const parents = await apiFetch('/messages/parent-recipients');
                    if (!parents || !parents.length) {
                        sel.innerHTML = '<option value="">لا يوجد أولياء أمور مرتبطون بطلابك</option>';
                    }
                    (parents || []).forEach(p => {
                        const kids = (p.children || []).join('، ');
                        sel.innerHTML += `<option value="Parent|${Number(p.userId)}">${esc(p.fullName)}${kids ? ' — ولي أمر: ' + esc(kids) : ''}</option>`;
                    });
                } catch (e) {
                    sel.innerHTML = '<option value="">تعذر تحميل أولياء الأمور</option>';
                }
            }
        }
        document.getElementById('msgContent').value = '';
        app().ui.openModal('newMessageModal');
    }

    async function sendNewMessage() {
        const sel = document.getElementById('msgRecipientSelect');
        const content = document.getElementById('msgContent')?.value?.trim();

        const [recipientType, recipientId] = (sel?.value || '').split('|');
        if (!recipientType) {
            app().ui.showToast('اختر المستلم', 'error');
            return;
        }
        if (!content) {
            app().ui.showToast('نص الرسالة مطلوب', 'error');
            return;
        }

        const btn = document.querySelector('#newMessageModal .btn-primary');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            if (recipientType === 'Parent') {
                await apiFetch('/messages/to-parent', 'POST', { parentUserId: parseInt(recipientId, 10), content });
            } else {
                await apiFetch('/messages', 'POST', {
                    recipientType,
                    recipientTeacherId: recipientId ? parseInt(recipientId, 10) : null,
                    content
                });
            }
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