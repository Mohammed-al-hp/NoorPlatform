/**
 * منصة نور — إدارة المستخدمين
 */
(function (global) {
    'use strict';

    const U = () => global.NoorUtils;
    let usersPage = 1;
    let usersSearch = '';
    let usersRoleFilter = '';
    const pageSize = 15;

    async function fetchUsers(page) {
        if (page) usersPage = page;
        const tbody = document.getElementById('usersTableBody');
        const empty = document.getElementById('usersEmptyState');
        const wrap = document.getElementById('usersTableWrap');
        if (!tbody) return;

        tbody.innerHTML = '<tr><td colspan="7"><div class="skeleton-line"></div></td></tr>';

        try {
            let q = `?page=${usersPage}&pageSize=${pageSize}&search=${encodeURIComponent(usersSearch)}`;
            if (usersRoleFilter) q += `&role=${encodeURIComponent(usersRoleFilter)}`;
            const res = await global.apiFetch('/users' + q);
            renderUsersTable(res);
        } catch (e) {
            tbody.innerHTML = '';
            if (empty) empty.style.display = 'block';
            if (wrap) wrap.style.display = 'none';
        }
    }

    function renderUsersTable(res) {
        const tbody = document.getElementById('usersTableBody');
        const empty = document.getElementById('usersEmptyState');
        const wrap = document.getElementById('usersTableWrap');
        const info = document.getElementById('usersPageInfo');
        const items = res.items || [];

        if (!items.length) {
            tbody.innerHTML = '';
            if (wrap) wrap.style.display = 'none';
            if (empty) empty.style.display = 'block';
            return;
        }

        if (wrap) wrap.style.display = 'block';
        if (empty) empty.style.display = 'none';

        const roleLabels = { Admin: 'مشرف', Teacher: 'محفظ', Student: 'طالب', Parent: 'ولي أمر' };

        tbody.innerHTML = items.map(u => `
            <tr>
                <td><strong>${U().escapeHtml(u.fullName)}</strong></td>
                <td>${roleLabels[u.role] || u.role}</td>
                <td dir="ltr">${U().escapeHtml(u.phone)}</td>
                <td>${u.lastLoginAt ? U().formatDateTimeEnGb(u.lastLoginAt) : '—'}</td>
                <td><span class="status-badge ${u.isActive ? 'status-present' : 'status-absent'}">${u.isActive ? 'نشط' : 'معطّل'}</span></td>
                <td>${u.mustChangePassword ? '⚠️ نعم' : 'لا'}</td>
                <td>
                    <div style="display:flex;gap:6px;flex-wrap:wrap">
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px" onclick="NoorUsers.viewUser(${u.id})">عرض</button>
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px" onclick="NoorUsers.editUser(${u.id})">تعديل</button>
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px" onclick="NoorUsers.toggleUser(${u.id})">${u.isActive ? 'تعطيل' : 'تفعيل'}</button>
                        <button class="btn btn-outline" style="padding:4px 10px;font-size:12px;color:#ef4444" onclick="NoorUsers.deleteUser(${u.id})">حذف</button>
                    </div>
                </td>
            </tr>`).join('');

        const totalPages = Math.max(1, Math.ceil((res.total || 0) / pageSize));
        if (info) info.textContent = `صفحة ${res.page} من ${totalPages}`;
        document.getElementById('usersPrevBtn').disabled = usersPage <= 1;
        document.getElementById('usersNextBtn').disabled = usersPage >= totalPages;
    }

    function searchUsers(val) {
        usersSearch = val || '';
        usersPage = 1;
        fetchUsers();
    }

    function filterUsersRole(role) {
        usersRoleFilter = role || '';
        usersPage = 1;
        fetchUsers();
    }

    function changeUsersPage(dir) {
        usersPage = Math.max(1, usersPage + dir);
        fetchUsers();
    }

    async function viewUser(id) {
        try {
            const u = await global.apiFetch(`/users/${id}`);
            global.setModalBody('userDetailBody', `
                <p><strong>الاسم:</strong> ${U().escapeHtml(u.fullName)}</p>
                <p><strong>الدور:</strong> ${U().escapeHtml(u.role)}</p>
                <p><strong>الجوال:</strong> <span dir="ltr">${U().escapeHtml(u.phone)}</span></p>
                <p><strong>البريد:</strong> ${U().escapeHtml(u.email || '—')}</p>
                <p><strong>آخر دخول:</strong> ${u.lastLoginAt ? U().formatDateTimeEnGb(u.lastLoginAt) : '—'}</p>
                <p><strong>الحالة:</strong> ${u.isActive ? 'نشط' : 'معطّل'}</p>
                <p><strong>تغيير كلمة المرور:</strong> ${u.mustChangePassword ? 'مطلوب' : 'لا'}</p>
                <p><strong>تاريخ الإنشاء:</strong> ${U().formatDateEnGb(u.createdAt)}</p>`, '👤 تفاصيل المستخدم');
            global.openModal('userDetailModal');
        } catch (e) {
            global.showToast('❌ تعذر التحميل');
        }
    }

    async function editUser(id) {
        try {
            const u = await global.apiFetch(`/users/${id}`);
            document.getElementById('userEditId').value = String(u.id);
            document.getElementById('userEditFullName').value = u.fullName;
            document.getElementById('userEditPhone').value = u.phone;
            document.getElementById('userEditRole').value = u.role;
            document.getElementById('userEditActive').checked = u.isActive;
            document.getElementById('userEditMustChange').checked = u.mustChangePassword;
            global.openModal('userEditModal');
        } catch (e) {
            global.showToast('❌ تعذر التحميل');
        }
    }

    async function saveUserEdit() {
        const id = document.getElementById('userEditId').value;
        try {
            await global.apiFetch(`/users/${id}`, 'PUT', {
                fullName: document.getElementById('userEditFullName').value.trim(),
                phone: document.getElementById('userEditPhone').value.trim(),
                role: document.getElementById('userEditRole').value,
                isActive: document.getElementById('userEditActive').checked,
                mustChangePassword: document.getElementById('userEditMustChange').checked
            });
            global.closeModal('userEditModal');
            global.showToast('✅ تم التحديث');
            fetchUsers();
        } catch (e) {
            global.showToast('❌ فشل التحديث');
        }
    }

    async function toggleUser(id) {
        try {
            await global.apiFetch(`/users/${id}/toggle-active`, 'PATCH');
            global.showToast('✅ تم تحديث الحالة');
            fetchUsers();
        } catch (e) {
            global.showToast('❌ فشل التحديث');
        }
    }

    async function deleteUser(id) {
        if (!confirm('تعطيل هذا الحساب؟')) return;
        try {
            await global.apiFetch(`/users/${id}`, 'DELETE');
            global.showToast('✅ تم تعطيل الحساب');
            fetchUsers();
        } catch (e) {
            global.showToast('❌ فشل الحذف');
        }
    }

    global.NoorUsers = {
        fetchUsers,
        searchUsers,
        filterUsersRole,
        changeUsersPage,
        viewUser,
        editUser,
        saveUserEdit,
        toggleUser,
        deleteUser
    };
})(window);
