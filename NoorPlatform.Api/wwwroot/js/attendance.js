/**
 * منصة نور — الحضور والغياب (تسجيل مسودة + حفظ مجمّع)
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const fmt = (d, o) => utils().formatDateEnGb(d, o);
    const ymd = (d) => utils().formatLocalDateYmd(d);
    const icon = (name, opts) => (global.Icon ? global.Icon(name, opts) : '');

    // ملاحظة: label تبقى نص عربي صِرف (تستخدمها updateAttendanceCounts بفحص .includes)،
    // والأيقونة تُضاف بشكل منفصل عبر iconName عند العرض في statusBadgeHtml/actionButtonsHtml.
    const STATUS = {
        Present: { label: 'حاضر', iconName: 'check-circle', bg: '#dcfce7', color: '#16a34a' },
        Late: { label: 'متأخر', iconName: 'clock', bg: '#fef9c3', color: '#ca8a04' },
        ExcusedAbsence: { label: 'غائب بإذن', iconName: 'file-text', bg: '#dbeafe', color: '#1d4ed8' },
        UnexcusedAbsence: { label: 'غائب بدون إذن', iconName: 'x-circle', bg: '#fee2e2', color: '#dc2626' },
        NotRecorded: { label: 'لم يُسجّل', iconName: null, bg: '#f1f5f9', color: '#64748b' }
    };

    const STATUS_BUTTONS = [
        { key: 'Present', short: 'حاضر', iconName: 'check-circle' },
        { key: 'Late', short: 'متأخر', iconName: 'clock' },
        { key: 'ExcusedAbsence', short: 'بإذن', iconName: 'file-text' },
        { key: 'UnexcusedAbsence', short: 'بدون إذن', iconName: 'x-circle' }
    ];

    function getState() {
        return app().state.attendance;
    }

    function getPending() {
        const st = getState();
        if (!st.pending) st.pending = {};
        return st.pending;
    }

    function currentStatus(studentId, savedStatus) {
        const pending = getPending()[studentId];
        if (pending) return pending;
        return savedStatus || 'NotRecorded';
    }

    function setPendingDirty(dirty) {
        const btn = document.getElementById('btnSaveAttendance');
        if (btn) {
            btn.style.display = dirty ? 'inline-flex' : 'none';
            btn.disabled = !dirty;
        }
    }

    function initAttendanceDateDisplay() {
        const opts = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
        const el = document.getElementById('currentDate');
        if (el) el.textContent = fmt(getState().date, opts);
        const picker = document.getElementById('attendanceDatePicker');
        if (picker) picker.value = ymd(getState().date);
    }

    function onAttendanceDatePicked(value) {
        if (!value) return;
        const st = getState();
        const parts = value.split('-').map(Number);
        st.date = new Date(parts[0], parts[1] - 1, parts[2]);
        getPending().clear?.();
        Object.keys(getPending()).forEach(k => delete getPending()[k]);
        setPendingDirty(false);
        initAttendanceDateDisplay();
        fetchAttendanceForDate();
    }

    function changeDate(dir) {
        const st = getState();
        st.date.setDate(st.date.getDate() + dir);
        onAttendanceDatePicked(ymd(st.date));
    }

    function selectCircle(el) {
        document.querySelectorAll('.circle-chip').forEach(c => c.classList.remove('active'));
        el.classList.add('active');
        getState().circleId = parseInt(el.dataset.id, 10) || null;
        Object.keys(getPending()).forEach(k => delete getPending()[k]);
        setPendingDirty(false);
        fetchAttendanceForDate();
    }

    async function renderAttendanceCircleChips() {
        const wrap = document.getElementById('attendanceCircleChips');
        if (!wrap) return;
        try {
            const circles = await apiFetch('/circles');
            app().state.circles = circles;
            if (!circles.length) {
                wrap.innerHTML = '<span style="font-size:13px;color:var(--text-muted)">لا توجد حلقات</span>';
                return;
            }
            wrap.innerHTML = circles.map((c, i) =>
                `<div class="circle-chip${i === 0 ? ' active' : ''}" data-id="${c.id}" onclick="NoorAttendance.selectCircle(this)">${utils().escapeHtml(c.name)}</div>`
            ).join('');
            const first = wrap.querySelector('.circle-chip');
            if (first) getState().circleId = parseInt(first.dataset.id, 10);
        } catch (e) {
            wrap.innerHTML = '<span style="color:var(--text-muted)">تعذر تحميل الحلقات</span>';
        }
    }

    async function fetchAttendanceForDate() {
        const circleId = getState().circleId;
        if (!circleId) {
            const wrap = document.getElementById('attendanceList');
            if (wrap) wrap.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">اختر حلقة أولاً</p>';
            updateAttendanceCounts();
            return;
        }
        try {
            const dateStr = ymd(getState().date);
            const data = await apiFetch(`/attendance/circle/${circleId}?date=${dateStr}`);
            getState().records = data;

            // ─── البند 8: جعل الحضور الافتراضي "حاضر" (لليوم الحالي فقط) ───
            let hasUnrecorded = false;
            const pending = getPending();
            const isToday = dateStr === ymd(new Date());

            if (isToday) {
                data.forEach(r => {
                    if (!r.status || r.status === 'NotRecorded') {
                        pending[r.studentId] = 'Present';
                        hasUnrecorded = true;
                    }
                });
            }

            renderAttendanceTable(data);
            
            if (hasUnrecorded) {
                setPendingDirty(true);
            }
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function statusBadgeHtml(status) {
        const cfg = STATUS[status] || STATUS.NotRecorded;
        const ic = cfg.iconName ? icon(cfg.iconName, { size: 13 }) + ' ' : '— ';
        return `<span class="status-badge" style="background:${cfg.bg};color:${cfg.color}">${ic}${cfg.label}</span>`;
    }

    function actionButtonsHtml(studentId, activeStatus) {
        return `<div style="display:flex;gap:6px;flex-wrap:wrap">
            ${STATUS_BUTTONS.map(b => {
            const active = activeStatus === b.key;
            return `<button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px${active ? ';font-weight:700;border-width:2px' : ''}"
                data-att-id="${studentId}" data-att-status="${b.key}">${icon(b.iconName, { size: 12 })} ${b.short}</button>`;
        }).join('')}
        </div>`;
    }

    function renderAttendanceTable(records) {
        const wrap = document.getElementById('attendanceList');
        if (!wrap) return;
        if (!records?.length) {
            wrap.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">لا يوجد طلاب في هذه الحلقة</p>';
            updateAttendanceCounts();
            return;
        }

        wrap.innerHTML = `
            <table style="width:100%;border-collapse:collapse">
              <thead><tr style="background:var(--bg);text-align:right">
                <th style="padding:12px 16px">الطالب</th>
                <th style="padding:12px 16px">الحالة</th>
                <th style="padding:12px 16px">تسجيل</th>
                <th style="padding:12px 16px">ملاحظات سلوكية</th>
              </tr></thead>
              <tbody>${records.map(r => {
            const sid = r.studentId;
            const saved = r.status || 'NotRecorded';
            const st = currentStatus(sid, saved);
            return `<tr style="border-bottom:1px solid var(--border)">
                <td style="padding:12px 16px;font-weight:600">${utils().escapeHtml(r.fullName || r.studentName)}</td>
                <td id="att-status-${sid}">${statusBadgeHtml(st)}</td>
                <td>${actionButtonsHtml(sid, st === 'NotRecorded' ? '' : st)}</td>
                <td style="padding:12px 16px;">
                    <input type="text" class="form-control" data-note-id="${sid}" value="${utils().escapeHtml(r.note || '')}" placeholder="ملاحظة (اختياري)..." style="font-size:12px; padding:6px 10px; width: 100%; min-width: 120px;" onchange="NoorAttendance.stageNote(${sid}, this.value)">
                </td>
            </tr>`;
        }).join('')}
              </tbody></table>`;
        updateAttendanceCounts();
    }

    function stageAttendance(studentId, status) {
        const saved = (getState().records || []).find(r => r.studentId === studentId);
        const savedStatus = saved?.status || 'NotRecorded';
        if (status === savedStatus) {
            delete getPending()[studentId];
        } else {
            getPending()[studentId] = status;
        }

        const display = currentStatus(studentId, savedStatus);
        const statusCell = document.getElementById('att-status-' + studentId);
        if (statusCell) statusCell.innerHTML = statusBadgeHtml(display);

        const row = statusCell?.closest('tr');
        if (row) {
            const td = row.querySelector('td:nth-child(3)');
            if (td) td.innerHTML = actionButtonsHtml(studentId, display === 'NotRecorded' ? '' : display);
        }

        const hasPending = Object.keys(getPending()).length > 0 || Object.keys(getState().pendingNotes || {}).length > 0;
        setPendingDirty(hasPending);
        updateAttendanceCounts();
    }

    function stageNote(studentId, note) {
        const saved = (getState().records || []).find(r => r.studentId === studentId);
        const savedNote = saved?.note || '';
        if (!getState().pendingNotes) getState().pendingNotes = {};
        
        if (note === savedNote) {
            delete getState().pendingNotes[studentId];
        } else {
            getState().pendingNotes[studentId] = note;
        }

        const hasPending = Object.keys(getPending()).length > 0 || Object.keys(getState().pendingNotes).length > 0;
        setPendingDirty(hasPending);
    }

    async function savePendingAttendance() {
        const pending = getPending();
        const pendingNotes = getState().pendingNotes || {};
        const studentIds = new Set([...Object.keys(pending), ...Object.keys(pendingNotes)]);
        
        if (!studentIds.size) {
            app().ui.showToast('لا توجد تغييرات للحفظ', 'info');
            return;
        }
        const dateStr = ymd(getState().date);
        const btn = document.getElementById('btnSaveAttendance');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            const recordsToSave = Array.from(studentIds).map(id => {
                const sid = parseInt(id, 10);
                const saved = (getState().records || []).find(r => r.studentId === sid);
                return {
                    studentId: sid,
                    status: pending[id] !== undefined ? pending[id] : (saved?.status || 'Present'),
                    note: pendingNotes[id] !== undefined ? pendingNotes[id] : (saved?.note || '')
                };
            });

            await apiFetch('/attendance/bulk', 'POST', {
                date: dateStr,
                records: recordsToSave
            });
            Object.keys(pending).forEach(k => delete pending[k]);
            Object.keys(pendingNotes).forEach(k => delete pendingNotes[k]);
            setPendingDirty(false);
            app().ui.showToast('تم حفظ سجل الحضور بنجاح', 'success');
            await fetchAttendanceForDate();
            global.NoorDashboard?.fetchStats?.();
        } catch (e) {
            app().api.handleApiError(e);
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
        }
    }

    function markAllPresentLocal() {
        const records = getState().records || [];
        records.forEach(r => stageAttendance(r.studentId, 'Present'));
        app().ui.showToast('تم تحديد الجميع حاضر — اضغط «حفظ الحضور» للتأكيد', 'info');
    }

    function updateAttendanceCounts() {
        // ملاحظة: الفحص هنا يعتمد على النص العربي فقط (بدون الأيقونة)، وهذا سليم
        // لأن label في STATUS ما زال نصًا عربيًا صرفًا؛ الأيقونة تُضاف بشكل منفصل في statusBadgeHtml.
        const cells = document.querySelectorAll('[id^="att-status-"] .status-badge');
        let present = 0, late = 0, excused = 0, unexcused = 0;
        cells.forEach(el => {
            const t = el.textContent || '';
            if (t.includes('حاضر') && !t.includes('غائب')) present++;
            else if (t.includes('متأخر')) late++;
            else if (t.includes('بإذن')) excused++;
            else if (t.includes('بدون إذن')) unexcused++;
        });
        const pEl = document.getElementById('presentCount');
        const aEl = document.getElementById('absentCount');
        const lEl = document.getElementById('lateCount');
        if (pEl) pEl.textContent = present;
        if (aEl) aEl.textContent = excused + unexcused;
        if (lEl) lEl.textContent = late;
    }

    if (!global._attendanceDelegated) {
        global._attendanceDelegated = true;
        document.getElementById('attendanceList')?.addEventListener('click', e => {
            const btn = e.target.closest('[data-att-id]');
            if (!btn) return;
            stageAttendance(parseInt(btn.dataset.attId, 10), btn.dataset.attStatus);
        });
        document.getElementById('attendanceDatePicker')?.addEventListener('change', e => {
            onAttendanceDatePicked(e.target.value);
        });
    }

    function bootAttendance() {
        if (typeof utils().formatDateEnGb !== 'function') return;
        initAttendanceDateDisplay();
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bootAttendance);
    } else {
        bootAttendance();
    }

    const mod = {
        initAttendanceDateDisplay,
        changeDate,
        selectCircle,
        renderAttendanceCircleChips,
        fetchAttendanceForDate,
        renderAttendanceTable,
        savePendingAttendance,
        markAllPresentLocal,
        updateAttendanceCounts,
        onAttendanceDatePicked,
        stageNote
    };

    global.NoorAttendance = mod;
    global.changeDate = changeDate;
    global.selectCircle = selectCircle;
    global.renderAttendanceCircleChips = renderAttendanceCircleChips;
    global.fetchAttendanceForDate = fetchAttendanceForDate;
    global.saveAttendance = savePendingAttendance;
    global.markAllPresent = () => mod.markAllPresentLocal();
    global.renderAttendanceTable = renderAttendanceTable;
})(window);
