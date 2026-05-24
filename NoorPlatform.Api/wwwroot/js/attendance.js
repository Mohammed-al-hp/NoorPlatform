/**
 * منصة نور — الحضور والغياب
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const fmt = (d, o) => utils().formatDateEnGb(d, o);
    const ymd = (d) => utils().formatLocalDateYmd(d);

    function getState() {
        return app().state.attendance;
    }

    function initAttendanceDateDisplay() {
        const opts = { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' };
        const el = document.getElementById('currentDate');
        if (el) el.textContent = fmt(getState().date, opts);
    }

    function changeDate(dir) {
        const st = getState();
        st.date.setDate(st.date.getDate() + dir);
        initAttendanceDateDisplay();
        fetchAttendanceForDate();
    }

    function selectCircle(el) {
        document.querySelectorAll('.circle-chip').forEach(c => c.classList.remove('active'));
        el.classList.add('active');
        getState().circleId = parseInt(el.dataset.id, 10) || null;
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
            fetchStudentsAttendance();
            return;
        }
        try {
            const dateStr = ymd(getState().date);
            const data = await apiFetch(`/attendance/circle/${circleId}?date=${dateStr}`);
            renderAttendanceTable(data);
        } catch {
            fetchStudentsAttendance();
        }
    }

    async function fetchStudentsAttendance() {
        try {
            const data = await apiFetch('/students');
            renderAttendanceFromStudents(data);
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function renderAttendanceFromStudents(students) {
        const wrap = document.getElementById('attendanceList');
        if (!wrap) return;
        if (!students.length) {
            wrap.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">لا يوجد طلاب</p>';
            updateAttendanceCounts();
            return;
        }
        wrap.innerHTML = `
            <table style="width:100%;border-collapse:collapse">
              <thead><tr style="background:var(--bg);text-align:right">
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">الطالب</th>
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">الحلقة</th>
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">الحالة</th>
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">تسجيل</th>
              </tr></thead>
              <tbody>${students.map(s => `
                  <tr style="border-bottom:1px solid var(--border)" id="att-row-${s.id}">
                    <td style="padding:12px 16px"><span style="font-weight:600">${utils().escapeHtml(s.fullName)}</span></td>
                    <td style="padding:12px 16px;color:var(--text-muted);font-size:13px">${utils().escapeHtml(s.circleName)}</td>
                    <td style="padding:12px 16px" id="att-status-${s.id}"><span class="status-badge" style="background:#f1f5f9;color:#64748b">— لم يُسجّل</span></td>
                    <td style="padding:12px 16px">
                      <div style="display:flex;gap:6px">
                        <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px" data-att-id="${s.id}" data-att-status="Present">✅ حاضر</button>
                        <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px;border-color:#ef4444;color:#ef4444" data-att-id="${s.id}" data-att-status="Absent">❌ غائب</button>
                        <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px;border-color:#f59e0b;color:#f59e0b" data-att-id="${s.id}" data-att-status="Late">⏰ متأخر</button>
                      </div>
                    </td>
                  </tr>`).join('')}
              </tbody>
            </table>`;
        updateAttendanceCounts();
    }

    function renderAttendanceTable(records) {
        const wrap = document.getElementById('attendanceList');
        if (!wrap || !records?.length) {
            renderAttendanceFromStudents([]);
            return;
        }
        wrap.innerHTML = `
            <table style="width:100%;border-collapse:collapse">
              <thead><tr style="background:var(--bg);text-align:right">
                <th style="padding:12px 16px">الطالب</th><th style="padding:12px 16px">الحالة</th><th style="padding:12px 16px">تسجيل</th>
              </tr></thead>
              <tbody>${records.map(r => {
            const labels = { Present: ['✅ حاضر', '#dcfce7', '#16a34a'], Absent: ['❌ غائب', '#fee2e2', '#dc2626'], Late: ['⏰ متأخر', '#fef9c3', '#ca8a04'] };
            const st = r.status || 'Present';
            const [label, bg, color] = labels[st] || labels.Present;
            return `<tr style="border-bottom:1px solid var(--border)">
                <td style="padding:12px 16px;font-weight:600">${utils().escapeHtml(r.fullName || r.studentName)}</td>
                <td id="att-status-${r.studentId}"><span class="status-badge" style="background:${bg};color:${color}">${label}</span></td>
                <td><div style="display:flex;gap:6px">
                  <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px" data-att-id="${r.studentId}" data-att-status="Present">✅</button>
                  <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px;color:#ef4444" data-att-id="${r.studentId}" data-att-status="Absent">❌</button>
                  <button type="button" class="btn btn-outline" style="padding:5px 10px;font-size:12px;color:#f59e0b" data-att-id="${r.studentId}" data-att-status="Late">⏰</button>
                </div></td></tr>`;
        }).join('')}
              </tbody></table>`;
        updateAttendanceCounts();
    }

    async function recordAtt(studentId, status) {
        try {
            const dateStr = ymd(getState().date);
            await apiFetch(`/attendance?studentId=${studentId}&status=${status}&date=${dateStr}`, 'POST');
            const statusCell = document.getElementById('att-status-' + studentId);
            const labels = { Present: ['✅ حاضر', '#dcfce7', '#16a34a'], Absent: ['❌ غائب', '#fee2e2', '#dc2626'], Late: ['⏰ متأخر', '#fef9c3', '#ca8a04'] };
            const [label, bg, color] = labels[status];
            if (statusCell) statusCell.innerHTML = `<span class="status-badge" style="background:${bg};color:${color}">${label}</span>`;
            updateAttendanceCounts();
            app().ui.showToast('✅ تم تسجيل ' + label + ' للطالب');
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function updateAttendanceCounts() {
        const present = document.querySelectorAll('[id^="att-status-"] .status-badge[style*="#dcfce7"]').length;
        const absent = document.querySelectorAll('[id^="att-status-"] .status-badge[style*="#fee2e2"]').length;
        const late = document.querySelectorAll('[id^="att-status-"] .status-badge[style*="#fef9c3"]').length;
        const pEl = document.getElementById('presentCount');
        const aEl = document.getElementById('absentCount');
        const lEl = document.getElementById('lateCount');
        if (pEl) pEl.textContent = present;
        if (aEl) aEl.textContent = absent;
        if (lEl) lEl.textContent = late;
    }

    if (!global._attendanceDelegated) {
        global._attendanceDelegated = true;
        document.getElementById('attendanceList')?.addEventListener('click', e => {
            const btn = e.target.closest('[data-att-id]');
            if (!btn) return;
            recordAtt(parseInt(btn.dataset.attId, 10), btn.dataset.attStatus);
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
        fetchStudentsAttendance,
        renderAttendanceFromStudents,
        renderAttendanceTable,
        recordAtt,
        updateAttendanceCounts
    };

    global.NoorAttendance = mod;
    global.changeDate = changeDate;
    global.selectCircle = selectCircle;
    global.renderAttendanceCircleChips = renderAttendanceCircleChips;
    global.fetchStudentsAttendance = fetchStudentsAttendance;
    global.fetchAttendanceForDate = fetchAttendanceForDate;
    global.recordAtt = recordAtt;
    global.renderAttendanceTable = renderAttendanceTable;
})(window);
