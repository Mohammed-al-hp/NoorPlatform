/**
 * منصة نور — جلسات الحفظ والتسميع
 */
(function (global) {
    'use strict';

    const U = () => global.NoorUtils;
    let sessionType = 'Memorization';
    let revisionSubMode = 'Sequential';
    let questionCounter = 0;

    function initHifzModal() {
        const modal = document.getElementById('addMemModal');
        if (!modal || modal.dataset.hifzInit === '1') return;
        modal.dataset.hifzInit = '1';

        populateCircleSelect();
        bindHifzModalEvents();
        resetHifzForm();
    }

    function bindHifzModalEvents() {
        const circleSel = document.getElementById('hifzCircleSelect');
        if (circleSel) {
            circleSel.addEventListener('change', onHifzCircleChange);
        }

        document.querySelectorAll('[data-session-type]').forEach(btn => {
            btn.addEventListener('click', function () {
                selectSessionTypeBtn(this, this.dataset.sessionType);
            });
        });

        document.querySelectorAll('[data-revision-mode]').forEach(btn => {
            btn.addEventListener('click', function () {
                selectRevisionMode(this, this.dataset.revisionMode);
            });
        });

        const addQ = document.getElementById('btnAddRevisionQuestion');
        if (addQ) addQ.addEventListener('click', addRevisionQuestion);

        const fromS = document.getElementById('hifzSeqFromSurah');
        const toS = document.getElementById('hifzSeqToSurah');
        if (fromS) fromS.addEventListener('change', updateSequentialPreview);
        if (toS) toS.addEventListener('change', updateSequentialPreview);

        const toSurahWrap = document.getElementById('hifzToSurahWrap');
        const showTo = document.getElementById('hifzShowToSurah');
        if (showTo) {
            showTo.addEventListener('change', function () {
                if (toSurahWrap) toSurahWrap.style.display = showTo.checked ? 'block' : 'none';
            });
        }
    }

    async function populateCircleSelect() {
        const sel = document.getElementById('hifzCircleSelect');
        if (!sel) return;
        sel.innerHTML = '<option value="">— اختر الحلقة —</option>';
        try {
            const circles = await global.apiFetch('/circles');
            window._circles = circles;
            sel.innerHTML = '<option value="">— اختر الحلقة —</option>' +
                circles.map(c => `<option value="${c.id}">${U().escapeHtml(c.name)}</option>`).join('');
        } catch (e) {
            console.error(e);
        }
    }

    async function onHifzCircleChange() {
        const circleId = parseInt(document.getElementById('hifzCircleSelect')?.value, 10);
        const studentSel = document.getElementById('hifzStudentSelect');
        if (!studentSel) return;

        studentSel.innerHTML = '<option value="">جاري التحميل...</option>';
        studentSel.disabled = true;

        if (!circleId) {
            studentSel.innerHTML = '<option value="">— اختر الحلقة أولاً —</option>';
            studentSel.disabled = true;
            return;
        }

        try {
            const circle = await global.apiFetch(`/circles/${circleId}`);
            const students = circle.students || [];
            if (!students.length) {
                studentSel.innerHTML = '<option value="">لا يوجد طلاب</option>';
            } else {
                studentSel.innerHTML = '<option value="">— اختر الطالب —</option>' +
                    students.map(s => `<option value="${s.id}">${U().escapeHtml(s.fullName)}</option>`).join('');
            }
            studentSel.disabled = false;
        } catch (e) {
            studentSel.innerHTML = '<option value="">لا يوجد طلاب</option>';
            studentSel.disabled = false;
        }
    }

    function selectSessionTypeBtn(btn, type) {
        document.querySelectorAll('#addMemModal [data-session-type]').forEach(b => {
            b.classList.remove('btn-primary');
            b.classList.add('btn-outline');
        });
        btn.classList.remove('btn-outline');
        btn.classList.add('btn-primary');
        sessionType = type === 'Revision' ? 'Revision' : 'Memorization';

        const revPanel = document.getElementById('hifzRevisionPanel');
        const memPanel = document.getElementById('hifzMemorizationPanel');
        if (revPanel) revPanel.style.display = sessionType === 'Revision' ? 'block' : 'none';
        if (memPanel) memPanel.style.display = sessionType === 'Memorization' ? 'block' : 'none';
    }

    function selectRevisionMode(btn, mode) {
        document.querySelectorAll('[data-revision-mode]').forEach(b => {
            b.classList.remove('btn-primary');
            b.classList.add('btn-outline');
        });
        btn.classList.remove('btn-outline');
        btn.classList.add('btn-primary');
        revisionSubMode = mode;

        const qPanel = document.getElementById('hifzQuestionsPanel');
        const sPanel = document.getElementById('hifzSequentialPanel');
        if (qPanel) qPanel.style.display = mode === 'Questions' ? 'block' : 'none';
        if (sPanel) sPanel.style.display = mode === 'Sequential' ? 'block' : 'none';
        if (mode === 'Sequential') updateSequentialPreview();
    }

    function addRevisionQuestion() {
        const container = document.getElementById('hifzQuestionsList');
        if (!container) return;
        questionCounter++;
        const id = questionCounter;
        const row = document.createElement('div');
        row.className = 'hifz-question-row';
        row.dataset.questionId = String(id);
        row.innerHTML = `
            <div class="form-group">
                <label class="form-label">السورة</label>
                <select class="form-input hifz-q-surah">${U().surahOptionsHtml()}</select>
            </div>
            <div class="form-group">
                <label class="form-label">من آية</label>
                <input class="form-input hifz-q-from" type="number" min="1" placeholder="1">
            </div>
            <div class="form-group">
                <label class="form-label">إلى آية</label>
                <input class="form-input hifz-q-to" type="number" min="1" placeholder="10">
            </div>
            <div class="form-group full">
                <label class="form-label">نص السؤال</label>
                <input class="form-input hifz-q-text" type="text" placeholder="مثال: اقرأ من منتصف سورة الملك">
            </div>
            <div class="form-group full" style="text-align:left">
                <button type="button" class="btn btn-outline" style="font-size:12px" data-remove-q="${id}">${window.Icon ? window.Icon('trash-2', {size:12}) : ''} حذف السؤال</button>
            </div>`;
        container.appendChild(row);
        row.querySelector('[data-remove-q]').addEventListener('click', () => row.remove());
    }

    function updateSequentialPreview() {
        const preview = document.getElementById('hifzSeqPreview');
        const from = document.getElementById('hifzSeqFromSurah')?.value;
        const to = document.getElementById('hifzSeqToSurah')?.value;
        if (!preview || !from || !to) return;
        const list = U().getSurahsInRange(from, to);
        preview.innerHTML = list.length
            ? list.map(s => `<span class="hifz-seq-chip">${U().escapeHtml(s.name)}</span>`).join('')
            : '<span style="color:var(--text-muted);font-size:12px">اختر من وإلى سورة</span>';
    }

    function resetHifzForm() {
        questionCounter = 0;
        const qList = document.getElementById('hifzQuestionsList');
        if (qList) qList.innerHTML = '';
        const opts = U().surahOptionsHtml();
        ['hifzFromSurah', 'hifzToSurah', 'hifzSeqFromSurah', 'hifzSeqToSurah'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.innerHTML = opts;
        });
        selectSessionTypeBtn(document.querySelector('#addMemModal [data-session-type="Memorization"]'), 'Memorization');
        const revBtn = document.querySelector('[data-revision-mode="Sequential"]');
        if (revBtn) selectRevisionMode(revBtn, 'Sequential');
        addRevisionQuestion();
    }

    async function saveSession() {
        const studentId = parseInt(document.getElementById('hifzStudentSelect')?.value, 10);
        if (!studentId || isNaN(studentId)) {
            global.showToast('يرجى اختيار الحلقة والطالب', 'error');
            return;
        }

        const evalMap = { excellent: 'ممتاز', good: 'جيد', review: 'يحتاج مراجعة' };
        const evaluation = evalMap[document.getElementById('hifzEvaluation')?.value] || 'جيد';
        const notes = document.getElementById('hifzNotes')?.value?.trim() || '';

        let body = {
            studentId,
            type: sessionType,
            evaluation,
            notes,
            date: new Date().toISOString()
        };

        if (sessionType === 'Memorization') {
            const surahName = document.getElementById('hifzFromSurah')?.value || '';
            const showTo = document.getElementById('hifzShowToSurah')?.checked;
            const toSurah = showTo ? document.getElementById('hifzToSurah')?.value : null;
            const fromV = document.getElementById('hifzFromVerse')?.value || '1';
            const toV = document.getElementById('hifzToVerse')?.value || '10';
            body.surahName = surahName;
            body.toSurahName = toSurah || null;
            body.verses = `${fromV}-${toV}`;
            body.startVerseText = document.getElementById('hifzStartText')?.value?.trim() || '';
            body.endVerseText = document.getElementById('hifzEndText')?.value?.trim() || '';
        } else {
            body.revisionMode = revisionSubMode;
            if (revisionSubMode === 'Questions') {
                const questions = [];
                document.querySelectorAll('#hifzQuestionsList .hifz-question-row').forEach(row => {
                    questions.push({
                        surah: row.querySelector('.hifz-q-surah')?.value,
                        from: row.querySelector('.hifz-q-from')?.value,
                        to: row.querySelector('.hifz-q-to')?.value,
                        text: row.querySelector('.hifz-q-text')?.value?.trim()
                    });
                });
                if (!questions.length) {
                    global.showToast('أضف سؤال مراجعة واحد على الأقل', 'error');
                    return;
                }
                body.surahName = questions[0].surah || '';
                body.verses = `${questions[0].from || 1}-${questions[0].to || 1}`;
                body.sessionDetailsJson = JSON.stringify({ questions });
                body.notes = (notes ? notes + '\n' : '') + questions.map((q, i) => `س${i + 1}: ${q.text}`).join('\n');
            } else {
                const from = document.getElementById('hifzSeqFromSurah')?.value;
                const to = document.getElementById('hifzSeqToSurah')?.value;
                if (!from || !to) {
                    global.showToast('حدد من سورة وإلى سورة للمراجعة', 'error');
                    return;
                }
                const range = U().getSurahsInRange(from, to);
                body.surahName = from;
                body.toSurahName = to;
                body.verses = 'مراجعة تسلسلية';
                body.sessionDetailsJson = JSON.stringify({ surahs: range.map(s => s.name) });
                body.notes = (notes ? notes + '\n' : '') + 'سور المراجعة: ' + range.map(s => s.name).join(' -> ');
            }
        }

        const btn = document.querySelector('#addMemModal .btn-primary');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await global.apiFetch('/hifz', 'POST', body);
            global.closeModal('addMemModal');
            global.showToast('تم حفظ جلسة التسميع بنجاح', 'success');
            if (typeof global.fetchMemorizationData === 'function') global.fetchMemorizationData();
        } catch (e) {
            if (global.handleApiError) global.handleApiError(e);
            else global.showToast('' + (e.message || 'حدث خطأ أثناء الحفظ', 'error'));
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
        }
    }

    async function populateMemorizationFilter() {
        const sel = document.getElementById('memStudentFilter');
        if (!sel) return;
        try {
            const data = await global.apiFetch('/students');
            window._students = data;
            if (!data.length) {
                sel.innerHTML = '<option value="">لا يوجد طلاب</option>';
                return;
            }
            sel.innerHTML = '<option value="">— اختر طالباً —</option>' +
                data.map(s => `<option value="${s.id}">${U().escapeHtml(s.fullName)}</option>`).join('');
        } catch (e) {
            sel.innerHTML = '<option value="">لا يوجد طلاب</option>';
        }
    }

    function filterStudentById(studentId) {
        const id = parseInt(studentId, 10);
        if (!id) {
            const header = document.getElementById('memProgressTitle');
            if (header) header.textContent = 'تقدم الحفظ';
            return;
        }
        const student = (window._students || []).find(s => s.id === id);
        if (!student) return;
        const header = document.getElementById('memProgressTitle');
        const sub = document.getElementById('memProgressSub');
        const pct = document.getElementById('memProgressPct');
        const fill = document.getElementById('memProgressFill');
        const badge = document.getElementById('memProgressBadge');
        const progress = student.progress ?? student.hifzProgress ?? 0;
        if (header) header.textContent = 'تقدم الحفظ — ' + student.fullName;
        if (sub) sub.textContent = student.circleName || '—';
        if (pct) pct.textContent = progress + '%';
        if (fill) fill.style.width = progress + '%';
        if (badge) badge.textContent = progress + '% مكتمل';
        loadStudentHifzMap(id);
    }

    async function loadStudentHifzMap(studentId) {
        try {
            const records = await global.apiFetch(`/hifz/student/${studentId}`);
            if (typeof global.renderQuranMap === 'function') global.renderQuranMap(records);
        } catch (e) { /* silent */ }
    }

    function openHifzModalForStudent(studentId) {
        initHifzModal();
        global.openModal('addMemModal');
        populateCircleSelect().then(async () => {
            const students = window._students || await global.apiFetch('/students');
            const st = students.find(s => s.id === studentId);
            if (st?.circleId) {
                const circleSel = document.getElementById('hifzCircleSelect');
                if (circleSel) {
                    circleSel.value = String(st.circleId);
                    await onHifzCircleChange();
                    const studentSel = document.getElementById('hifzStudentSelect');
                    if (studentSel) studentSel.value = String(studentId);
                }
            }
        });
    }

    global.NoorHifz = {
        initHifzModal,
        saveSession,
        populateMemorizationFilter,
        filterStudentById,
        openHifzModalForStudent,
        onHifzCircleChange
    };

    global.saveSession = saveSession;
    global.filterStudent = function (val) { filterStudentById(val); };

    const _openModal = global.openModal;
    if (_openModal) {
        global.openModal = function (id) {
            _openModal(id);
            if (id === 'addMemModal') {
                initHifzModal();
                resetHifzForm();
            }
        };
    }
})(window);
