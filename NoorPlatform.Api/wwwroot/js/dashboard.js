/**
 * منصة نور — لوحة التحكم والإعلانات والمتصدرين
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = (s) => utils().escapeHtml(s);

    async function fetchStats() {
        try {
            const data = await apiFetch('/dashboard/stats');
            const el = id => document.getElementById(id);
            if (el('dashStudents')) el('dashStudents').textContent = data.students;
            if (el('dashTeachers')) el('dashTeachers').textContent = data.teachers;
            if (el('dashCircles')) el('dashCircles').textContent = data.circles;
            if (el('dashAttendance')) el('dashAttendance').textContent = data.attendanceToday || '0%';

            const weekly = Array.isArray(data.weeklyAttendance)
                ? data.weeklyAttendance
                : (data.weeklyAttendance ? Object.values(data.weeklyAttendance) : []);
            if (weekly.length) {
                const barChart = document.getElementById('weeklyBarChart');
                if (barChart) {
                    barChart.innerHTML = weekly.map(d => {
                        const pct = Number(d.percentage) || 0;
                        const barPx = Math.max(Math.round(pct * 1.2), pct > 0 ? 10 : 4);
                        const color = pct > 70 ? 'var(--gradient)' : 'linear-gradient(135deg,#94a3b8,#cbd5e1)';
                        return `<div class="bar-col">
                            <div class="bar" style="height:${barPx}px;background:${color}" title="${pct}%"></div>
                            <div class="bar-label">${esc(d.dayName)}</div>
                        </div>`;
                    }).join('');
                }
            }

            if (data.levelDistribution) {
                const ld = data.levelDistribution;
                const total = ld.advanced + ld.intermediate + ld.beginner;
                if (total > 0) {
                    const advPct = Math.round(ld.advanced / total * 100);
                    const intPct = Math.round(ld.intermediate / total * 100);
                    const begPct = 100 - advPct - intPct;
                    const svg = document.getElementById('donutSvg');
                    if (svg) {
                        svg.innerHTML = `
                            <circle cx="18" cy="18" r="15.9155" fill="none" stroke="#e2e8f0" stroke-width="3"/>
                            <circle cx="18" cy="18" r="15.9155" fill="none" stroke="#10b981" stroke-width="3"
                                stroke-dasharray="${advPct} ${100 - advPct}" stroke-dashoffset="25" stroke-linecap="round"/>
                            <circle cx="18" cy="18" r="15.9155" fill="none" stroke="#3b82f6" stroke-width="3"
                                stroke-dasharray="${intPct} ${100 - intPct}" stroke-dashoffset="${25 - advPct}" stroke-linecap="round"/>
                            <circle cx="18" cy="18" r="15.9155" fill="none" stroke="#f59e0b" stroke-width="3"
                                stroke-dasharray="${begPct} ${100 - begPct}" stroke-dashoffset="${25 - advPct - intPct}" stroke-linecap="round"/>
                            <text x="18" y="20" text-anchor="middle" font-size="5" fill="var(--text)" font-weight="bold" font-family="Tajawal,sans-serif">${total}</text>`;
                    }
                    const legend = document.getElementById('donutLegend');
                    if (legend) {
                        legend.innerHTML = `
                            <div class="legend-item"><div class="legend-dot" style="background:#10b981"></div><span>متقدم</span><span class="legend-val">${ld.advanced}</span></div>
                            <div class="legend-item"><div class="legend-dot" style="background:#3b82f6"></div><span>متوسط</span><span class="legend-val">${ld.intermediate}</span></div>
                            <div class="legend-item"><div class="legend-dot" style="background:#f59e0b"></div><span>مبتدئ</span><span class="legend-val">${ld.beginner}</span></div>`;
                    }
                }
                const centerEl = document.getElementById('donutCenter');
                if (centerEl) centerEl.textContent = ld.advanced + ld.intermediate + ld.beginner;
            }

            if (data.recentHifz) {
                const tbody = document.getElementById('recentHifzBody');
                if (tbody) {
                    if (!data.recentHifz.length) {
                        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد جلسات تسميع بعد</td></tr>';
                    } else {
                        const gradients = ['linear-gradient(135deg,#10b981,#3b82f6)', 'linear-gradient(135deg,#14b8a6,#3b82f6)', 'linear-gradient(135deg,#8b5cf6,#3b82f6)', 'linear-gradient(135deg,#f59e0b,#ef4444)', 'linear-gradient(135deg,#ec4899,#8b5cf6)'];
                        tbody.innerHTML = data.recentHifz.map((r, i) => {
                            const evalClass = r.evaluation === 'ممتاز' ? 'status-excellent' : r.evaluation === 'جيد' ? 'status-good' : 'status-late';
                            const evalIcon = r.evaluation === 'ممتاز' ? '⭐' : r.evaluation === 'جيد' ? '👍' : '🔄';
                            return `<tr>
                                <td>${i + 1}</td>
                                <td><div class="student-cell"><div class="avatar" style="background:${gradients[i % 5]}">${esc((r.studentName || '').slice(0, 2))}</div><span>${esc(r.studentName)}</span></div></td>
                                <td>${esc(r.circleName)}</td>
                                <td>${esc(r.surahName)} (${esc(r.verses)})</td>
                                <td><span class="status-badge ${evalClass}">${evalIcon} ${esc(r.evaluation)}</span></td>
                            </tr>`;
                        }).join('');
                    }
                }
            }
            fetchActivities();
        } catch (e) {
            app().api.handleApiError(e, { silent: true });
        }
    }

    async function fetchActivities() {
        const timeline = document.getElementById('notifTimeline');
        if (!timeline) return;
        try {
            const activities = await apiFetch('/dashboard/activities');
            if (!activities?.length) {
                timeline.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)">لا توجد نشاطات مؤخراً</div>';
                return;
            }
            timeline.innerHTML = activities.map(a => {
                const timeStr = new Date(a.createdAt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', numberingSystem: 'latn' });
                const dotClass = a.color === 'green' ? 'nd-green' : a.color === 'blue' ? 'nd-blue' : a.color === 'amber' ? 'nd-amber' : 'nd-red';
                return `<div class="notif-item">
                    <div class="notif-dot-wrap">
                        <div class="notif-dot ${dotClass}">${a.icon}</div>
                        <div class="notif-line"></div>
                    </div>
                    <div class="notif-content">
                        <div class="notif-content-top"><p>${esc(a.description)}</p></div>
                        <div class="notif-time">${timeStr} — ${esc(a.userName)}</div>
                    </div>
                </div>`;
            }).join('');
        } catch (e) {
            timeline.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)">تعذر تحميل النشاطات</div>';
        }
    }

    async function fetchAnnouncements() {
        const list = document.getElementById('annList');
        if (!list) return;
        try {
            const data = await apiFetch('/announcements');
            list.innerHTML = data.map(a => `
            <div class="ann-card" data-title="${esc(a.title)}">
              <div class="ann-indicator" style="background:${esc(a.color)}"></div>
              <div class="ann-card-content">
                <div class="ann-card-top"><h4>${esc(a.title)}</h4><time>${utils().formatDateEnGb(a.createdAt)}</time></div>
                <p>${esc(a.content)}</p>
                <div class="ann-target" style="color:${esc(a.color)}">🎯 ${esc(a.target)}</div>
              </div>
            </div>`).join('');
            list.querySelectorAll('.ann-card').forEach(card => {
                card.addEventListener('click', () => app().ui.showToast('📢 ' + card.dataset.title), { once: false });
            });
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    async function fetchLeaderboard() {
        const wrap = document.getElementById('leaderboardWrap');
        if (!wrap) return;
        try {
            const data = await apiFetch('/dashboard/leaderboard');
            if (!data?.length) {
                wrap.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا يوجد طلاب لعرضهم</div>';
                return;
            }
            wrap.innerHTML = data.map(s => {
                let medal = s.rank <= 3 ? ['🥇', '🥈', '🥉'][s.rank - 1] : '#' + s.rank;
                return `<div style="display:flex;align-items:center;gap:15px;padding:12px;background:var(--card);border:1px solid var(--border);border-radius:12px">
                    <div style="font-size:24px;font-weight:bold;width:40px;text-align:center">${medal}</div>
                    <div style="flex:1"><div style="font-weight:700">${esc(s.fullName)}</div><div style="font-size:12px;color:var(--text-muted)">${esc(s.circleName)} • حضور: ${s.attendanceRate}%</div></div>
                    <div style="font-weight:800;color:var(--green)">${s.points} نقطة</div>
                </div>`;
            }).join('');
        } catch (e) {
            wrap.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">تعذر تحميل المتصدرين</div>';
        }
    }

    global.NoorDashboard = { fetchStats, fetchActivities, fetchAnnouncements, fetchLeaderboard };
    global.fetchStats = fetchStats;
    global.fetchActivities = fetchActivities;
    global.fetchAnnouncements = fetchAnnouncements;
    global.fetchLeaderboard = fetchLeaderboard;
})(window);
