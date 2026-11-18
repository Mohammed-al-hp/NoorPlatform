// app-ui-extensions.js - واجهة المستخدم، بوابة الولي، الحضور وغيرها
(function(global) {
    'use strict';

    // ═══ بوابة ولي الأمر: بطاقة لكل ابن (موضع الحفظ الحالي، المحفوظ، الحضور) + عمود جانبي بآخر الجلسات ═══
    const PARENT_DAY_NAMES = ['أحد', 'اثنين', 'ثلاثاء', 'أربعاء', 'خميس', 'جمعة', 'سبت'];
    const ATT_LABELS = { Present: 'حاضر', Late: 'متأخر', ExcusedAbsence: 'غائب بإذن', UnexcusedAbsence: 'غائب دون إذن' };
    const pIcon = (n, s) => (window.Icon ? window.Icon(n, { size: s || 14 }) : '');

    function gradeClass(ev) {
        const e = String(ev || '');
        if (!e) return 'neutral';
        if (e.includes('ضعيف') || e.includes('يحتاج')) return 'fail';
        if (e.includes('مقبول')) return 'warn';
        return 'pass';
    }

    function sessionRange(s) {
        const surah = s.toSurahName && s.toSurahName !== s.surahName
            ? `${escapeHtml(s.surahName)} ← ${escapeHtml(s.toSurahName)}`
            : escapeHtml(s.surahName || '');
        return s.verses ? `${surah} <span dir="ltr">(${escapeHtml(s.verses)})</span>` : surah;
    }

    function renderCurrentPosition(c) {
        const p = c.currentPosition;
        if (!p) {
            return `<div class="parent-now parent-now-empty">
                <span class="parent-now-label">${pIcon('book-open')} الحفظ الجديد</span>
                <p>لم تُسجَّل جلسات حفظ جديد بعد.</p>
            </div>`;
        }
        if (!p.currentSurahName) {
            return `<div class="parent-now"><span class="parent-now-label">${pIcon('award')} الحفظ الجديد</span>
                <div class="parent-now-surah">أتمّ حفظ القرآن الكريم</div></div>`;
        }
        const inSurah = !p.surahCompleted;
        const pct = inSurah ? Math.round(p.lastVerse / p.verseCount * 100) : 0;
        return `<div class="parent-now">
            <span class="parent-now-label">${pIcon('book-open')} يحفظ الآن</span>
            <div class="parent-now-surah">سورة ${escapeHtml(p.currentSurahName)}</div>
            ${inSurah
                ? `<div class="parent-now-meta">وصل إلى الآية ${p.lastVerse} من ${p.verseCount}</div>
                   <div class="progress-bar parent-now-bar"><div class="progress-fill" style="width:${pct}%"></div></div>`
                : `<div class="parent-now-meta">أتمّ سورة ${escapeHtml(p.surahName)} — يبدأ الجلسة القادمة بسورة ${escapeHtml(p.currentSurahName)}</div>`}
        </div>`;
    }

    function renderWeekAttendance(c) {
        const days = c.weekAttendance || [];
        if (!days.length) return '';
        return `<div class="parent-week" aria-label="حضور آخر سبعة أيام">
            ${days.map(d => {
                const day = PARENT_DAY_NAMES[new Date(d.date + 'T12:00:00').getDay()];
                const cls = d.status ? 'att-' + d.status : 'att-none';
                const title = d.status ? ATT_LABELS[d.status] || d.status : 'لا يوجد تسجيل';
                return `<div class="parent-week-day" title="${escapeHtml(d.date)}: ${escapeHtml(title)}">
                    <span class="parent-week-dot ${cls}"></span><small>${day}</small></div>`;
            }).join('')}
        </div>`;
    }

    function renderChildCard(c) {
        const meta = [c.circleName, c.teacherName ? 'المحفّظ: ' + c.teacherName : null].filter(Boolean).map(escapeHtml).join(' • ');
        const progressTitle = c.memorizedVerses ? `${c.memorizedVerses} آية من 6214` : '';
        return `<div class="parent-child-card">
            <div class="parent-child-head">
                <div class="student-avatar-lg" style="background:var(--gradient)">${escapeHtml(c.fullName).slice(0, 2)}</div>
                <div><h3>${escapeHtml(c.fullName)}</h3><p>${meta}</p></div>
            </div>
            ${renderCurrentPosition(c)}
            <div class="student-card-stats parent-child-stats">
                <div class="mini-stat" title="${progressTitle}"><label>المحفوظ من القرآن</label><p>${c.progress}%</p></div>
                <div class="mini-stat"><label>الحضور</label><p>${c.attendance}%</p></div>
                <div class="mini-stat"><label>النقاط</label><p style="color:var(--amber-dark)">${pIcon('star')} ${c.points || 0}</p></div>
                ${window.NoorMonthlyRating ? window.NoorMonthlyRating.miniStatHtml(c.monthlyHifzRating) : ''}
            </div>
            <div class="parent-section-label">حضور آخر سبعة أيام</div>
            ${renderWeekAttendance(c)}
            ${c.lastNote && c.lastNote !== 'لا توجد ملاحظات'
                ? `<div class="parent-note">${pIcon('message-circle')} <strong>آخر ملاحظة من المحفّظ:</strong> ${escapeHtml(c.lastNote)}</div>`
                : ''}
            <div class="parent-child-actions">
                <button class="btn btn-outline" onclick="NoorMessages.openChildDetails(${c.id}, ${jsArg(c.fullName)})">${pIcon('clipboard-list')} السجل الكامل</button>
                <button class="btn btn-outline" onclick="NoorSmart.openChildMastery(${c.id}, ${jsArg(c.fullName)})">${pIcon('book-open')} خريطة الإتقان</button>
            </div>
        </div>`;
    }

    function renderParentTimeline(children) {
        const items = [];
        children.forEach(c => (c.recentSessions || []).forEach(s => items.push({ child: c.fullName, ...s })));
        items.sort((a, b) => new Date(b.date) - new Date(a.date));
        if (!items.length) return '<p class="parent-empty">لا توجد جلسات مسجلة بعد</p>';
        const multi = children.length > 1;
        return items.slice(0, 8).map(s => `
            <div class="parent-timeline-item">
                <span class="parent-timeline-type ${s.type === 'Revision' ? 'rev' : 'mem'}">${s.type === 'Revision' ? 'مراجعة' : 'حفظ جديد'}</span>
                <div class="parent-timeline-body">
                    <div>${sessionRange(s)}</div>
                    <small>${multi ? escapeHtml(s.child) + ' • ' : ''}${typeof formatDateEnGb === 'function' ? formatDateEnGb(s.date) : escapeHtml(String(s.date).slice(0, 10))}</small>
                </div>
                ${s.evaluation ? `<span class="parent-grade ${gradeClass(s.evaluation)}">${escapeHtml(s.evaluation)}</span>` : ''}
            </div>`).join('');
    }

    async function fetchParentView() {
        const grid = document.getElementById('parentChildrenGrid');
        if (!grid) return;
        const alertsEl = document.getElementById('parentAlerts');
        const timeline = document.getElementById('parentTimeline');
        grid.innerHTML = '<div class="parent-empty"><span class="spin-icon">' + pIcon('loader', 16) + '</span> جاري تحميل بيانات الأبناء...</div>';
        try {
            const response = await apiFetch('/dashboard/parent-summary');
            const children = response.children || response;
            const alerts = response.alerts || [];

            if (alertsEl) {
                alertsEl.innerHTML = alerts.map(a => `
                    <div class="parent-alert">
                        <span>${pIcon('alert-triangle', 20)}</span>
                        <span style="flex:1">${escapeHtml(a.message)}</span>
                        <button class="btn" onclick="navigate('parentFees', null)">عرض الفاتورة</button>
                    </div>`).join('');
            }

            if (!children.length) {
                grid.innerHTML = '<div class="parent-empty">لا يوجد أبناء مسجلون لحسابك</div>';
                if (timeline) timeline.innerHTML = '';
                return;
            }
            grid.innerHTML = children.map(renderChildCard).join('');
            if (timeline) timeline.innerHTML = renderParentTimeline(children);
        } catch {
            grid.innerHTML = '<div class="parent-empty" style="color:#dc2626">' + pIcon('x-circle', 16) + ' تعذر تحميل البيانات. تأكد من تسجيل الدخول كولي أمر.</div>';
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
                weeklyMsg.textContent = 'أحسنت! لقد حققت هدفك الأسبوعي';
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
                ? badges.map(b => `<span class="hero-badge" style="background:var(--amber-light);color:var(--amber-dark);">${window.Icon ? window.Icon('medal-gold', {size:12}) : ''} ${escapeHtml(b)}</span>`).join('')
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
                        <div style="margin-bottom:8px">${window.Icon ? window.Icon('book-open', {size:28}) : ''}</div>
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
                const medalIcon = window.Icon ? window.Icon('medal-gold', {size:20}) : '';
                const medals = [medalIcon, medalIcon, medalIcon];
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
                let badgesHtml = `<span class="hero-badge" style="background:var(--amber-light);color:var(--amber-dark);">${window.Icon ? window.Icon('star', {size:13}) : ''} ${data.points || 0} نقطة</span>`;
                parseStudentBadges(data.badges).forEach(b => {
                    badgesHtml += `<span class="hero-badge">${window.Icon ? window.Icon('medal-gold', {size:13}) : ''} ${escapeHtml(b)}</span>`;
                });
                if (data.teacherRating > 0) {
                    badgesHtml += `<span class="hero-badge">${window.Icon ? window.Icon('star', {size:13}) : ''} ${data.teacherRating.toFixed(1)} / 5</span>`;
                }
                heroBadges.innerHTML = badgesHtml;
            }
            
            renderStudentGamification(data);
            window.NoorMonthlyRating?.renderStudentCard('studentMonthlyRating', data.monthlyHifzRating);
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
            if (typeof showToast === 'function') showToast('تعذر تحميل بيانات الطالب', 'error');
        }
    }
    async function fetchStudentFullHifzRecord(studentId) {
        const tbody = document.getElementById('studentFullHifzTable');
        if (!tbody) return;
        try {
            const records = await apiFetch(`/hifz/student/${studentId}`);
            renderQuranMap(records);
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
        Present: { label: 'حاضر', iconName: 'check-circle', cls: 'status-present' },
        Late: { label: 'متأخر', iconName: 'clock', cls: 'status-late' },
        ExcusedAbsence: { label: 'غائب بإذن', iconName: 'file-text', cls: 'status-absent' },
        UnexcusedAbsence: { label: 'غائب بدون إذن', iconName: 'x-circle', cls: 'status-absent' }
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
            if (typeof showToast === 'function') showToast('تم إرسال إشعار الغياب', 'success');
        } catch (e) {
            if (typeof showToast === 'function') showToast('' + (e.message || 'تعذر إرسال إشعار الغياب', 'error'));
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
                memorizedSurahs[key] += r.verseCount || global.NoorUtils.parseVerseCount(r.verses || '');
            }
        });

        grid.innerHTML = (window.SURAHS || []).map(s => {
            const memorized = memorizedSurahs[s.name] || 0;
            let cls = 'empty', cellIcon = window.Icon ? window.Icon('book-open', {size:16}) : '';
            if (memorized >= s.v) { cls = 'memorized'; cellIcon = window.Icon ? window.Icon('check-circle', {size:16}) : ''; }
            else if (memorized > 0) { cls = 'partial'; cellIcon = window.Icon ? window.Icon('edit', {size:16}) : ''; }
            const pct = memorized > 0 ? Math.min(Math.round(memorized / s.v * 100), 100) : 0;
            const title = pct > 0 ? `${s.name}: ${memorized}/${s.v} آية (${pct}%)` : `${s.name}: لم يُحفظ`;
            return `
  <div class="surah-cell ${cls}" title="${title}">
   <span class="surah-icon">${cellIcon}</span>
    <span>${s.name}</span>
    <span class="surah-num">${s.n}</span>
    ${pct > 0 ? `<span style="font-size:9px">${pct}%</span>` : ''}
  </div>`;
        }).join('');
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

    function setFabOpen(isOpen) {
        const btn = document.getElementById('fabBtn');
        const menu = document.getElementById('fabMenu');
        if (!btn || !menu) return;
        menu.classList.toggle('open', isOpen);
        btn.classList.toggle('open', isOpen);
        btn.innerHTML = window.Icon ? window.Icon(isOpen ? 'x' : 'menu', {size:20}) : '';
        // طمس الخلفية خلف القائمة العائمة
        document.getElementById('fabBackdrop')?.classList.toggle('open', isOpen);
        document.body.classList.toggle('fab-open', isOpen);
    }

    function toggleFab() {
        const menu = document.getElementById('fabMenu');
        if (!menu) return;
        setFabOpen(!menu.classList.contains('open'));
    }

    /** تمييز القسم الحالي في القائمة العائمة. */
    function markActiveFab(page) {
        document.querySelectorAll('#fabMenu .fab-menu-item').forEach(item => {
            const onclick = item.getAttribute('onclick') || '';
            item.classList.toggle('active', onclick.includes("navigate('" + page + "'"));
        });
    }
    window.NoorMarkActiveFab = markActiveFab;

    document.addEventListener('click', (e) => {
        const fab = document.getElementById('fabContainer');
        if (fab && !fab.contains(e.target)) setFabOpen(false);
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
                const hijri = hijriFormatter.format(now);
                hijriEl.innerHTML = (window.Icon ? window.Icon('calendar', {size:14}) : '') + ' '
                    + '<span class="hijri-weekday">' + days[now.getDay()] + '، </span>' + hijri;
                hijriEl.title = 'التاريخ الهجري';
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
        if (av && window.Icon) av.innerHTML = window.Icon('user', { size: 18 });
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
                        showToast('تحديث جديد متوفر — أعد تحميل الصفحة', 'info');
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
        if (t) t.innerHTML = window.Icon ? window.Icon(isDark ? 'sun' : 'moon', {size:18}) : '';
    }
    
    if (localStorage.getItem('noor_dark') === '1') {
        document.body.classList.add('dark-mode');
        document.addEventListener('DOMContentLoaded', () => {
            const t = document.getElementById('darkToggle');
            if (t) t.innerHTML = window.Icon ? window.Icon('sun', {size:18}) : '';
        });
    }

   

    // Search & Center Summary Hooks
    function searchAttendanceReports() {
        // Toggle inline search bar above the attendance table
        let bar = document.getElementById('attendanceSearchBar');
        if (!bar) {
            // Create the search bar on first click
            const wrap = document.getElementById('attendanceList');
            if (!wrap) { showToast('افتح صفحة الحضور أولاً', 'warning'); return; }
            bar = document.createElement('div');
            bar.id = 'attendanceSearchBar';
            bar.style.cssText = 'display:flex;gap:10px;align-items:center;margin-bottom:12px;padding:10px 14px;background:var(--bg);border-radius:var(--radius);border:1px solid var(--border);';
            bar.innerHTML = `
                <span>${window.Icon ? window.Icon('search', {size:16}) : ''}</span>
                <input id="attendanceSearchInput" type="text" class="form-input"
                       placeholder="ابحث باسم الطالب..." dir="rtl"
                       style="flex:1;padding:8px 12px;font-size:14px;border-radius:8px">
                <button class="btn btn-outline" style="padding:6px 12px;font-size:12px"
                        onclick="clearAttendanceSearch()">${window.Icon ? window.Icon('x', {size:12}) : ''} مسح</button>`;
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
        body.innerHTML = '<div style="text-align:center;padding:20px;"><span class="spin-icon">' + (window.Icon ? window.Icon('loader', {size:16}) : '') + '</span> جاري جلب إحصائيات المركز...</div>';
        
        try {
            const res = await apiFetch('/reports/center-summary');
            body.innerHTML = `
                <div class="cards-grid">
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--blue-light);color:var(--blue-dark)">${window.Icon ? window.Icon('users', {size:22}) : ''}</div>
                        <div class="stat-info"><p>الطلاب</p><h3>${res.totalStudents}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--green-light);color:var(--green-dark)">${window.Icon ? window.Icon('user-check', {size:22}) : ''}</div>
                        <div class="stat-info"><p>المحفظين</p><h3>${res.totalTeachers}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--amber-light);color:var(--amber-dark)">${window.Icon ? window.Icon('circle-dot', {size:22}) : ''}</div>
                        <div class="stat-info"><p>الحلقات</p><h3>${res.totalCircles}</h3></div>
                    </div>
                    <div class="stat-card">
                        <div class="stat-icon" style="background:var(--indigo-light);color:var(--indigo-dark)">${window.Icon ? window.Icon('book-open', {size:22}) : ''}</div>
                        <div class="stat-info"><p>تسميع الشهر</p><h3>${res.monthSessions}</h3></div>
                    </div>
                </div>
                <div style="margin-top:15px;text-align:center;font-size:12px;color:var(--text-muted)">
                    تم التحديث: ${res.generatedAt}
                </div>
            `;
        } catch(e) {
            body.innerHTML = '<div style="text-align:center;color:red;padding:20px;">' + (window.Icon ? window.Icon('x-circle', {size:16}) : '') + ' تعذر جلب الإحصائيات</div>';
        }
    }

    // Loading state helper
    function setBtnLoading(btnId, isLoading, originalText = '') {
        const btn = typeof btnId === 'string' ? document.getElementById(btnId) : btnId;
        if (!btn) return;
        if (isLoading) {
            btn.dataset.origText = btn.innerHTML;
            btn.innerHTML = (window.Icon ? '<span class="spin-icon">' + window.Icon('loader', {size:14}) + '</span>' : '') + ' جاري...';
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
    global.openMemModalForStudent = openMemModalForStudent;
    global.confirmDelete = confirmDelete;
    global.executeDelete = executeDelete;
    global.handleGlobalSearch = handleGlobalSearch;
    global.toggleFab = toggleFab;
    global.updateTopbarAvatar = updateTopbarAvatar;
    global.installPWA = installPWA;
    global.toggleDarkMode = toggleDarkMode;
    global.searchAttendanceReports = searchAttendanceReports;
    global.filterAttendanceRows = filterAttendanceRows;
    global.clearAttendanceSearch = clearAttendanceSearch;
    global.showCenterSummary = showCenterSummary;
    global.setBtnLoading = setBtnLoading;
})(window);
