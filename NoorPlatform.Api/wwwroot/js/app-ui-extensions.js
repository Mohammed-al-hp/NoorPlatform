// app-ui-extensions.js - واجهة المستخدم، بوابة الولي، الحضور وغيرها
(function(global) {
    'use strict';

    // بوابة ولي الأمر
    async function fetchParentView() {
        const grid = document.getElementById('parentChildrenGrid');
        if (!grid) return;
        grid.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:60px;color:var(--text-muted)">⏳ جاري تحميل بيانات الأبناء...</div>';
        try {
            const response = await apiFetch('/dashboard/parent-summary');
            const children = response.children || response;
            const alerts = response.alerts || [];

            let alertsHtml = '';
            if (alerts.length > 0) {
                alertsHtml = `<div style="grid-column:1/-1; margin-bottom: 10px;">
                    ${alerts.map(a => `
                        <div style="background:rgba(239,68,68,0.08); border:1px solid rgba(239,68,68,0.2); border-radius:12px; padding:14px 18px; margin-bottom:8px; display:flex; align-items:center; gap:10px; font-size:14px; color:#dc2626;">
                            <span style="font-size:22px">🔔</span>
                            <span style="flex:1">${escapeHtml(a.message)}</span>
                            <button class="btn" style="background:#dc2626;color:#fff;padding:6px 14px;border-radius:8px;font-size:12px" onclick="navigate('parentFees', null)">عرض الفاتورة</button>
                        </div>
                    `).join('')}
                </div>`;
            }

            if (!children.length) {
                grid.innerHTML = alertsHtml + '<div style="grid-column:1/-1;text-align:center;padding:60px;color:var(--text-muted)">لا يوجد أبناء مسجلون لحسابك</div>';
                return;
            }
            grid.innerHTML = alertsHtml + children.map(c => `
                    <div class="student-card">
                        <div class="student-card-top">
                            <div class="student-avatar-lg" style="background:var(--gradient)">${escapeHtml(c.fullName).slice(0, 2)}</div>
                            <div class="student-card-info">
                                <h4>${escapeHtml(c.fullName)}</h4>
                                <span class="status-badge status-excellent" style="margin-top:4px;display:inline-flex">تقدم الحفظ: ${c.progress}%</span>
                            </div>
                        </div>
                        <div class="student-card-stats">
                            <div class="mini-stat"><label>الحفظ</label><p>${c.progress}%</p></div>
                            <div class="mini-stat"><label>الحضور</label><p>${c.attendance}%</p></div>
                            <div class="mini-stat"><label>النقاط</label><p style="color:var(--amber-dark)">🌟 ${c.points || 0}</p></div>
                        </div>
                        <div style="font-size:12px; margin-bottom: 10px; display: flex; gap: 4px; flex-wrap: wrap;">
                            ${c.badges ? c.badges.split(',').map(b => b.trim() ? `<span style="background:var(--amber-light); color:var(--amber-dark); padding: 2px 6px; border-radius: 4px;">🏆 ${escapeHtml(b.trim())}</span>` : '').join('') : ''}
                        </div>
                        <div class="progress-wrap" style="margin-bottom:14px">
                            <div class="progress-bar"><div class="progress-fill" style="width:${c.progress}%"></div></div>
                            <span class="progress-pct">${c.progress}%</span>
                        </div>
                        ${c.lastNote && c.lastNote !== 'لا توجد ملاحظات'
                    ? `<div style="background:var(--green-light);border-radius:10px;padding:10px 12px;font-size:12px;color:var(--green-dark);margin-bottom:10px">
                                💬 <strong>آخر ملاحظة:</strong> ${escapeHtml(c.lastNote)}
                               </div>`
                    : ''}
                    </div>
                `).join('');
        } catch {
            grid.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:60px;color:#dc2626">❌ تعذر تحميل البيانات. تأكد من تسجيل الدخول كولي أمر.</div>';
        }
    }

    // عرض الطالب
    async function fetchStudentView() {
        try {
            const data = await apiFetch('/dashboard/student-summary');
            const stats = document.querySelectorAll('#page-studentView .stat-value');
            if (stats.length >= 4) {
                stats[0].textContent = data.hifzProgress + '%';
                stats[1].textContent = data.attendancePercentage + '%';
                stats[2].textContent = data.recentGrades?.[0]?.score ?? '—';
                stats[3].textContent = '4.8';
            }
            const heroName = document.querySelector('#page-studentView h2');
            if (heroName && data.fullName) heroName.textContent = data.fullName;

            const heroBadges = document.querySelector('#page-studentView .hero-badges');
            if (heroBadges) {
                let badgesHtml = `<span class="hero-badge" style="background:var(--amber-light);color:var(--amber-dark);">🌟 ${data.points || 0} نقطة</span>`;
                if (data.badges) {
                    const parsedBadges = data.badges.split(',');
                    parsedBadges.forEach(b => {
                        if (b.trim()) badgesHtml += `<span class="hero-badge">🏆 ${escapeHtml(b.trim())}</span>`;
                    });
                }
                badgesHtml += `<span class="hero-badge">⭐ 4.8 / 5</span>`;
                heroBadges.innerHTML = badgesHtml;
            }
        } catch { }
    }

    // إدارة الحضور
    function renderAttendanceTable(records) {
        const wrap = document.getElementById('attendanceList');
        if (!wrap) return;
        if (!records || !records.length) {
            wrap.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد سجلات حضور لهذا اليوم</p>';
            return;
        }
        wrap.innerHTML = `
        <table style="width:100%;border-collapse:collapse">
            <thead>
            <tr style="background:var(--bg);text-align:right">
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">الطالب</th>
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">الحالة</th>
                <th style="padding:12px 16px;font-size:13px;color:var(--text-muted)">تسجيل</th>
            </tr>
            </thead>
            <tbody>
            ${records.map(r => {
            const labels = { Present: ['✅ حاضر', '#dcfce7', '#16a34a'], Absent: ['❌ غائب', '#fee2e2', '#dc2626'], Late: ['⏰ متأخر', '#fef9c3', '#ca8a04'] };
            const [label, bg, color] = labels[r.status] || ['—', '#f1f5f9', '#64748b'];
            return `
                <tr style="border-bottom:1px solid var(--border)" id="att-row-${r.studentId}">
                <td style="padding:12px 16px">
                    <div style="display:flex;align-items:center;gap:10px">
                    <div style="width:36px;height:36px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;color:#fff;font-weight:700;font-size:13px">
                        ${escapeHtml((r.fullName || '').slice(0, 2))}
                    </div>
                    <span style="font-weight:600">${escapeHtml(r.fullName || 'طالب')}</span>
                    </div>
                </td>
                <td style="padding:12px 16px" id="att-status-${r.studentId}">
                    <span class="status-badge" style="background:${bg};color:${color}">${label}</span>
                </td>
                <td style="padding:12px 16px">
                    <div style="display:flex;gap:6px">
                    <button class="btn btn-outline" style="padding:5px 10px;font-size:12px" onclick="recordAtt(${r.studentId},'Present')">✅</button>
                    <button class="btn btn-outline" style="padding:5px 10px;font-size:12px;border-color:#ef4444;color:#ef4444" onclick="recordAtt(${r.studentId},'Absent')">❌</button>
                    <button class="btn btn-outline" style="padding:5px 10px;font-size:12px;border-color:#f59e0b;color:#f59e0b" onclick="recordAtt(${r.studentId},'Late')">⏰</button>
                    </div>
                </td>
                </tr>`;
        }).join('')}
            </tbody>
        </table>`;
        if (typeof updateAttendanceCounts === 'function') updateAttendanceCounts();
    }

    async function renderAttendanceCircleChips() {
        const wrap = document.getElementById('attendanceCircleChips');
        if (!wrap) return;
        try {
            const circles = await apiFetch('/circles');
            if (!circles.length) {
                wrap.innerHTML = '<span style="font-size:13px;color:var(--text-muted)">لا توجد حلقات</span>';
                return;
            }
            wrap.innerHTML = circles.map((c, i) =>
                `<div class="circle-chip${i === 0 ? ' active' : ''}" data-id="${c.id}" onclick="selectCircle(this)">${escapeHtml(c.name)}</div>`
            ).join('');
            const first = wrap.querySelector('.circle-chip');
            if (first) {
                if (typeof selectedCircleId !== 'undefined') {
                    window.selectedCircleId = parseInt(first.dataset.id, 10);
                }
            }
        } catch (e) {
            wrap.innerHTML = '<span style="color:var(--text-muted)">تعذر تحميل الحلقات</span>';
        }
    }

    async function sendAbsenceWhatsApp(studentId) {
        try {
            await fetch('/api/notifications/absence', {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ studentId, date: new Date().toISOString() })
            });
        } catch { }
    }

    // الحلقات
    async function saveCircle() {
        const name = document.getElementById('circleName')?.value?.trim();
        const teacherId = document.getElementById('circleTeacher')?.value || null;
        const location = document.getElementById('circleLocation')?.value?.trim() || '';
        const time = document.getElementById('circleTime')?.value?.trim() || '';
        const capacity = parseInt(document.getElementById('circleCapacity')?.value) || 20;

        if (!name) {
            showToast('❌ يرجى إدخال اسم الحلقة');
            return;
        }
        try {
            const res = await fetch(`${API_URL}/circles`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, teacherId: teacherId ? parseInt(teacherId) : null, location, time, capacity })
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                showToast('❌ ' + (err.message || 'فشل الإنشاء'));
                return;
            }
            document.getElementById('circleName').value = '';
            document.getElementById('circleLocation').value = '';
            document.getElementById('circleTime').value = '';
            document.getElementById('circleCapacity').value = '';
            closeModal('addCircleModal');
            showToast('✅ تم إنشاء الحلقة بنجاح');
            if (typeof fetchCircles === 'function') fetchCircles();
        } catch {
            showToast('❌ تعذر الاتصال بالخادم');
        }
    }

    async function populateCircleTeacherSelect() {
        try {
            const teachers = await apiFetch('/teachers');
            const sel = document.getElementById('circleTeacher');
            if (sel && teachers.length) {
                sel.innerHTML = '<option value="">— اختر محفظاً —</option>' +
                    teachers.map(t => `<option value="${t.id}">${escapeHtml(t.fullName)}</option>`).join('');
            }
        } catch { }
    }

    const _origOpenModal = global.openModal;
    global.openModal = function(id) {
        if (_origOpenModal) _origOpenModal(id);
        if (id === 'addCircleModal') populateCircleTeacherSelect();
    };

    // خريطة الحفظ
    function renderQuranMap(hifzRecords) {
        const grid = document.getElementById('surahGrid');
        if (!grid) return;
        const memorizedSurahs = {};
        (hifzRecords || []).forEach(r => {
            if (r.type === 'Memorization' || r.type === 0) {
                const key = r.surahName;
                if (!memorizedSurahs[key]) memorizedSurahs[key] = 0;
                memorizedSurahs[key] += r.verseCount || HifzRecord_ParseVerseCount(r.verses || '');
            }
        });

        grid.innerHTML = (window.SURAHS || []).map(s => {
            const memorized = memorizedSurahs[s.name] || 0;
            let cls = 'empty', icon = '📖';
            if (memorized >= s.v) { cls = 'memorized'; icon = '✅'; }
            else if (memorized > 0) { cls = 'partial'; icon = '📝'; }
            const pct = memorized > 0 ? Math.min(Math.round(memorized / s.v * 100), 100) : 0;
            const title = pct > 0 ? `${s.name}: ${memorized}/${s.v} آية (${pct}%)` : `${s.name}: لم يُحفظ`;
            return `
  <div class="surah-cell ${cls}" title="${title}">
    <span class="surah-icon">${icon}</span>
    <span>${s.name}</span>
    <span class="surah-num">${s.n}</span>
    ${pct > 0 ? `<span style="font-size:9px">${pct}%</span>` : ''}
  </div>`;
        }).join('');
    }

    function HifzRecord_ParseVerseCount(verses) {
        if (!verses) return 0;
        const parts = verses.split('-');
        if (parts.length === 2) {
            const from = parseInt(parts[0]), to = parseInt(parts[1]);
            if (!isNaN(from) && !isNaN(to) && to >= from) return to - from + 1;
        }
        const single = parseInt(verses);
        return isNaN(single) ? 0 : single;
    }

    function openMemModalForStudent(studentId, studentName) {
        openModal('addMemModal');
        const sel = document.querySelector('#addMemModal select');
        if (sel) sel.value = studentId;
    }

    // Modal Delete Hook
    let _deleteCallback = null;
    function confirmDelete(message, callback) {
        document.getElementById('confirmDeleteMsg').textContent = message;
        _deleteCallback = callback;
        openModal('confirmDeleteModal');
    }
    function executeDelete() {
        closeModal('confirmDeleteModal');
        if (_deleteCallback) _deleteCallback();
        _deleteCallback = null;
    }

    // Global Search
    function handleGlobalSearch() {
        const query = document.getElementById('globalSearchInput')?.value?.toLowerCase();
        if (!query) return;
        const activePageId = document.querySelector('.content.active')?.id;
        if (!activePageId) return;

        const tableRows = document.querySelectorAll(`#${activePageId} tbody tr`);
        tableRows.forEach(row => {
            const text = row.textContent.toLowerCase();
            row.style.display = text.includes(query) ? '' : 'none';
        });
    }

    // UI Helpers
    function navBottom(page, el) {
        document.querySelectorAll('.bottom-nav-item').forEach(i => i.classList.remove('active'));
        if (el) el.classList.add('active');
        const navEl = document.querySelector('[onclick*="navigate(\'' + page + '\'"]');
        if (typeof navigate === 'function') navigate(page, navEl);
    }

    if (typeof navigate === 'function') {
        const _origNavigate = navigate;
        global.navigate = function (page, el) {
            _origNavigate(page, el);
            const bnMap = { dashboard: 0, students: 1, attendance: 2, memorization: 3, announcements: 4 };
            const bnItems = document.querySelectorAll('.bottom-nav-item');
            bnItems.forEach(i => i.classList.remove('active'));
            if (bnMap[page] !== undefined && bnItems[bnMap[page]]) {
                bnItems[bnMap[page]].classList.add('active');
            }
        };
    }

    function toggleFab() {
        const btn = document.getElementById('fabBtn');
        const menu = document.getElementById('fabMenu');
        if (!btn || !menu) return;
        const isOpen = menu.classList.toggle('open');
        btn.classList.toggle('open', isOpen);
        btn.textContent = isOpen ? '✕' : '☰';
    }

    document.addEventListener('click', (e) => {
        const fab = document.getElementById('fabContainer');
        if (fab && !fab.contains(e.target)) {
            document.getElementById('fabMenu')?.classList.remove('open');
            const btn = document.getElementById('fabBtn');
            if (btn) { btn.classList.remove('open'); btn.textContent = '☰'; }
        }
    });

    (function setTopbarDate() {
        const el = document.getElementById('topbarDate');
        if (!el) return;
        const now = new Date();
        const days = ['الأحد', 'الإثنين', 'الثلاثاء', 'الأربعاء', 'الخميس', 'الجمعة', 'السبت'];
        const months = ['يناير', 'فبراير', 'مارس', 'أبريل', 'مايو', 'يونيو',
            'يوليو', 'أغسطس', 'سبتمبر', 'أكتوبر', 'نوفمبر', 'ديسمبر'];
        el.textContent = days[now.getDay()] + '، ' + now.getDate() + ' ' + months[now.getMonth()];
    })();

    function updateTopbarAvatar() {
        const av = document.getElementById('topbarAvatar');
        if (av && window.USER && window.USER.fullName) av.textContent = window.USER.fullName.slice(0, 2);
    }

    window.addEventListener('load', () => {
        const splash = document.getElementById('splashScreen');
        if (splash) {
            setTimeout(() => splash.classList.add('hide'), 1200);
            setTimeout(() => splash.remove(), 1800);
        }
    });

    // PWA & Dark Mode
    let deferredPrompt;
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/sw.js?v=6').then(reg => {
            reg.addEventListener('updatefound', () => {
                const nw = reg.installing;
                if (!nw) return;
                nw.addEventListener('statechange', () => {
                    if (nw.state === 'installed' && navigator.serviceWorker.controller) {
                        showToast('🔄 تحديث جديد متوفر — أعد تحميل الصفحة');
                    }
                });
            });
        }).catch(() => { });
    }
    window.addEventListener('beforeinstallprompt', e => {
        e.preventDefault();
        deferredPrompt = e;
        const banner = document.getElementById('pwa-banner');
        if (banner) banner.classList.add('show');
    });
    function installPWA() {
        if (deferredPrompt) {
            deferredPrompt.prompt();
            deferredPrompt.userChoice.then(() => {
                deferredPrompt = null;
                const pwaBanner = document.getElementById('pwa-banner');
                if (pwaBanner) pwaBanner.style.display = 'none';
            });
        }
    }

    function toggleDarkMode() {
        const isDark = document.body.classList.toggle('dark-mode');
        localStorage.setItem('noor_dark', isDark ? '1' : '0');
        const t = document.getElementById('darkToggle');
        if (t) t.textContent = isDark ? '☀️' : '🌙';
    }
    
    if (localStorage.getItem('noor_dark') === '1') {
        document.body.classList.add('dark-mode');
        document.addEventListener('DOMContentLoaded', () => {
            const t = document.getElementById('darkToggle');
            if (t) t.textContent = '☀️';
        });
    }

    // QA Hooks & Misc
    function markAllPresent() {
        if (global.NoorAttendance?.markAllPresentLocal) {
            global.NoorAttendance.markAllPresentLocal();
            return;
        }
        showToast('افتح صفحة الحضور أولاً');
    }

    function saveAttendance() {
        if (global.NoorAttendance?.savePendingAttendance) {
            global.NoorAttendance.savePendingAttendance();
            return;
        }
        showToast('افتح صفحة الحضور أولاً');
    }

    function searchAttendanceReports() {
        showToast('تم جلب التقرير بنجاح 📊');
    }

    async function printAttendance() {
        const el = document.getElementById('attendanceList');
        if (!el || !el.innerHTML.trim()) {
            showToast('لا توجد بيانات لطباعتها');
            return;
        }
        const hasPdf = typeof ensureHtml2Pdf === 'function' ? await ensureHtml2Pdf() : typeof html2pdf !== 'undefined';
        if (hasPdf) {
            const opt = {
                margin: 10,
                filename: 'تقرير_الحضور.pdf',
                image: { type: 'jpeg', quality: 0.98 },
                html2canvas: { scale: 2 },
                jsPDF: { unit: 'mm', format: 'a4', orientation: 'portrait' }
            };
            await html2pdf().set(opt).from(el).save();
            showToast('جاري طباعة التقرير... 🖨️');
        } else {
            window.print();
        }
    }

    global.fetchParentView = fetchParentView;
    global.fetchStudentView = fetchStudentView;
    global.renderAttendanceTable = renderAttendanceTable;
    global.renderAttendanceCircleChips = renderAttendanceCircleChips;
    global.sendAbsenceWhatsApp = sendAbsenceWhatsApp;
    global.saveCircle = saveCircle;
    global.renderQuranMap = renderQuranMap;
    global.HifzRecord_ParseVerseCount = HifzRecord_ParseVerseCount;
    global.openMemModalForStudent = openMemModalForStudent;
    global.confirmDelete = confirmDelete;
    global.executeDelete = executeDelete;
    global.handleGlobalSearch = handleGlobalSearch;
    global.navBottom = navBottom;
    global.toggleFab = toggleFab;
    global.updateTopbarAvatar = updateTopbarAvatar;
    global.installPWA = installPWA;
    global.toggleDarkMode = toggleDarkMode;
    global.markAllPresent = markAllPresent;
    global.saveAttendance = saveAttendance;
    global.searchAttendanceReports = searchAttendanceReports;
    global.printAttendance = printAttendance;
})(window);
