/**
 * منصة نور — إدارة المحفظين والحلقات
 * تم دمج تحديثات المحفظين والحلقات في هذا الملف
 */
(function (global) {
    'use strict';

    const apiFetch = (e, m, b) => global.NoorApp.api.apiFetch(e, m, b);
    const esc = (s) => global.NoorUtils?.escapeHtml(s) ?? String(s ?? '');
    const ui = () => global.NoorApp.ui;

    // =========================================================================
    // 1. إدارة المحفظين (Teachers)
    // =========================================================================

    let _teacherSearch = '';

    async function fetchTeachers(search) {
        if (search !== undefined) _teacherSearch = search;
        const grid = document.getElementById('teachersGrid');
        const count = document.getElementById('teachersPageCount');
        if (!grid) return;

        grid.innerHTML = `<div style="grid-column:1/-1">${[1, 2, 3].map(() => '<div class="skeleton" style="height:160px;border-radius:20px;margin-bottom:12px"></div>').join('')}</div>`;

        try {
            const q = _teacherSearch ? `?search=${encodeURIComponent(_teacherSearch)}` : '';
            const data = await apiFetch(`/teachers${q}`);
            if (count) count.textContent = `${data.length} محفظ`;

            if (!data.length) {
                grid.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:60px;color:var(--text-muted)">
                    <div style="margin-bottom:16px">${window.Icon ? window.Icon('user-check', {size:48}) : ''}</div>
                    <p>لا يوجد محفظون مسجلون</p>
                </div>`;
                return;
            }

            grid.innerHTML = data.map(t => renderTeacherCard(t)).join('');
        } catch (e) {
            grid.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:40px;color:#ef4444">تعذر تحميل المحفظين</div>`;
            global.NoorApp.api.handleApiError(e);
        }
    }

    function renderTeacherCard(t) {
        const rating = t.averageRating > 0 ? t.averageRating.toFixed(1) : '—';
        const starIcon = window.Icon ? window.Icon('star', {size:12}) : '';
        const stars = t.averageRating > 0 ? starIcon.repeat(Math.round(t.averageRating)) : '';
        const birthStr = t.birthDate ? new Date(t.birthDate).toLocaleDateString('ar-LY', { year: 'numeric', month: 'long', day: 'numeric' }) : '—';

        return `
        <div class="student-card" style="background:var(--card);border:1px solid var(--border);border-radius:var(--radius);padding:20px;display:flex;flex-direction:column;gap:14px">
            <div style="display:flex;align-items:center;gap:14px">
                <div style="width:52px;height:52px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;flex-shrink:0">${window.Icon ? window.Icon('user-check', {size:20}) : ''}</div>
                <div style="flex:1;min-width:0">
                    <div style="font-weight:800;font-size:15px;color:var(--text)">${esc(t.fullName)}</div>
                    <div style="font-size:12px;color:var(--text-muted);margin-top:2px">${esc(t.circleName)}</div>
                </div>
                <div style="text-align:center">
                    <div style="font-size:16px;font-weight:900;color:var(--green)">${rating}</div>
                    <div style="font-size:11px;color:var(--text-muted)">${stars || 'لا تقييم'}</div>
                </div>
            </div>

            <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px">
                <div style="background:var(--bg);border-radius:10px;padding:10px;text-align:center">
                    <div style="font-size:18px;font-weight:800;color:var(--blue)">${t.studentCount ?? 0}</div>
                    <div style="font-size:11px;color:var(--text-muted)">طالب</div>
                </div>
                <div style="background:var(--bg);border-radius:10px;padding:10px;text-align:center">
                    <div style="font-size:13px;font-weight:700;color:var(--text)">${esc(t.qualification || '—')}</div>
                    <div style="font-size:11px;color:var(--text-muted)">المؤهل</div>
                </div>
            </div>

            <div style="font-size:12px;color:var(--text-muted);display:flex;align-items:center;gap:6px">
                <span>${window.Icon ? window.Icon('calendar', {size:12}) : ''}</span>
                <span>${birthStr}</span>
            </div>

            <div style="display:flex;gap:8px;margin-top:4px">
                <button class="btn btn-outline" style="flex:1;font-size:12px;padding:7px" onclick="viewTeacher(${t.id})">${window.Icon ? window.Icon('eye', {size:12}) : ''} عرض</button>
                <button class="btn btn-outline" style="flex:1;font-size:12px;padding:7px" onclick="editTeacher(${t.id})">${window.Icon ? window.Icon('edit', {size:12}) : ''} تعديل</button>
                <button class="btn btn-outline" style="flex:1;font-size:12px;padding:7px;color:#ef4444;border-color:#ef4444" onclick="deleteTeacher(${t.id}, '${esc(t.fullName)}')">${window.Icon ? window.Icon('trash-2', {size:12}) : ''} حذف</button>
            </div>
        </div>`;
    }

    async function viewTeacher(id) {
        try {
            const t = await apiFetch(`/teachers/${id}`);
            const birthStr = t.birthDate ? new Date(t.birthDate).toLocaleDateString('ar-LY', { year: 'numeric', month: 'long', day: 'numeric' }) : '—';
            const circlesHtml = (t.circles || []).map(c => `<li>${esc(c.name)} (${c.studentCount} طالب)</li>`).join('') || '<li>لا توجد حلقات</li>';

            global.setModalBody('teacherProfileBody', `
                <div style="text-align:center;margin-bottom:20px">
                    <div style="width:64px;height:64px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;margin:0 auto 12px">${window.Icon ? window.Icon('user-check', {size:28}) : ''}</div>
                    <h3 style="font-size:18px;font-weight:800">${esc(t.fullName)}</h3>
                    <p style="color:var(--text-muted);font-size:13px">${esc(t.qualification || '—')}</p>
                </div>
                <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:16px">
                    <div style="background:var(--bg);border-radius:10px;padding:12px;text-align:center">
                        <div style="font-size:20px;font-weight:900;color:var(--green)">${t.averageRating > 0 ? t.averageRating.toFixed(1) : '—'}</div>
                        <div style="font-size:11px;color:var(--text-muted)">متوسط التقييم</div>
                    </div>
                    <div style="background:var(--bg);border-radius:10px;padding:12px;text-align:center">
                        <div style="font-size:20px;font-weight:900;color:var(--blue)">${(t.circles || []).reduce((s, c) => s + c.studentCount, 0)}</div>
                        <div style="font-size:11px;color:var(--text-muted)">إجمالي الطلاب</div>
                    </div>
                </div>
                <p><strong>تاريخ الميلاد:</strong> ${birthStr}</p>
                <p style="margin-top:8px"><strong>البريد الإلكتروني:</strong> ${esc(t.email || '—')}</p>
                <h4 style="margin-top:16px;margin-bottom:8px">الحلقات</h4>
                <ul style="padding-right:20px;line-height:2">${circlesHtml}</ul>
            `, (window.Icon ? window.Icon('clipboard-list', {size:16}) : '') + ' ملف المحفظ');
            global.openModal('teacherProfileModal');
        } catch (e) {
            ui().showToast('تعذر تحميل بيانات المحفظ', 'error');
        }
    }

    async function editTeacher(id) {
        try {
            const t = await apiFetch(`/teachers/${id}`);
            document.getElementById('editTeacherId').value = String(t.id);
            document.getElementById('editTeacherName').value = t.fullName || '';
            document.getElementById('editTeacherQual').value = t.qualification || '';
            document.getElementById('editTeacherBirthDate').value = t.birthDate ? t.birthDate.split('T')[0] : '';
            document.getElementById('editTeacherRating').value = t.averageRating || '';
            global.openModal('editTeacherModal');
        } catch (e) {
            ui().showToast('تعذر تحميل بيانات المحفظ', 'error');
        }
    }

    async function saveTeacher() {
        const fullName = document.getElementById('teacherFullName')?.value?.trim();
        const phone = document.getElementById('teacherPhone')?.value?.trim();
        const qualification = document.getElementById('teacherQualification')?.value?.trim();
        const birthDate = document.getElementById('teacherBirthDate')?.value;

        if (!fullName || !phone) {
            ui().showToast('الاسم الثلاثي ورقم الهاتف مطلوبان', 'error');
            return;
        }

        const btn = document.querySelector('#addTeacherModal .btn-primary');
        try {
            global.setBtnLoading(btn, true);
            const res = await apiFetch('/teachers', 'POST', {
                fullName, phone, qualification: qualification || '', birthDate: birthDate || null
            });
            ui().showToast('تم إضافة المحفظ بنجاح', 'success');
            global.closeModal('addTeacherModal');

            ['teacherFullName', 'teacherPhone', 'teacherQualification', 'teacherBirthDate']
                .forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });

            if (res?.credentials && typeof global.showAccountCredentialsModal === 'function') {
                global.showAccountCredentialsModal(res.credentials, res.credentials.phone);
            }
            fetchTeachers();
        } catch (e) {
            global.NoorApp.api.handleApiError(e);
        } finally {
            global.setBtnLoading(btn, false);
        }
    }

    async function submitEditTeacher() {
        const id = document.getElementById('editTeacherId')?.value;
        const fullName = document.getElementById('editTeacherName')?.value?.trim();
        const qualification = document.getElementById('editTeacherQual')?.value?.trim();
        const birthDate = document.getElementById('editTeacherBirthDate')?.value;
        const ratingRaw = document.getElementById('editTeacherRating')?.value;
        const averageRating = ratingRaw ? parseFloat(ratingRaw) : undefined;

        if (!id) return;

        const btn = document.querySelector('#editTeacherModal .btn-primary');
        try {
            global.setBtnLoading(btn, true);
            await apiFetch(`/teachers/${id}`, 'PUT', {
                fullName: fullName || undefined,
                qualification: qualification ?? '',
                birthDate: birthDate || null,
                averageRating
            });
            ui().showToast('تم تحديث بيانات المحفظ', 'success');
            global.closeModal('editTeacherModal');
            fetchTeachers();
        } catch (e) {
            global.NoorApp.api.handleApiError(e);
        } finally {
            global.setBtnLoading(btn, false);
        }
    }

    function deleteTeacher(id, name) {
        global.confirmDelete(`هل تريد أرشفة المحفظ "${name}"؟\nيمكن استعادته لاحقًا من الأرشيف.`, async () => {
            try {
                await apiFetch(`/teachers/${id}`, 'DELETE');
                ui().showToast('تم حذف المحفظ', 'success');
                fetchTeachers();
            } catch (e) {
                global.NoorApp.api.handleApiError(e);
            }
        });
    }

    // =========================================================================
    // 2. إدارة الحلقات (Circles)
    // =========================================================================

    async function fetchCircles() {
        const grid = document.getElementById('circlesGrid');
        const count = document.getElementById('circlesPageCount');
        if (!grid) return;

        grid.innerHTML = `<div style="grid-column:1/-1">${[1, 2, 3].map(() => '<div class="skeleton" style="height:160px;border-radius:20px;margin-bottom:12px"></div>').join('')}</div>`;

        try {
            const data = await apiFetch('/circles');
            global.NoorApp.state.circles = data;
            window._circles = data; // ← توافق مع editStudent في page-controller.js
            if (count) count.textContent = `${data.length} حلقة`;

            if (!data.length) {
                grid.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:60px;color:var(--text-muted)">
                    <div style="margin-bottom:16px">${window.Icon ? window.Icon('circle-dot', {size:48}) : ''}</div>
                    <p>لا توجد حلقات بعد</p>
                </div>`;
                return;
            }

            grid.innerHTML = data.map(c => renderCircleCard(c)).join('');
            populateCircleDropdowns(data);
        } catch (e) {
            grid.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:40px;color:#ef4444">تعذر تحميل الحلقات</div>`;
        }
    }

    function renderCircleCard(c) {
        const extraBadge = c.isExtra
            ? '<span class="status-badge" style="background:#fef3c7;color:#b45309;font-size:11px;margin-right:6px">إضافية</span>'
            : '';
        return `
        <div class="student-card" style="background:var(--card);border:1px solid var(--border);border-radius:var(--radius);padding:20px;display:flex;flex-direction:column;gap:14px">
            <div style="display:flex;align-items:center;gap:14px">
                <div style="width:52px;height:52px;border-radius:14px;background:var(--gradient);display:flex;align-items:center;justify-content:center;flex-shrink:0">${c.icon || (window.Icon ? window.Icon('circle-dot', {size:24}) : '')}</div>
                <div style="flex:1;min-width:0">
                    <div style="font-weight:800;font-size:15px">${extraBadge}${esc(c.name)}</div>
                    <div style="font-size:12px;color:var(--text-muted);margin-top:2px">${window.Icon ? window.Icon('user-check', {size:12}) : ''} ${esc(c.teacherName || 'لم يحدد')}</div>
                </div>
            </div>
            <div style="display:grid;grid-template-columns:1fr 1fr;gap:8px">
                <div style="background:var(--bg);border-radius:10px;padding:10px;text-align:center">
                    <div style="font-size:18px;font-weight:800;color:var(--green)">${c.studentCount ?? 0}</div>
                    <div style="font-size:11px;color:var(--text-muted)">طالب</div>
                </div>
                <div style="background:var(--bg);border-radius:10px;padding:10px;text-align:center">
                    <div style="font-size:12px;font-weight:700;color:var(--text)">${esc(c.time || '—')}</div>
                    <div style="font-size:11px;color:var(--text-muted)">الوقت</div>
                </div>
            </div>
            ${c.location ? `<div style="font-size:12px;color:var(--text-muted)">${window.Icon ? window.Icon('map-pin', {size:12}) : ''} ${esc(c.location)}</div>` : ''}
            <div style="display:flex;gap:8px;margin-top:4px;flex-wrap:wrap">
                ${c.isExtra ? `<button class="btn btn-primary" style="flex:1;font-size:12px;padding:7px" onclick="openExtraEnroll(${c.id}, '${esc(c.name).replace(/'/g, "\\'")}')">${window.Icon ? window.Icon('user-plus', {size:12}) : ''} تسجيل طلاب</button>` : ''}
                <button class="btn btn-outline" style="flex:1;font-size:12px;padding:7px" onclick="editCircle(${c.id})">${window.Icon ? window.Icon('edit', {size:12}) : ''} تعديل</button>
                <button class="btn btn-outline" style="flex:1;font-size:12px;padding:7px;color:#ef4444;border-color:#ef4444" onclick="deleteCircle(${c.id}, '${esc(c.name)}')">${window.Icon ? window.Icon('trash-2', {size:12}) : ''} حذف</button>
            </div>
        </div>`;
    }

    async function openExtraEnroll(circleId, circleName) {
        try {
            const [detail, students] = await Promise.all([
                apiFetch(`/circles/${circleId}`),
                apiFetch('/students')
            ]);
            const enrolledIds = new Set((detail.students || []).map(s => s.id));
            const body = `
                <p style="margin-bottom:12px;color:var(--text-muted);font-size:13px">اختر طلاب الحلقة الإضافية: <strong>${esc(circleName)}</strong></p>
                <input type="text" id="extraEnrollSearch" class="form-input" placeholder="بحث باسم الطالب..." style="margin-bottom:10px" oninput="filterExtraEnrollList(this.value)">
                <div id="extraEnrollList" style="max-height:320px;overflow:auto;display:flex;flex-direction:column;gap:6px">
                    ${(students || []).map(s => `
                        <label class="extra-enroll-item" data-name="${esc((s.fullName || '').toLowerCase())}" style="display:flex;align-items:center;gap:10px;padding:8px;border:1px solid var(--border);border-radius:10px;cursor:pointer">
                            <input type="checkbox" class="extra-enroll-cb" value="${s.id}" ${enrolledIds.has(s.id) ? 'checked' : ''}>
                            <span style="font-size:13px;font-weight:600">${esc(s.fullName)}</span>
                        </label>`).join('') || '<p>لا يوجد طلاب</p>'}
                </div>`;
            if (typeof global.openConfirmModal === 'function') {
                // fallback simple: use a dedicated modal if present
            }
            let modal = document.getElementById('extraEnrollModal');
            if (!modal) {
                modal = document.createElement('div');
                modal.className = 'modal-overlay';
                modal.id = 'extraEnrollModal';
                modal.innerHTML = `
                    <div class="modal" style="max-width:520px">
                        <div class="modal-header"><h3>تسجيل طلاب في حلقة إضافية</h3>
                        <button class="modal-close" onclick="closeModal('extraEnrollModal')"><span data-icon="x" data-icon-size="16"></span></button></div>
                        <div class="modal-body" id="extraEnrollBody"></div>
                        <div class="modal-footer">
                            <button class="btn btn-outline" onclick="closeModal('extraEnrollModal')">إلغاء</button>
                            <button class="btn btn-primary" id="extraEnrollSaveBtn">حفظ التسجيل</button>
                        </div>
                    </div>`;
                document.body.appendChild(modal);
            }
            document.getElementById('extraEnrollBody').innerHTML = body;
            const saveBtn = document.getElementById('extraEnrollSaveBtn');
            saveBtn.onclick = async () => {
                const ids = [...document.querySelectorAll('.extra-enroll-cb:checked')].map(cb => parseInt(cb.value, 10));
                try {
                    await apiFetch(`/circles/${circleId}/enrollments`, 'POST', { studentIds: ids });
                    // إزالة غير المحددين
                    const toRemove = [...enrolledIds].filter(id => !ids.includes(id));
                    for (const sid of toRemove) {
                        try { await apiFetch(`/circles/${circleId}/enrollments/${sid}`, 'DELETE'); } catch { /* ignore */ }
                    }
                    ui().showToast('تم تحديث تسجيل الطلاب', 'success');
                    global.closeModal('extraEnrollModal');
                    fetchCircles();
                } catch (e) {
                    global.NoorApp.api.handleApiError(e);
                }
            };
            global.openModal('extraEnrollModal');
        } catch (e) {
            global.NoorApp.api.handleApiError(e);
        }
    }

    function populateCircleDropdowns(circles) {
        const selectors = ['#halaqa', '#editStudentCircle', '#circleTeacher', '#editCircleTeacher', '#convertCircleId', '#hifzCircleSelect', '#reportCircleFilter'];
        selectors.forEach(sel => {
            const el = document.querySelector(sel);
            if (!el) return;
            const first = el.options[0]?.outerHTML || '';
            el.innerHTML = first + circles.map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
        });

        // حلقات رسمية فقط لخيار الحلقة الأم
        const parentSel = document.getElementById('circleParentId');
        if (parentSel) {
            const first = parentSel.options[0]?.outerHTML || '<option value="">— بدون —</option>';
            parentSel.innerHTML = first + (circles || []).filter(c => !c.isExtra)
                .map(c => `<option value="${c.id}">${esc(c.name)}</option>`).join('');
        }

        // استعادة تعبئة تاريخ التسجيل التلقائي عند جلب الحلقات
        const reg = document.getElementById('registrationDate');
        if (reg && !reg.value) reg.value = new Date().toISOString().slice(0, 10);
    }

    async function saveCircle() {
        const name = document.getElementById('circleName')?.value?.trim();
        const teacher = document.getElementById('circleTeacher')?.value;
        const location = document.getElementById('circleLocation')?.value?.trim();
        if (!name) { ui().showToast('اسم الحلقة مطلوب', 'error'); return; }

        const time = getCircleTimeValue('add');
        const isExtra = !!document.getElementById('circleIsExtra')?.checked;
        const sessionDate = document.getElementById('circleSessionDate')?.value || null;
        const parentRaw = document.getElementById('circleParentId')?.value;
        const parentCircleId = parentRaw ? parseInt(parentRaw, 10) : null;
        const btn = document.querySelector('#addCircleModal .btn-primary');
        try {
            global.setBtnLoading(btn, true);
            await apiFetch('/circles', 'POST', {
                name,
                time: time || '',
                location: location || '',
                teacherId: teacher ? parseInt(teacher) : null,
                isExtra,
                sessionDate: isExtra && sessionDate ? sessionDate : null,
                parentCircleId: isExtra ? parentCircleId : null
            });
            ui().showToast('تم إنشاء الحلقة بنجاح', 'success');
            global.closeModal('addCircleModal');
            ['circleName', 'circleLocation', 'circleTime', 'circleSessionDate'].forEach(id => {
                const el = document.getElementById(id); if (el) el.value = '';
            });
            const extraChk = document.getElementById('circleIsExtra');
            if (extraChk) extraChk.checked = false;
            if (typeof toggleCircleExtraFields === 'function') toggleCircleExtraFields(false);
            const parentSel = document.getElementById('circleParentId');
            if (parentSel) parentSel.value = '';
            fetchCircles();
        } catch (e) {
            global.NoorApp.api.handleApiError(e);
        } finally {
            global.setBtnLoading(btn, false);
        }
    }

    async function editCircle(id) {
        try {
            const c = await apiFetch(`/circles/${id}`);
            document.getElementById('editCircleId').value = String(c.id);
            document.getElementById('editCircleName').value = c.name || '';
            document.getElementById('editCircleLocation').value = c.location || '';

            const time = c.time || '';
            const isPrayer = ['فجر', 'ظهر', 'عصر', 'مغرب', 'عشاء', 'صلاة'].some(k => time.includes(k));
            if (typeof setCircleScheduleType === 'function') setCircleScheduleType(isPrayer ? 'prayer' : 'custom', 'editCircleModal');

            if (isPrayer) {
                const sel = document.getElementById('editCircleTimeSelect');
                if (sel) sel.value = time;
            } else {
                const parts = time.split(' - ');
                if (parts.length === 2) {
                    const f = document.getElementById('editCircleTimeFrom');
                    const t = document.getElementById('editCircleTimeTo');
                    if (f) f.value = parts[0];
                    if (t) t.value = parts[1];
                }
            }

            const teachers = await apiFetch('/teachers');
            const sel = document.getElementById('editCircleTeacher');
            if (sel) {
                sel.innerHTML = '<option value="">— بدون محفظ —</option>' + teachers.map(t => `<option value="${t.id}" ${t.id === c.teacherId ? 'selected' : ''}>${esc(t.fullName)}</option>`).join('');
            }
            const removeChk = document.getElementById('editCircleRemoveTeacher');
            if (removeChk) removeChk.checked = false;

            global.openModal('editCircleModal');
        } catch (e) {
            ui().showToast('تعذر تحميل بيانات الحلقة', 'error');
        }
    }

    async function submitEditCircle() {
        const id = document.getElementById('editCircleId')?.value;
        const name = document.getElementById('editCircleName')?.value?.trim();
        const location = document.getElementById('editCircleLocation')?.value?.trim();
        const teacher = document.getElementById('editCircleTeacher')?.value;
        const removeTeacher = document.getElementById('editCircleRemoveTeacher')?.checked || false;
        const time = getCircleTimeValue('edit');
        const btn = document.querySelector('#editCircleModal .btn-primary');

        if (!id) return;
        try {
            global.setBtnLoading(btn, true);
            await apiFetch(`/circles/${id}`, 'PUT', {
                name: name || undefined, time: time || undefined, location: location || undefined,
                teacherId: teacher && !removeTeacher ? parseInt(teacher) : undefined, removeTeacher
            });
            ui().showToast('تم تحديث الحلقة', 'success');
            global.closeModal('editCircleModal');
            fetchCircles();
        } catch (e) {
            global.NoorApp.api.handleApiError(e);
        } finally {
            global.setBtnLoading(btn, false);
        }
    }

    function deleteCircle(id, name) {
        global.confirmDelete(`هل تريد حذف الحلقة "${name}"؟\nتأكد من نقل الطلاب أولاً.`, async () => {
            try {
                await apiFetch(`/circles/${id}`, 'DELETE');
                ui().showToast('تم حذف الحلقة', 'success');
                fetchCircles();
            } catch (e) {
                global.NoorApp.api.handleApiError(e);
            }
        });
    }

    function getCircleTimeValue(prefix) {
        const prayerPanel = document.getElementById(`${prefix === 'add' ? 'add' : 'edit'}CirclePrayerPanel`);
        const isHidden = prayerPanel && prayerPanel.style.display === 'none';
        if (!isHidden) {
            const sel = document.getElementById(`${prefix === 'add' ? '' : 'edit'}CircleTimeSelect`);
            return sel ? sel.value : '';
        }
        const from = document.getElementById(`${prefix === 'add' ? '' : 'edit'}CircleTimeFrom`)?.value;
        const to = document.getElementById(`${prefix === 'add' ? '' : 'edit'}CircleTimeTo`)?.value;
        return from && to ? `${from} - ${to}` : (from || '');
    }

    // تصدير جميع الدوال لتكون متاحة للـ HTML
    global.fetchTeachers = fetchTeachers;
    global.viewTeacher = viewTeacher;
    global.editTeacher = editTeacher;
    global.saveTeacher = saveTeacher;
    global.submitEditTeacher = submitEditTeacher;
    global.deleteTeacher = deleteTeacher;

    global.fetchCircles = fetchCircles;
    global.saveCircle = saveCircle;
    global.editCircle = editCircle;
    global.submitEditCircle = submitEditCircle;
    global.deleteCircle = deleteCircle;
    global.openExtraEnroll = openExtraEnroll;

    function toggleCircleExtraFields(show) {
        const el = document.getElementById('circleExtraFields');
        if (el) el.style.display = show ? '' : 'none';
    }

    function filterExtraEnrollList(q) {
        const term = (q || '').trim().toLowerCase();
        document.querySelectorAll('#extraEnrollList .extra-enroll-item').forEach(item => {
            const name = item.dataset.name || '';
            item.style.display = !term || name.includes(term) ? '' : 'none';
        });
    }

    global.toggleCircleExtraFields = toggleCircleExtraFields;
    global.filterExtraEnrollList = filterExtraEnrollList;

})(window);
