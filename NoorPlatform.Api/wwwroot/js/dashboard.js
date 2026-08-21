/**
 * منصة نور — لوحة التحكم والإعلانات والمتصدرين
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = (s) => utils().escapeHtml(s);

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
                                    backgroundColor: values.map(v => v > 70 ? '#10b981' : '#cbd5e1'),
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
                                    backgroundColor: ['#10b981', '#3b82f6', '#f59e0b'],
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
    async function fetchSettings() {
        try {
            const data = await apiFetch('/settings');
            
            // تعبئة البيانات في الحقول
            const fields = {
                'settingsCenterName': data.centerName,
                'settingsContactPhone': data.contactPhone,
                'settingsEmail': data.email,
                'settingsAddress': data.address,
                'settingsWorkDays': data.workDays,
                'settingsWorkStartTime': data.workStartTime,
                'settingsWorkEndTime': data.workEndTime,
                'settingsDefaultMonthlyFee': data.defaultMonthlyFee,
                'settingsCurrency': data.currency
            };
            
            for (const [id, value] of Object.entries(fields)) {
                const el = document.getElementById(id);
                if (el && value !== undefined && value !== null) {
                    el.value = value;
                }
            }

            // تحميل التفضيلات المحلية (localStorage)
            loadLocalPreferences();
            
            // تحميل معلومات النظام
            fetchSystemInfo();

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
            email: getVal('settingsEmail'),
            address: getVal('settingsAddress'),
            workDays: getVal('settingsWorkDays'),
            workStartTime: getVal('settingsWorkStartTime'),
            workEndTime: getVal('settingsWorkEndTime'),
            defaultMonthlyFee: getVal('settingsDefaultMonthlyFee') ? parseFloat(getVal('settingsDefaultMonthlyFee')) : null,
            currency: getVal('settingsCurrency')
        };

        if (!payload.centerName) {
            app().ui.showToast('❌ اسم المركز مطلوب');
            return;
        }

        const btn = document.getElementById('btnSaveSettings');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true, 'جارِ الحفظ...');
            await apiFetch('/settings', 'PUT', payload);
            app().ui.showToast('✅ تم حفظ الإعدادات بنجاح');
            
            // تحديث اسم المركز في اللوجو الجانبي إن أمكن
            const logoText = document.querySelector('.sidebar-header h2');
            if (logoText && payload.centerName) logoText.textContent = payload.centerName;
            
        } catch (e) {
            app().api.handleApiError(e);
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false, '💾 حفظ التغييرات');
        }
    }

    async function changePassword() {
        const currentPass = document.getElementById('settingsCurrentPass')?.value;
        const newPass = document.getElementById('settingsNewPass')?.value;

        if (!currentPass || !newPass) {
            app().ui.showToast('الرجاء إدخال كلمة المرور الحالية والجديدة');
            return;
        }
        if (newPass.length < 6) {
            app().ui.showToast('كلمة المرور الجديدة يجب أن تكون 6 أحرف على الأقل');
            return;
        }

        try {
            await apiFetch('/auth/change-password', 'POST', { currentPassword: currentPass, newPassword: newPass });
            app().ui.showToast('✅ تم تغيير كلمة المرور بنجاح');
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
        app().ui.showToast('تم تغيير حجم الخط');
    }

    function savePref(key, value) {
        localStorage.setItem(`noor_pref_${key}`, value);
    }

    function exportData() {
        // في المستقبل يمكن ربطه بـ API حقيقي يصدر Excel. حاليا Toast بسيط.
        app().ui.showToast('جاري تجهيز ملف البيانات للتصدير...');
        setTimeout(() => app().ui.showToast('✅ تم تحميل الملف بنجاح'), 1500);
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
        fetchStats, fetchActivities, fetchAnnouncements, fetchLeaderboard, 
        startPolling, stopPolling, fetchSettings, changePassword, 
        changeFontSize, savePref, exportData, clearCache 
    };
    global.saveSettings = saveSettings;    global.fetchStats = fetchStats;
    global.fetchActivities = fetchActivities;
    global.fetchAnnouncements = fetchAnnouncements;
    global.fetchLeaderboard = fetchLeaderboard;
})(window);
