/**
 * منصة نور — الميزات الذكية (الويب)
 *  1) جرس الإشعارات داخل التطبيق (سحب دوري كل 45 ثانية)
 *  2) بطاقة «مراجعة اليوم» للطالب (التكرار المتباعد)
 *  3) الخريطة الحرارية لإتقان الحفظ (الطالب + صفحة التسميع للمحفّظ/المشرف)
 *  4) التحليلات والإنذار المبكر للمشرف
 *  5) التقرير الشهري لولي الأمر (إرسال يدوي)
 */
(function (global) {
    'use strict';

    const POLL_MS = 45000;
    const esc = (s) => (typeof global.escapeHtml === 'function'
        ? global.escapeHtml(s == null ? '' : String(s))
        : String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])));
    const icon = (name, size) => (global.Icon ? global.Icon(name, { size: size || 16 }) : '');
    const toast = (m, t) => (typeof global.showToast === 'function' ? global.showToast(m, t) : null);
    const role = () => global.NoorApp?.state?.user?.role;
    const loggedIn = () => !!global.NoorApp?.state?.token;
    const api = (path, method, body) => global.apiFetch(path, method, body);

    function timeAgo(iso) {
        const d = new Date(iso.endsWith('Z') || iso.includes('+') ? iso : iso + 'Z');
        const s = Math.max(0, (Date.now() - d.getTime()) / 1000);
        if (s < 60) return 'الآن';
        if (s < 3600) return `منذ ${Math.floor(s / 60)} د`;
        if (s < 86400) return `منذ ${Math.floor(s / 3600)} س`;
        if (s < 7 * 86400) return `منذ ${Math.floor(s / 86400)} يوم`;
        return d.toLocaleDateString('en-GB');
    }

    function modal(title, html) {
        const wrap = document.createElement('div');
        wrap.className = 'sf-modal';
        wrap.innerHTML = `<div class="sf-modal-box" role="dialog" aria-modal="true">
            <header><h3>${esc(title)}</h3><button type="button" aria-label="إغلاق">×</button></header>
            <div class="sf-modal-body">${html}</div></div>`;
        const close = () => wrap.remove();
        wrap.addEventListener('click', e => { if (e.target === wrap) close(); });
        wrap.querySelector('header button').addEventListener('click', close);
        document.body.appendChild(wrap);
        return wrap;
    }

    // ═══════════════════════ 1) جرس الإشعارات ═══════════════════════
    const KIND_ICON = { Hifz: 'book-open', Absence: 'x-circle', Announcement: 'megaphone', Message: 'message-circle', MessageReply: 'message-circle', MonthlyReport: 'file-text', System: 'bell' };
    let pollTimer = null, lastUnread = -1, lastLatestId = null;

    function ensureBell() {
        if (document.getElementById('nfBell')) return;
        const topbar = document.querySelector('.topbar');
        if (!topbar) return;
        const wrap = document.createElement('div');
        wrap.className = 'nf-bell-wrap';
        wrap.innerHTML = `<button type="button" class="nf-bell" id="nfBell" aria-label="الإشعارات" title="الإشعارات">
                ${icon('bell', 18)}<span class="nf-badge" id="nfBadge">0</span></button>
            <div class="nf-panel" id="nfPanel" role="region" aria-label="الإشعارات">
                <div class="nf-panel-head"><h4>الإشعارات</h4><button type="button" id="nfReadAll">تعليم الكل كمقروء</button></div>
                <div class="nf-list" id="nfList"><div class="nf-empty">جاري التحميل...</div></div>
            </div>`;
        const dark = document.getElementById('darkToggle');
        topbar.insertBefore(wrap, dark ? dark.nextSibling : topbar.firstChild);

        document.getElementById('nfBell').addEventListener('click', e => { e.stopPropagation(); togglePanel(); });
        document.getElementById('nfReadAll').addEventListener('click', async e => {
            e.stopPropagation();
            try { await api('/inbox/read-all', 'POST'); await loadList(); setBadge(0); } catch (err) { global.handleApiError?.(err); }
        });
        document.addEventListener('click', e => {
            const panel = document.getElementById('nfPanel');
            if (panel && panel.classList.contains('open') && !wrap.contains(e.target)) panel.classList.remove('open');
        });
    }

    function setBadge(n) {
        const b = document.getElementById('nfBadge');
        if (!b) return;
        b.textContent = n > 99 ? '99+' : String(n);
        b.classList.toggle('show', n > 0);
        const bell = document.getElementById('nfBell');
        if (bell) bell.setAttribute('aria-label', n > 0 ? `الإشعارات (${n} غير مقروء)` : 'الإشعارات');
    }

    async function poll() {
        if (!loggedIn()) return;
        try {
            const r = await api('/inbox/unread-count');
            if (!r) return;
            if (lastLatestId !== null && r.latestId && r.latestId !== lastLatestId && r.unread > lastUnread) {
                const bell = document.getElementById('nfBell');
                if (bell) { bell.classList.remove('nf-ring'); void bell.offsetWidth; bell.classList.add('nf-ring'); }
                if (document.getElementById('nfPanel')?.classList.contains('open')) loadList();
            }
            lastUnread = r.unread; lastLatestId = r.latestId ?? null;
            setBadge(r.unread);
        } catch (e) {
            if (e?.status === 401) stopPolling();
        }
    }

    async function togglePanel() {
        const p = document.getElementById('nfPanel');
        if (!p) return;
        p.classList.toggle('open');
        if (p.classList.contains('open')) await loadList();
    }

    async function loadList() {
        const list = document.getElementById('nfList');
        if (!list) return;
        try {
            const r = await api('/inbox?take=30');
            setBadge(r.unread);
            if (!r.items.length) { list.innerHTML = '<div class="nf-empty">لا توجد إشعارات بعد</div>'; return; }
            list.innerHTML = r.items.map(n => `
                <div class="nf-item ${n.isRead ? '' : 'unread'}" data-id="${n.id}" data-link="${esc(n.link || '')}" tabindex="0">
                    <div class="nf-item-icon k-${esc(n.kind)}">${icon(KIND_ICON[n.kind] || 'bell', 16)}</div>
                    <div class="nf-item-body">
                        <div class="nf-item-title"><span>${esc(n.title)}</span><span class="nf-item-time">${timeAgo(n.createdAt)}</span></div>
                        <div class="nf-item-text">${esc(n.body)}</div>
                    </div>
                </div>`).join('');
            list.querySelectorAll('.nf-item').forEach(el => {
                const open = () => openItem(el, r.items.find(i => String(i.id) === el.dataset.id));
                el.addEventListener('click', open);
                el.addEventListener('keydown', e => { if (e.key === 'Enter') open(); });
            });
        } catch (e) {
            list.innerHTML = '<div class="nf-empty">تعذر تحميل الإشعارات</div>';
        }
    }

    async function openItem(el, item) {
        if (!item) return;
        if (!item.isRead) {
            try { await api(`/inbox/${item.id}/read`, 'POST'); item.isRead = true; el.classList.remove('unread'); poll(); } catch { /* تجاهل */ }
        }
        document.getElementById('nfPanel')?.classList.remove('open');
        if (item.kind === 'MonthlyReport') { modal(item.title, `<div class="sf-pre">${esc(item.body)}</div>`); return; }
        if (item.link && typeof global.navigate === 'function') {
            const navEl = document.querySelector(`[onclick*="navigate('${item.link}'"]`);
            global.navigate(item.link, navEl);
        }
    }

    function startPolling() {
        ensureBell();
        if (pollTimer) return;
        poll();
        pollTimer = setInterval(() => { if (!document.hidden) poll(); }, POLL_MS);
        document.addEventListener('visibilitychange', () => { if (!document.hidden) poll(); });
    }
    function stopPolling() { if (pollTimer) clearInterval(pollTimer); pollTimer = null; }

    // ═══════════════════════ 2) مراجعة اليوم ═══════════════════════
    async function renderReviewCard(container, studentId) {
        if (!container) return;
        container.innerHTML = `<div class="sf-card"><div class="sf-card-body sf-muted">جاري حساب جدول المراجعة...</div></div>`;
        try {
            const r = await api(studentId ? `/hifz/review-schedule/${studentId}` : '/hifz/review-schedule/my');
            const items = r.due || [];
            const list = items.length
                ? `<div class="rv-list">${items.map((s, i) => `
                    <div class="rv-item">
                        <span class="rv-rank">${i + 1}</span>
                        <span class="rv-name">${esc(s.name)}
                            <span class="rv-meta">آخر مراجعة قبل ${s.daysSinceReview} يوم · ${s.overdueDays > 0 ? `متأخرة ${s.overdueDays} يوم` : 'موعدها اليوم'} · آخر تقييم: ${esc(s.lastEvaluation || '—')}</span>
                        </span>
                        <span class="rv-bar" title="الثبات ${s.retention}%"><i style="width:${s.retention}%;background:${heatColor(s.retention)}"></i></span>
                        <span class="sf-chip ${s.retention < 60 ? 'gold' : ''}">${Math.round(s.retention)}%</span>
                    </div>`).join('')}</div>`
                : `<div class="rv-done">${icon('check-circle', 18)} ما شاء الله! لا توجد سور مستحقة للمراجعة اليوم</div>`;
            const upcoming = (r.upcoming || []).length
                ? `<p class="sf-muted" style="margin:12px 0 0">قريباً: ${r.upcoming.map(u => esc(u.name)).join('، ')}</p>` : '';
            container.innerHTML = `<div class="sf-card">
                <div class="sf-card-head"><div><h3>${icon('refresh-cw', 16)} مراجعة اليوم</h3>
                    <p>اقتراحات مرتبة حسب الأولوية حتى لا يتفلت المحفوظ القديم</p></div>
                    <span class="sf-chip gold">${r.dueCount} مستحقة</span></div>
                <div class="sf-card-body">
                    <div class="rv-summary">
                        <div class="rv-stat"><b>${r.totalMemorized}</b><span>سورة محفوظة</span></div>
                        <div class="rv-stat"><b>${r.dueCount}</b><span>تحتاج مراجعة</span></div>
                        <div class="rv-stat"><b>${Math.round(r.averageRetention)}%</b><span>متوسط الثبات</span></div>
                    </div>${list}${upcoming}
                </div></div>`;
        } catch (e) {
            container.innerHTML = '';
        }
    }

    // ═══════════════════════ 3) الخريطة الحرارية ═══════════════════════
    // من الأخضر الزمردي الداكن (ثبات كامل) إلى الذهبي الباهت (منسيّ تقريباً)
    const STOPS = [[0, [246, 235, 208]], [40, [232, 220, 181]], [60, [168, 205, 182]], [80, [76, 154, 110]], [100, [26, 92, 58]]];
    function heatColor(v) {
        const x = Math.max(0, Math.min(100, v ?? 0));
        for (let i = 1; i < STOPS.length; i++) {
            if (x <= STOPS[i][0]) {
                const [a, ca] = STOPS[i - 1], [b, cb] = STOPS[i];
                const t = (x - a) / (b - a);
                const c = ca.map((c0, k) => Math.round(c0 + (cb[k] - c0) * t));
                return `rgb(${c[0]},${c[1]},${c[2]})`;
            }
        }
        return 'rgb(26,92,58)';
    }
    const textOn = (v) => (v >= 62 ? '#fff' : '#3d3420');

    async function renderHeatmap(container, studentId) {
        if (!container) return;
        container.innerHTML = '<div class="mem-empty">جاري تحميل خريطة الإتقان...</div>';
        let data;
        try {
            data = await api(studentId ? `/hifz/heatmap/${studentId}` : '/hifz/heatmap/my');
        } catch (e) {
            container.innerHTML = '<div class="mem-empty">تعذر تحميل خريطة الإتقان</div>';
            return;
        }
        const overdue = data.surahs.filter(s => s.memorized && s.dueDate && new Date(s.dueDate) <= new Date()).length;
        container.innerHTML = `
            <div class="hm-stats">
                <span class="sf-chip">${data.memorizedCount} سورة محفوظة</span>
                <span class="sf-chip">متوسط الإتقان ${Math.round(data.averageRetention)}%</span>
                ${overdue ? `<span class="sf-chip gold">${overdue} مستحقة للمراجعة</span>` : ''}
            </div>
            <div class="hm-legend"><span>متقن ومُراجَع حديثاً</span><span class="hm-scale"></span><span>بهت بطول الانقطاع</span>
                <span style="margin-inline-start:12px"><i class="hm-none-dot"></i> لم يُحفظ</span>
                <span><i class="hm-none-dot" style="background:var(--gold);border:0;border-radius:50%;width:9px;height:9px"></i> حان موعد مراجعتها</span></div>
            <div class="hm-juz" aria-label="الأجزاء">${data.juz.map(j => {
                const bg = j.retention == null ? 'var(--bg-secondary)' : heatColor(j.retention);
                const fg = j.retention == null ? 'var(--text-muted)' : textOn(j.retention);
                return `<div class="hm-juz-cell" data-juz="${j.juz}" style="background:${bg};color:${fg}"
                    title="الجزء ${j.juz}: ${j.memorizedCount}/${j.surahCount} سورة${j.retention != null ? ` — إتقان ${Math.round(j.retention)}%` : ''}">${j.juz}</div>`;
            }).join('')}</div>
            <div class="hm-grid">${data.surahs.map(s => {
                if (!s.memorized) return `<div class="hm-cell none" data-juz="${s.juz}" title="${esc(s.name)}: لم يُحفظ بعد"><strong>${esc(s.name)}</strong><small>${s.number}</small></div>`;
                const due = s.dueDate && new Date(s.dueDate) <= new Date();
                return `<div class="hm-cell ${due ? 'overdue' : ''}" data-juz="${s.juz}" style="background:${heatColor(s.retention)};color:${textOn(s.retention)}"
                    title="${esc(s.name)} — إتقان ${Math.round(s.retention)}% · آخر مراجعة قبل ${s.daysSinceReview} يوم · ${esc(s.lastEvaluation || '')}">
                    <strong>${esc(s.name)}</strong><small>${Math.round(s.retention)}% · ${s.daysSinceReview === 0 ? 'اليوم' : `قبل ${s.daysSinceReview} ي`}</small></div>`;
            }).join('')}</div>`;

        // تصفية السور حسب الجزء عند النقر (نقرة ثانية تلغي التصفية)
        container.querySelectorAll('.hm-juz-cell').forEach(cell => cell.addEventListener('click', () => {
            const active = cell.classList.contains('active');
            container.querySelectorAll('.hm-juz-cell').forEach(c => c.classList.remove('active'));
            const juz = Number(cell.dataset.juz);
            const surahNums = new Set(data.surahs.filter(s => coversJuz(s, juz, data.surahs)).map(s => s.number));
            container.querySelectorAll('.hm-cell').forEach((c, i) => c.classList.toggle('dim', !active && !surahNums.has(data.surahs[i].number)));
            if (!active) cell.classList.add('active');
        }));
    }

    // نفس قاعدة الخادم: السورة تمتد حتى جزء بداية السورة التالية
    function coversJuz(s, juz, all) {
        const next = all[s.number];
        const startsJuz = next && [46, 58, 67, 78].includes(next.number);
        const end = !next ? 30 : Math.max(s.juz, startsJuz ? next.juz - 1 : next.juz);
        return juz >= s.juz && juz <= end;
    }

    function mountStudentView() {
        const page = document.getElementById('page-studentView');
        if (!page || role() !== 'Student') return;
        let card = document.getElementById('studentReviewCard');
        if (!card) {
            card = document.createElement('div');
            card.id = 'studentReviewCard';
            const anchor = document.getElementById('studentMonthlyRating');
            page.insertBefore(card, anchor ? anchor.nextSibling : page.children[1]);
        }
        renderReviewCard(card, null);
        // الخريطة الحرارية في حاوية مستقلة (الخريطة القديمة تُحمَّل لاحقاً في surahGrid فتُخفى)
        const grid = document.getElementById('surahGrid');
        if (grid) {
            grid.style.display = 'none';
            const legend = grid.parentElement?.querySelector('.map-legend');
            if (legend) legend.style.display = 'none';
            let heat = document.getElementById('studentHeatmap');
            if (!heat) { heat = document.createElement('div'); heat.id = 'studentHeatmap'; grid.parentElement.appendChild(heat); }
            renderHeatmap(heat, null);
        }
    }

    // صفحة التسميع: الخريطة الحرارية للطالب المختار + مراجعة اليوم
    function mountMemorization(studentId) {
        const list = document.getElementById('surahList');
        if (!list) return;
        const legend = list.parentElement?.querySelector('.mem-legend');
        let heat = document.getElementById('memHeatmap');
        let rv = document.getElementById('memReviewCard');
        if (!heat) {
            heat = document.createElement('div'); heat.id = 'memHeatmap';
            rv = document.createElement('div'); rv.id = 'memReviewCard'; rv.style.marginTop = '16px';
            list.after(heat); heat.after(rv);
        }
        const show = !!studentId;
        list.style.display = show ? 'none' : '';
        if (legend) legend.style.display = show ? 'none' : '';
        heat.style.display = rv.style.display = show ? '' : 'none';
        if (!show) return;
        renderHeatmap(heat, studentId);
        renderReviewCard(rv, studentId);
    }

    // ═══════════════════════ 4) التحليلات والإنذار المبكر ═══════════════════════
    const charts = {};
    function drawBar(canvasId, labels, series) {
        const el = document.getElementById(canvasId);
        if (!el || typeof global.Chart !== 'function') return;
        charts[canvasId]?.destroy();
        const css = getComputedStyle(document.documentElement);
        const text = css.getPropertyValue('--text-muted').trim() || '#6B7280';
        charts[canvasId] = new global.Chart(el, {
            type: 'bar',
            data: { labels, datasets: series.map(s => ({ label: s.label, data: s.data, backgroundColor: s.color, borderRadius: 6, maxBarThickness: 28 })) },
            options: {
                responsive: true, maintainAspectRatio: false,
                plugins: { legend: { labels: { color: text, font: { family: 'Tajawal' } } }, tooltip: { rtl: true, callbacks: { label: c => `${c.dataset.label}: ${c.raw ?? '—'}${c.dataset.label.includes('٪') ? '%' : ''}` } } },
                scales: {
                    x: { ticks: { color: text, font: { family: 'Tajawal' } }, grid: { display: false } },
                    y: { beginAtZero: true, ticks: { color: text }, grid: { color: 'rgba(120,120,120,.12)' } }
                }
            }
        });
    }

    async function loadAnalytics() {
        const page = document.getElementById('page-analytics');
        if (!page || role() !== 'Admin') return;
        const days = Number(document.getElementById('anDays')?.value || 30);
        const riskBody = document.getElementById('anRiskBody');
        if (riskBody) riskBody.innerHTML = '<tr><td colspan="5" class="sf-muted" style="text-align:center;padding:24px">جاري التحليل...</td></tr>';
        try {
            const [risk, perf] = await Promise.all([api(`/analytics/at-risk?days=${days}`), api(`/analytics/performance?days=${days}`)]);
            const set = (id, v) => { const e = document.getElementById(id); if (e) e.textContent = v; };
            set('anKpiHigh', risk.high); set('anKpiMedium', risk.medium); set('anKpiTotal', risk.total);
            const rates = perf.circles.map(c => c.metrics.attendanceRate).filter(v => v != null);
            set('anKpiAttendance', rates.length ? Math.round(rates.reduce((a, b) => a + b, 0) / rates.length) + '%' : '—');

            if (riskBody) riskBody.innerHTML = risk.items.length ? risk.items.map(s => `
                <tr>
                    <td><strong>${esc(s.name)}</strong><div class="sf-muted" style="font-size:12px">${esc(s.circleName || 'بدون حلقة')}${s.teacherName ? ' · ' + esc(s.teacherName) : ''}</div></td>
                    <td><span class="an-score ${s.level}">${s.score}</span></td>
                    <td><ul class="an-reasons">${s.reasons.map(r => `<li>${esc(r)}</li>`).join('') || '<li>مؤشرات متوسطة</li>'}</ul></td>
                    <td style="white-space:nowrap">${s.absences}/${s.attendanceDays} غياب<br><span class="sf-muted">${s.daysSinceLastHifz == null ? 'بلا تسميع' : `آخر تسميع قبل ${s.daysSinceLastHifz} يوم`}</span></td>
                    <td style="white-space:nowrap">
                        <button class="sf-btn outline" type="button" data-preview="${s.studentId}" title="معاينة التقرير الشهري">${icon('file-text', 14)}</button>
                        ${s.parentPhone ? `<a class="sf-btn outline" target="_blank" rel="noopener" href="https://wa.me/${encodeURIComponent(waPhone(s.parentPhone))}?text=${encodeURIComponent(`السلام عليكم، نود التواصل معكم بخصوص متابعة ${s.name} في الحلقة.`)}" title="تواصل مع ولي الأمر">${icon('message-circle', 14)}</a>` : ''}
                    </td>
                </tr>`).join('')
                : `<tr><td colspan="5" style="text-align:center;padding:24px;color:var(--green);font-weight:700">${icon('check-circle', 16)} لا يوجد طلاب معرّضون للانقطاع حالياً</td></tr>`;
            riskBody?.querySelectorAll('[data-preview]').forEach(b => b.addEventListener('click', () => previewReport(Number(b.dataset.preview))));

            const short = (s) => (s || '—').length > 14 ? s.slice(0, 13) + '…' : (s || '—');
            drawBar('anCirclesChart', perf.circles.map(c => short(c.name)), [
                { label: 'نسبة الحضور ٪', data: perf.circles.map(c => c.metrics.attendanceRate), color: '#1A5C3A' },
                { label: 'نجاح الجلسات ٪', data: perf.circles.map(c => c.metrics.successRate), color: '#B8860B' }
            ]);
            drawBar('anTeachersChart', perf.teachers.map(t => short(t.name)), [
                { label: 'جلسات لكل طالب', data: perf.teachers.map(t => t.metrics.sessionsPerStudent), color: '#2D8B57' },
                { label: 'أيام رصد الحضور', data: perf.teachers.map(t => t.metrics.recordedDays), color: '#DAA520' }
            ]);
            const tBody = document.getElementById('anTeachersBody');
            if (tBody) tBody.innerHTML = perf.teachers.map(t => `<tr>
                <td><strong>${esc(t.name)}</strong></td><td>${t.metrics.students}</td>
                <td>${t.metrics.attendanceRate ?? '—'}${t.metrics.attendanceRate != null ? '%' : ''}</td>
                <td>${t.metrics.sessions}</td><td>${t.metrics.successRate ?? '—'}${t.metrics.successRate != null ? '%' : ''}</td>
                <td>${t.metrics.newVerses}</td><td>${t.metrics.recordedDays}/${perf.windowDays}</td></tr>`).join('')
                || '<tr><td colspan="7" class="sf-muted" style="text-align:center">لا يوجد محفّظون</td></tr>';
        } catch (e) {
            global.handleApiError?.(e);
        }
    }

    const waPhone = (p) => {
        let d = String(p || '').replace(/\D/g, '');
        if (d.startsWith('00')) d = d.slice(2);
        if (d.startsWith('0')) d = '218' + d.slice(1);
        else if (!d.startsWith('218') && d.length === 9) d = '218' + d;
        return d;
    };

    // ═══════════════════════ 5) التقرير الشهري لولي الأمر ═══════════════════════
    function monthValue() {
        const v = document.getElementById('prMonth')?.value; // yyyy-mm
        if (!v) return {};
        const [y, m] = v.split('-').map(Number);
        return { year: y, month: m };
    }

    async function previewReport(studentId) {
        const { year, month } = monthValue();
        try {
            const q = year ? `?year=${year}&month=${month}` : '';
            const r = await api(`/parent-reports/preview/${studentId}${q}`);
            const box = modal('معاينة التقرير الشهري', `
                <div class="sf-pre">${esc(r.text)}</div>
                <div style="display:flex;gap:8px;margin-top:14px;flex-wrap:wrap">
                    <button class="sf-btn" type="button" id="prSendOne">${icon('send', 14)} ${r.whatsAppConfigured ? 'إرسال عبر واتساب' : 'تسجيل وتجهيز الإرسال'}</button>
                    ${r.shareLink ? `<a class="sf-btn outline" href="${esc(r.shareLink)}" target="_blank" rel="noopener">${icon('message-circle', 14)} فتح في واتساب</a>` : '<span class="sf-muted">لا يوجد رقم لولي الأمر</span>'}
                </div>`);
            box.querySelector('#prSendOne').addEventListener('click', async (e) => {
                e.target.disabled = true;
                await sendReports({ studentIds: [studentId] });
                box.remove();
            });
        } catch (e) { global.handleApiError?.(e); }
    }

    async function sendReports(scope) {
        const { year, month } = monthValue();
        try {
            const r = await api('/parent-reports/send', 'POST', Object.assign({ year, month }, scope));
            toast(r.message, r.sent || r.prepared ? 'success' : 'warning');
            renderSendResults(r);
            return r;
        } catch (e) { global.handleApiError?.(e); return null; }
    }

    const STATUS_LABEL = { Sent: ['أُرسل', ''], Prepared: ['جاهز للإرسال', 'gold'], NoPhone: ['بلا رقم', 'red'], Failed: ['تعذر', 'red'] };
    function renderSendResults(r) {
        const box = document.getElementById('prResults');
        if (!box || !r) return;
        box.innerHTML = `<div class="an-scroll"><table class="an-table"><thead><tr><th>الطالب</th><th>الحالة</th><th></th></tr></thead><tbody>
            ${r.results.map(x => {
                const [label, cls] = STATUS_LABEL[x.status] || [x.status, ''];
                return `<tr><td>${esc(x.studentName)}</td><td><span class="sf-chip ${cls}">${label}</span></td>
                    <td>${x.shareLink ? `<a class="sf-btn outline" href="${esc(x.shareLink)}" target="_blank" rel="noopener">${icon('message-circle', 14)} إرسال يدوي</a>` : ''}</td></tr>`;
            }).join('')}</tbody></table></div>`;
    }

    function reportCardHtml(isAdmin) {
        const d = new Date(); d.setDate(1); d.setMonth(d.getMonth() - 1);
        const ym = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
        return `<div class="sf-card" id="parentReportCard">
            <div class="sf-card-head"><div><h3>${icon('file-text', 16)} التقرير الشهري لأولياء الأمور</h3>
                <p>ملخص الحفظ والمراجعة والحضور والصلوات وملاحظة المحفّظ — يُرسل تلقائياً أول كل شهر، ويمكن إرساله يدوياً هنا</p></div></div>
            <div class="sf-card-body">
                <div style="display:flex;gap:10px;flex-wrap:wrap;align-items:center;margin-bottom:12px">
                    <label class="sf-muted" for="prMonth">الشهر</label>
                    <input type="month" id="prMonth" class="sf-select" value="${ym}">
                    <select id="prCircle" class="sf-select" aria-label="الحلقة"><option value="">${isAdmin ? 'كل الطلاب' : 'كل طلاب حلقاتي'}</option></select>
                    <button class="sf-btn gold" type="button" id="prSendBtn">${icon('send', 14)} إرسال التقارير</button>
                </div>
                <div id="prResults" class="sf-muted">اختر الحلقة ثم اضغط «إرسال التقارير». إن لم يكن واتساب مُعدّاً تُجهَّز التقارير مع روابط إرسال يدوي، وتصل نسخة لولي الأمر داخل التطبيق.</div>
            </div></div>`;
    }

    async function wireReportCard(isAdmin) {
        const sel = document.getElementById('prCircle');
        try {
            const circles = await api('/circles');
            (circles || []).forEach(c => { const o = document.createElement('option'); o.value = c.id; o.textContent = c.name; sel?.appendChild(o); });
        } catch { /* تبقى «الكل» */ }
        const btn = document.getElementById('prSendBtn');
        btn?.addEventListener('click', async () => {
            const circleId = sel?.value ? Number(sel.value) : null;
            const label = circleId ? `طلاب «${sel.options[sel.selectedIndex].text}»` : (isAdmin ? 'كل الطلاب' : 'كل طلاب حلقاتك');
            if (!confirm(`سيتم إرسال التقرير الشهري لأولياء أمور ${label}. متابعة؟`)) return;
            btn.disabled = true;
            await sendReports(circleId ? { circleId } : { all: true });
            btn.disabled = false;
        });
    }

    function mountTeacherReportCard() {
        if (role() !== 'Teacher') return;
        const page = document.getElementById('page-dashboard');
        if (!page || document.getElementById('parentReportCard')) return;
        const holder = document.createElement('div');
        holder.innerHTML = reportCardHtml(false);
        const grid = page.querySelector('.stat-grid');
        page.insertBefore(holder.firstElementChild, grid ? grid.nextSibling : page.firstChild);
        wireReportCard(false);
    }

    function mountAnalyticsPage() {
        const page = document.getElementById('page-analytics');
        if (!page || page.dataset.ready) return;
        page.dataset.ready = '1';
        page.innerHTML = `
            <div class="page-header" style="display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap;margin-bottom:16px">
                <div><h2 style="margin:0">التحليلات والتوقعات</h2><p class="sf-muted" style="margin:4px 0 0">إنذار مبكر بالانقطاع ومقارنة أداء الحلقات والمحفّظين</p></div>
                <div style="display:flex;gap:8px;align-items:center">
                    <select id="anDays" class="sf-select" aria-label="الفترة"><option value="30">آخر 30 يوماً</option><option value="60">آخر 60 يوماً</option><option value="90">آخر 90 يوماً</option></select>
                    <button class="sf-btn outline" type="button" id="anRefresh">${icon('refresh-cw', 14)} تحديث</button>
                </div>
            </div>
            <div class="an-kpis">
                <div class="an-kpi warn"><b id="anKpiHigh">—</b><span>خطر انقطاع مرتفع</span></div>
                <div class="an-kpi gold"><b id="anKpiMedium">—</b><span>يحتاج متابعة</span></div>
                <div class="an-kpi"><b id="anKpiAttendance">—</b><span>متوسط حضور الحلقات</span></div>
                <div class="an-kpi"><b id="anKpiTotal">—</b><span>طالب نشط</span></div>
            </div>
            <div class="sf-card">
                <div class="sf-card-head"><div><h3>${icon('alert-triangle', 16)} مؤشر الإنذار المبكر</h3>
                    <p>الطلاب المعرّضون للانقطاع بناءً على تكرار الغياب وتوقف التسميع — لاتخاذ إجراء تربوي مبكر</p></div></div>
                <div class="an-scroll"><table class="an-table"><thead><tr><th>الطالب</th><th>الخطر</th><th>الأسباب</th><th>المؤشرات</th><th>إجراء</th></tr></thead>
                    <tbody id="anRiskBody"></tbody></table></div>
            </div>
            <div class="an-grid">
                <div class="sf-card"><div class="sf-card-head"><h3>${icon('bar-chart-2', 16)} مقارنة الحلقات</h3></div>
                    <div class="sf-card-body"><div class="an-chart-wrap"><canvas id="anCirclesChart" aria-label="مقارنة الحلقات"></canvas></div></div></div>
                <div class="sf-card"><div class="sf-card-head"><h3>${icon('bar-chart', 16)} التزام المحفّظين وإنجازهم</h3></div>
                    <div class="sf-card-body"><div class="an-chart-wrap"><canvas id="anTeachersChart" aria-label="مقارنة المحفظين"></canvas></div></div></div>
            </div>
            <div class="sf-card"><div class="sf-card-head"><h3>${icon('user-check', 16)} جدول أداء المحفّظين</h3></div>
                <div class="an-scroll"><table class="an-table"><thead><tr><th>المحفّظ</th><th>الطلاب</th><th>الحضور</th><th>الجلسات</th><th>نجاح الجلسات</th><th>آيات جديدة</th><th>أيام الرصد</th></tr></thead>
                <tbody id="anTeachersBody"></tbody></table></div></div>
            ${reportCardHtml(true)}`;
        document.getElementById('anRefresh').addEventListener('click', loadAnalytics);
        document.getElementById('anDays').addEventListener('change', loadAnalytics);
        wireReportCard(true);
        if (global.renderNoorIcons) global.renderNoorIcons(page);
    }

    // ═══════════════════════ الربط مع التنقل ═══════════════════════
    const prevHook = global._onNavigatePage;
    global._onNavigatePage = function (page) {
        if (typeof prevHook === 'function') prevHook(page);
        if (!loggedIn()) return;
        startPolling();
        if (page === 'studentView') mountStudentView();
        if (page === 'dashboard') mountTeacherReportCard();
        if (page === 'analytics') { mountAnalyticsPage(); loadAnalytics(); }
    };

    // خريطة الإتقان في صفحة التسميع عند اختيار طالب
    function hookMemorization() {
        const h = global.NoorHifz;
        if (!h || h._sfHooked) return;
        const orig = h.filterStudentById;
        h.filterStudentById = function (id) { orig(id); mountMemorization(parseInt(id, 10)); };
        global.filterStudent = function (val) { h.filterStudentById(val); };
        h._sfHooked = true;
    }

    function boot() {
        hookMemorization();
        if (loggedIn()) {
            startPolling();
            mountTeacherReportCard();
        }
        // بعد تسجيل الدخول (الصفحة لا تُعاد تحميلها): نراقب ظهور التوكن
        const watcher = setInterval(() => {
            if (loggedIn()) { startPolling(); mountTeacherReportCard(); clearInterval(watcher); }
        }, 1500);
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();

    // ولي الأمر: خريطة إتقان الابن وجدول مراجعته في نافذة
    function openChildMastery(studentId, name) {
        const box = modal('خريطة الإتقان — ' + (name || ''), '<div id="sfChildReview"></div><div id="sfChildHeat"></div>');
        box.querySelector('.sf-modal-box').style.width = '860px';
        renderReviewCard(box.querySelector('#sfChildReview'), studentId);
        renderHeatmap(box.querySelector('#sfChildHeat'), studentId);
    }

    global.NoorSmart = { openChildMastery, renderHeatmap, renderReviewCard, heatColor, loadAnalytics, previewReport, poll };
})(window);
