// pedagogical.js — الاختبارات الشفوية والتقييم التربوي
(function (global) {
    'use strict';

    let _oralQIndex = 0;
    let _currentPeriodId = null;
    let _activeTab = 'matn';

    function todayYmd() {
        const d = new Date();
        return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    }

    function kindLabel(k) {
        if (k === 'FullRecitation') return 'سرد كامل';
        if (k === 'AthmanSampling') return 'تسمية أثمان';
        return k || '—';
    }

    async function loadStudentOptions(selectId) {
        const sel = document.getElementById(selectId);
        if (!sel) return;
        try {
            const students = await apiFetch('/students');
            const keep = sel.options[0]?.outerHTML || '<option value="">— اختر طالباً —</option>';
            sel.innerHTML = keep + (students || []).map(s =>
                `<option value="${s.id}">${escapeHtml(s.fullName)}</option>`).join('');
        } catch (e) {
            handleApiError(e, { silent: true });
        }
    }

    async function loadCircleOptions(selectId, officialOnly) {
        const sel = document.getElementById(selectId);
        if (!sel) return;
        try {
            const circles = await apiFetch('/circles');
            const list = officialOnly ? (circles || []).filter(c => !c.isExtra) : (circles || []);
            const keep = sel.options[0]?.outerHTML || '<option value="">— اختياري —</option>';
            sel.innerHTML = keep + list.map(c =>
                `<option value="${c.id}">${escapeHtml(c.name)}</option>`).join('');
        } catch (e) {
            handleApiError(e, { silent: true });
        }
    }

    // ════════════════════════════════════════════════════════
    // Tabs / page entry
    // ════════════════════════════════════════════════════════

    function showPedagoTab(name) {
        _activeTab = name || 'matn';
        document.querySelectorAll('.pedago-tab-btn').forEach(btn => {
            const on = btn.dataset.tab === _activeTab;
            btn.classList.toggle('btn-primary', on);
            btn.classList.toggle('btn-outline', !on);
        });
        document.querySelectorAll('.pedago-tab-panel').forEach(p => {
            p.style.display = p.dataset.tab === _activeTab ? '' : 'none';
        });
        loadPedagoTab(_activeTab);
    }

    async function fetchPedagogicalPage(tab) {
        if (tab === 'oral') {
            await renderOralPage();
            return;
        }
        if (tab === 'prayer') {
            await renderStudentPrayerPage();
            return;
        }
        if (tab === 'parentHome') {
            await renderParentHomePage();
            return;
        }
        if (tab === 'myEvaluations') {
            await renderMyEvaluationsPage();
            return;
        }
        showPedagoTab(tab || 'matn');
    }

    function loadPedagoTab(name) {
        switch (name) {
            case 'matn': return renderMatnSection();
            case 'targets': return renderTargetsSection();
            case 'periods': return renderPeriodsSection();
            case 'dress': return renderDressSection();
            case 'prayer': return renderStaffPrayerSection();
            default: return renderMatnSection();
        }
    }

    // ════════════════════════════════════════════════════════
    // 1. Oral exams
    // ════════════════════════════════════════════════════════

    async function renderOralPage() {
        const list = document.getElementById('oralExamsList');
        const formWrap = document.getElementById('oralExamForm');
        if (formWrap && !formWrap.dataset.ready) {
            formWrap.dataset.ready = '1';
            formWrap.innerHTML = `
                <div class="chart-card" style="margin-bottom:20px">
                    <div class="card-header"><h3>تسجيل اختبار شفوي</h3></div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الطالب *</label>
                            <select id="oralStudentId" class="form-input"><option value="">— اختر طالباً —</option></select>
                        </div>
                        <div class="form-group">
                            <label class="form-label">النوع *</label>
                            <select id="oralKind" class="form-input">
                                <option value="FullRecitation">سرد كامل</option>
                                <option value="AthmanSampling" selected>تسمية أثمان</option>
                            </select>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">النطاق / الوصف</label>
                            <input id="oralScope" class="form-input" type="text" placeholder="مثال: الأثمان 1–5 من البقرة">
                        </div>
                        <div class="form-group">
                            <label class="form-label">أقصى فتح قبل الرسوب</label>
                            <input id="oralMaxOpenings" class="form-input" type="number" min="1" value="3">
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">التاريخ</label>
                            <input id="oralDate" class="form-input" type="date" value="${todayYmd()}">
                        </div>
                        <div class="form-group">
                            <label class="form-label">الحلقة (اختياري)</label>
                            <select id="oralCircleId" class="form-input"><option value="">— اختياري —</option></select>
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظات</label>
                        <input id="oralNotes" class="form-input" type="text" placeholder="اختياري">
                    </div>
                    <div style="display:flex;justify-content:space-between;align-items:center;margin:12px 0 8px">
                        <strong>الأسئلة</strong>
                        <button type="button" class="btn btn-outline" style="font-size:12px;padding:6px 12px" onclick="addOralQuestionRow()">+ إضافة سؤال</button>
                    </div>
                    <div id="oralQuestionsRows"></div>
                    <div style="margin-top:14px">
                        <button type="button" class="btn btn-primary" onclick="saveOralExam()">${window.Icon ? window.Icon('save', { size: 14 }) : ''} حفظ الاختبار</button>
                    </div>
                </div>`;
            _oralQIndex = 0;
            addOralQuestionRow();
            await Promise.all([loadStudentOptions('oralStudentId'), loadCircleOptions('oralCircleId')]);
        }
        if (!list) return;
        list.innerHTML = '<p style="text-align:center;padding:24px;color:var(--text-muted)">جاري التحميل...</p>';
        try {
            const data = await apiFetch('/oral-exams');
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد جلسات بعد</p>';
                return;
            }
            list.innerHTML = data.map(s => `
                <div class="chart-card" style="margin-bottom:12px">
                    <div class="card-header">
                        <div>
                            <h3>${escapeHtml(s.studentName || '')}</h3>
                            <p>${formatDateEnGb(s.date)} · ${escapeHtml(kindLabel(s.kind))} · ${escapeHtml(s.scopeLabel || '')}</p>
                        </div>
                        <span class="status-badge status-excellent">${s.overallPercent ?? 0}% — ${escapeHtml(s.overallGrade || '')}</span>
                    </div>
                    <div style="display:flex;gap:16px;margin-top:8px;font-size:13px;color:var(--text-muted)">
                        <span>أسئلة: ${s.questionsCount ?? 0}</span>
                        <span>${s.isConsideredMemorized ? 'يُعتبر حافظاً ✓' : 'لم يُعتبر حافظاً'}</span>
                        ${s.circleName ? `<span>${escapeHtml(s.circleName)}</span>` : ''}
                    </div>
                    <div style="margin-top:10px">
                        <button class="btn btn-delete" style="font-size:12px;padding:6px 12px" onclick="deleteOralExam(${s.id})">${window.Icon ? window.Icon('trash-2', { size: 12 }) : ''} حذف</button>
                    </div>
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
            list.innerHTML = '<p style="color:#dc2626;text-align:center;padding:20px">تعذر تحميل الجلسات</p>';
        }
    }

    function addOralQuestionRow() {
        const wrap = document.getElementById('oralQuestionsRows');
        if (!wrap) return;
        const i = _oralQIndex++;
        const row = document.createElement('div');
        row.className = 'oral-q-row';
        row.style.cssText = 'display:grid;grid-template-columns:2fr 1fr 1fr 1fr 1fr auto;gap:8px;align-items:end;margin-bottom:8px';
        row.innerHTML = `
            <div class="form-group" style="margin:0">
                <label class="form-label">التسمية</label>
                <input class="form-input oq-label" type="text" placeholder="سؤال ${i + 1}">
            </div>
            <div class="form-group" style="margin:0">
                <label class="form-label">تردد</label>
                <input class="form-input oq-hes" type="number" min="0" value="0">
            </div>
            <div class="form-group" style="margin:0">
                <label class="form-label">تنبيه</label>
                <input class="form-input oq-alert" type="number" min="0" value="0">
            </div>
            <div class="form-group" style="margin:0">
                <label class="form-label">فتح</label>
                <input class="form-input oq-open" type="number" min="0" value="0">
            </div>
            <div class="form-group" style="margin:0">
                <label class="form-label">درجة %</label>
                <input class="form-input oq-score" type="number" min="0" max="100" placeholder="تلقائي">
            </div>
            <button type="button" class="btn btn-outline" style="padding:8px" onclick="this.closest('.oral-q-row').remove()" title="حذف">×</button>`;
        wrap.appendChild(row);
    }

    async function saveOralExam() {
        const studentId = parseInt(document.getElementById('oralStudentId')?.value, 10);
        const kind = document.getElementById('oralKind')?.value;
        if (!studentId || !kind) {
            showToast('اختر الطالب ونوع الاختبار', 'error');
            return;
        }
        const rows = document.querySelectorAll('#oralQuestionsRows .oral-q-row');
        const questions = [];
        rows.forEach(r => {
            const scoreRaw = r.querySelector('.oq-score')?.value;
            questions.push({
                label: r.querySelector('.oq-label')?.value?.trim() || '',
                hesitationCount: parseInt(r.querySelector('.oq-hes')?.value, 10) || 0,
                alertCount: parseInt(r.querySelector('.oq-alert')?.value, 10) || 0,
                openingCount: parseInt(r.querySelector('.oq-open')?.value, 10) || 0,
                scorePercent: scoreRaw !== '' && scoreRaw != null ? parseFloat(scoreRaw) : null
            });
        });
        if (!questions.length) {
            showToast('أضف سؤالاً واحداً على الأقل', 'warning');
            return;
        }
        const circleRaw = document.getElementById('oralCircleId')?.value;
        try {
            await apiFetch('/oral-exams', 'POST', {
                studentId,
                kind,
                scopeLabel: document.getElementById('oralScope')?.value?.trim() || '',
                maxOpeningsBeforeFail: parseInt(document.getElementById('oralMaxOpenings')?.value, 10) || 3,
                date: document.getElementById('oralDate')?.value || null,
                circleId: circleRaw ? parseInt(circleRaw, 10) : null,
                notes: document.getElementById('oralNotes')?.value?.trim() || null,
                questions
            });
            showToast('تم تسجيل الاختبار الشفوي', 'success');
            const form = document.getElementById('oralExamForm');
            if (form) form.dataset.ready = '';
            await renderOralPage();
        } catch (err) {
            handleApiError(err);
        }
    }

    async function deleteOralExam(id) {
        if (!confirm('حذف جلسة الاختبار الشفوي؟')) return;
        try {
            await apiFetch(`/oral-exams/${id}`, 'DELETE');
            showToast('تم الحذف', 'success');
            await renderOralPage();
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 2. Matn
    // ════════════════════════════════════════════════════════

    async function renderMatnSection() {
        const el = document.getElementById('pedagoMatn');
        if (!el) return;
        if (!el.dataset.ready) {
            el.dataset.ready = '1';
            el.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>تسجيل متن</h3></div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الطالب *</label>
                            <select id="matnStudentId" class="form-input" onchange="loadMatnList()"><option value="">— اختر طالباً —</option></select>
                        </div>
                        <div class="form-group">
                            <label class="form-label">التاريخ</label>
                            <input id="matnDate" class="form-input" type="date" value="${todayYmd()}">
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">اسم المتن *</label>
                            <input id="matnName" class="form-input" type="text" placeholder="مثال: تحفة الأطفال">
                        </div>
                        <div class="form-group">
                            <label class="form-label">الجزء</label>
                            <input id="matnPortion" class="form-input" type="text" placeholder="من ... إلى ...">
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">النوع</label>
                            <select id="matnType" class="form-input">
                                <option value="Memorization">حفظ</option>
                                <option value="Revision">مراجعة</option>
                            </select>
                        </div>
                        <div class="form-group">
                            <label class="form-label">التقييم</label>
                            <select id="matnEval" class="form-input">
                                <option>ممتاز</option><option>جيد جداً</option><option>جيد</option><option>مقبول</option><option>ضعيف</option>
                            </select>
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظات</label>
                        <input id="matnNotes" class="form-input" type="text">
                    </div>
                    <button class="btn btn-primary" onclick="saveMatnRecord()">${window.Icon ? window.Icon('save', { size: 14 }) : ''} حفظ</button>
                </div>
                <div id="matnList"></div>`;
            await loadStudentOptions('matnStudentId');
        }
        await loadMatnList();
    }

    async function loadMatnList() {
        const sid = parseInt(document.getElementById('matnStudentId')?.value, 10);
        const list = document.getElementById('matnList');
        if (!list) return;
        if (!sid) { list.innerHTML = ''; return; }
        try {
            const data = await apiFetch(`/pedagogical/matn?studentId=${sid}`);
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:20px">لا توجد سجلات</p>';
                return;
            }
            list.innerHTML = data.map(m => `
                <div class="chart-card" style="margin-bottom:10px;padding:14px">
                    <div style="display:flex;justify-content:space-between;gap:12px">
                        <div>
                            <strong>${escapeHtml(m.matnName)}</strong>
                            <span style="color:var(--text-muted);font-size:12px"> · ${escapeHtml(m.portion || '')}</span>
                            <div style="font-size:12px;color:var(--text-muted);margin-top:4px">${formatDateEnGb(m.date)} · ${m.type === 'Revision' ? 'مراجعة' : 'حفظ'} · ${escapeHtml(m.evaluation || '')}</div>
                        </div>
                        <button class="btn btn-delete" style="font-size:11px;padding:4px 10px" onclick="deleteMatnRecord(${m.id})">حذف</button>
                    </div>
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
            list.innerHTML = '<p style="color:#dc2626">تعذر التحميل</p>';
        }
    }

    async function saveMatnRecord() {
        const studentId = parseInt(document.getElementById('matnStudentId')?.value, 10);
        const matnName = document.getElementById('matnName')?.value?.trim();
        if (!studentId || !matnName) {
            showToast('الطالب واسم المتن مطلوبان', 'error');
            return;
        }
        try {
            await apiFetch('/pedagogical/matn', 'POST', {
                studentId,
                date: document.getElementById('matnDate')?.value || null,
                matnName,
                portion: document.getElementById('matnPortion')?.value?.trim() || '',
                type: document.getElementById('matnType')?.value || 'Memorization',
                evaluation: document.getElementById('matnEval')?.value || '',
                notes: document.getElementById('matnNotes')?.value?.trim() || null
            });
            showToast('تم تسجيل المتن', 'success');
            ['matnName', 'matnPortion', 'matnNotes'].forEach(id => {
                const el = document.getElementById(id); if (el) el.value = '';
            });
            await loadMatnList();
        } catch (err) {
            handleApiError(err);
        }
    }

    async function deleteMatnRecord(id) {
        if (!confirm('حذف سجل المتن؟')) return;
        try {
            await apiFetch(`/pedagogical/matn/${id}`, 'DELETE');
            showToast('تم الحذف', 'success');
            await loadMatnList();
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 3. Monthly targets
    // ════════════════════════════════════════════════════════

    async function renderTargetsSection() {
        const el = document.getElementById('pedagoTargets');
        if (!el) return;
        const now = new Date();
        if (!el.dataset.ready) {
            el.dataset.ready = '1';
            el.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>هدف شهري</h3></div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الطالب *</label>
                            <select id="tgtStudentId" class="form-input" onchange="loadTargetsList()"><option value="">— اختر طالباً —</option></select>
                        </div>
                        <div class="form-group">
                            <label class="form-label">السنة</label>
                            <input id="tgtYear" class="form-input" type="number" value="${now.getFullYear()}">
                        </div>
                        <div class="form-group">
                            <label class="form-label">الشهر</label>
                            <input id="tgtMonth" class="form-input" type="number" min="1" max="12" value="${now.getMonth() + 1}">
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الهدف (أثمان)</label>
                            <input id="tgtTarget" class="form-input" type="number" min="0" value="8">
                        </div>
                        <div class="form-group">
                            <label class="form-label">المُنجز</label>
                            <input id="tgtAchieved" class="form-input" type="number" min="0" value="0">
                        </div>
                    </div>
                    <div class="form-group">
                        <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
                            <input type="checkbox" id="tgtSpecial"> وضع خاص
                        </label>
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظة الوضع الخاص</label>
                        <input id="tgtSpecialNote" class="form-input" type="text">
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظات</label>
                        <input id="tgtNotes" class="form-input" type="text">
                    </div>
                    <button class="btn btn-primary" onclick="saveMonthlyTarget()">حفظ الهدف</button>
                    <button class="btn btn-outline" style="margin-right:8px" onclick="syncMonthlyFromOral()">مزامنة الإنجاز من الشفوي</button>
                </div>
                <div id="targetsList"></div>`;
            await loadStudentOptions('tgtStudentId');
        }
        await loadTargetsList();
    }

    async function loadTargetsList() {
        const sid = parseInt(document.getElementById('tgtStudentId')?.value, 10);
        const list = document.getElementById('targetsList');
        if (!list) return;
        if (!sid) { list.innerHTML = ''; return; }
        try {
            const data = await apiFetch(`/pedagogical/monthly-targets?studentId=${sid}`);
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:20px">لا توجد أهداف</p>';
                return;
            }
            list.innerHTML = data.map(t => `
                <div class="chart-card" style="margin-bottom:10px;padding:14px">
                    <strong>${t.year}/${String(t.month).padStart(2, '0')}</strong>
                    <span style="margin-right:12px">${t.achievedAthmanCount}/${t.targetAthmanCount} أثمان</span>
                    <span class="status-badge status-excellent">${t.progressScoreOutOf10}/10</span>
                    ${t.isSpecialMode ? '<span class="status-badge" style="background:#fef3c7;color:#b45309">وضع خاص</span>' : ''}
                    ${t.notes ? `<div style="font-size:12px;color:var(--text-muted);margin-top:6px">${escapeHtml(t.notes)}</div>` : ''}
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
        }
    }

    async function saveMonthlyTarget() {
        const studentId = parseInt(document.getElementById('tgtStudentId')?.value, 10);
        const year = parseInt(document.getElementById('tgtYear')?.value, 10);
        const month = parseInt(document.getElementById('tgtMonth')?.value, 10);
        const targetAthmanCount = parseInt(document.getElementById('tgtTarget')?.value, 10);
        if (!studentId || !year || !month) {
            showToast('أكمل بيانات الطالب والسنة والشهر', 'error');
            return;
        }
        try {
            await apiFetch('/pedagogical/monthly-targets', 'PUT', {
                studentId, year, month, targetAthmanCount,
                achievedAthmanCount: parseInt(document.getElementById('tgtAchieved')?.value, 10) || 0,
                isSpecialMode: !!document.getElementById('tgtSpecial')?.checked,
                specialModeNote: document.getElementById('tgtSpecialNote')?.value?.trim() || null,
                notes: document.getElementById('tgtNotes')?.value?.trim() || null
            });
            showToast('تم حفظ الهدف الشهري', 'success');
            await loadTargetsList();
        } catch (err) {
            handleApiError(err);
        }
    }

    async function syncMonthlyFromOral() {
        const studentId = parseInt(document.getElementById('tgtStudentId')?.value, 10);
        const year = parseInt(document.getElementById('tgtYear')?.value, 10);
        const month = parseInt(document.getElementById('tgtMonth')?.value, 10);
        const targetAthmanCount = parseInt(document.getElementById('tgtTarget')?.value, 10);
        if (!studentId || !year || !month) {
            showToast('أكمل بيانات الطالب والسنة والشهر', 'error');
            return;
        }
        try {
            const res = await apiFetch('/pedagogical/monthly-targets/sync-from-oral', 'POST', {
                studentId, year, month,
                targetAthmanCount: Number.isFinite(targetAthmanCount) ? targetAthmanCount : null,
                isSpecialMode: !!document.getElementById('tgtSpecial')?.checked,
                specialModeNote: document.getElementById('tgtSpecialNote')?.value?.trim() || null,
                notes: document.getElementById('tgtNotes')?.value?.trim() || null
            });
            if (document.getElementById('tgtAchieved')) {
                document.getElementById('tgtAchieved').value = res.achievedAthmanCount ?? res.achievedFromOral ?? 0;
            }
            showToast(res.message || 'تمت المزامنة', 'success');
            await loadTargetsList();
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 4. Evaluation periods
    // ════════════════════════════════════════════════════════

    async function renderPeriodsSection() {
        const el = document.getElementById('pedagoPeriods');
        if (!el) return;
        if (!el.dataset.ready) {
            el.dataset.ready = '1';
            el.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>فترة تقييم جديدة</h3></div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الاسم *</label>
                            <input id="periodName" class="form-input" type="text" placeholder="تقييم الفصل الأول">
                        </div>
                        <div class="form-group">
                            <label class="form-label">الحلقة</label>
                            <select id="periodCircleId" class="form-input"><option value="">— الكل —</option></select>
                        </div>
                    </div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">من</label>
                            <input id="periodStart" class="form-input" type="date">
                        </div>
                        <div class="form-group">
                            <label class="form-label">إلى</label>
                            <input id="periodEnd" class="form-input" type="date">
                        </div>
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظات</label>
                        <input id="periodNotes" class="form-input" type="text">
                    </div>
                    <button class="btn btn-primary" onclick="saveEvaluationPeriod()">إنشاء الفترة</button>
                </div>
                <div id="periodsList"></div>
                <div id="periodEvalPanel" style="display:none;margin-top:16px"></div>`;
            await loadCircleOptions('periodCircleId');
        }
        await loadPeriodsList();
    }

    async function loadPeriodsList() {
        const list = document.getElementById('periodsList');
        if (!list) return;
        try {
            const data = await apiFetch('/pedagogical/periods');
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:20px">لا توجد فترات</p>';
                return;
            }
            list.innerHTML = data.map(p => `
                <div class="chart-card" style="margin-bottom:10px;padding:14px;cursor:pointer" onclick="openPeriodEvaluations(${p.id}, '${escapeHtml(p.name).replace(/'/g, "\\'")}')">
                    <div style="display:flex;justify-content:space-between">
                        <div>
                            <strong>${escapeHtml(p.name)}</strong>
                            <div style="font-size:12px;color:var(--text-muted);margin-top:4px">
                                ${formatDateEnGb(p.startDate)} → ${formatDateEnGb(p.endDate)}
                                ${p.circleName ? ' · ' + escapeHtml(p.circleName) : ''}
                            </div>
                        </div>
                        <span class="status-badge status-good">${p.evaluationsCount || 0} تقييم</span>
                    </div>
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
        }
    }

    async function saveEvaluationPeriod() {
        const name = document.getElementById('periodName')?.value?.trim();
        const startDate = document.getElementById('periodStart')?.value;
        const endDate = document.getElementById('periodEnd')?.value;
        if (!name || !startDate || !endDate) {
            showToast('الاسم وتواريخ البداية والنهاية مطلوبة', 'error');
            return;
        }
        const circleRaw = document.getElementById('periodCircleId')?.value;
        try {
            await apiFetch('/pedagogical/periods', 'POST', {
                name, startDate, endDate,
                circleId: circleRaw ? parseInt(circleRaw, 10) : null,
                notes: document.getElementById('periodNotes')?.value?.trim() || null
            });
            showToast('تم إنشاء فترة التقييم', 'success');
            document.getElementById('periodName').value = '';
            await loadPeriodsList();
        } catch (err) {
            handleApiError(err);
        }
    }

    async function openPeriodEvaluations(periodId, name) {
        _currentPeriodId = periodId;
        const panel = document.getElementById('periodEvalPanel');
        if (!panel) return;
        panel.style.display = '';
        panel.innerHTML = `
            <div class="chart-card">
                <div class="card-header"><h3>تقييمات: ${escapeHtml(name)}</h3></div>
                <div class="form-row">
                    <div class="form-group">
                        <label class="form-label">الطالب</label>
                        <select id="evalStudentId" class="form-input"><option value="">— اختر —</option></select>
                    </div>
                    <div class="form-group" style="display:flex;align-items:flex-end;gap:8px">
                        <button class="btn btn-outline" onclick="autoDraftEvaluation()">مسودة تلقائية</button>
                        <button class="btn btn-primary" onclick="savePeriodEvaluation()">حفظ التقييم</button>
                    </div>
                </div>
                <div class="form-row">
                    <div class="form-group"><label class="form-label">حضور %</label><input id="evalAtt" class="form-input" type="number" min="0" max="100" value="0"></div>
                    <div class="form-group"><label class="form-label">حفظ %</label><input id="evalHifz" class="form-input" type="number" min="0" max="100" value="0"></div>
                    <div class="form-group"><label class="form-label">مراجعة %</label><input id="evalRev" class="form-input" type="number" min="0" max="100" value="0"></div>
                </div>
                <div class="form-row">
                    <div class="form-group"><label class="form-label">تقدم %</label><input id="evalProg" class="form-input" type="number" min="0" max="100" value="0"></div>
                    <div class="form-group"><label class="form-label">متن %</label><input id="evalMatn" class="form-input" type="number" min="0" max="100" value="0"></div>
                    <div class="form-group"><label class="form-label">لباس %</label><input id="evalDress" class="form-input" type="number" min="0" max="100" value="0"></div>
                </div>
                <div class="form-row">
                    <div class="form-group"><label class="form-label">صلاة (استشاري)</label><input id="evalPrayer" class="form-input" type="number" min="0" max="100" placeholder="—"></div>
                    <div class="form-group"><label class="form-label">بيت (استشاري)</label><input id="evalHome" class="form-input" type="number" min="0" max="100" placeholder="—"></div>
                    <div class="form-group" style="display:flex;align-items:flex-end">
                        <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
                            <input type="checkbox" id="evalIncludeAdv"> تضمين الاستشاري في المجموع
                        </label>
                    </div>
                </div>
                <div class="form-group">
                    <label class="form-label">ملاحظات الشيخ</label>
                    <textarea id="evalNotes" class="form-input" rows="2"></textarea>
                </div>
                <div id="periodEvalsList" style="margin-top:12px"></div>
            </div>`;
        await loadStudentOptions('evalStudentId');
        try {
            const evals = await apiFetch(`/pedagogical/periods/${periodId}/evaluations`);
            const list = document.getElementById('periodEvalsList');
            if (list) {
                list.innerHTML = !evals?.length
                    ? '<p style="color:var(--text-muted);font-size:13px">لا توجد تقييمات محفوظة</p>'
                    : evals.map(e => `
                        <div style="padding:8px 0;border-bottom:1px solid var(--border);font-size:13px;display:flex;justify-content:space-between">
                            <span>${escapeHtml(e.studentName)}</span>
                            <span class="status-badge status-excellent">${e.overallScore}% — ${escapeHtml(e.gradeLabel || '')}</span>
                        </div>`).join('');
            }
        } catch (err) {
            handleApiError(err, { silent: true });
        }
    }

    async function autoDraftEvaluation() {
        const studentId = parseInt(document.getElementById('evalStudentId')?.value, 10);
        if (!_currentPeriodId || !studentId) {
            showToast('اختر الطالب أولاً', 'warning');
            return;
        }
        try {
            const d = await apiFetch(`/pedagogical/periods/${_currentPeriodId}/auto-draft/${studentId}`, 'POST');
            document.getElementById('evalAtt').value = d.attendanceScore ?? 0;
            document.getElementById('evalHifz').value = d.hifzScore ?? 0;
            if (document.getElementById('evalRev')) document.getElementById('evalRev').value = d.revisionScore ?? 0;
            document.getElementById('evalProg').value = d.progressScore ?? 0;
            document.getElementById('evalMatn').value = d.matnScore ?? 0;
            document.getElementById('evalDress').value = d.dressScore ?? 0;
            document.getElementById('evalPrayer').value = d.prayerAdvisoryScore ?? '';
            document.getElementById('evalHome').value = d.parentHomeAdvisoryScore ?? '';
            document.getElementById('evalIncludeAdv').checked = !!d.includeAdvisoryInOverall;
            showToast(d.message || 'تم حساب المسودة', 'success');
        } catch (err) {
            handleApiError(err);
        }
    }

    async function savePeriodEvaluation() {
        const studentId = parseInt(document.getElementById('evalStudentId')?.value, 10);
        if (!_currentPeriodId || !studentId) {
            showToast('اختر الطالب', 'error');
            return;
        }
        const prayerRaw = document.getElementById('evalPrayer')?.value;
        const homeRaw = document.getElementById('evalHome')?.value;
        try {
            await apiFetch(`/pedagogical/periods/${_currentPeriodId}/evaluations`, 'POST', {
                studentId,
                attendanceScore: parseFloat(document.getElementById('evalAtt')?.value) || 0,
                hifzScore: parseFloat(document.getElementById('evalHifz')?.value) || 0,
                revisionScore: parseFloat(document.getElementById('evalRev')?.value) || 0,
                progressScore: parseFloat(document.getElementById('evalProg')?.value) || 0,
                matnScore: parseFloat(document.getElementById('evalMatn')?.value) || 0,
                dressScore: parseFloat(document.getElementById('evalDress')?.value) || 0,
                prayerAdvisoryScore: prayerRaw !== '' ? parseFloat(prayerRaw) : null,
                parentHomeAdvisoryScore: homeRaw !== '' ? parseFloat(homeRaw) : null,
                includeAdvisoryInOverall: !!document.getElementById('evalIncludeAdv')?.checked,
                sheikhNotes: document.getElementById('evalNotes')?.value?.trim() || null
            });
            showToast('تم حفظ تقييم الفترة', 'success');
            const nameEl = document.querySelector('#periodEvalPanel h3');
            const name = nameEl?.textContent?.replace(/^تقييمات:\s*/, '') || '';
            await openPeriodEvaluations(_currentPeriodId, name);
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 5. Dress bulk
    // ════════════════════════════════════════════════════════

    async function renderDressSection() {
        const el = document.getElementById('pedagoDress');
        if (!el) return;
        if (!el.dataset.ready) {
            el.dataset.ready = '1';
            el.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>تسجيل اللباس</h3></div>
                    <div class="form-row">
                        <div class="form-group">
                            <label class="form-label">الحلقة</label>
                            <select id="dressCircleId" class="form-input" onchange="loadDressStudents()"><option value="">— اختر حلقة —</option></select>
                        </div>
                        <div class="form-group">
                            <label class="form-label">التاريخ</label>
                            <input id="dressDate" class="form-input" type="date" value="${todayYmd()}" onchange="loadDressStudents()">
                        </div>
                    </div>
                    <div id="dressRows"></div>
                    <button class="btn btn-primary" style="margin-top:12px" onclick="saveDressBulk()">حفظ اللباس</button>
                </div>`;
            await loadCircleOptions('dressCircleId');
        }
    }

    async function loadDressStudents() {
        const circleId = parseInt(document.getElementById('dressCircleId')?.value, 10);
        const date = document.getElementById('dressDate')?.value;
        const wrap = document.getElementById('dressRows');
        if (!wrap) return;
        if (!circleId) { wrap.innerHTML = ''; return; }
        wrap.innerHTML = '<p style="color:var(--text-muted)">جاري التحميل...</p>';
        try {
            const circle = await apiFetch(`/circles/${circleId}`);
            const list = circle.students || circle.Students || [];
            let existing = [];
            if (date) {
                try { existing = await apiFetch(`/pedagogical/dress?date=${encodeURIComponent(date)}`); } catch { /* ignore */ }
            }
            const byId = {};
            (existing || []).forEach(r => { byId[r.studentId] = r; });
            if (!list.length) {
                wrap.innerHTML = '<p style="color:var(--text-muted)">لا يوجد طلاب</p>';
                return;
            }
            wrap.innerHTML = `
                <div style="display:grid;grid-template-columns:2fr 1fr 1fr 1.5fr;gap:8px;padding:8px 0;border-bottom:1px solid var(--border);font-size:12px;font-weight:700;color:var(--text-muted)">
                    <span>الطالب</span><span>ملتزم</span><span>درجة /10</span><span>ملاحظة</span>
                </div>
                ${list.map(s => {
                    const id = s.id || s.studentId;
                    const name = s.fullName || s.studentName || s.name || '';
                    const ex = byId[id];
                    return `<div class="dress-row" data-sid="${id}" style="display:grid;grid-template-columns:2fr 1fr 1fr 1.5fr;gap:8px;align-items:center;padding:8px 0;border-bottom:1px solid rgba(226,232,240,0.5)">
                        <span style="font-size:13px;font-weight:600">${escapeHtml(name)}</span>
                        <label><input type="checkbox" class="dr-ok" ${ex ? (ex.isCompliant ? 'checked' : '') : 'checked'}></label>
                        <input class="form-input dr-score" type="number" min="0" max="10" step="0.5" value="${ex?.scoreOutOf10 ?? 10}" style="padding:6px 8px">
                        <input class="form-input dr-note" type="text" value="${escapeHtml(ex?.note || '')}" placeholder="..." style="padding:6px 8px">
                    </div>`;
                }).join('')}`;
        } catch (err) {
            handleApiError(err);
            wrap.innerHTML = '<p style="color:#dc2626">تعذر تحميل الطلاب</p>';
        }
    }

    async function saveDressBulk() {
        const date = document.getElementById('dressDate')?.value;
        if (!date) { showToast('حدد التاريخ', 'error'); return; }
        const rows = document.querySelectorAll('#dressRows .dress-row');
        const records = [];
        rows.forEach(r => {
            records.push({
                studentId: parseInt(r.dataset.sid, 10),
                isCompliant: !!r.querySelector('.dr-ok')?.checked,
                scoreOutOf10: parseFloat(r.querySelector('.dr-score')?.value) || 0,
                note: r.querySelector('.dr-note')?.value?.trim() || null
            });
        });
        if (!records.length) { showToast('لا توجد سجلات', 'warning'); return; }
        try {
            await apiFetch('/pedagogical/dress/bulk', 'POST', { date, records });
            showToast(`تم حفظ ${records.length} سجل لباس`, 'success');
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 6. Prayer — student + staff
    // ════════════════════════════════════════════════════════

    async function renderStudentPrayerPage() {
        const form = document.getElementById('myPrayerForm');
        const list = document.getElementById('myPrayerList');
        if (form && !form.dataset.ready) {
            form.dataset.ready = '1';
            form.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>تسجيل صلاة اليوم</h3></div>
                    <div class="form-group">
                        <label class="form-label">التاريخ</label>
                        <input id="prayerMyDate" class="form-input" type="date" value="${todayYmd()}">
                    </div>
                    <div class="form-group">
                        <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
                            <input type="checkbox" id="prayerMosque" checked> صليت في المسجد
                        </label>
                    </div>
                    <div class="form-group">
                        <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
                            <input type="checkbox" id="prayerOnTime" checked> في الوقت
                        </label>
                    </div>
                    <div class="form-group">
                        <label class="form-label">عدد الصلوات في المسجد (0–5)</label>
                        <input id="prayerCount" class="form-input" type="number" min="0" max="5" value="5">
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظة</label>
                        <input id="prayerNote" class="form-input" type="text">
                    </div>
                    <button class="btn btn-primary" onclick="saveMyPrayer()">إرسال التسجيل</button>
                    <p style="font-size:12px;color:var(--text-muted);margin-top:8px">بعد الإرسال يُقفل السجل ولا يمكن تعديله إلا عبر الشيخ.</p>
                </div>`;
        }
        if (!list) return;
        try {
            const logs = await apiFetch('/pedagogical/prayer/my');
            if (!logs?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:24px">لا توجد سجلات بعد</p>';
                return;
            }
            list.innerHTML = logs.map(l => `
                <div class="chart-card" style="margin-bottom:8px;padding:12px">
                    <strong>${escapeHtml(l.date)}</strong>
                    <span style="margin-right:10px">${l.prayedInMosque ? 'في المسجد' : 'خارج المسجد'}</span>
                    <span>${l.onTime ? 'في الوقت' : 'متأخر'}</span>
                    <span style="margin-right:10px">×${l.mosquePrayerCount ?? 0}</span>
                    ${l.isLocked ? '<span class="status-badge status-good">مقفول</span>' : ''}
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
            list.innerHTML = '<p style="color:#dc2626">تعذر التحميل</p>';
        }
    }

    async function saveMyPrayer() {
        const date = document.getElementById('prayerMyDate')?.value;
        if (!date) { showToast('حدد التاريخ', 'error'); return; }
        try {
            await apiFetch('/pedagogical/prayer/my', 'POST', {
                date,
                prayedInMosque: !!document.getElementById('prayerMosque')?.checked,
                onTime: !!document.getElementById('prayerOnTime')?.checked,
                mosquePrayerCount: parseInt(document.getElementById('prayerCount')?.value, 10) || 0,
                studentNote: document.getElementById('prayerNote')?.value?.trim() || null
            });
            showToast('تم تسجيل الصلاة', 'success');
            await renderStudentPrayerPage();
        } catch (err) {
            handleApiError(err);
        }
    }

    async function renderStaffPrayerSection() {
        const el = document.getElementById('pedagoPrayer');
        if (!el) return;
        if (!el.dataset.ready) {
            el.dataset.ready = '1';
            el.innerHTML = `
                <div class="chart-card">
                    <div class="card-header"><h3>متابعة صلاة الطلاب</h3></div>
                    <div class="form-group">
                        <label class="form-label">الطالب</label>
                        <select id="staffPrayerStudent" class="form-input" onchange="loadStaffPrayerLogs()"><option value="">— اختر —</option></select>
                    </div>
                    <div id="staffPrayerList"></div>
                </div>`;
            await loadStudentOptions('staffPrayerStudent');
        }
    }

    async function loadStaffPrayerLogs() {
        const sid = parseInt(document.getElementById('staffPrayerStudent')?.value, 10);
        const list = document.getElementById('staffPrayerList');
        if (!list) return;
        if (!sid) { list.innerHTML = ''; return; }
        try {
            const logs = await apiFetch(`/pedagogical/prayer?studentId=${sid}`);
            if (!logs?.length) {
                list.innerHTML = '<p style="color:var(--text-muted)">لا توجد سجلات</p>';
                return;
            }
            list.innerHTML = logs.map(l => `
                <div class="chart-card" style="margin-bottom:8px;padding:12px">
                    <div style="display:flex;justify-content:space-between;gap:8px;flex-wrap:wrap">
                        <div>
                            <strong>${escapeHtml(l.date)}</strong>
                            · ${l.prayedInMosque ? 'مسجد' : 'خارج'} · ${l.onTime ? 'وقت' : 'متأخر'} · ×${l.mosquePrayerCount ?? 0}
                            ${l.studentNote ? `<div style="font-size:12px;color:var(--text-muted)">${escapeHtml(l.studentNote)}</div>` : ''}
                        </div>
                        <button class="btn btn-outline" style="font-size:11px;padding:4px 10px" onclick="overridePrayerLog(${l.id})">تعديل الشيخ</button>
                    </div>
                </div>`).join('');
        } catch (err) {
            handleApiError(err);
        }
    }

    async function overridePrayerLog(id) {
        const note = prompt('ملاحظة التعديل (اختياري):') ?? '';
        const mosque = confirm('هل صليت في المسجد؟ (موافق = نعم)');
        const onTime = confirm('هل كانت في الوقت؟ (موافق = نعم)');
        try {
            await apiFetch(`/pedagogical/prayer/${id}/override`, 'PUT', {
                prayedInMosque: mosque,
                onTime,
                sheikhOverrideNote: note || null
            });
            showToast('تم تعديل سجل الصلاة', 'success');
            await loadStaffPrayerLogs();
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 7. Parent home
    // ════════════════════════════════════════════════════════

    async function renderParentHomePage() {
        const form = document.getElementById('parentHomeForm');
        const list = document.getElementById('parentHomeList');
        if (form && !form.dataset.ready) {
            form.dataset.ready = '1';
            form.innerHTML = `
                <div class="chart-card" style="margin-bottom:16px">
                    <div class="card-header"><h3>تقييم المتابعة المنزلية الأسبوعي</h3></div>
                    <div class="form-group">
                        <label class="form-label">الابن *</label>
                        <select id="phStudentId" class="form-input" onchange="loadParentHomeList()"><option value="">— اختر —</option></select>
                    </div>
                    <div class="form-group">
                        <label class="form-label">بداية الأسبوع</label>
                        <input id="phWeekStart" class="form-input" type="date" value="${todayYmd()}">
                    </div>
                    <div class="form-group">
                        <label class="form-label">التقييم</label>
                        <select id="phRating" class="form-input">
                            <option value="Excellent">ممتاز</option>
                            <option value="VeryGood">جيد جداً</option>
                            <option value="Good" selected>جيد</option>
                            <option value="Acceptable">مقبول</option>
                            <option value="Weak">ضعيف</option>
                        </select>
                    </div>
                    <div class="form-group">
                        <label class="form-label">ملاحظات</label>
                        <textarea id="phNotes" class="form-input" rows="2"></textarea>
                    </div>
                    <button class="btn btn-primary" onclick="saveParentHome()">حفظ التقييم</button>
                </div>`;
            await loadStudentOptions('phStudentId');
        }
        await loadParentHomeList();
    }

    async function loadParentHomeList() {
        const sid = parseInt(document.getElementById('phStudentId')?.value, 10);
        const list = document.getElementById('parentHomeList');
        if (!list) return;
        if (!sid) { list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:20px">اختر ابناً لعرض السجل</p>'; return; }
        try {
            const data = await apiFetch(`/pedagogical/parent-home?studentId=${sid}`);
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted)">لا توجد تقييمات</p>';
                return;
            }
            list.innerHTML = data.map(f => `
                <div class="chart-card" style="margin-bottom:8px;padding:12px">
                    <strong>${escapeHtml(f.weekStartDate)}</strong>
                    <span class="status-badge status-excellent" style="margin-right:8px">${escapeHtml(f.ratingLabel || f.rating)}</span>
                    ${f.notes ? `<div style="font-size:12px;color:var(--text-muted);margin-top:4px">${escapeHtml(f.notes)}</div>` : ''}
                </div>`).join('');
        } catch (err) {
            handleApiError(err, { silent: true });
        }
    }

    async function saveParentHome() {
        const studentId = parseInt(document.getElementById('phStudentId')?.value, 10);
        const weekStartDate = document.getElementById('phWeekStart')?.value;
        if (!studentId || !weekStartDate) {
            showToast('اختر الابن وتاريخ بداية الأسبوع', 'error');
            return;
        }
        try {
            await apiFetch('/pedagogical/parent-home', 'POST', {
                studentId,
                weekStartDate,
                rating: document.getElementById('phRating')?.value || 'Good',
                notes: document.getElementById('phNotes')?.value?.trim() || null
            });
            showToast('تم حفظ المتابعة المنزلية', 'success');
            await loadParentHomeList();
        } catch (err) {
            handleApiError(err);
        }
    }

    // ════════════════════════════════════════════════════════
    // 8. Student / parent period evaluations
    // ════════════════════════════════════════════════════════

    async function renderMyEvaluationsPage() {
        const list = document.getElementById('myEvaluationsList');
        if (!list) return;
        list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:24px">جاري التحميل...</p>';
        try {
            const data = await apiFetch('/pedagogical/my-evaluations');
            if (!data?.length) {
                list.innerHTML = '<p style="text-align:center;color:var(--text-muted);padding:32px">لا توجد تقييمات فترة محفوظة بعد، أو أن العرض غير مفعّل من الإعدادات.</p>';
                return;
            }
            list.innerHTML = data.map(e => `
                <div class="chart-card" style="margin-bottom:12px;padding:16px">
                    <div style="display:flex;justify-content:space-between;gap:12px;flex-wrap:wrap;align-items:flex-start">
                        <div>
                            <strong style="font-size:15px">${escapeHtml(e.periodName || '')}</strong>
                            <div style="font-size:12px;color:var(--text-muted);margin-top:4px">
                                ${formatDateEnGb(e.periodStart)} → ${formatDateEnGb(e.periodEnd)}
                                ${e.studentName ? ' · ' + escapeHtml(e.studentName) : ''}
                            </div>
                        </div>
                        <span class="status-badge status-excellent">${e.overallScore ?? 0}% — ${escapeHtml(e.gradeLabel || '')}</span>
                    </div>
                    <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(90px,1fr));gap:8px;margin-top:12px;font-size:12px">
                        <div>حضور: <strong>${e.attendanceScore ?? 0}</strong></div>
                        <div>حفظ: <strong>${e.hifzScore ?? 0}</strong></div>
                        <div>مراجعة: <strong>${e.revisionScore ?? 0}</strong></div>
                        <div>تقدم: <strong>${e.progressScore ?? 0}</strong></div>
                        <div>متون: <strong>${e.matnScore ?? 0}</strong></div>
                        <div>لباس: <strong>${e.dressScore ?? 0}</strong></div>
                    </div>
                    ${(e.prayerAdvisoryScore != null || e.parentHomeAdvisoryScore != null) ? `
                        <div style="margin-top:10px;font-size:12px;color:var(--text-muted)">
                            استرشادي —
                            ${e.prayerAdvisoryScore != null ? 'صلاة: ' + e.prayerAdvisoryScore + ' ' : ''}
                            ${e.parentHomeAdvisoryScore != null ? 'بيت: ' + e.parentHomeAdvisoryScore : ''}
                        </div>` : ''}
                    ${e.sheikhNotes ? `<div style="margin-top:10px;font-size:13px;padding:8px;background:var(--bg);border-radius:8px">${escapeHtml(e.sheikhNotes)}</div>` : ''}
                </div>`).join('');
        } catch (err) {
            handleApiError(err);
            list.innerHTML = '<p style="text-align:center;color:#dc2626;padding:24px">تعذر تحميل التقييمات</p>';
        }
    }

    // Exports
    global.fetchPedagogicalPage = fetchPedagogicalPage;
    global.showPedagoTab = showPedagoTab;
    global.addOralQuestionRow = addOralQuestionRow;
    global.saveOralExam = saveOralExam;
    global.deleteOralExam = deleteOralExam;
    global.loadMatnList = loadMatnList;
    global.saveMatnRecord = saveMatnRecord;
    global.deleteMatnRecord = deleteMatnRecord;
    global.loadTargetsList = loadTargetsList;
    global.saveMonthlyTarget = saveMonthlyTarget;
    global.syncMonthlyFromOral = syncMonthlyFromOral;
    global.saveEvaluationPeriod = saveEvaluationPeriod;
    global.openPeriodEvaluations = openPeriodEvaluations;
    global.autoDraftEvaluation = autoDraftEvaluation;
    global.savePeriodEvaluation = savePeriodEvaluation;
    global.loadDressStudents = loadDressStudents;
    global.saveDressBulk = saveDressBulk;
    global.saveMyPrayer = saveMyPrayer;
    global.loadStaffPrayerLogs = loadStaffPrayerLogs;
    global.overridePrayerLog = overridePrayerLog;
    global.loadParentHomeList = loadParentHomeList;
    global.saveParentHome = saveParentHome;
    global.renderMyEvaluationsPage = renderMyEvaluationsPage;
})(window);
