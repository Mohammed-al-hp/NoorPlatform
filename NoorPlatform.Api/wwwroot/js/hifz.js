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
        populateMatnSelect();
        bindHifzModalEvents();
        resetHifzForm();
    }

    async function populateMatnSelect() {
        const sel = document.getElementById('hifzMatnName');
        if (!sel) return;
        try {
            const matns = await global.apiFetch('/matns');
            sel.innerHTML = matns.length
                ? matns.map(m => `<option value="${U().escapeHtml(m.name)}">${U().escapeHtml(m.name)}</option>`).join('')
                : '<option value="">— لا توجد متون مضافة —</option>';
        } catch (e) {
            sel.innerHTML = '<option value="">تعذر تحميل المتون</option>';
        }
    }
    function bindHifzModalEvents() {
        const circleSel = document.getElementById('hifzCircleSelect');
        if (circleSel) {
            circleSel.addEventListener('change', onHifzCircleChange);
        }

        const studentSel = document.getElementById('hifzStudentSelect');
        if (studentSel) {
            studentSel.addEventListener('change', onHifzStudentChange);
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
        const fromSeqVerse = document.getElementById('hifzSeqFromVerse');
        if (fromS) fromS.addEventListener('change', onSeqFromSurahChange);
        if (toS) toS.addEventListener('change', onSeqToSurahChange);
        const toSeqVerse = document.getElementById('hifzSeqToVerse');
        if (toSeqVerse) toSeqVerse.addEventListener('change', autoFillSeqEndVerseText);
        if (fromSeqVerse) fromSeqVerse.addEventListener('change', autoFillSeqVerseText);

        const toSurahWrap = document.getElementById('hifzToSurahWrap');
        const showTo = document.getElementById('hifzShowToSurah');
        if (showTo) {
            showTo.addEventListener('change', function () {
                if (toSurahWrap) toSurahWrap.style.display = showTo.checked ? 'block' : 'none';
            });
        }

        // ─── جلب نص الآيات تلقائيًا (رواية قالون) عند اختيار السورة/الآيات ───
        const memFromSurah = document.getElementById('hifzFromSurah');
        const memFromVerse = document.getElementById('hifzFromVerse');
        const memToVerse = document.getElementById('hifzToVerse');
        [memFromSurah, memFromVerse, memToVerse].forEach(el => {
            if (el) el.addEventListener('change', autoFillVerseText);
        });

        // تتبع التعديل اليدوي: إن كتب المحفّظ بنفسه بهذين الحقلين، لا نستبدل ما كتبه لاحقًا
        const startInput = document.getElementById('hifzStartText');
        const endInput = document.getElementById('hifzEndText');
        if (startInput) startInput.addEventListener('input', () => { startInput.dataset.userEdited = '1'; });
        if (endInput) endInput.addEventListener('input', () => { endInput.dataset.userEdited = '1'; });
    }

    async function autoFillVerseText() {
        const startInput = document.getElementById('hifzStartText');
        const endInput = document.getElementById('hifzEndText');
        if (!startInput && !endInput) return;
        if (!global.NoorQuranText) return; // المصدر غير محمّل بالصفحة

        const surahName = document.getElementById('hifzFromSurah')?.value;
        const fromVerse = document.getElementById('hifzFromVerse')?.value;
        const toVerse = document.getElementById('hifzToVerse')?.value || fromVerse;
        if (!surahName || !fromVerse) return;

        try {
            if (startInput && !startInput.dataset.userEdited) {
                startInput.placeholder = 'جاري جلب النص...';
                const startText = await global.NoorQuranText.fetchVerseTextByName(surahName, fromVerse);
                if (startText) startInput.value = startText;
                startInput.placeholder = 'اقرأ من قوله تعالى...';
            }
            if (endInput && !endInput.dataset.userEdited) {
                endInput.placeholder = 'جاري جلب النص...';
                const endText = await global.NoorQuranText.fetchVerseTextByName(surahName, toVerse);
                if (endText) endInput.value = endText;
                endInput.placeholder = '...إلى قوله';
            }
        } catch (e) {
            console.warn('تعذر جلب نص الآيات تلقائيًا:', e);
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

    async function onHifzStudentChange() {
        // الاقتراح التلقائي يفيد فقط بوضع "حفظ جديد" (Memorization)
        if (sessionType !== 'Memorization') return;

        const studentId = parseInt(document.getElementById('hifzStudentSelect')?.value, 10);
        const hintEl = document.getElementById('hifzLastPositionHint');
        if (!studentId) {
            if (hintEl) hintEl.textContent = '';
            return;
        }

        if (hintEl) hintEl.textContent = 'جاري التحقق من آخر موضع حفظ...';

        try {
            const pos = await global.apiFetch(`/hifz/last-position/${studentId}`);
            if (!pos || !pos.hasPrevious) {
                if (hintEl) hintEl.textContent = 'لا يوجد سجل حفظ سابق لهذا الطالب — ابدأ من حيث تشاء.';
                return;
            }

            const fromSurahSel = document.getElementById('hifzFromSurah');
            const fromVerseInput = document.getElementById('hifzFromVerse');

            if (fromSurahSel && pos.surahName) {
                fromSurahSel.value = pos.surahName;
            }
            if (fromVerseInput && pos.nextVerse) {
                fromVerseInput.value = pos.nextVerse;
            }

            if (hintEl) {
                const crossedSurah = pos.lastSurahName !== pos.surahName;
                hintEl.textContent = crossedSurah
                    ? `آخر توقف: نهاية سورة ${pos.lastSurahName} (الآية ${pos.lastVerse}) بتاريخ ${pos.lastSessionDate}. تم اقتراح البدء من سورة ${pos.surahName} — الآية ${pos.nextVerse} (سورة جديدة).`
                    : `آخر توقف: سورة ${pos.lastSurahName} — الآية ${pos.lastVerse} (بتاريخ ${pos.lastSessionDate}). تم اقتراح البدء من الآية ${pos.nextVerse}.`;
            }

            // تحديث نص الآية تلقائيًا بناءً على الموضع الجديد المقترح
            autoFillVerseText();
        } catch (e) {
            if (hintEl) hintEl.textContent = '';
            console.warn('تعذر جلب آخر موضع حفظ للطالب:', e);
        }
    }

    async function suggestSequentialStart() {
        const studentId = parseInt(document.getElementById('hifzStudentSelect')?.value, 10);
        const hintEl = document.getElementById('hifzSeqPositionHint');
        if (!studentId) {
            if (hintEl) hintEl.textContent = '';
            return;
        }

        if (hintEl) hintEl.textContent = 'جاري التحقق من آخر موضع توقف...';

        try {
            const pos = await global.apiFetch(`/hifz/last-position/${studentId}?mode=Sequential`);
            if (!pos || !pos.hasPrevious) {
                if (hintEl) hintEl.textContent = 'لا يوجد سجل سابق — يرجى تحديد نقطة البداية يدويًا.';
                return;
            }

            const fromSurahSel = document.getElementById('hifzSeqFromSurah');
            if (fromSurahSel && pos.surahName) {
                fromSurahSel.value = pos.surahName;
            }
            populateSeqVerseSelect(pos.nextVerse);

            if (hintEl) {
                hintEl.textContent = `آخر توقف بالمراجعة: سورة ${pos.lastSurahName || pos.surahName} (بتاريخ ${pos.lastSessionDate || '—'}). تم اقتراح البدء من سورة ${pos.surahName} — الآية ${pos.nextVerse}.`;
            }
        } catch (e) {
            if (hintEl) hintEl.textContent = '';
            console.warn('تعذر جلب آخر موضع مراجعة تسلسلية:', e);
        }
    }

    function populateSeqVerseSelect(suggestedVerse) {
        const surahName = document.getElementById('hifzSeqFromSurah')?.value;
        const verseSel = document.getElementById('hifzSeqFromVerse');
        if (!verseSel || !surahName) return;

        const surah = U().SURAHS.find(s => s.name === surahName);
        const total = surah ? surah.v : 286;
        let options = '';
        for (let i = 1; i <= total; i++) {
            options += `<option value="${i}">آية ${i}</option>`;
        }
        verseSel.innerHTML = options;
        if (suggestedVerse) verseSel.value = String(suggestedVerse);
        autoFillSeqVerseText();
    }

    function onSeqFromSurahChange() {
        const suggested = this?.dataset?.suggestedVerse || null;
        populateSeqVerseSelect(suggested);
        updateSequentialPreview();
    }

    async function autoFillSeqVerseText() {
        const startInput = document.getElementById('hifzSeqStartText');
        if (!startInput || !global.NoorQuranText) return;

        const surahName = document.getElementById('hifzSeqFromSurah')?.value;
        const verse = document.getElementById('hifzSeqFromVerse')?.value;
        if (!surahName || !verse) return;

        try {
            startInput.placeholder = 'جاري جلب النص...';
            const text = await global.NoorQuranText.fetchVerseTextByName(surahName, verse);
            if (text) startInput.value = text;
            startInput.placeholder = 'اقرأ من قوله تعالى...';
        } catch (e) {
            console.warn('تعذر جلب نص آية بداية المراجعة:', e);
        }
    }
    function populateSeqToVerseSelect() {
        const surahName = document.getElementById('hifzSeqToSurah')?.value;
        const verseSel = document.getElementById('hifzSeqToVerse');
        if (!verseSel || !surahName) return;

        const surah = U().SURAHS.find(s => s.name === surahName);
        const total = surah ? surah.v : 286;
        let options = '';
        for (let i = 1; i <= total; i++) {
            options += `<option value="${i}">آية ${i}</option>`;
        }
        verseSel.innerHTML = options;
        verseSel.value = String(total); // افتراضيًا: آخر آية بالسورة (يمكن تغييرها لو توقف بالمنتصف)
        autoFillSeqEndVerseText();
    }

    function onSeqToSurahChange() {
        populateSeqToVerseSelect();
        updateSequentialPreview();
    }

    async function autoFillSeqEndVerseText() {
        const endInput = document.getElementById('hifzSeqEndText');
        if (!endInput || !global.NoorQuranText) return;

        const surahName = document.getElementById('hifzSeqToSurah')?.value;
        const verse = document.getElementById('hifzSeqToVerse')?.value;
        if (!surahName || !verse) return;

        try {
            endInput.placeholder = 'جاري جلب النص...';
            const text = await global.NoorQuranText.fetchVerseTextByName(surahName, verse);
            if (text) endInput.value = text;
            endInput.placeholder = '...إلى قوله';
        } catch (e) {
            console.warn('تعذر جلب نص آية نهاية المراجعة:', e);
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

        const hintEl = document.getElementById('hifzLastPositionHint');
        if (sessionType === 'Memorization') {
            onHifzStudentChange();
        } else if (hintEl) {
            hintEl.textContent = '';
        }
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
        if (mode === 'Sequential') {
            updateSequentialPreview();
            suggestSequentialStart();
        }    }

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
                <label class="form-label">التسمية</label>
                <input class="form-input hifz-q-label" type="text" placeholder="مثال: الثمن الأول">
            </div>
            <div class="form-group">
                <label class="form-label">تردد</label>
                <input class="form-input hifz-q-hes" type="number" min="0" value="0">
            </div>
            <div class="form-group">
                <label class="form-label">تنبيه</label>
                <input class="form-input hifz-q-alert" type="number" min="0" value="0">
            </div>
            <div class="form-group">
                <label class="form-label">فتح</label>
                <input class="form-input hifz-q-open" type="number" min="0" value="0">
            </div>
            <div class="form-group full">
                <label class="form-label">درجة % (اختياري — تلقائي لو تُرك فارغًا)</label>
                <input class="form-input hifz-q-score" type="number" min="0" max="100" placeholder="تلقائي">
            </div>
            <div class="form-group full" style="text-align:left">
                <button type="button" class="btn btn-outline" style="font-size:12px" data-remove-q="${id}">${window.Icon ? window.Icon('trash-2', { size: 12 }) : ''} حذف السؤال</button>
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
        // ... (السطر التالي يبقى كما هو، لا تغيير هنا)
            const el = document.getElementById(id);
            if (el) el.innerHTML = opts;
        });
        // إعادة تفعيل الملء التلقائي لنص الآيات لجلسة جديدة
        const startInput = document.getElementById('hifzStartText');
        const endInput = document.getElementById('hifzEndText');
        if (startInput) { startInput.value = ''; delete startInput.dataset.userEdited; }
        if (endInput) { endInput.value = ''; delete endInput.dataset.userEdited; }
        const hintEl = document.getElementById('hifzLastPositionHint');
        if (hintEl) hintEl.textContent = '';
        const matnCheck = document.getElementById('hifzIncludeMatn');
        const matnFields = document.getElementById('hifzMatnFields');
        if (matnCheck) matnCheck.checked = false;
        if (matnFields) matnFields.style.display = 'none';
        const matnPortion = document.getElementById('hifzMatnPortion');
        if (matnPortion) matnPortion.value = '';
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

        const evalMap = { excellent: 'ممتاز', veryGood: 'جيد جدًا', good: 'جيد', weak: 'ضعيف', review: 'يحتاج مراجعة' };
        const evaluation = evalMap[document.getElementById('hifzEvaluation')?.value] || 'جيد';
        const notes = document.getElementById('hifzNotes')?.value?.trim() || '';

        // وضع "الأسئلة" يُحفظ بمسار منفصل تمامًا (/oral-exams) — يخرج مبكرًا هنا
        if (sessionType === 'Revision' && revisionSubMode === 'Questions') {
            await saveOralExamFromHifzModal(studentId, notes);
            return;
        }

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
                const fromVerse = document.getElementById('hifzSeqFromVerse')?.value || '1';
                const toVerse = document.getElementById('hifzSeqToVerse')?.value || '1';
                const startText = document.getElementById('hifzSeqStartText')?.value?.trim() || '';
                const endText = document.getElementById('hifzSeqEndText')?.value?.trim() || '';
                body.surahName = from;
                body.toSurahName = to;
                body.verses = `${fromVerse}-${toVerse}`;
                body.startVerseText = startText;
                body.endVerseText = endText;
                body.sessionDetailsJson = JSON.stringify({ surahs: range.map(s => s.name) });
                body.notes = (notes ? notes + '\n' : '') + 'سور المراجعة: ' + range.map(s => s.name).join(' -> ');
            }
        }

        const btn = document.querySelector('#addMemModal .btn-primary');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await global.apiFetch('/hifz', 'POST', body);

            // تسجيل متن اختياري بنفس الجلسة (فقط مع وضع "حفظ جديد")
            if (sessionType === 'Memorization' && document.getElementById('hifzIncludeMatn')?.checked) {
                const matnName = document.getElementById('hifzMatnName')?.value;
                if (matnName) {
                    try {
                        await global.apiFetch('/pedagogical/matn', 'POST', {
                            studentId,
                            date: body.date,
                            matnName,
                            portion: document.getElementById('hifzMatnPortion')?.value?.trim() || '',
                            type: 'Memorization',
                            evaluation,
                            notes: null
                        });
                    } catch (matnErr) {
                        console.warn('تعذر حفظ سجل المتن:', matnErr);
                        global.showToast('تم حفظ التسميع، لكن تعذر حفظ سجل المتن', 'warning');
                    }
                }
            }

            global.closeModal('addMemModal');
            global.showToast('تم حفظ جلسة التسميع بنجاح', 'success');
            if (typeof global.fetchMemorizationData === 'function') global.fetchMemorizationData();
        } catch (e) {
            if (global.handleApiError) global.handleApiError(e);
            else global.showToast(e.message || 'حدث خطأ أثناء الحفظ', 'error');
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false);
        }
    }

    async function saveOralExamFromHifzModal(studentId, notes) {
        const kind = document.getElementById('hifzOralKind')?.value || 'AthmanSampling';
        const maxOpenings = parseInt(document.getElementById('hifzOralMaxOpenings')?.value, 10) || 3;
        const scopeLabel = document.getElementById('hifzOralScope')?.value?.trim() || '';
        const circleId = parseInt(document.getElementById('hifzCircleSelect')?.value, 10) || null;

        const questions = [];
        document.querySelectorAll('#hifzQuestionsList .hifz-question-row').forEach(row => {
            const scoreRaw = row.querySelector('.hifz-q-score')?.value;
            questions.push({
                label: row.querySelector('.hifz-q-label')?.value?.trim() || '',
                hesitationCount: parseInt(row.querySelector('.hifz-q-hes')?.value, 10) || 0,
                alertCount: parseInt(row.querySelector('.hifz-q-alert')?.value, 10) || 0,
                openingCount: parseInt(row.querySelector('.hifz-q-open')?.value, 10) || 0,
                scorePercent: scoreRaw !== '' && scoreRaw != null ? parseFloat(scoreRaw) : null
            });
        });

        if (!questions.length) {
            global.showToast('أضف سؤال مراجعة واحد على الأقل', 'error');
            return;
        }

        const btn = document.querySelector('#addMemModal .btn-primary');
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await global.apiFetch('/oral-exams', 'POST', {
                studentId,
                kind,
                scopeLabel,
                maxOpeningsBeforeFail: maxOpenings,
                circleId,
                notes: notes || null,
                questions
            });
            global.closeModal('addMemModal');
            global.showToast('تم حفظ اختبار الأسئلة بنجاح', 'success');
            if (typeof global.fetchMemorizationData === 'function') global.fetchMemorizationData();
        } catch (e) {
            if (global.handleApiError) global.handleApiError(e);
            else global.showToast(e.message || 'حدث خطأ أثناء الحفظ', 'error');
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
                    if (studentSel) {
                        studentSel.value = String(studentId);
                        await onHifzStudentChange();
                    }
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
