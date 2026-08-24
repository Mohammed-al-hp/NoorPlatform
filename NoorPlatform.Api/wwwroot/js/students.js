/**
 * منصة نور — إدارة الطلاب وقائمة الانتظار
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const st = () => app().state.students;
    const apiFetch = (e, m, b) => app().api.apiFetch(e, m, b);
    const utils = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));
    const esc = s => utils().escapeHtml(s);
    const wa = p => utils().toWhatsAppLibyanPhone(p);
    const icon = (name, opts) => (global.Icon ? global.Icon(name, opts) : '');

    async function fetchStudents() {
        const grid = document.getElementById('studentsGrid') || document.querySelector('#page-students .cards-grid');
        if (grid) {
            grid.innerHTML = `<div style="grid-column:1/-1">${[1, 2, 3].map(() => '<div class="skeleton" style="height:160px;border-radius:20px;margin-bottom:12px"></div>').join('')}</div>`;
        }
        try {
            const url = st().archive ? '/students/archived' : '/students';
            const data = await apiFetch(url);
            st().all = data;
            window._students = data; // لضمان توافق التصدير إلى Excel في reports.js
            const map = { all: null, beginner: 'مبتدئ', intermediate: 'متوسط', advanced: 'متقدم' };
            const level = map[st().filter];
            const filtered = (level && !st().archive) ? data.filter(s => s.level === level) : data;
            renderStudentCards(filtered);
            const cnt = document.getElementById('studentsCount');
            if (cnt) {
                const role = app().state.user?.role || '';
                const suffix = st().archive ? ' طالب في الأرشيف' : (role === 'Teacher' ? ' طالب في حلقتك' : ' طالب مسجل في المركز');
                cnt.textContent = data.length + suffix;
            }
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function renderStudentCards(data) {
        const grid = document.getElementById('studentsGrid') || document.querySelector('#page-students .cards-grid');
        if (!grid) return;
        if (!data.length) {
            grid.innerHTML = `<div class="empty-state" style="grid-column:1/-1"><div class="empty-state-icon">${icon('graduation-cap', { size: 40 })}</div><div class="empty-state-title">لا يوجد طلاب</div></div>`;
            return;
        }
        const gradients = ['linear-gradient(135deg,#10b981,#3b82f6)', 'linear-gradient(135deg,#8b5cf6,#3b82f6)', 'linear-gradient(135deg,#f59e0b,#ef4444)', 'linear-gradient(135deg,#14b8a6,#10b981)', 'linear-gradient(135deg,#ec4899,#8b5cf6)'];
        grid.innerHTML = data.map((s, i) => {
            const initials = esc((s.fullName || '').slice(0, 2));
            const grad = gradients[i % gradients.length];
            const safeName = (s.fullName || '').replace(/'/g, "\\'");
            return `<div class="student-card" style="animation-delay:${i * 0.04}s">
              <div class="student-card-top">
                <div class="student-avatar-lg" style="background:${grad}">${initials}</div>
                <div class="student-card-info" style="flex:1">
                  <h4>${esc(s.fullName)}</h4>
                  <span style="font-size:11px">${esc(s.circleName)}</span>
                  <div style="margin-top:6px"><span class="status-badge status-good" style="font-size:10px">${esc(s.level)}</span></div>
                </div>
              </div>
              <div class="student-card-stats">
                <div class="mini-stat"><label>الحفظ</label><p>${s.progress}%</p></div>
                <div class="mini-stat"><label>الحضور</label><p>${s.attendance}%</p></div>
              </div>
              <div class="progress-wrap" style="margin-bottom:14px">
                <div class="progress-bar"><div class="progress-fill" style="width:${s.progress}%"></div></div>
                <span class="progress-pct">${s.progress}%</span>
              </div>
              <div class="student-card-actions">
                ${st().archive ? `<button class="btn btn-primary" onclick="NoorStudents.restoreStudent(${s.id},'${safeName}')" style="flex:1">${icon('refresh-cw', { size: 14 })} استعادة</button>` : `
                  <button class="btn btn-view" onclick="viewStudentDetails(${s.id})">${icon('eye', { size: 14 })} عرض</button>
                  <button class="btn btn-edit" onclick="editStudent(${s.id})">${icon('edit', { size: 14 })}</button>
                  <button class="btn-pdf" onclick="exportStudentPDF(${s.id})">${icon('file-text', { size: 14 })} PDF</button>
                  <button class="btn btn-delete" onclick="NoorStudents.deleteStudent(${s.id},'${safeName}')">${icon('archive', { size: 14 })} أرشفة</button>`}
              </div>
            </div>`;
        }).join('');
    }

    const filterStudentsLive = debounce(function (q) {
        if (st().waitingMode) return;
        const filtered = st().all.filter(s => s.fullName.includes(q) || (s.circleName || '').includes(q));
        renderStudentCards(filtered);
    }, 300);

    function setStudentFilter(filter, el) {
        st().filter = filter;
        st().archive = filter === 'archived';
        st().waitingMode = filter === 'waiting';
        document.getElementById('btnAddStudent').style.display = st().waitingMode ? 'none' : 'inline-flex';
        document.getElementById('btnAddWaiting').style.display = st().waitingMode ? 'inline-flex' : 'none';
        document.getElementById('btnExportStudents').style.display = st().waitingMode ? 'none' : 'inline-flex';
        const searchBar = document.getElementById('studentSearchInput')?.closest('.students-search-bar');
        if (searchBar) searchBar.style.display = st().waitingMode ? 'none' : 'block';
        const backBtn = document.getElementById('waitingBackBtn');
        if (backBtn) backBtn.style.display = st().waitingMode ? 'inline-flex' : 'none';
        document.querySelectorAll('.filter-chip').forEach(c => c.classList.remove('active'));
        if (el) el.classList.add('active');
        if (st().waitingMode) {
            if (location.hash !== '#waiting') history.replaceState({ noorFilter: 'waiting' }, '', '#waiting');
            fetchWaitingList();
        } else {
            if (location.hash === '#waiting') history.replaceState(null, '', '#students');
            fetchStudents();
        }
    }

    function exitWaitingList() {
        setStudentFilter('all', document.querySelector('.filter-chips .filter-chip'));
    }

    async function fetchWaitingList() {
        const grid = document.getElementById('studentsGrid');
        if (!grid) return;
        try {
            const data = await apiFetch('/waiting-list');
            st().waiting = data;
            const cnt = document.getElementById('studentsCount');
            if (cnt) cnt.textContent = data.length + ' في قائمة الانتظار';
            renderWaitingListCards(data);
        } catch (e) {
            app().api.handleApiError(e);
        }
    }

    function renderWaitingListCards(data) {
        const grid = document.getElementById('studentsGrid');
        if (!data.length) {
            grid.innerHTML = `<div class="empty-state" style="grid-column:1/-1"><div class="empty-state-title">قائمة الانتظار فارغة</div></div>`;
            return;
        }
        const statusMap = { Pending: 'قيد الانتظار', Contacted: 'تم التواصل', Accepted: 'مقبول', Rejected: 'مرفوض' };
        grid.innerHTML = data.map(w => `
            <div class="student-card waiting-card" data-waiting-id="${w.id}">
              <h4>${esc(w.fullName)}</h4>
              <span>${icon('phone', { size: 13 })} ${esc(w.displayPhone || w.phone || 'غير متاح')}</span>
              <div style="font-size:12px;color:var(--text-muted);margin:8px 0">ولي الأمر: ${esc(w.parentName || '—')}</div>
              <div class="student-card-actions">
                <button type="button" class="btn btn-primary btn-convert-waiting" data-id="${w.id}">${icon('graduation-cap', { size: 14 })} تحويل</button>
                <button type="button" class="btn btn-outline btn-wa-waiting" data-phone="${esc(w.displayParentPhone || w.parentPhone || w.displayPhone || w.phone || '')}" data-name="${esc(w.fullName)}">${icon('message-circle', { size: 14 })}</button>
                <button type="button" class="btn btn-edit btn-edit-waiting" data-id="${w.id}">${icon('edit', { size: 14 })}</button>
                <button type="button" class="btn btn-delete btn-del-waiting" data-id="${w.id}">${icon('trash-2', { size: 14 })}</button>
              </div>
            </div>`).join('');
    }

    async function deleteStudent(id, name) {
        global.confirmDelete(`أرشفة الطالب "${name}"؟`, async () => {
            try {
                await apiFetch('/students/' + id, 'DELETE');
                app().ui.showToast('تم الأرشفة', 'success');
                fetchStudents();
                global.NoorDashboard?.fetchStats?.();
            } catch (e) {
                app().api.handleApiError(e);
            }
        });
    }

    async function restoreStudent(id, name) {
        global.confirmDelete(`استعادة "${name}"؟`, async () => {
            try {
                await apiFetch('/students/' + id + '/restore', 'POST');
                app().ui.showToast('تمت الاستعادة', 'success');
                fetchStudents();
                global.NoorDashboard?.fetchStats?.();
            } catch (e) {
                app().api.handleApiError(e);
            }
        });
    }

    let _circleFilterIdx = -1;
    function cycleCircleFilter(el) {
        const circles = app().state.circles;
        if (!circles?.length) return;
        _circleFilterIdx = (_circleFilterIdx + 1) % (circles.length + 1);
        if (_circleFilterIdx === circles.length) {
            _circleFilterIdx = -1;
            el.textContent = 'جميع الحلقات ◂';
            renderStudentCards(st().all);
        } else {
            const c = circles[_circleFilterIdx];
            el.textContent = c.name + ' ◂';
            renderStudentCards(st().all.filter(s => s.circleName === c.name));
        }
    }

    if (!global._waitingListBound) {
        global._waitingListBound = true;
        document.getElementById('studentsGrid')?.addEventListener('click', e => {
            const convert = e.target.closest('.btn-convert-waiting');
            if (convert) { if (typeof openConvertWaitingModal === 'function') openConvertWaitingModal(parseInt(convert.dataset.id, 10)); return; }
            const edit = e.target.closest('.btn-edit-waiting');
            if (edit) { if (typeof openEditWaitingModal === 'function') openEditWaitingModal(parseInt(edit.dataset.id, 10)); return; }
            const del = e.target.closest('.btn-del-waiting');
            if (del) { if (typeof deleteWaitingEntry === 'function') deleteWaitingEntry(parseInt(del.dataset.id, 10)); return; }
            const waBtn = e.target.closest('.btn-wa-waiting');
            if (waBtn) {
                const phone = wa(waBtn.dataset.phone);
                const msg = encodeURIComponent(`السلام عليكم ${waBtn.dataset.name}، منصة نور لتحفيظ القرآن.`);
                window.open(`https://wa.me/${phone}?text=${msg}`, '_blank');
            }
        });
    }

    async function populateExistingParentSelect() {
        const sel = document.getElementById('existingParentId');
        if (!sel) return;
        try {
            const res = await apiFetch('/parents?pageSize=100');
            const items = res.items || [];
            sel.innerHTML = '<option value="">— أدخل بيانات ولي أمر جديد بالأسفل —</option>' +
                items.map(p => `<option value="${p.id}">${esc(p.fullName)} — ${esc(p.accountPhone)}</option>`).join('');
        } catch (e) {
            sel.innerHTML = '<option value="">— تعذر تحميل قائمة أولياء الأمور —</option>';
        }
    }

    global.NoorStudents = {
        fetchStudents,
        renderStudentCards,
        filterStudentsLive,
        setStudentFilter,
        exitWaitingList,
        fetchWaitingList,
        deleteStudent,
        restoreStudent,
        cycleCircleFilter,
        populateExistingParentSelect
    };
    global.populateExistingParentSelect = populateExistingParentSelect;
    global.fetchStudents = fetchStudents;
    global.filterStudentsLive = filterStudentsLive;
    global.setStudentFilter = setStudentFilter;
    global.exitWaitingList = exitWaitingList;
    let _convertWaitingId = null;

    function openWaitingListModal() {
        document.getElementById('waitingListFormTitle').innerHTML = icon('clock', { size: 16 }) + ' إضافة لقائمة الانتظار';
        document.getElementById('waitingEntryId').value = '';
        document.getElementById('waitingListForm').reset();
        document.getElementById('wlStatusGroup').style.display = 'none';
        app().ui.openModal('waitingListFormModal');
    }

    function openEditWaitingModal(id) {
        const w = st().waiting.find(x => x.id === id);
        if (!w) return;
        document.getElementById('waitingListFormTitle').innerHTML = icon('edit', { size: 16 }) + ' تعديل قائمة الانتظار';
        document.getElementById('waitingEntryId').value = w.id;
        document.getElementById('wlFullName').value = w.fullName;
        document.getElementById('wlPhone').value = w.displayPhone || w.phone;
        document.getElementById('wlAge').value = w.age || '';
        document.getElementById('wlParentName').value = w.parentName || '';
        document.getElementById('wlParentPhone').value = w.displayParentPhone || w.parentPhone || '';
        document.getElementById('wlLevel').value = w.requestedLevel || 'مبتدئ';
        document.getElementById('wlPreferredTime').value = w.preferredTime || '';
        document.getElementById('wlNotes').value = w.notes || '';
        document.getElementById('wlStatus').value = w.status || 'Pending';
        document.getElementById('wlStatusGroup').style.display = 'block';
        app().ui.openModal('waitingListFormModal');
    }

    async function saveWaitingListEntry(e) {
        e.preventDefault();
        const id = document.getElementById('waitingEntryId').value;
        const payload = {
            fullName: document.getElementById('wlFullName').value.trim(),
            phone: document.getElementById('wlPhone').value.trim(),
            parentName: document.getElementById('wlParentName').value.trim(),
            parentPhone: document.getElementById('wlParentPhone').value.trim(),
            age: parseInt(document.getElementById('wlAge').value, 10) || null,
            requestedLevel: document.getElementById('wlLevel').value,
            preferredTime: document.getElementById('wlPreferredTime').value.trim(),
            notes: document.getElementById('wlNotes').value.trim(),
            status: document.getElementById('wlStatus').value
        };
        try {
            if (id) await apiFetch(`/waiting-list/${id}`, 'PUT', payload);
            else await apiFetch('/waiting-list', 'POST', payload);
            app().ui.closeModal('waitingListFormModal');
            app().ui.showToast('تم الحفظ', 'success');
            fetchWaitingList();
        } catch (err) {
            app().api.handleApiError(err, { skipLogout: true });
        }
    }

    async function deleteWaitingEntry(id) {
        global.confirmDelete('حذف هذا السجل من قائمة الانتظار؟', async () => {
            try {
                await apiFetch(`/waiting-list/${id}`, 'DELETE');
                app().ui.showToast('تم الحذف', 'success');
                fetchWaitingList();
            } catch (e) {
                app().api.handleApiError(e, { skipLogout: true });
            }
        });
    }

    function openConvertWaitingModal(id) {
        _convertWaitingId = id;
        const w = st().waiting.find(x => x.id === id);
        if (!w) return;
        document.getElementById('convertWaitingName').textContent = 'تحويل: ' + w.fullName;
        const sel = document.getElementById('convertCircleId');
        sel.innerHTML = (app().state.circles || []).map(c =>
            `<option value="${c.id}">${esc(c.name)}</option>`).join('');
        app().ui.openModal('convertWaitingModal');
    }

    async function confirmConvertWaiting() {
        const circleId = parseInt(document.getElementById('convertCircleId').value, 10);
        if (!_convertWaitingId || !circleId) return;
        try {
            const res = await apiFetch(`/waiting-list/${_convertWaitingId}/convert-to-student`, 'POST', { circleId });
            app().ui.closeModal('convertWaitingModal');
            app().ui.showToast(res.message || 'تم التحويل', 'success');
            if (res.credentials && typeof showAccountCredentialsModal === 'function') {
                showAccountCredentialsModal(res.credentials, res.credentials.displayPhone);
            }
            st().waitingMode = false;
            setStudentFilter('all', document.querySelector('.filter-chip'));
        } catch (e) {
            app().api.handleApiError(e, { skipLogout: true });
        }
    }

    global.openWaitingListModal = openWaitingListModal;
    global.openEditWaitingModal = openEditWaitingModal;
    global.saveWaitingListEntry = saveWaitingListEntry;
    global.deleteWaitingEntry = deleteWaitingEntry;
    global.openConvertWaitingModal = openConvertWaitingModal;
    global.confirmConvertWaiting = confirmConvertWaiting;

    global.fetchWaitingList = fetchWaitingList;
    global.cycleCircleFilter = cycleCircleFilter;
})(window);
