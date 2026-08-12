/**
 * منصة نور — إدارة أولياء الأمور
 */
(function (global) {
    'use strict';

    const U = () => global.NoorUtils;
    let parentsPage = 1;
    let parentsSearch = '';
    const pageSize = 15;

    async function fetchParents(page) {
        if (page) parentsPage = page;
        const tbody = document.getElementById('parentsTableBody');
        const empty = document.getElementById('parentsEmptyState');
        const wrap = document.getElementById('parentsTableWrap');
        if (!tbody) return;

        tbody.innerHTML = '<tr><td colspan="6"><div class="skeleton-line"></div><div class="skeleton-line" style="width:70%"></div></td></tr>';
        if (wrap) wrap.style.display = 'block';
        if (empty) empty.style.display = 'none';

        try {
            const q = `?page=${parentsPage}&pageSize=${pageSize}&search=${encodeURIComponent(parentsSearch)}`;
            const res = await global.apiFetch('/parents' + q);
            renderParentsTable(res);
        } catch (e) {
            tbody.innerHTML = '';
            if (empty) empty.style.display = 'block';
            if (wrap) wrap.style.display = 'none';
            global.showToast('❌ تعذر تحميل أولياء الأمور');
        }
    }

    function renderParentsTable(res) {
        const tbody = document.getElementById('parentsTableBody');
        const empty = document.getElementById('parentsEmptyState');
        const wrap = document.getElementById('parentsTableWrap');
        const info = document.getElementById('parentsPageInfo');
        const items = res.items || [];

        if (!items.length) {
            tbody.innerHTML = '';
            if (wrap) wrap.style.display = 'none';
            if (empty) empty.style.display = 'block';
            if (info) info.textContent = '';
            return;
        }

        if (wrap) wrap.style.display = 'block';
        if (empty) empty.style.display = 'none';

        tbody.innerHTML = items.map(p => `
            <tr>
                <td><strong>${U().escapeHtml(p.fullName)}</strong></td>
                <td dir="ltr">${U().escapeHtml(p.phone)}</td>
                <td>${p.childrenCount}</td>
                <td dir="ltr">${U().escapeHtml(p.accountPhone)}</td>
                <td><span class="status-badge ${p.isActive ? 'status-present' : 'status-absent'}">${p.isActive ? 'نشط' : 'معطّل'}</span></td>
                <td>
                    <div style="display:flex;gap:6px;flex-wrap:wrap">
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px" onclick="NoorParents.viewParent(${p.id})">عرض</button>
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px" onclick="NoorParents.editParent(${p.id})">تعديل</button>
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px;color:#ef4444;border-color:#ef4444" onclick="NoorParents.deleteParent(${p.id})">حذف</button>
                    </div>
                </td>
            </tr>`).join('');

        const totalPages = Math.max(1, Math.ceil((res.total || 0) / pageSize));
        if (info) info.textContent = `صفحة ${res.page} من ${totalPages} — ${res.total} ولي أمر`;
        document.getElementById('parentsPrevBtn').disabled = parentsPage <= 1;
        document.getElementById('parentsNextBtn').disabled = parentsPage >= totalPages;
    }

    const searchParents = U().debounce(function(val) {
        parentsSearch = val;
        parentsPage = 1;
        fetchParents();
    }, 300);

    function changeParentsPage(dir) {
        parentsPage = Math.max(1, parentsPage + dir);
        fetchParents();
    }

    async function viewParent(id) {
        try {
            const p = await global.apiFetch(`/parents/${id}`);
            const childrenHtml = (p.children || []).map(c =>
                `<li>${U().escapeHtml(c.fullName)} — ${U().escapeHtml(c.circleName)}</li>`
            ).join('') || '<li>لا يوجد أبناء مرتبطون</li>';

            global.setModalBody('parentDetailBody', `
                <p><strong>الاسم:</strong> ${U().escapeHtml(p.fullName)}</p>
                <p><strong>الهاتف:</strong> <span dir="ltr">${U().escapeHtml(p.phone)}</span></p>
                <p><strong>الحساب:</strong> <span dir="ltr">${U().escapeHtml(p.accountPhone)}</span></p>
                <p><strong>الحالة:</strong> ${p.isActive ? 'نشط' : 'معطّل'}</p>
                <p><strong>يجب تغيير كلمة المرور:</strong> ${p.mustChangePassword ? 'نعم' : 'لا'}</p>
                <h4 style="margin-top:16px">الأبناء</h4>
                <ul style="padding-right:20px">${childrenHtml}</ul>`, '👨‍👦 تفاصيل ولي الأمر');
            global.openModal('parentDetailModal');
        } catch (e) {
            global.showToast('❌ تعذر تحميل التفاصيل');
        }
    }

    async function openAddParentModal() {
        document.getElementById('parentFormTitle').textContent = '➕ إضافة ولي أمر';
        document.getElementById('parentEditId').value = '';
        document.getElementById('parentFormFullName').value = '';
        document.getElementById('parentFormPhone').value = '';
        await loadParentStudentCheckboxes([]);
        global.openModal('parentFormModal');
    }

    async function editParent(id) {
        try {
            const p = await global.apiFetch(`/parents/${id}`);
            document.getElementById('parentFormTitle').textContent = '✏️ تعديل ولي أمر';
            document.getElementById('parentEditId').value = String(p.id);
            document.getElementById('parentFormFullName').value = p.fullName;
            document.getElementById('parentFormPhone').value = p.phone;
            await loadParentStudentCheckboxes((p.children || []).map(c => c.id));
            global.openModal('parentFormModal');
        } catch (e) {
            global.showToast('❌ تعذر تحميل البيانات');
        }
    }

    async function loadParentStudentCheckboxes(selectedIds) {
        const box = document.getElementById('parentChildrenCheckboxes');
        if (!box) return;
        try {
            const students = await global.apiFetch('/students');
            box.innerHTML = students.length
                ? students.map(s => {
                    const checked = selectedIds.includes(s.id) ? ' checked' : '';
                    return `<label style="display:flex;align-items:center;gap:8px;padding:6px 0;font-size:13px">
                        <input type="checkbox" value="${s.id}"${checked}> ${U().escapeHtml(s.fullName)}
                    </label>`;
                }).join('')
                : '<p style="color:var(--text-muted);font-size:13px">لا يوجد طلاب</p>';
        } catch (e) {
            box.innerHTML = '<p style="color:var(--text-muted)">تعذر تحميل الطلاب</p>';
        }
    }

    async function saveParentForm() {
        const id = document.getElementById('parentEditId').value;
        const fullName = document.getElementById('parentFormFullName').value.trim();
        const phone = document.getElementById('parentFormPhone').value.trim();
        const childIds = [...document.querySelectorAll('#parentChildrenCheckboxes input:checked')].map(cb => parseInt(cb.value, 10));

        if (!fullName || !phone) {
            global.showToast('❌ الاسم والهاتف مطلوبان');
            return;
        }

        const btn = document.querySelector('#parentFormModal .btn-primary');
        try {
            global.setBtnLoading(btn, true);
            if (id) {
                await global.apiFetch(`/parents/${id}`, 'PUT', { fullName, phone, childStudentIds: childIds });
                global.showToast('✅ تم تحديث ولي الأمر');
            } else {
                const res = await global.apiFetch('/parents', 'POST', { fullName, phone, childStudentIds: childIds });
                global.showToast('✅ تم إضافة ولي الأمر');
                if (res && res.credentials && typeof global.showAccountCredentialsModal === 'function') {
                    global.showAccountCredentialsModal(res.credentials, res.credentials.phone);
                }
            }
            global.closeModal('parentFormModal');
            fetchParents();
        } catch (e) {
            global.showToast('❌ ' + (e.message || 'فشل الحفظ'));
        } finally {
            global.setBtnLoading(btn, false);
        }
    }

    function deleteParent(id) {
        global.confirmDelete('أرشفة ولي الأمر وفك ربط الأبناء؟ (يمكن استعادته لاحقًا)', async () => {
            try {
                await global.apiFetch(`/parents/${id}`, 'DELETE');
                global.showToast('✅ تم الحذف');
                fetchParents();
            } catch (e) {
                global.showToast('❌ تعذر الحذف');
            }
        });
    }

    global.NoorParents = {
        fetchParents,
        searchParents,
        changeParentsPage,
        viewParent,
        editParent,
        deleteParent,
        openAddParentModal,
        saveParentForm
    };
})(window);
