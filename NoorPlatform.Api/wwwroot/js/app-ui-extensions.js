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
                    <button class="btn btn-outline" style="width:100%;font-size:12px;padding:8px" onclick="NoorMessages.openChildDetails(${c.id}, '${escapeHtml(c.fullName).replace(/'/g, "\\'")}')">
                        📋 عرض التفاصيل الكاملة
                    </button>
                </div>
            `).join('');
        } catch {
            grid.innerHTML = '<div style="grid-column:1/-1;text-align:center;padding:60px;color:#dc2626">❌ تعذر تحميل البيانات. تأكد من تسجيل الدخول كولي أمر.</div>';
        }
    }

    function parseStudentBadges(badges) {
        if (!badges || typeof badges !== 'string') return [];
        return badges.split(',').map(b => b.trim()).filter(Boolean);
    }

    function renderStudentGamification(data) {
        const weeklyGoal = data.weeklyGoal || 50;
        const weeklyVerses = data.weeklyVerses || 0;
        const pct = Math.min(100, Math.round((weeklyVerses / weeklyGoal) * 100));

        const weeklyProgress = document.getElementById('gamificationWeeklyProgress');
        if (weeklyProgress) weeklyProgress.textContent = `${weeklyVerses} / ${weeklyGoal}`;

        const progressBar = document.getElementById('gamificationProgressBar');
        if (progressBar) progressBar.style.width = pct + '%';

        const weeklyMsg = document.getElementById('gamificationWeeklyMessage');
        if (weeklyMsg) {
            if (weeklyVerses >= weeklyGoal) {
                weeklyMsg.textContent = 'أحسنت! لقد حققت هدفك الأسبوعي 🎉';
            } else if (weeklyVerses > 0) {
                weeklyMsg.textContent = `استمر يا بطل! تبقّى عليك ${weeklyGoal - weeklyVerses} آية للوصول للهدف.`;
            } else {
                weeklyMsg.textContent = 'ابدأ حفظك هذا الأسبوع — كل آية تقربك من هدفك.';
            }
        }

        const pointsEl = document.getElementById('gamificationPoints');
        if (pointsEl) pointsEl.textContent = data.points || 0;

        const badgesEl = document.getElementById('gamificationBadges');
        if (badgesEl) {
            const badges = parseStudentBadges(data.badges);
            badgesEl.innerHTML = badges.length
                ? badges.map(b => `<span class="hero-badge" style="background:var(--amber-light);color:var(--amber-dark);">🏆 ${escapeHtml(b)}</span>`).join('')
                : '<span style="font-size:12px;color:var(--text-muted)">لا توجد أوسمة بعد — واصل التميّز!</span>';
        }

        const reviewEl = document.getElementById('gamificationNextReview');
        if (reviewEl) {
            const review = data.nextReview || data.lastMemorization;
            if (review && review.surah) {
                const surahRange = review.toSurah && review.toSurah !== review.surah
                    ? `${escapeHtml(review.surah)} ← ${escapeHtml(review.toSurah)}`
                    : escapeHtml(review.surah);
                reviewEl.innerHTML = `
                    <div style="text-align:center;padding:8px 0">
                        <div style="font-size:28px;margin-bottom:8px">📖</div>
                        <div style="font-size:20px;font-weight:800;color:var(--green-dark);margin-bottom:6px">${surahRange}</div>
                        <div style="font-size:15px;font-weight:700;color:var(--text);margin-bottom:8px">الآيات: ${escapeHtml(review.verses || '—')}</div>
                        <div style="font-size:12px;color:var(--text-muted)">آخر حفظ: ${escapeHtml(review.date || '—')}${review.evaluation ? ' • ' + escapeHtml(review.evaluation) : ''}</div>
                        <p style="margin-top:14px;font-size:13px;color:var(--text-muted);line-height:1.6">هذا هو ورد مراجعتك القادم — راجع ما حفظته جيداً قبل الجلسة التالية.</p>
                    </div>`;
            } else {
                reviewEl.innerHTML = '<div style="text-align:center;color:var(--text-muted);padding:20px">لا يوجد حفظ مسجّل بعد. ابدأ أول تسميع ليظهر ورد المراجعة.</div>';
            }
        }

        const boardEl = document.getElementById('gamificationCircleLeaderboard');
        if (boardEl) {
            const top3 = data.circleTop3 || [];
            if (!top3.length) {
                boardEl.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)">لست منضماً لحلقة بعد، أو لا يوجد طلاب للمقارنة.</div>';
            } else {
                const medals = ['🥇', '🥈', '🥉'];
                boardEl.innerHTML = top3.map((s, i) => {
                    const highlight = s.isCurrentUser
                        ? 'background:var(--green-light);border:1px solid var(--green);'
                        : 'background:var(--bg);border:1px solid var(--border);';
                    const you = s.isCurrentUser ? ' <span style="font-size:11px;color:var(--green-dark)">(أنت)</span>' : '';
                    return `
                        <div style="display:flex;align-items:center;gap:12px;padding:12px 14px;border-radius:12px;${highlight}">
                            <span style="font-size:22px;width:32px;text-align:center">${medals[i] || (i + 1)}</span>
                            <div style="flex:1;min-width:0">
                                <div style="font-weight:800;font-size:14px">${escapeHtml(s.fullName || '—')}${you}</div>
                                <div style="font-size:12px;color:var(--text-muted)">${s.points || 0} نقطة</div>
                            </div>
                        </div>`;
                }).join('');
            }
        }
    }

    // عرض الطالب (الرئيسية)
    async function fetchStudentView() {
        try {
            const data = await apiFetch('/dashboard/student-summary');
            if (data.id) {
                window._studentDbId = data.id;
                fetchStudentFullHifzRecord(data.id);
            }

            const heroName = document.getElementById('studentHeroName');
            if (heroName) heroName.textContent = data.fullName || '—';

            const heroAvatar = document.getElementById('studentHeroAvatar');
            if (heroAvatar && data.fullName) heroAvatar.textContent = data.fullName.slice(0, 2);

            const heroCircle = document.getElementById('studentHeroCircle');
            if (heroCircle) heroCircle.textContent = `${data.circleName || 'بدون حلقة'} • المحفظ: ${data.teacherName || '—'} • حفظ ${data.hifzProgress || 0}% • حضور ${data.attendancePercentage || 0}%`;

            const heroBadges = document.getElementById('studentHeroBadges');
            if (heroBadges) {
                let badgesHtml = `<span class="hero-badge" style="background:var(--amber-light);color:var(--amber-dark);">🌟 ${data.points || 0} نقطة</span>`;
                parseStudentBadges(data.badges).forEach(b => {
                    badgesHtml += `<span class="hero-badge">🏆 ${escapeHtml(b)}</span>`;
                });
                if (data.teacherRating > 0) {
                    badgesHtml += `<span class="hero-badge">⭐ ${data.teacherRating.toFixed(1)} / 5</span>`;
                }
                heroBadges.innerHTML = badgesHtml;
            }
            
            renderStudentGamification(data);
            fetchStudentAttendance();

            const hifzTbody = document.querySelector('#studentHifzTable tbody');
            if (hifzTbody) {
                if (data.recentHifz && data.recentHifz.length) {
                    hifzTbody.innerHTML = data.recentHifz.map(r => {
                        const isExcellent = r.evaluation === 'ممتاز';
                        const isGood = r.evaluation === 'جيد';
                        const cls = isExcellent ? 'status-excellent' : (isGood ? 'status-good' : 'status-late');
                        return `
                            <tr>
                                <td>${r.date}</td>
                                <td>${escapeHtml(r.surahName || '')}</td>
                                <td>${escapeHtml(r.verses || '')}</td>
                                <td><span class="status-badge ${cls}">${escapeHtml(r.evaluation || 'جيد')}</span></td>
                            </tr>
                        `;
                    }).join('');
                } else {
                    hifzTbody.innerHTML = '<tr><td colspan="4" style="text-align:center;padding:20px;color:var(--text-muted)">لا توجد سجلات تسميع</td></tr>';
                }
            }

            const notesList = document.getElementById('studentNotesList');
            if (notesList) {
                if (data.teacherNotes && data.teacherNotes.length) {
                    notesList.innerHTML = data.teacherNotes.map((n, i) => {
                        const bg = i === 0 ? 'var(--green-light)' : 'var(--bg)';
                        const border = i === 0 ? 'none' : '1px solid var(--border)';
                        const color = i === 0 ? 'var(--green-dark)' : 'var(--text-muted)';
                        return `
                        <div style="background:${bg};border:${border};border-radius:12px;padding:12px 14px">
                            <p style="font-size:12px;font-weight:700;color:${color};margin-bottom:4px">
                                ${escapeHtml(n.teacherName)} • ${n.date}
                            </p>
                            <p style="font-size:13px;color:var(--text)">
                                ${escapeHtml(n.notes)}
                            </p>
                        </div>
                        `;
                    }).join('');
                } else {
                    notesList.innerHTML = '<p style="text-align:center;color:var(--text-muted);font-size:13px;">لا توجد رسائل من المحفظ</p>';
                }
            }
        } catch (err) {
            console.error('Error fetching student view:', err);
            if (typeof showToast === 'function') showToast('❌ تعذر تحميل بيانات الطالب');
        }
    }
    async function fetchStudentFullHifzRecord(studentId) {
        const tbody = document.getElementById('studentFullHifzTable');
        if (!tbody) return;
        try {
            const records = await apiFetch(`/hifz/student/${studentId}`);
            if (!records.length) {
                tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;padding:20px;color:var(--text-muted)">لا توجد سجلات تسميع بعد</td></tr>';
                return;
            }
            tbody.innerHTML = records.map(r => {
                const typeLabel = r.type === 'Memorization' ? 'حفظ جديد' : 'مراجعة';
                const evalClass = r.evaluation === 'ممتاز' ? 'status-excellent' : (r.evaluation === 'جيد' ? 'status-good' : 'status-late');
                return `<tr>
                <td>${new Date(r.date).toLocaleDateString('en-GB')}</td>
                <td>${escapeHtml(r.surahName || '')}</td>
                <td>${escapeHtml(r.verses || '')}</td>
                <td>${typeLabel}</td>
                <td><span class="status-badge ${evalClass}">${escapeHtml(r.evaluation || '—')}</span></td>
                <td>${escapeHtml(r.notes || '—')}</td>
            </tr>`;
            }).join('');
        } catch (err) {
            tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;padding:20px;color:#ef4444">تعذر تحميل السجل</td></tr>';
        }
    }

    // إدارة الحضور — سجل الطالب الخاص (من /attendance/my)
    const STUDENT_ATT_STATUS = {
        Present: { label: '✅ حاضر', cls: 'status-present' },
        Late: { label: '⏰ متأخر', cls: 'status-late' },
        ExcusedAbsence: { label: '📋 غائب بإذن', cls: 'status-absent' },
        UnexcusedAbsence: { label: '❌ غائب بدون إذن', cls: 'status-absent' }
    };

    function renderStudentAttendance(data) {
        const summary = data?.summary || {};
        const presentEl = document.getElementById('studentAttPresent');
        const lateEl = document.getElementById('studentAttLate');
        const absentEl = document.getElementById('studentAttAbsent');
        const rateEl = document.getElementById('studentAttRate');
        if (presentEl) presentEl.textContent = summary.present || 0;
        if (lateEl) lateEl.textContent = summary.late || 0;
        if (absentEl) absentEl.textContent = (summary.excusedAbsence || 0) + (summary.unexcusedAbsence || 0);
        if (rateEl) rateEl.textContent = summary.attendanceRate ?? 0;

        const tbody = document.querySelector('#studentAttendanceTable tbody');
        if (!tbody) return;
        const records = data?.records || [];
        if (!records.length) {
            tbody.innerHTML = '<tr><td colspan="3" style="text-align:center;padding:24px;color:var(--text-muted)">لا توجد سجلات حضور بعد</td></tr>';
            return;
        }
        tbody.innerHTML = records.map(r => {
            const meta = STUDENT_ATT_STATUS[r.status] || { label: escapeHtml(r.status || '—'), cls: 'status-late' };
            return `<tr>
                <td>${escapeHtml(r.date || '—')}</td>
                <td><span class="status-badge ${meta.cls}">${meta.label}</span></td>
                <td style="color:var(--text-muted);font-size:13px">${r.note ? escapeHtml(r.note) : '—'}</td>
            </tr>`;
        }).join('');
    }

    async function fetchStudentAttendance() {
        const tbody = document.querySelector('#studentAttendanceTable tbody');
        try {
            const data = await apiFetch('/attendance/my');
            renderStudentAttendance(data);
        } catch (err) {
            console.error('Error fetching student attendance:', err);
            if (tbody) {
                tbody.innerHTML = '<tr><td colspan="3" style="text-align:center;padding:24px;color:#dc2626">تعذر تحميل سجل الحضور</td></tr>';
            }
        }
    }

    async function sendAbsenceWhatsApp(studentId) {
        try {
            const res = await fetch('/api/notifications/absence', {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ studentId, date: new Date().toISOString() })
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || 'تعذر إرسال إشعار الغياب');
            }
            if (typeof showToast === 'function') showToast('✅ تم إرسال إشعار الغياب');
        } catch (e) {
            if (typeof showToast === 'function') showToast('❌ ' + (e.message || 'تعذر إرسال إشعار الغياب'));
        }
    }

    // الحلقات
    

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

        // التاريخ الميلادي
        const gregEl = document.getElementById('topbarDateGregorian');
        if (gregEl) gregEl.textContent = days[now.getDay()] + '، ' + now.getDate() + ' ' + months[now.getMonth()];

        // التاريخ الهجري باستخدام Intl API (تقويم أم القرى)
        const hijriEl = document.getElementById('topbarDateHijri');
        if (hijriEl) {
            try {
                const hijriFormatter = new Intl.DateTimeFormat('ar-SA-u-ca-islamic-umalqura', {
                    day: 'numeric', month: 'long', year: 'numeric'
                });
                hijriEl.textContent = '📅 ' + hijriFormatter.format(now);
            } catch (e) {
                hijriEl.textContent = '';
            }
        }

        // fallback: إذا كانت العناصر الجديدة غير موجودة (توافق عكسي)
        if (!gregEl && !hijriEl) {
            el.textContent = days[now.getDay()] + '، ' + now.getDate() + ' ' + months[now.getMonth()];
        }
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
        navigator.serviceWorker.register('/sw.js?v=10').then(reg => {
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

    // Search & Center Summary Hooks
    function searchAttendanceReports() {
        // Toggle inline search bar above the attendance table
        let bar = document.getElementById('attendanceSearchBar');
        if (!bar) {
            // Create the search bar on first click
            const wrap = document.getElementById('attendanceList');
            if (!wrap) { showToast('افتح صفحة الحضور أولاً'); return; }
            bar = document.createElement('div');
            bar.id = 'attendanceSearchBar';
            bar.style.cssText = 'display:flex;gap:10px;align-items:center;margin-bottom:12px;padding:10px 14px;background:var(--bg);border-radius:var(--radius);border:1px solid var(--border);';
            bar.innerHTML = `
                <span style="font-size:18px">🔍</span>
                <input id="attendanceSearchInput" type="text" class="form-input"
                       placeholder="ابحث باسم الطالب..." dir="rtl"
                       style="flex:1;padding:8px 12px;font-size:14px;border-radius:8px">
                <button class="btn btn-outline" style="padding:6px 12px;font-size:12px"
                        onclick="clearAttendanceSearch()">✕ مسح</button>`;
            wrap.parentNode.insertBefore(bar, wrap);

            // Debounced real-time filter
            const input = document.getElementById('attendanceSearchInput');
            let timer;
            input.addEventListener('input', function () {
                clearTimeout(timer);
                timer = setTimeout(() => filterAttendanceRows(this.value), 200);
            });
            input.focus();
        } else {
            // Toggle visibility
            const isVisible = bar.style.display !== 'none';
            bar.style.display = isVisible ? 'none' : 'flex';
            if (!isVisible) {
                const input = document.getElementById('attendanceSearchInput');
                if (input) { input.value = ''; input.focus(); }
                filterAttendanceRows(''); // reset filter
            }
        }
    }

    function filterAttendanceRows(query) {
        const rows = document.querySelectorAll('#attendanceList table tbody tr');
        const q = (query || '').trim().toLowerCase();
        if (!rows.length) return;

        rows.forEach(row => {
            if (!q) {
                row.style.display = '';
                return;
            }
            const nameCell = row.querySelector('td:first-child');
            const name = (nameCell?.textContent || '').toLowerCase();
            row.style.display = name.includes(q) ? '' : 'none';
        });
    }

    function clearAttendanceSearch() {
        const input = document.getElementById('attendanceSearchInput');
        if (input) input.value = '';
        filterAttendanceRows('');
        const bar = document.getElementById('attendanceSearchBar');
        if (bar) bar.style.display = 'none';
    }

    async function showCenterSummary() {
        openModal('centerSummaryModal');
        const body = document.getElementById('centerSummaryBody');
        if (!body) return;
        body.innerHTML = '<div style="text-align:center;padding:20px;">⏳ جاري جلب إحصائيات المركز...</div>';
        
        try {
            const res = await apiFetch('/reports/center-summary');
            body.innerHTML = `
                <div class="cards-grid">
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--blue-light);color:var(--blue-dark)">👥</div>
                        <div class="stat-info"><p>الطلاب</p><h3>${res.totalStudents}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--green-light);color:var(--green-dark)">👨‍🏫</div>
                        <div class="stat-info"><p>المحفظين</p><h3>${res.totalTeachers}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--amber-light);color:var(--amber-dark)">⭕</div>
                        <div class="stat-info"><p>الحلقات</p><h3>${res.totalCircles}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--indigo-light);color:var(--indigo-dark)">📖</div>
                        <div class="stat-info"><p>تسميع الشهر</p><h3>${res.monthSessions}</h3></div>
                    </div>
                </div>
                <div style="margin-top:15px;text-align:center;font-size:12px;color:var(--text-muted)">
                    تم التحديث: ${res.generatedAt}
                </div>
            `;
        } catch(e) {
            body.innerHTML = '<div style="text-align:center;color:red;padding:20px;">❌ تعذر جلب الإحصائيات</div>';
        }
    }

    // Loading state helper
    function setBtnLoading(btnId, isLoading, originalText = '') {
        const btn = typeof btnId === 'string' ? document.getElementById(btnId) : btnId;
        if (!btn) return;
        if (isLoading) {
            btn.dataset.origText = btn.innerHTML;
            btn.innerHTML = '⏳ جاري...';
            btn.disabled = true;
        } else {
            btn.innerHTML = originalText || btn.dataset.origText || 'حفظ';
            btn.disabled = false;
        }
    }

    

    global.fetchParentView = fetchParentView;
    global.fetchStudentView = fetchStudentView;
    global.fetchStudentFullHifzRecord = fetchStudentFullHifzRecord;
    global.fetchStudentAttendance = fetchStudentAttendance;
    global.sendAbsenceWhatsApp = sendAbsenceWhatsApp;
    global.renderQuranMap = renderQuranMap;
    global.HifzRecord_ParseVerseCount = HifzRecord_ParseVerseCount;
    global.openMemModalForStudent = openMemModalForStudent;
    global.confirmDelete = confirmDelete;
    global.executeDelete = executeDelete;
    global.handleGlobalSearch = handleGlobalSearch;
    global.toggleFab = toggleFab;
    global.updateTopbarAvatar = updateTopbarAvatar;
    global.installPWA = installPWA;
    global.toggleDarkMode = toggleDarkMode;
    global.markAllPresent = markAllPresent;
    global.saveAttendance = saveAttendance;
    global.searchAttendanceReports = searchAttendanceReports;
    global.filterAttendanceRows = filterAttendanceRows;
    global.clearAttendanceSearch = clearAttendanceSearch;
    global.showCenterSummary = showCenterSummary;
    global.setBtnLoading = setBtnLoading;
})(window);
