/**
 * منصة نور — التقييم الشهري بحسب جلسات الحفظ الجديد الناجحة (من 10)
 * يُستخدم في: لوحة الطالب، بطاقات أبناء ولي الأمر، وتقرير المحفظ/الأدمن في صفحة تقدم الحفظ والمراجعة.
 */
(function (global) {
    'use strict';

    const esc = (s) => (global.NoorUtils?.escapeHtml ? global.NoorUtils.escapeHtml(String(s ?? '')) : String(s ?? ''));
    const icon = (n, o) => (global.Icon ? global.Icon(n, o) : '');
    const MONTHS = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو', 'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];

    function monthLabel(r) {
        return r ? `${MONTHS[(r.month || 1) - 1]} ${r.year}` : '';
    }

    function scoreColor(score) {
        if (score >= 8) return 'var(--green)';
        if (score >= 5) return 'var(--amber-dark, #b45309)';
        return '#dc2626';
    }

    /** بطاقة مفصلة (لوحة الطالب). */
    function cardHtml(r, title) {
        if (!r) return '';
        const pct = Math.min(100, Math.round((r.score / 10) * 100));
        return `
            <div class="stat-card" style="background:var(--card);color:var(--text);box-shadow:var(--shadow);padding:18px 20px">
                <div style="display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap">
                    <h3 style="font-size:16px;margin:0">${icon('award', { size: 16 })} ${esc(title || 'تقييم الحفظ الجديد — ' + monthLabel(r))}</h3>
                    <span style="font-size:28px;font-weight:900;color:${scoreColor(r.score)}">${r.score} <small style="font-size:14px;color:var(--text-muted)">/ 10</small></span>
                </div>
                <div style="width:100%;height:10px;background:#e2e8f0;border-radius:10px;overflow:hidden;margin:10px 0">
                    <div style="width:${pct}%;height:100%;background:var(--gradient)"></div>
                </div>
                <p style="font-size:13px;color:var(--text-muted);margin:0">
                    ${r.successfulSessions} جلسة حفظ جديد ناجحة من أصل ${r.target} مطلوبة (شريحة العمر ${esc(r.ageBand)})
                </p>
                ${r.warning ? `<p style="font-size:12px;color:#b45309;margin:6px 0 0">${icon('alert-triangle', { size: 12 })} ${esc(r.warning)}</p>` : ''}
            </div>`;
    }

    /** خانة صغيرة داخل بطاقة (ولي الأمر). */
    function miniStatHtml(r) {
        if (!r) return '';
        const title = `${r.successfulSessions}/${r.target} جلسة ناجحة — ${monthLabel(r)}${r.warning ? ' — ' + r.warning : ''}`;
        return `<div class="mini-stat" title="${esc(title)}"><label>تقييم الشهر</label><p style="color:${scoreColor(r.score)}">${r.score}/10</p></div>`;
    }

    function renderStudentCard(containerId, rating) {
        const el = document.getElementById(containerId);
        if (!el) return;
        el.innerHTML = cardHtml(rating);
        el.style.display = rating ? 'block' : 'none';
    }

    // ─── تقرير المحفظ/الأدمن ───
    function currentMonthValue() {
        const d = new Date();
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
    }

    async function loadReport() {
        const monthInput = document.getElementById('monthlyRatingMonth');
        const tbody = document.getElementById('monthlyRatingBody');
        const meta = document.getElementById('monthlyRatingMeta');
        if (!monthInput || !tbody) return;
        if (!monthInput.value) monthInput.value = currentMonthValue();
        const [year, month] = monthInput.value.split('-').map(Number);
        const circleId = document.getElementById('monthlyRatingCircle')?.value || '';

        tbody.innerHTML = '<tr><td colspan="5"><div class="skeleton-line"></div></td></tr>';
        try {
            let q = `/hifz/monthly-ratings?year=${year}&month=${month}`;
            if (circleId) q += `&circleId=${encodeURIComponent(circleId)}`;
            const data = await global.apiFetch(q);
            if (meta) {
                meta.textContent = `الهدف: 5–7 سنوات = ${data.targets.age5To7} · 8–10 = ${data.targets.age8To10} · 11 فأكثر = ${data.targets.age11Plus} جلسات — التقييمات الناجحة: ${(data.passingEvaluations || []).join('، ')}`;
            }
            const items = data.items || [];
            if (!items.length) {
                tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;padding:20px;color:var(--text-muted)">لا يوجد طلاب</td></tr>';
                return;
            }
            tbody.innerHTML = items.map(i => {
                const r = i.rating || {};
                return `<tr>
                    <td><strong>${esc(i.fullName)}</strong><div style="font-size:11px;color:var(--text-muted)">${esc(i.circleName)}</div></td>
                    <td>${r.ageYears ?? '—'}${r.warning ? ` <span title="${esc(r.warning)}" style="color:#b45309">${icon('alert-triangle', { size: 12 })}</span>` : ''}</td>
                    <td>${r.successfulSessions ?? 0} / ${r.target ?? '—'}</td>
                    <td>${r.totalMemorizationSessions ?? 0}</td>
                    <td><strong style="color:${scoreColor(r.score ?? 0)}">${r.score ?? 0}</strong> / 10</td>
                </tr>`;
            }).join('');
        } catch (e) {
            tbody.innerHTML = `<tr><td colspan="5" style="text-align:center;padding:20px;color:#dc2626">${esc(e.message || 'تعذر تحميل التقرير')}</td></tr>`;
        }
    }

    function populateCircleFilter() {
        const sel = document.getElementById('monthlyRatingCircle');
        if (!sel) return;
        const circles = global.NoorApp?.state?.circles || [];
        const current = sel.value;
        sel.innerHTML = '<option value="">كل الحلقات</option>' +
            circles.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
        sel.value = current;
    }

    function initReport() {
        populateCircleFilter();
        loadReport();
    }

    global.NoorMonthlyRating = { cardHtml, miniStatHtml, renderStudentCard, loadReport, initReport, monthLabel };
})(window);
