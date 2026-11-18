/**
 * منصة نور — جلسات تقدم الحفظ والمراجعة
 */
(function (global) {
    'use strict';

    const U = () => global.NoorUtils;
    let sessionType = 'Memorization';
    let revisionSubMode = 'Sequential';
    let questionCounter = 0;

    let _oralMaxOpenings = 1;

    function initHifzModal() {
        const modal = document.getElementById('addMemModal');
        if (!modal || modal.dataset.hifzInit === '1') return;
        modal.dataset.hifzInit = '1';

        populateCircleSelect();
        populateMatnSelect();
        loadOralMaxOpenings();
        bindHifzModalEvents();
        resetHifzForm();
    }

    /**
     * سلّم التقديرات المعتمد: ممتاز 85–100، جيد جداً 75–84، جيد 65–74، مقبول 50–64 (للمراجعة فقط)،
     * ضعيف ويحتاج مراجعة 49 فأقل (جلسة راسبة تُعاد). الحفظ الجديد بلا «مقبول».
     */
    const Grades = (function () {
        const EXCELLENT = 'ممتاز', VERY_GOOD = 'جيد جداً', GOOD = 'جيد', ACCEPTABLE = 'مقبول', WEAK = 'ضعيف ويحتاج مراجعة';
        const MEMO = [EXCELLENT, VERY_GOOD, GOOD, WEAK];
        const REV = [EXCELLENT, VERY_GOOD, GOOD, ACCEPTABLE, WEAK];
        const RANGES = { [EXCELLENT]: '85–100%', [VERY_GOOD]: '75–84%', [GOOD]: '65–74%', [ACCEPTABLE]: '50–64%' };

        function list(type) { return type === 'Revision' ? REV : MEMO; }

        function fromPercent(p, type) {
            if (p >= 85) return EXCELLENT;
            if (p >= 75) return VERY_GOOD;
            if (p >= 65) return GOOD;
            if (p >= 50 && type === 'Revision') return ACCEPTABLE;
            return WEAK;
        }

        /** يحوّل التقديرات القديمة («ضعيف»، «يحتاج مراجعة»، «جيد جدًا») إلى الاسم المعتمد. */
        function normalize(ev) {
            const e = (ev || '').replace(/[\u064B-\u065F\u0670]/g, '').trim();
            if (!e) return '';
            if (e.includes('ضعيف') || e.includes('يحتاج مراجع')) return WEAK;
            if (e.includes('ممتاز')) return EXCELLENT;
            if (e.startsWith('جيد جدا')) return VERY_GOOD;
            if (e.includes('مقبول')) return ACCEPTABLE;
            if (e === 'جيد') return GOOD;
            return ev;
        }

        function fillSelect(select, type, current) {
            if (!select) return;
            const keep = normalize(current !== undefined ? current : select.value);
            const items = list(type).slice();
            if (keep && !items.includes(keep)) items.push(keep); // قيمة قديمة غير معتمدة لهذا النوع
            select.innerHTML = items.map(g =>
                `<option value="${g}">${g}${RANGES[g] ? ' (' + RANGES[g] + ')' : g === WEAK ? ' — جلسة راسبة' : ''}</option>`).join('');
            select.value = keep && items.includes(keep) ? keep : EXCELLENT;
        }

        function applyPercent() {
            const input = document.getElementById('hifzPercent');
            const select = document.getElementById('hifzEvaluation');
            const hint = document.getElementById('hifzGradeHint');
            if (!input || !select) return;
            const raw = input.value.trim();
            if (raw === '') { if (hint) hint.textContent = ''; return; }
            const p = Math.max(0, Math.min(100, parseFloat(raw)));
            if (!Number.isFinite(p)) return;
            const g = fromPercent(p, sessionType);
            select.value = g;
            if (hint) {
                hint.textContent = g === WEAK
                    ? 'جلسة راسبة: لا تُحتسب ولا تمنح نقاطاً، ويُعيدها الطالب لاحقاً'
                    : 'التقدير: ' + g;
                hint.classList.toggle('is-fail', g === WEAK);
            }
        }

        return { MEMO, REV, EXCELLENT, VERY_GOOD, GOOD, ACCEPTABLE, WEAK, list, fromPercent, normalize, fillSelect, applyPercent };
    })();
    global.NoorGrades = Grades;

    async function loadOralMaxOpenings() {
        try {
            const d = await global.apiFetch('/pedagogical/grading-defaults');
            _oralMaxOpenings = d?.oralMaxOpeningsBeforeFail ?? 1;
            const field = document.getElementById('hifzOralMaxOpenings');
            if (field) field.value = _oralMaxOpenings;
            // المتون غير مفعّلة في إعدادات المركز ← يختفي خيار تسجيل المتن مع الجلسة
            const matnGroup = document.getElementById('hifzMatnGroup');
            if (matnGroup) matnGroup.style.display = d?.enableMatn === false ? 'none' : '';
            if (d?.enableMatn === false) {
                const cb = document.getElementById('hifzIncludeMatn');
                if (cb) cb.checked = false;
            }
        } catch (e) {
            _oralMaxOpenings = 1;
        }
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
        const oralQuestionsList = document.getElementById('hifzQuestionsList');
        if (oralQuestionsList) {
            oralQuestionsList.addEventListener('input', function (e) {
                if (e.target.classList.contains('hifz-q-open')) {
                    checkQuestionCancelled(e.target.closest('.hifz-question-row'));
                }
            });
        }
        const maxOpeningsField = document.getElementById('hifzOralMaxOpenings');
        if (maxOpeningsField) {
            maxOpeningsField.addEventListener('input', function () {
                document.querySelectorAll('#hifzQuestionsList .hifz-question-row').forEach(checkQuestionCancelled);
            });
        }

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

        // ─── اختيار الآيات بنصّها (رواية قالون، دون إنترنت) ───
        const memFromSurah = document.getElementById('hifzFromSurah');
        const memToSurah = document.getElementById('hifzToSurah');
        const memFromVerse = document.getElementById('hifzFromVerse');
        const memToVerse = document.getElementById('hifzToVerse');
        if (memFromSurah) memFromSurah.addEventListener('change', () => populateMemVerseSelects(1, null));
        if (memToSurah) memToSurah.addEventListener('change', () => populateMemVerseSelects(memFromVerse?.value, null));
        if (showTo) showTo.addEventListener('change', () => populateMemVerseSelects(memFromVerse?.value, null));
        if (memFromVerse) memFromVerse.addEventListener('change', () => {
            // في السورة الواحدة: النهاية لا تسبق البداية
            if (!isMultiSurah() && memToVerse && parseInt(memToVerse.value, 10) < parseInt(memFromVerse.value, 10))
                memToVerse.value = memFromVerse.value;
            clearUserEdited();
            autoFillVerseText();
        });
        if (memToVerse) memToVerse.addEventListener('change', () => { clearUserEdited(); autoFillVerseText(); });
        const search = document.getElementById('hifzVerseSearch');
        if (search) {
            let t = null;
            search.addEventListener('input', () => { clearTimeout(t); t = setTimeout(renderVerseSearch, 200); });
        }

        // تتبع التعديل اليدوي: إن كتب المحفّظ بنفسه بهذين الحقلين، لا نستبدل ما كتبه لاحقًا
        const startInput = document.getElementById('hifzStartText');
        const endInput = document.getElementById('hifzEndText');
        if (startInput) startInput.addEventListener('input', () => { startInput.dataset.userEdited = '1'; });
        if (endInput) endInput.addEventListener('input', () => { endInput.dataset.userEdited = '1'; });
    }

    // ════════════ اختيار الآيات بنصّها ════════════
    function isMultiSurah() {
        return !!document.getElementById('hifzShowToSurah')?.checked;
    }

    function surahVerseCount(surahName) {
        const surah = U().SURAHS.find(s => s.name === surahName);
        return surah ? surah.v : 1;
    }

    function clearUserEdited() {
        ['hifzStartText', 'hifzEndText'].forEach(id => { const el = document.getElementById(id); if (el) delete el.dataset.userEdited; });
    }

    /** خيارات الآيات «رقم — أول كلمات الآية»؛ وإن تعذّر تحميل النص فبالأرقام فقط. */
    async function verseOptions(surahName, selected) {
        let html = global.NoorQuranText ? await global.NoorQuranText.verseOptionsHtml(surahName, selected) : '';
        if (!html) {
            const total = surahVerseCount(surahName);
            for (let i = 1; i <= total; i++) html += `<option value="${i}"${String(i) === String(selected) ? ' selected' : ''}>آية ${i}</option>`;
        }
        return html;
    }

    /** يملأ قائمتي «من آية» و«إلى آية» حسب السورة (وإلى سورة عند تعدد السور). */
    async function populateMemVerseSelects(fromVerse, toVerse) {
        const fromSurah = document.getElementById('hifzFromSurah')?.value;
        const fromSel = document.getElementById('hifzFromVerse');
        const toSel = document.getElementById('hifzToVerse');
        if (!fromSurah || !fromSel || !toSel) return;
        const toSurah = isMultiSurah() ? (document.getElementById('hifzToSurah')?.value || fromSurah) : fromSurah;
        const from = Math.min(parseInt(fromVerse, 10) || 1, surahVerseCount(fromSurah));
        let to = parseInt(toVerse, 10);
        if (!to) to = toSurah === fromSurah ? from : 1;
        to = Math.min(to, surahVerseCount(toSurah));
        fromSel.innerHTML = await verseOptions(fromSurah, from);
        toSel.innerHTML = await verseOptions(toSurah, to);
        fromSel.value = String(from);
        toSel.value = String(to);
        clearUserEdited();
        autoFillVerseText();
    }

    /** البحث بكلمة في السورة المختارة (و«إلى سورة» عند تعدد السور) مع زرين: بداية / نهاية. */
    async function renderVerseSearch() {
        const box = document.getElementById('hifzVerseResults');
        const q = document.getElementById('hifzVerseSearch')?.value || '';
        if (!box) return;
        if (!q.trim() || !global.NoorQuranText) { box.innerHTML = ''; return; }
        const fromSurah = document.getElementById('hifzFromSurah')?.value;
        const toSurah = isMultiSurah() ? document.getElementById('hifzToSurah')?.value : null;
        const groups = [];
        groups.push({ surah: fromSurah, items: await global.NoorQuranText.searchInSurah(fromSurah, q) });
        if (toSurah && toSurah !== fromSurah) groups.push({ surah: toSurah, items: await global.NoorQuranText.searchInSurah(toSurah, q) });
        const esc = U().escapeHtml;
        const rows = [];
        groups.forEach(g => g.items.slice(0, 8).forEach(v => {
            const canStart = g.surah === fromSurah;
            const canEnd = g.surah === (toSurah || fromSurah);
            rows.push(`<div class="verse-result">
                <span class="verse-result-num">${groups.length > 1 ? esc(g.surah) + ' ' : ''}${v.number}</span>
                <span class="verse-result-text quran-text">${esc(v.text)}</span>
                <span class="verse-result-actions">
                    ${canStart ? `<button type="button" class="btn btn-outline" onclick="NoorHifz.pickVerse('from', ${v.number})">بداية</button>` : ''}
                    ${canEnd ? `<button type="button" class="btn btn-outline" onclick="NoorHifz.pickVerse('to', ${v.number})">نهاية</button>` : ''}
                </span>
            </div>`);
        }));
        box.innerHTML = rows.length ? rows.join('') : '<div class="verse-result-empty">لا توجد آية بهذه الكلمة في السورة المختارة</div>';
    }

    function pickVerse(which, number) {
        const sel = document.getElementById(which === 'from' ? 'hifzFromVerse' : 'hifzToVerse');
        if (!sel) return;
        sel.value = String(number);
        sel.dispatchEvent(new Event('change'));
        global.showToast(which === 'from' ? `تم اختيار آية البداية: ${number}` : `تم اختيار آية النهاية: ${number}`, 'success');
    }

    /** فتح صفحة المصحف عند السورة والآية المختارتين. */
    function openMushafAtSelection(mode) {
        if (!global.NoorMushaf) return;
        const ids = mode === 'seq' ? ['hifzSeqFromSurah', 'hifzSeqFromVerse'] : ['hifzFromSurah', 'hifzFromVerse'];
        const surahName = document.getElementById(ids[0])?.value;
        const verse = parseInt(document.getElementById(ids[1])?.value, 10) || 1;
        const surah = U().SURAHS.find(s => s.name === surahName);
        global.NoorMushaf.open({ surah: surah ? surah.n : 1, verse });
    }

    async function autoFillVerseText() {
        const startInput = document.getElementById('hifzStartText');
        const endInput = document.getElementById('hifzEndText');
        if (!startInput && !endInput) return;
        if (!global.NoorQuranText) return; // المصدر غير محمّل بالصفحة

        const surahName = document.getElementById('hifzFromSurah')?.value;
        const endSurahName = isMultiSurah() ? (document.getElementById('hifzToSurah')?.value || surahName) : surahName;
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
                const endText = await global.NoorQuranText.fetchVerseTextByName(endSurahName, toVerse);
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
            if (fromSurahSel && pos.surahName) {
                fromSurahSel.value = pos.surahName;
            }
            await populateMemVerseSelects(pos.nextVerse || 1, pos.nextVerse || 1);

            if (hintEl) {
                const crossedSurah = pos.lastSurahName !== pos.surahName;
                hintEl.textContent = crossedSurah
                    ? `آخر توقف: نهاية سورة ${pos.lastSurahName} (الآية ${pos.lastVerse}) بتاريخ ${pos.lastSessionDate}. تم اقتراح البدء من سورة ${pos.surahName} — الآية ${pos.nextVerse} (سورة جديدة).`
                    : `آخر توقف: سورة ${pos.lastSurahName} — الآية ${pos.lastVerse} (بتاريخ ${pos.lastSessionDate}). تم اقتراح البدء من الآية ${pos.nextVerse}.`;
            }

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

    async function populateSeqVerseSelect(suggestedVerse) {
        const surahName = document.getElementById('hifzSeqFromSurah')?.value;
        const verseSel = document.getElementById('hifzSeqFromVerse');
        if (!verseSel || !surahName) return;
        verseSel.innerHTML = await verseOptions(surahName, suggestedVerse || 1);
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
    async function populateSeqToVerseSelect() {
        const surahName = document.getElementById('hifzSeqToSurah')?.value;
        const verseSel = document.getElementById('hifzSeqToVerse');
        if (!verseSel || !surahName) return;
        const total = surahVerseCount(surahName);
        // افتراضيًا: آخر آية بالسورة (يمكن تغييرها لو توقف بالمنتصف)
        verseSel.innerHTML = await verseOptions(surahName, total);
        verseSel.value = String(total);
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

        Grades.fillSelect(document.getElementById('hifzEvaluation'), sessionType);
        Grades.applyPercent();

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
                <label class="form-label">درجة % (تُحسب تلقائياً إن تُركت فارغة)</label>
                <input class="form-input hifz-q-score" type="number" min="0" max="100" placeholder="تلقائي">
            </div>
            <div class="form-group full" style="text-align:left">
                <button type="button" class="btn btn-outline" style="font-size:12px" data-remove-q="${id}">${window.Icon ? window.Icon('trash-2', { size: 12 }) : ''} حذف السؤال</button>
            </div>`;
        container.appendChild(row);
        row.querySelector('[data-remove-q]').addEventListener('click', () => row.remove());
        checkQuestionCancelled(row);
    }

    function checkQuestionCancelled(row) {
        if (!row) return;
        const openInput = row.querySelector('.hifz-q-open');
        const maxAllowed = parseInt(document.getElementById('hifzOralMaxOpenings')?.value, 10) || _oralMaxOpenings;
        const openings = parseInt(openInput?.value, 10) || 0;
        const cancelled = openings > maxAllowed;

        let badge = row.querySelector('.hifz-q-cancelled-badge');
        if (cancelled) {
            row.style.background = 'rgba(220,38,38,0.06)';
            row.style.borderRadius = '8px';
            if (!badge) {
                badge = document.createElement('div');
                badge.className = 'hifz-q-cancelled-badge full';
                badge.style.cssText = 'grid-column:1/-1;color:#dc2626;font-size:12px;font-weight:700;padding:4px 0';
                badge.textContent = 'هذا السؤال مُلغى — تجاوز الحد المسموح من الفتحات';
                row.appendChild(badge);
            }
        } else {
            row.style.background = '';
            if (badge) badge.remove();
        }
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

    /** اليوم بتوقيت ليبيا بصيغة YYYY-MM-DD. */
    function libyaTodayYmd() {
        const d = new Date(Date.now() + 2 * 3600 * 1000);
        return d.toISOString().slice(0, 10);
    }

    function setSessionDate(ymd) {
        const el = document.getElementById('hifzSessionDate');
        if (!el) return;
        el.max = libyaTodayYmd();
        if (el._flatpickr) { el._flatpickr.set('maxDate', el.max); el._flatpickr.setDate(ymd, false); }
        else el.value = ymd;
    }

    /** تاريخ الجلسة المختار: اليوم = الوقت الحالي، وتاريخ سابق = منتصف ذلك اليوم (تجنّباً لانزياح المنطقة الزمنية). */
    function readSessionDate() {
        const ymd = document.getElementById('hifzSessionDate')?.value;
        const today = libyaTodayYmd();
        // الوقت الحالي بتوقيت ليبيا دون لاحقة Z — يُخزَّن كما هو فلا تنتقل جلسة ما بعد منتصف الليل لليوم السابق
        if (!ymd || ymd === today) return { iso: new Date(Date.now() + 2 * 3600 * 1000).toISOString().slice(0, 19), ymd: today };
        if (ymd > today) return { error: 'لا يمكن تسجيل جلسة بتاريخ مستقبلي' };
        return { iso: ymd + 'T12:00:00', ymd };
    }

    function resetHifzForm() {
        questionCounter = 0;
        setSessionDate(libyaTodayYmd());
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
        const verseSearch = document.getElementById('hifzVerseSearch');
        if (verseSearch) verseSearch.value = '';
        const verseResults = document.getElementById('hifzVerseResults');
        if (verseResults) verseResults.innerHTML = '';
        populateMemVerseSelects(1, 1);
        const pct = document.getElementById('hifzPercent');
        if (pct) pct.value = '';
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

        const sessionDate = readSessionDate();
        if (sessionDate.error) {
            global.showToast(sessionDate.error, 'error');
            return;
        }

        const evaluation = document.getElementById('hifzEvaluation')?.value || Grades.GOOD;
        const notes = document.getElementById('hifzNotes')?.value?.trim() || '';

        // وضع "الأسئلة" يُحفظ بمسار منفصل تمامًا (/oral-exams) — يخرج مبكرًا هنا
        if (sessionType === 'Revision' && revisionSubMode === 'Questions') {
            await saveOralExamFromHifzModal(studentId, notes, sessionDate.ymd);
            return;
        }

        let body = {
            studentId,
            type: sessionType,
            evaluation,
            notes,
            date: sessionDate.iso
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
                        const dup = matnErr?.status === 409;
                        global.showToast(dup
                            ? 'تم حفظ التسميع. ' + matnErr.message
                            : 'تم حفظ التسميع، لكن تعذر حفظ سجل المتن', 'warning');
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

    async function saveOralExamFromHifzModal(studentId, notes, dateYmd) {
        const kind = document.getElementById('hifzOralKind')?.value || 'AthmanSampling';
        const maxOpenings = parseInt(document.getElementById('hifzOralMaxOpenings')?.value, 10) || 1;
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
                date: dateYmd || null,
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
        const list = document.getElementById('surahList');
        const plan = document.getElementById('memPlanBody');
        if (!id) {
            const header = document.getElementById('memProgressTitle');
            const sub = document.getElementById('memProgressSub');
            const pct = document.getElementById('memProgressPct');
            const fill = document.getElementById('memProgressFill');
            const badge = document.getElementById('memProgressBadge');
            if (header) header.textContent = 'تقدم الحفظ';
            if (sub) sub.textContent = 'اختر طالباً لعرض التقدم وخريطة السور';
            if (pct) pct.textContent = '0%';
            if (fill) fill.style.width = '0%';
            if (badge) badge.textContent = '—';
            if (list) list.innerHTML = '<div class="mem-empty">اختر طالباً من القائمة لعرض السور المحفوظة والمتبقية</div>';
            if (plan) plan.innerHTML = '<p>تظهر هنا خطة مبسطة بعد اختيار الطالب حسب نسبة حفظه الحالية.</p>';
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
        if (plan) {
            const remain = Math.max(0, 100 - progress);
            plan.innerHTML = `
                <p><span data-icon="target" data-icon-size="14"></span> الهدف الأسبوعي: <strong style="color:var(--text)">نصف حزب تقريباً</strong></p>
                <p><span data-icon="trending-up" data-icon-size="14"></span> المتبقي من المصحف: <strong style="color:var(--text)">${remain}%</strong></p>
                <p><span data-icon="zap" data-icon-size="14"></span> المستوى الحالي: <strong style="color:var(--green)">${progress >= 70 ? 'ممتاز' : progress >= 40 ? 'جيد' : 'يحتاج متابعة'}</strong></p>`;
            if (global.renderNoorIcons) global.renderNoorIcons(plan);
        }
        loadStudentHifzMap(id);
    }

    function renderMemorizationSurahList(hifzRecords) {
        const list = document.getElementById('surahList');
        if (!list) return;
        const memorizedSurahs = {};
        (hifzRecords || []).forEach(r => {
            if (r.type === 'Memorization' || r.type === 0) {
                const key = r.surahName;
                if (!memorizedSurahs[key]) memorizedSurahs[key] = 0;
                memorizedSurahs[key] += r.verseCount || (global.NoorUtils && global.NoorUtils.parseVerseCount ? global.NoorUtils.parseVerseCount(r.verses || '') : 0);
            }
        });
        const surahs = window.SURAHS || [];
        if (!surahs.length) {
            list.innerHTML = '<div class="mem-empty">لا توجد قائمة سور</div>';
            return;
        }
        list.innerHTML = '<div class="mem-surah-grid">' + surahs.map(s => {
            const memorized = memorizedSurahs[s.name] || 0;
            let cls = 'pending';
            let label = 'لم يُحفظ';
            const pct = memorized > 0 ? Math.min(Math.round(memorized / s.v * 100), 100) : 0;
            if (memorized >= s.v) { cls = 'done'; label = 'محفوظ'; }
            else if (memorized > 0) { cls = 'partial'; label = pct + '%'; }
            return `<div class="mem-surah-cell ${cls}" title="${s.name}: ${memorized}/${s.v}">
                <strong>${s.name}</strong>
                <span>${label}</span>
            </div>`;
        }).join('') + '</div>';
    }

    async function loadStudentHifzMap(studentId) {
        const list = document.getElementById('surahList');
        if (list) list.innerHTML = '<div class="mem-empty">جاري تحميل خريطة الحفظ...</div>';
        try {
            const records = await global.apiFetch(`/hifz/student/${studentId}`);
            renderMemorizationSurahList(records);
            if (typeof global.renderQuranMap === 'function') global.renderQuranMap(records);
        } catch (e) {
            if (list) list.innerHTML = '<div class="mem-empty">تعذر تحميل سجل الحفظ</div>';
        }
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
        pickVerse,
        openMushafAtSelection,
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
