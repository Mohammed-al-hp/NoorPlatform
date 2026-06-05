// exams.js - إدارة الاختبارات والنتائج
(function(global) {
    'use strict';
    
    async function fetchExams() {
        try {
            const data = await apiFetch('/exams');
            const list = document.getElementById('examsList');
            if (!list) return;
            if (!data.length) {
                list.innerHTML = '<p style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد اختبارات بعد</p>';
                return;
            }
            list.innerHTML = data.map(e => `
            <div class="chart-card" style="margin-bottom:16px">
            <div class="card-header">
                <div>
                <h3>${escapeHtml(e.title)}</h3>
                <p>${formatDateEnGb(e.date)}</p>
                </div>
                <span class="status-badge status-excellent">${e.averageScore}% متوسط</span>
            </div>
            <div style="display:flex;gap:24px;margin-top:12px">
                <div class="mini-stat"><label>المشاركون</label><p>${e.participantsCount}</p></div>
                <div class="mini-stat"><label>المتوسط</label><p>${e.averageScore}%</p></div>
            </div>
            <p style="font-size:13px;color:var(--text-muted);margin-top:8px">${escapeHtml(e.description || '')}</p>
            <div style="margin-top:14px;display:flex;gap:8px">
                <button class="btn btn-primary" style="font-size:12px;padding:8px 14px" onclick="openExamResults(${e.id}, '${escapeHtml(e.title).replace(/'/g, "\\'")}')">
                📊 إدخال نتائج الطلاب
                </button>
                <button class="btn btn-delete" style="font-size:12px;padding:8px 14px" onclick="deleteExam(${e.id}, '${escapeHtml(e.title).replace(/'/g, "\\'")}')">
                🗑 حذف
                </button>
            </div>
            </div>`).join('');
        } catch {
            showToast('❌ تعذر تحميل الاختبارات');
        }
    }

    async function saveExam() {
        const title = document.getElementById('examTitle')?.value?.trim();
        const date = document.getElementById('examDate')?.value;
        const desc = document.getElementById('examDesc')?.value?.trim() || '';

        if (!title || !date) {
            showToast('❌ يرجى ملء العنوان والتاريخ');
            return;
        }

        const btn = document.querySelector('#addExamModal .btn-primary');
        if (btn) {
            btn.disabled = true;
            btn.textContent = 'جارٍ المعالجة...';
        }

        try {
            const res = await fetch(`${API_URL}/exams`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
                body: JSON.stringify({ title, date: new Date(date).toISOString(), description: desc })
            });
            if (!res.ok) throw new Error();
            document.getElementById('examTitle').value = '';
            document.getElementById('examDate').value = '';
            document.getElementById('examDesc').value = '';
            closeModal('addExamModal');
            showToast('✅ تم إنشاء الاختبار بنجاح');
            fetchExams();
        } catch {
            showToast('❌ حدث خطأ أثناء الإنشاء');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = '💾 حفظ';
            }
        }
    }

    let _currentExamId = null;
    let _currentExamStudents = [];

    async function openExamResults(examId, examTitle) {
        _currentExamId = examId;
        openModal('addExamResultModal');
        document.getElementById('examResultFormTitle').textContent = '📝 ' + examTitle;
        const rowsEl = document.getElementById('examResultRows');
        rowsEl.innerHTML = '<div style="text-align:center;padding:20px;color:var(--text-muted)">⏳ جاري تحميل الطلاب...</div>';

        try {
            const students = await apiFetch('/students');
            _currentExamStudents = students;
            rowsEl.innerHTML = `
                    <div style="display:grid;grid-template-columns:2fr 1fr 1fr 1.5fr;gap:8px;padding:8px 0;border-bottom:1px solid var(--border);font-size:12px;font-weight:700;color:var(--text-muted)">
                        <span>الطالب</span><span>الدرجة</span><span>من</span><span>تعليق (اختياري)</span>
                    </div>
                    ${students.map(s => `
                    <div style="display:grid;grid-template-columns:2fr 1fr 1fr 1.5fr;gap:8px;align-items:center;padding:8px 0;border-bottom:1px solid rgba(226,232,240,0.5)">
                        <div style="display:flex;align-items:center;gap:8px;font-size:13px;font-weight:600">
                            <div style="width:30px;height:30px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;color:#fff;font-size:11px;font-weight:700">${escapeHtml(s.fullName).slice(0, 2)}</div>
                            ${escapeHtml(s.fullName)}
                        </div>
                        <input class="form-input er-score" data-sid="${s.id}" type="number" min="0" max="100" placeholder="0" style="padding:7px 10px;font-size:13px">
                        <input class="form-input er-max" data-sid="${s.id}" type="number" min="1" value="100" style="padding:7px 10px;font-size:13px">
                        <input class="form-input er-feedback" data-sid="${s.id}" type="text" placeholder="تعليق..." style="padding:7px 10px;font-size:13px">
                    </div>`).join('')}`;
        } catch {
            rowsEl.innerHTML = '<p style="color:#dc2626;text-align:center;padding:20px">تعذر تحميل قائمة الطلاب</p>';
        }
    }

    async function submitExamResults() {
        if (!_currentExamId) return;
        const scores = document.querySelectorAll('.er-score');
        const maxes = document.querySelectorAll('.er-max');
        const feedbacks = document.querySelectorAll('.er-feedback');

        const results = [];
        scores.forEach((inp, i) => {
            const score = parseFloat(inp.value);
            if (!isNaN(score) && score >= 0) {
                results.push({
                    studentId: parseInt(inp.dataset.sid),
                    score,
                    maxScore: parseFloat(maxes[i].value) || 100,
                    feedback: feedbacks[i].value.trim() || null
                });
            }
        });

        if (!results.length) {
            showToast('⚠️ يرجى إدخال درجة واحدة على الأقل');
            return;
        }

        const btn = document.querySelector('#addExamResultModal .btn-primary');
        if (btn) {
            btn.disabled = true;
            btn.textContent = 'جارٍ المعالجة...';
        }

        try {
            const res = await fetch(`${API_URL}/exams/${_currentExamId}/results`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${TOKEN}`, 'Content-Type': 'application/json' },
                body: JSON.stringify(results)
            });
            if (!res.ok) throw new Error();
            closeModal('addExamResultModal');
            showToast(`✅ تم حفظ ${results.length} نتيجة بنجاح`);
            fetchExams();
        } catch {
            showToast('❌ حدث خطأ أثناء حفظ النتائج');
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = '💾 حفظ النتائج';
            }
        }
    }

    global.fetchExams = fetchExams;
    global.saveExam = saveExam;
    global.openExamResults = openExamResults;
    global.submitExamResults = submitExamResults;
})(window);
