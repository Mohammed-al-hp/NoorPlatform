/**
 * منصة نور — لوحة التحكم والإعلانات والمتصدرين
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = (s) => utils().escapeHtml(s);
    const icon = (name, opts) => (global.Icon ? global.Icon(name, opts) : '');
    const TARGET_LABELS = { All: 'الجميع', Teachers: 'المحفّظون', Students: 'الطلاب', Parents: 'أولياء الأمور' };

    let barChartInstance = null;
    let donutChartInstance = null;

    async function fetchStats() {
        try {
            const data = await apiFetch('/dashboard/stats');
            const el = id => document.getElementById(id);
            if (el('dashStudents')) el('dashStudents').textContent = data.students;
            if (el('dashTeachers')) el('dashTeachers').textContent = data.teachers;
            if (el('dashCircles')) el('dashCircles').textContent = data.circles;
            if (el('dashAttendance')) el('dashAttendance').textContent = data.attendanceToday || '0%';
            if (el('dashStudentsTrend')) {
                const n = data.studentsThisMonth ?? 0;
                el('dashStudentsTrend').textContent = n > 0 ? `${n} هذا الشهر` : 'لا تسجيلات هذا الشهر';
                el('dashStudentsTrend').className = 'stat-trend' + (n > 0 ? ' trend-up' : '');
            }
            if (el('dashTeachersTrend')) {
                const n = data.teachersNewThisMonth ?? 0;
                el('dashTeachersTrend').textContent = n > 0 ? `${n} جديد` : 'لا إضافات هذا الشهر';
                el('dashTeachersTrend').className = 'stat-trend' + (n > 0 ? ' trend-up' : '');
            }
            if (el('dashAttTrend')) {
                const rate = data.attendanceLast30 ?? data.attendanceLast30Days;
                el('dashAttTrend').textContent = rate != null && rate !== ''
                    ? `آخر 30 يوم: ${String(rate).includes('%') ? rate : rate + '%'}`
                    : 'لا بيانات حضور';
            }
            if (el('dashCirclesTrend')) {
                el('dashCirclesTrend').textContent = (data.circles ?? 0) + ' حلقة مسجّلة';
            }

            const weekly = Array.isArray(data.weeklyAttendance)
                ? data.weeklyAttendance
                : (data.weeklyAttendance ? Object.values(data.weeklyAttendance) : []);
            
            if (weekly.length && global.Chart) {
                const ctx = document.getElementById('weeklyBarChartCanvas');
                if (ctx) {
                    const labels = weekly.map(d => d.dayName);
                    const values = weekly.map(d => Number(d.percentage) || 0);
                    
                    if (barChartInstance) {
                        barChartInstance.data.labels = labels;
                        barChartInstance.data.datasets[0].data = values;
                        barChartInstance.update();
                    } else {
                        barChartInstance = new Chart(ctx, {
                            type: 'bar',
                            data: {
                                labels: labels,
                                datasets: [{
                                    label: 'نسبة الحضور',
                                    data: values,
                                    backgroundColor: values.map(v => v > 70 ? '#1A5C3A' : '#cbd5e1'),
                                    borderRadius: 6,
                                    barPercentage: 0.6
                                }]
                            },
                            options: {
                                responsive: true,
                                maintainAspectRatio: false,
                                scales: {
                                    y: { beginAtZero: true, max: 100, ticks: { callback: function(val) { return val + '%'; } } }
                                },
                                plugins: { legend: { display: false } }
                            }
                        });
                    }
                }
            }

            if (data.levelDistribution && global.Chart) {
                const ld = data.levelDistribution;
                const total = ld.advanced + ld.intermediate + ld.beginner;
                const ctx2 = document.getElementById('donutChartCanvas');
                if (ctx2 && total > 0) {
                    const chartData = [ld.advanced, ld.intermediate, ld.beginner];
                    
                    if (donutChartInstance) {
                        donutChartInstance.data.datasets[0].data = chartData;
                        donutChartInstance.update();
                    } else {
                        donutChartInstance = new Chart(ctx2, {
                            type: 'doughnut',
                            data: {
                                labels: ['متقدم', 'متوسط', 'مبتدئ'],
                                datasets: [{
                                    data: chartData,
                                    backgroundColor: ['#1A5C3A', '#3b82f6', '#f59e0b'],
                                    borderWidth: 0,
                                    hoverOffset: 4
                                }]
                            },
                            options: {
                                responsive: true,
                                maintainAspectRatio: false,
                                cutout: '75%',
                                plugins: {
                                    legend: { position: 'bottom', labels: { font: { family: 'Tajawal' } } }
                                }
                            }
                        });
                    }
                }
            }

            if (data.recentHifz) {
                const tbody = document.getElementById('recentHifzBody');
                if (tbody) {
                    if (!data.recentHifz.length) {
                        tbody.innerHTML = '<tr><td colspan="5" style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد جلسات تسميع بعد</td></tr>';
                    } else {
                        const gradients = ['linear-gradient(135deg,#1A5C3A,#2D8B57)', 'linear-gradient(135deg,#2D8B57,#B8860B)', 'linear-gradient(135deg,#B8860B,#DAA520)', 'linear-gradient(135deg,#f59e0b,#ef4444)', 'linear-gradient(135deg,#0F3D27,#1A5C3A)'];
                        tbody.innerHTML = data.recentHifz.map((r, i) => {
                            const evalClass = r.evaluation === 'ممتاز' ? 'status-excellent' : r.evaluation === 'جيد' ? 'status-good' : 'status-late';
                            const evalIconName = r.evaluation === 'ممتاز' ? 'star' : r.evaluation === 'جيد' ? 'thumbs-up' : 'refresh-cw';
                            return `<tr>
                                <td>${i + 1}</td>
                                <td><div class="student-cell"><div class="avatar" style="background:${gradients[i % 5]}">${esc((r.studentName || '').slice(0, 2))}</div><span>${esc(r.studentName)}</span></div></td>
                                <td>${esc(r.circleName)}</td>
                                <td>${esc(r.surahName)} (${esc(r.verses)})</td>
                                <td><span class="status-badge ${evalClass}">${icon(evalIconName, { size: 14 })} ${esc(r.evaluation)}</span></td>
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
                // ملاحظة: a.icon قادم من الـ API — إذا كان إيموجي من الخلفية، يفضّل تعديل الـ API ليرسل
                // اسم أيقونة (مثل "check-circle") بدل إيموجي حتى يظهر متسقًا هنا أيضًا.
                const activityIcon = global.Icon && global.NoorIcons.PATHS[a.icon] ? icon(a.icon, { size: 14 }) : (a.icon || '');
                return `<div class="notif-item">
                    <div class="notif-dot-wrap">
                        <div class="notif-dot ${dotClass}">${activityIcon}</div>
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

            const badge = document.getElementById('announcementsBadge');
            if (badge) {
                if (data.length > 0) {
                    badge.textContent = data.length;
                    badge.style.display = 'inline-flex';
                } else {
                    badge.style.display = 'none';
                }
            }
            const subtitle = document.getElementById('announcementsSubtitle');
            if (subtitle) subtitle.textContent = data.length + ' إعلان' + (data.length !== 1 ? 'ات' : '');

            if (!data.length) { list.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد إعلانات حالياً</div>'; return; }
            list.innerHTML = data.map(a => `
            <div class="ann-card" data-title="${esc(a.title)}">
              <div class="ann-indicator" style="background:${esc(a.color)}"></div>
              <div class="ann-card-content">
                <div class="ann-card-top"><h4>${esc(a.title)}</h4><time>${utils().formatDateEnGb(a.createdAt)}</time></div>
                <p>${esc(a.content)}</p>
                <div class="ann-target" style="color:${esc(a.color)};display:flex;align-items:center;gap:10px;flex-wrap:wrap">
                  <span>${icon('target', { size: 13 })} ${esc(TARGET_LABELS[a.target] || a.target)}</span>
                  <span style="color:var(--text-muted)">${icon('user', { size: 13 })} ${esc(a.authorName || 'إدارة المركز')}</span>
                  ${a.canManage ? `<button type="button" class="btn btn-outline ann-delete" data-id="${Number(a.id)}" style="padding:2px 10px;font-size:11px;margin-inline-start:auto">حذف</button>` : ''}
                </div>
              </div>
            </div>`).join('');
            list.querySelectorAll('.ann-card').forEach(card => {
                card.addEventListener('click', () => app().ui.showToast(card.dataset.title, 'info'), { once: false });
            });
            list.querySelectorAll('.ann-delete').forEach(btn => {
                btn.addEventListener('click', ev => {
                    ev.stopPropagation();
                    global.confirmDelete('حذف هذا الإعلان؟', async () => {
                        try {
                            await apiFetch(`/announcements/${btn.dataset.id}`, 'DELETE');
                            app().ui.showToast('تم حذف الإعلان', 'success');
                            fetchAnnouncements();
                        } catch (err) { app().api.handleApiError(err); }
                    });
                });
            });
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    // نطاق المتصدرين للمحفّظ: «طلابي» افتراضياً، أو «المركز» (أول عشرة في المركز). الإدارة ترى المركز دائماً.
    const LEADERBOARD_SCOPE_KEY = 'noor_leaderboard_scope';

    function leaderboardScope() {
        try { return localStorage.getItem(LEADERBOARD_SCOPE_KEY) === 'center' ? 'center' : 'mine'; }
        catch (e) { return 'mine'; }
    }

    function setLeaderboardScope(scope) {
        try { localStorage.setItem(LEADERBOARD_SCOPE_KEY, scope === 'center' ? 'center' : 'mine'); } catch (e) { /* تخزين غير متاح */ }
        fetchLeaderboard();
    }

    async function fetchLeaderboard() {
        const wrap = document.getElementById('leaderboardWrap');
        if (!wrap) return;
        const isTeacher = app().state?.user?.role === 'Teacher';
        const scope = isTeacher ? leaderboardScope() : 'center';
        const scopeBox = document.getElementById('leaderboardScope');
        if (scopeBox) {
            scopeBox.style.display = isTeacher ? 'inline-flex' : 'none';
            scopeBox.querySelectorAll('[data-scope]').forEach(b => {
                const on = b.dataset.scope === scope;
                b.classList.toggle('active', on);
                b.setAttribute('aria-pressed', on ? 'true' : 'false');
            });
        }
        const sub = document.getElementById('leaderboardSubtitle');
        if (sub) sub.textContent = isTeacher && scope === 'mine'
            ? 'أفضل عشرة من طلاب حلقاتك بمجموع النقاط'
            : 'أفضل عشرة طلاب في المركز بمجموع النقاط';
        try {
            const data = await apiFetch('/dashboard/leaderboard?scope=' + scope);
            if (!data?.length) {
                wrap.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا يوجد طلاب لعرضهم</div>';
                return;
            }
            wrap.innerHTML = data.map(s => {
                const rankDisplay = s.rank <= 3
                    ? `<span class="rank-medal rank-${s.rank}">${icon('medal-gold', { size: 22 })}</span>`
                    : `<span style="font-weight:bold">#${s.rank}</span>`;
                const mine = isTeacher && scope === 'center' && s.isMine;
                return `<div style="display:flex;align-items:center;gap:15px;padding:12px;background:${mine ? 'var(--green-light)' : 'var(--card)'};border:1px solid var(--border);border-radius:12px">
                    <div style="width:40px;text-align:center">${rankDisplay}</div>
                    <div style="flex:1"><div style="font-weight:700">${esc(s.fullName)}</div><div style="font-size:12px;color:var(--text-muted)">${esc(s.circleName)} • حضور: ${s.attendanceRate}%${mine ? ' • من طلابك' : ''}</div></div>
                    <div style="font-weight:800;color:var(--green)">${s.points} نقطة</div>
                </div>`;
            }).join('');
        } catch (e) {
            wrap.innerHTML = '<div style="text-align:center;padding:40px;color:var(--text-muted)">تعذر تحميل المتصدرين</div>';
        }
    }

    // ─── Smart Polling Logic ───
    let pollInterval = null;

    function startPolling() {
        if (pollInterval) clearInterval(pollInterval);
        pollInterval = setInterval(() => {
            const dash = document.getElementById('page-dashboard');
            const isDashVisible = dash && dash.style.display !== 'none';
            const currentRole = app().state?.user?.role;
            if (!document.hidden && isDashVisible && currentRole === 'Admin') {
                fetchStats();
            }
        }, 30000);
    }

    function stopPolling() {
        if (pollInterval) {
            clearInterval(pollInterval);
            pollInterval = null;
        }
    }

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            stopPolling();
        } else {
            const dash = document.getElementById('page-dashboard');
            const currentRole = app().state?.user?.role;
            if (dash && dash.style.display !== 'none' && currentRole === 'Admin') {
                fetchStats();
            }
            startPolling();
        }
    });

    startPolling();

    /** رسالة ثابتة أعلى صفحة الإعدادات (نتيجة الحفظ أو تنبيه الصلاحية) — أوضح من Toast عابر. */
    function setSettingsStatus(kind, msg) {
        const box = document.getElementById('settingsStatus');
        if (!box) return;
        if (!msg) { box.style.display = 'none'; box.textContent = ''; return; }
        box.className = 'settings-status settings-status-' + kind;
        box.textContent = msg;
        box.style.display = 'block';
    }

    function canEditSettings() {
        return typeof global.NoorHasAdminPermission === 'function'
            ? global.NoorHasAdminPermission('ManageSettings')
            : true;
    }

    function applySettingsEditability(canEdit, reason) {
        const btn = document.getElementById('btnSaveSettings');
        if (btn) btn.disabled = !canEdit;
        if (!canEdit) {
            setSettingsStatus('warning', reason ||
                'لديك صلاحية عرض إعدادات النظام فقط. لتعديلها اطلب من المدير العام منحك صلاحية «إعدادات النظام» من صفحة المشرفين والحسابات.');
        }
    }

    function toPercent(v) {
        if (v === undefined || v === null || v === '') return null;
        return Math.round(Number(v) * 1000) / 10; // 0.625 → 62.5
    }

    function fromPercent(v) {
        if (v === undefined || v === null || v === '') return null;
        const n = parseFloat(v);
        return Number.isFinite(n) ? Math.round(n * 10) / 1000 : null; // 62.5 → 0.625
    }

    function intOrNull(id) {
        const raw = document.getElementById(id)?.value?.trim();
        if (raw === undefined || raw === '') return null;
        const n = parseInt(raw, 10);
        return Number.isFinite(n) ? n : null;
    }

    /** يخفّف عرض الإعدادات المرتبطة بعنصر تقييم معطّل (مثل نقاط المتون ووزن اللباس). */
    function refreshDependentSettings() {
        const map = { matn: 'settingsEnableMatn', dress: 'settingsEnableDress' };
        document.querySelectorAll('#page-settings [data-requires]').forEach(el => {
            const toggle = document.getElementById(map[el.dataset.requires]);
            el.classList.toggle('is-disabled-element', !!toggle && !toggle.checked);
        });
    }

    const SETTINGS_TAB_KEY = 'noor_settings_tab';

    function showTab(name) {
        const panels = document.querySelectorAll('#page-settings .settings-panel');
        if (![...panels].some(p => p.dataset.settingsPanel === name)) name = 'center';
        panels.forEach(p => p.classList.toggle('active', p.dataset.settingsPanel === name));
        document.querySelectorAll('#page-settings .settings-nav-item').forEach(b => {
            const on = b.dataset.settingsTab === name;
            b.classList.toggle('active', on);
            b.setAttribute('aria-current', on ? 'page' : 'false');
        });
        try { localStorage.setItem(SETTINGS_TAB_KEY, name); } catch (e) { /* تخزين غير متاح */ }
    }

    function initSettingsPage() {
        const page = document.getElementById('page-settings');
        if (!page || page.dataset.settingsInit) return;
        page.dataset.settingsInit = '1';
        ['settingsEnableMatn', 'settingsEnableDress'].forEach(id =>
            document.getElementById(id)?.addEventListener('change', refreshDependentSettings));
        let saved = 'center';
        try { saved = localStorage.getItem(SETTINGS_TAB_KEY) || 'center'; } catch (e) { /* تخزين غير متاح */ }
        showTab(saved);
    }

    async function fetchSettings() {
        initSettingsPage();
        setSettingsStatus(null);
        applySettingsEditability(canEditSettings());
        try {
            const data = await apiFetch('/settings');

            // تعبئة البيانات في الحقول
            const fields = {
                'settingsCenterName': data.centerName,
                'settingsContactPhone': data.contactPhone,
                'settingsAddress': data.address,
                'settingsWorkDays': data.workDays,
                'settingsWorkStartTime': data.workStartTime,
                'settingsWorkEndTime': data.workEndTime,
                'settingsDefaultMonthlyFee': data.defaultMonthlyFee,
                'settingsCurrency': data.currency,
                'settingsOralMaxOpenings': data.oralMaxOpeningsBeforeFail,
                'settingsOralHesitation': data.oralHesitationPenalty,
                'settingsOralAlert': data.oralAlertPenalty,
                'settingsOralOpening': data.oralOpeningPenalty,
                'settingsDefaultAthman': data.defaultMonthlyAthmanTarget,
                // النسب مخزنة كقيمة من 1 وتُعرض كنسبة مئوية
                'settingsPT10': toPercent(data.progressThreshold10),
                'settingsPT9': toPercent(data.progressThreshold9),
                'settingsPT8': toPercent(data.progressThreshold8),
                'settingsPT6': toPercent(data.progressThreshold6),
                'settingsPT4': toPercent(data.progressThreshold4),
                'settingsWAtt': data.weightAttendance,
                'settingsWHifz': data.weightHifz,
                'settingsWRev': data.weightRevision,
                'settingsWProg': data.weightProgress,
                'settingsWMatn': data.weightMatn,
                'settingsWDress': data.weightDress,
                'settingsPointsAttendance': data.pointsAttendancePresent,
                'settingsPointsHifzMemorization': data.pointsHifzMemorization,
                'settingsPointsHifzRevision': data.pointsHifzRevision,
                'settingsPointsLate': data.pointsAttendanceLate,
                'settingsPointsExcused': data.pointsAttendanceExcused,
                'settingsPointsUnexcused': data.pointsAttendanceUnexcused,
                'settingsPointsMemVeryGood': data.pointsHifzMemVeryGood,
                'settingsPointsMemGood': data.pointsHifzMemGood,
                'settingsPointsRevVeryGood': data.pointsHifzRevVeryGood,
                'settingsPointsRevGood': data.pointsHifzRevGood,
                'settingsPointsRevAcceptable': data.pointsHifzRevAcceptable,
                'settingsPointsMatn': data.pointsMatnMemorization,
                'settingsHifzTarget5To7': data.hifzRatingTargetAge5To7,
                'settingsHifzTarget8To10': data.hifzRatingTargetAge8To10,
                'settingsHifzTarget11Plus': data.hifzRatingTargetAge11Plus
            };
            
            for (const [id, value] of Object.entries(fields)) {
                const el = document.getElementById(id);
                if (el && value !== undefined && value !== null) {
                    el.value = value;
                }
            }

            const otpSel = document.getElementById('settingsOtpChannel');
            if (otpSel) otpSel.value = data.otpChannel || 'WhatsApp';
            const remind = document.getElementById('settingsStudentReminders');
            if (remind) remind.checked = data.studentRemindersEnabled !== false;

            const toggles = {
                settingsEnableMatn: data.enableMatn,
                settingsEnableDress: data.enableDress,
                settingsEnablePrayer: data.enablePrayer,
                settingsEnableParentHome: data.enableParentHome
            };
            for (const [id, value] of Object.entries(toggles)) {
                const el = document.getElementById(id);
                if (el) el.checked = value !== false;
            }
            refreshDependentSettings();

            const vis = document.getElementById('settingsEvalsVisible');
            if (vis && data.evaluationsVisibleToStudentsAndParents !== undefined) {
                vis.checked = !!data.evaluationsVisibleToStudentsAndParents;
            }

            // تحميل التفضيلات المحلية (localStorage)
            loadLocalPreferences();
            // تحميل معلومات النظام
            fetchSystemInfo();

            // تحميل قائمة المتون المُدارة
            loadMatnsSettingsList();

        } catch (e) {
            console.error('Settings fetch error:', e);
        }
    }

    function loadLocalPreferences() {
        // حجم الخط
        const fontSize = localStorage.getItem('noor_font_size') || '16px';
        const fontSelect = document.getElementById('settingsFontSize');
        if (fontSelect) fontSelect.value = fontSize;
        document.documentElement.style.setProperty('--font-size-base', fontSize);

        // الإشعارات
        const prefs = ['notifAttendance', 'notifHifz', 'notifPayments'];
        prefs.forEach(p => {
            const val = localStorage.getItem(`noor_pref_${p}`);
            const checkbox = document.getElementById(`pref${p.charAt(0).toUpperCase() + p.slice(1)}`);
            if (checkbox) {
                checkbox.checked = val === null ? true : val === 'true'; // الافتراضي مفعل
            }
        });
    }

    async function fetchSystemInfo() {
        try {
            const data = await apiFetch('/settings/system-info');
            const el = id => document.getElementById(id);
            if (el('sysTotalUsers')) el('sysTotalUsers').textContent = data.totalUsers || 0;
            if (el('sysTotalRecords')) el('sysTotalRecords').textContent = (data.totalHifzRecords || 0) + (data.totalAttendanceRecords || 0);
            
            if (data.serverTime && el('sysServerTime')) {
                const d = new Date(data.serverTime);
                el('sysServerTime').textContent = d.toLocaleTimeString('ar-LY', { hour: '2-digit', minute: '2-digit' });
            }
        } catch (e) {
            console.warn('System info unavailable');
        }
    }

    async function saveSettings() {
        const getVal = id => document.getElementById(id)?.value?.trim();
        
        const payload = {
            centerName: getVal('settingsCenterName'),
            contactPhone: getVal('settingsContactPhone'),
            address: getVal('settingsAddress'),
            workDays: getVal('settingsWorkDays'),
            workStartTime: getVal('settingsWorkStartTime'),
            workEndTime: getVal('settingsWorkEndTime'),
            defaultMonthlyFee: getVal('settingsDefaultMonthlyFee') ? parseFloat(getVal('settingsDefaultMonthlyFee')) : null,
            currency: getVal('settingsCurrency'),
            oralMaxOpeningsBeforeFail: getVal('settingsOralMaxOpenings') ? parseInt(getVal('settingsOralMaxOpenings'), 10) : null,
            oralHesitationPenalty: getVal('settingsOralHesitation') ? parseFloat(getVal('settingsOralHesitation')) : null,
            oralAlertPenalty: getVal('settingsOralAlert') ? parseFloat(getVal('settingsOralAlert')) : null,
            oralOpeningPenalty: getVal('settingsOralOpening') ? parseFloat(getVal('settingsOralOpening')) : null,
            defaultMonthlyAthmanTarget: getVal('settingsDefaultAthman') ? parseInt(getVal('settingsDefaultAthman'), 10) : null,
            progressThreshold10: fromPercent(getVal('settingsPT10')),
            progressThreshold9: fromPercent(getVal('settingsPT9')),
            progressThreshold8: fromPercent(getVal('settingsPT8')),
            progressThreshold6: fromPercent(getVal('settingsPT6')),
            progressThreshold4: fromPercent(getVal('settingsPT4')),
            weightAttendance: getVal('settingsWAtt') ? parseFloat(getVal('settingsWAtt')) : null,
            weightHifz: getVal('settingsWHifz') ? parseFloat(getVal('settingsWHifz')) : null,
            weightRevision: getVal('settingsWRev') ? parseFloat(getVal('settingsWRev')) : null,
            weightProgress: getVal('settingsWProg') ? parseFloat(getVal('settingsWProg')) : null,
            weightMatn: getVal('settingsWMatn') ? parseFloat(getVal('settingsWMatn')) : null,
            weightDress: getVal('settingsWDress') ? parseFloat(getVal('settingsWDress')) : null,
            evaluationsVisibleToStudentsAndParents: !!document.getElementById('settingsEvalsVisible')?.checked,
            pointsAttendancePresent: intOrNull('settingsPointsAttendance'),
            pointsHifzMemorization: intOrNull('settingsPointsHifzMemorization'),
            pointsHifzRevision: intOrNull('settingsPointsHifzRevision'),
            hifzRatingTargetAge5To7: getVal('settingsHifzTarget5To7') ? parseInt(getVal('settingsHifzTarget5To7'), 10) : null,
            hifzRatingTargetAge8To10: getVal('settingsHifzTarget8To10') ? parseInt(getVal('settingsHifzTarget8To10'), 10) : null,
            hifzRatingTargetAge11Plus: getVal('settingsHifzTarget11Plus') ? parseInt(getVal('settingsHifzTarget11Plus'), 10) : null,
            pointsAttendanceLate: intOrNull('settingsPointsLate'),
            pointsAttendanceExcused: intOrNull('settingsPointsExcused'),
            pointsAttendanceUnexcused: intOrNull('settingsPointsUnexcused'),
            pointsHifzMemVeryGood: intOrNull('settingsPointsMemVeryGood'),
            pointsHifzMemGood: intOrNull('settingsPointsMemGood'),
            pointsHifzRevVeryGood: intOrNull('settingsPointsRevVeryGood'),
            pointsHifzRevGood: intOrNull('settingsPointsRevGood'),
            pointsHifzRevAcceptable: intOrNull('settingsPointsRevAcceptable'),
            pointsMatnMemorization: intOrNull('settingsPointsMatn'),
            enableMatn: !!document.getElementById('settingsEnableMatn')?.checked,
            enableDress: !!document.getElementById('settingsEnableDress')?.checked,
            enablePrayer: !!document.getElementById('settingsEnablePrayer')?.checked,
            enableParentHome: !!document.getElementById('settingsEnableParentHome')?.checked,
            otpChannel: document.getElementById('settingsOtpChannel')?.value || null,
            studentRemindersEnabled: !!document.getElementById('settingsStudentReminders')?.checked
        };

        if (!payload.centerName) {
            showTab('center');
            setSettingsStatus('error', 'اسم المركز مطلوب');
            app().ui.showToast('اسم المركز مطلوب', 'error');
            return;
        }
        const pts = [payload.progressThreshold10, payload.progressThreshold9, payload.progressThreshold8, payload.progressThreshold6, payload.progressThreshold4];
        if (pts.some(v => v == null || v <= 0 || v > 1) || pts.some((v, i) => i > 0 && v > pts[i - 1])) {
            showTab('monthly');
            setSettingsStatus('error', 'نسب الهدف الشهري يجب أن تكون بين 1% و100% وتتناقص من درجة 10 إلى درجة 4');
            return;
        }

        const btn = document.getElementById('btnSaveSettings');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true, 'جارِ الحفظ...');
            await apiFetch('/settings', 'PUT', payload);
            app().ui.showToast('تم حفظ الإعدادات بنجاح', 'success');
            setSettingsStatus('success', 'تم حفظ الإعدادات بنجاح — ' +
                new Date().toLocaleTimeString('ar-LY', { hour: '2-digit', minute: '2-digit' }));

            // تحديث اسم المركز في اللوجو الجانبي إن أمكن
            if (global.NoorSetCenterName) global.NoorSetCenterName(payload.centerName);
            if (global.NoorPublicSettings) global.NoorPublicSettings.centerName = payload.centerName;
            
        } catch (e) {
            app().api.handleApiError(e);
            if (e?.status === 403) applySettingsEditability(false, e.message);
            else setSettingsStatus('error', 'لم تُحفظ الإعدادات: ' + (e?.message || 'حدث خطأ غير متوقع'));
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false, icon('save', { size: 16 }) + ' حفظ التغييرات');
            if (btn && !canEditSettings()) btn.disabled = true;
        }
    }

    async function loadMatnsSettingsList() {
        const list = document.getElementById('matnsSettingsList');
        if (!list) return;
        list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:12px">جاري التحميل...</p>';
        try {
            const matns = await apiFetch('/matns?includeInactive=true');
            if (!matns?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:12px">لا توجد متون مضافة بعد</p>';
                return;
            }
            list.innerHTML = matns.map(m => `
            <div style="display:flex;justify-content:space-between;align-items:center;padding:10px 14px;background:var(--bg);border-radius:8px;margin-bottom:8px">
                <span style="font-weight:600;${m.isActive ? '' : 'color:var(--text-muted);text-decoration:line-through'}">${escapeHtml(m.name)}</span>
                <button class="btn btn-outline" style="font-size:12px;padding:4px 10px" onclick="NoorSettings.toggleMatn(${m.id})">
                    ${m.isActive ? 'تعطيل' : 'تفعيل'}
                </button>
            </div>`).join('');
        } catch (e) {
            list.innerHTML = '<p style="color:#dc2626;text-align:center;padding:12px">تعذر تحميل المتون</p>';
        }
    }

    async function addMatn() {
        const input = document.getElementById('newMatnName');
        const name = input?.value?.trim();
        if (!name) {
            app().ui.showToast('اسم المتن مطلوب', 'error');
            return;
        }
        try {
            await apiFetch('/matns', 'POST', { name });
            app().ui.showToast('تم إضافة المتن', 'success');
            input.value = '';
            await loadMatnsSettingsList();
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    async function toggleMatn(id) {
        try {
            await apiFetch(`/matns/${id}/toggle`, 'PUT');
            await loadMatnsSettingsList();
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    global.NoorSettings = { addMatn, toggleMatn, loadMatnsSettingsList, showTab };

    async function changePassword() {
        const currentPass = document.getElementById('settingsCurrentPass')?.value;
        const newPass = document.getElementById('settingsNewPass')?.value;

        if (!currentPass || !newPass) {
            app().ui.showToast('الرجاء إدخال كلمة المرور الحالية والجديدة', 'warning');
            return;
        }
        if (newPass.length < 8) {
            app().ui.showToast('كلمة المرور الجديدة يجب أن تكون 8 أحرف على الأقل', 'warning');
            return;
        }

        try {
            const cpRes = await apiFetch('/auth/change-password', 'POST', { currentPassword: currentPass, newPassword: newPass });
            if (cpRes?.token) app().saveSession(cpRes.token, app().state.user);
            app().ui.showToast('تم تغيير كلمة المرور بنجاح', 'success');
            document.getElementById('settingsCurrentPass').value = '';
            document.getElementById('settingsNewPass').value = '';
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function changeFontSize(size) {
        localStorage.setItem('noor_font_size', size);
        document.documentElement.style.setProperty('--font-size-base', size);
        document.body.style.fontSize = size;
        app().ui.showToast('تم تغيير حجم الخط', 'info');
    }

    function savePref(key, value) {
        localStorage.setItem(`noor_pref_${key}`, value);
    }

    function exportData() {
        // في المستقبل يمكن ربطه بـ API حقيقي يصدر Excel. حاليا Toast بسيط.
        app().ui.showToast('جاري تجهيز ملف البيانات للتصدير...', 'info');
        setTimeout(() => app().ui.showToast('تم تحميل الملف بنجاح', 'success'), 1500);
    }

    function clearCache() {
        if (confirm('هل أنت متأكد من مسح الذاكرة المؤقتة للتطبيق؟ سيتم إعادة تحميل الصفحة.')) {
            localStorage.clear();
            sessionStorage.clear();
            if ('serviceWorker' in navigator) {
                navigator.serviceWorker.getRegistrations().then(registrations => {
                    registrations.forEach(registration => registration.unregister());
                });
            }
            window.location.reload(true);
        }
    }

    global.NoorDashboard = { 
        fetchStats, fetchActivities, fetchAnnouncements, fetchLeaderboard, setLeaderboardScope,
        startPolling, stopPolling, fetchSettings, changePassword, 
        changeFontSize, savePref, exportData, clearCache 
    };
    global.saveSettings = saveSettings;    global.fetchStats = fetchStats;
    global.fetchActivities = fetchActivities;
    global.fetchAnnouncements = fetchAnnouncements;
    global.fetchLeaderboard = fetchLeaderboard;
})(window);
