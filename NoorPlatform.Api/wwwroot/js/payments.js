/**
 * منصة نور — الإدارة المالية (تصميم احترافي)
 */
(function (global) {
    'use strict';

    let paymentsPage = 1;
    const paymentsPageSize = 50;
    let allPayments = [];
    let filteredPayments = [];
    let currentStatusFilter = 'all';
    let currentSearchTerm = '';

    async function fetchPayments(page) {
        if (page) paymentsPage = page;
        try {
            try { await apiFetch('/payments/mark-overdue', 'PATCH'); } catch (_) { /* ignore */ }
            const data = await apiFetch(`/payments?page=${paymentsPage}&pageSize=${paymentsPageSize}`);
            const items = data.items || data;
            allPayments = items;
            applyFilters();
            renderPaymentsSummary(data.summary, items);
            renderPaymentsPagination(data);
        } catch (err) {
            console.error(err);
        }
    }

    function renderPaymentsSummary(summary, items) {
        const paid = (items || []).filter(p => p.status === 'Paid');
        const pending = (items || []).filter(p => p.status === 'Pending');
        const overdue = (items || []).filter(p => p.status === 'Overdue');
        const sumOf = arr => arr.reduce((s, p) => s + (p.amount || 0), 0);

        const fmt = n => Number(n || 0).toLocaleString('ar-LY', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        const setEl = (id, val) => { const el = document.getElementById(id); if (el) el.textContent = val; };

        const paidAmount = summary ? summary.paidAmount : sumOf(paid);
        const pendingAmount = summary ? summary.pendingAmount : sumOf(pending);
        const overdueAmount = summary ? summary.overdueAmount : sumOf(overdue);
        const totalAmount = summary ? summary.totalAmount : sumOf(items || []);
        const paidCount = summary ? summary.paidCount : paid.length;
        const pendingCount = summary ? summary.pendingCount : pending.length;
        const overdueCount = summary ? summary.overdueCount : overdue.length;
        const totalCount = summary ? summary.totalCount : (items || []).length;

        setEl('paymentsPaidAmount', fmt(paidAmount) + ' د.ل');
        setEl('paymentsPaidCount', `${paidCount} فاتورة`);
        setEl('paymentsPendingAmount', fmt(pendingAmount) + ' د.ل');
        setEl('paymentsPendingCount', `${pendingCount} فاتورة`);
        setEl('paymentsOverdueAmount', fmt(overdueAmount) + ' د.ل');
        setEl('paymentsOverdueCount', `${overdueCount} فاتورة`);
        setEl('paymentsTotalAmount', fmt(totalAmount) + ' د.ل');
        setEl('paymentsTotalCount', `${totalCount} فاتورة إجمالاً`);
    }

    function applyFilters() {
        filteredPayments = allPayments.filter(p => {
            const matchStatus = currentStatusFilter === 'all' || p.status === currentStatusFilter;
            const matchSearch = !currentSearchTerm || (p.studentName || '').toLowerCase().includes(currentSearchTerm.toLowerCase());
            return matchStatus && matchSearch;
        });
        renderPaymentsTable(filteredPayments);
    }

    function renderPaymentsTable(items) {
        const tbody = document.getElementById('paymentsTableBody');
        if (!tbody) return;

        if (!items.length) {
            tbody.innerHTML = `<tr><td colspan="7" style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد فواتير مطابقة</td></tr>`;
            return;
        }

        tbody.innerHTML = items.map((p, i) => {
            const statusClass = p.status === 'Paid' ? 'status-excellent' : p.status === 'Pending' ? 'status-good' : 'status-weak';
            const statusText = p.status === 'Paid' ? 'مدفوعة' : p.status === 'Pending' ? 'بانتظار الدفع' : p.status === 'Overdue' ? 'متأخرة' : p.status;
            const statusColor = p.status === 'Paid' ? '#22c55e' : p.status === 'Pending' ? '#f59e0b' : '#ef4444';
            const actionBtn = p.status !== 'Paid'
                ? `<button class="btn btn-primary" style="padding:6px 14px;font-size:12px" onclick="confirmReceived(${Number(p.id)})"><span data-icon="check" data-icon-size="12"></span> تأكيد الدفع</button>`
                : `<span style="color:var(--text-muted);font-size:12px"><span data-icon="check-circle" data-icon-size="14" style="color:#22c55e"></span> تم</span>`;

            const rowNum = (paymentsPage - 1) * paymentsPageSize + i + 1;
            return `<tr>
                <td style="font-weight:600;color:var(--text-muted)">${rowNum}</td>
                <td>
                    <div style="display:flex;align-items:center;gap:10px">
                        <div style="width:34px;height:34px;border-radius:50%;background:var(--gradient);display:flex;align-items:center;justify-content:center;color:#fff;font-size:12px;font-weight:700;flex-shrink:0">${escapeHtml((p.studentName || '').slice(0, 2))}</div>
                        <span style="font-weight:600;font-size:13px">${escapeHtml(p.studentName || '—')}</span>
                    </div>
                </td>
                <td style="font-size:13px;color:var(--text-muted)">${escapeHtml(p.description || '—')}</td>
                <td style="font-weight:700;font-size:14px">${escapeHtml(String(p.amount))} <span style="font-size:11px;color:var(--text-muted)">د.ل</span></td>
                <td style="font-size:13px">${formatDateEnGb(p.dueDate)}</td>
                <td><span class="status-badge" style="background:${statusColor}15;color:${statusColor};border:1px solid ${statusColor}30;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600">${statusText}</span></td>
                <td>${actionBtn}</td>
            </tr>`;
        }).join('');
    }

    function filterPaymentsByStatus(status, btn) {
        currentStatusFilter = status;
        document.querySelectorAll('#paymentStatusFilter .filter-chip').forEach(c => c.classList.remove('active'));
        if (btn) btn.classList.add('active');
        applyFilters();
    }

    function searchPayments(term) {
        currentSearchTerm = term;
        applyFilters();
    }

    function renderPaymentsPagination(data) {
        const total = data.total || 0;
        const totalPages = Math.max(1, Math.ceil(total / paymentsPageSize));
        const paginationEl = document.getElementById('paymentsPagination');
        if (!paginationEl) return;

        if (totalPages <= 1) { paginationEl.innerHTML = ''; return; }

        paginationEl.innerHTML = `
            <button class="btn btn-outline" style="padding:6px 14px;font-size:13px"
                onclick="changePaymentsPage(-1)" ${paymentsPage <= 1 ? 'disabled' : ''}>◀ السابق</button>
            <span style="font-size:13px;color:var(--text-muted)">صفحة ${paymentsPage} من ${totalPages}</span>
            <button class="btn btn-outline" style="padding:6px 14px;font-size:13px"
                onclick="changePaymentsPage(1)" ${paymentsPage >= totalPages ? 'disabled' : ''}>التالي ▶</button>`;
    }

    function changePaymentsPage(dir) {
        paymentsPage = Math.max(1, paymentsPage + dir);
        fetchPayments();
    }

    async function fetchParentFees() {
        try {
            const data = await apiFetch('/payments/parent');
            const grid = document.getElementById('parentFeesGrid');
            if (!grid) return;

            if (!data.length) {
                grid.innerHTML = '<div class="empty-state">لا توجد رسوم مستحقة</div>';
                return;
            }

            grid.innerHTML = data.map(p => {
                const statusColor = p.status === 'Paid' ? '#22c55e' : p.status === 'Pending' ? '#f59e0b' : '#ef4444';
                const statusText = p.status === 'Paid' ? 'مدفوعة' : p.status === 'Pending' ? 'بانتظار الدفع' : 'متأخرة';
                return `
                <div class="chart-card" style="padding:20px;border-right:4px solid ${statusColor}">
                    <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:12px">
                        <h3 style="font-size:15px;font-weight:700;margin:0">${escapeHtml(p.studentName)}</h3>
                        <span class="status-badge" style="background:${statusColor}15;color:${statusColor};border:1px solid ${statusColor}30;padding:4px 12px;border-radius:20px;font-size:12px;font-weight:600">${statusText}</span>
                    </div>
                    <div style="display:flex;gap:20px;font-size:13px;color:var(--text-muted);margin-bottom:12px">
                        <span><strong>المبلغ:</strong> ${escapeHtml(String(p.amount))} د.ل</span>
                        <span><strong>الاستحقاق:</strong> ${formatDateEnGb(p.dueDate)}</span>
                    </div>
                    <p style="font-size:13px;color:var(--text-muted);margin:0">${escapeHtml(p.description || '')}</p>
                    ${p.status !== 'Paid' ? `<p style="margin-top:14px;font-size:12px;color:var(--text-muted);background:rgba(245,158,11,0.08);padding:10px 12px;border-radius:10px">السداد يتم في المركز، وتُحدَّث الحالة بعد استلام الإدارة.</p>` : ''}
                </div>`;
            }).join('');
        } catch (err) {
            console.error(err);
        }
    }

    async function showNewInvoiceModal() {
        try {
            const students = await apiFetch('/students');
            const select = document.getElementById('invoiceStudentId');
            select.innerHTML = '<option value="">اختر الطالب...</option>';
            students.forEach(s => {
                const opt = document.createElement('option');
                opt.value = s.id;
                opt.textContent = s.fullName || s.user?.fullName || '';
                opt.dataset.parentName = s.parentName || s.guardianName || '';
                opt.dataset.parentPhone = s.parentPhone || '';
                opt.dataset.parentId = s.parentId || '';
                select.appendChild(opt);
            });
            document.getElementById('invoiceAmount').value = '';
            document.getElementById('invoiceDesc').value = '';
            const due = new Date();
            due.setMonth(due.getMonth() + 1);
            due.setDate(1);
            due.setDate(0);
            document.getElementById('invoiceDueDate').value = due.toISOString().slice(0, 10);
            const hint = document.getElementById('invoiceParentHint');
            if (hint) { hint.style.display = 'none'; hint.textContent = ''; }
            try {
                const settings = await apiFetch('/settings');
                const fee = settings.defaultMonthlyFee;
                if (fee > 0) document.getElementById('invoiceAmount').value = fee;
            } catch (_) { /* ignore */ }
            document.getElementById('addInvoiceModal').classList.add('open');
            if (global.refreshIcons) global.refreshIcons();
        } catch (err) {
            showToast('حدث خطأ في جلب الطلاب', 'error');
        }
    }

    function onInvoiceStudentChanged() {
        const select = document.getElementById('invoiceStudentId');
        const hint = document.getElementById('invoiceParentHint');
        if (!select || !hint) return;
        const opt = select.options[select.selectedIndex];
        if (!opt || !opt.value) {
            hint.style.display = 'none';
            hint.textContent = '';
            return;
        }
        const parentName = opt.dataset.parentName || '';
        const parentPhone = opt.dataset.parentPhone || '';
        if (!opt.dataset.parentId) {
            hint.style.display = 'block';
            hint.style.background = 'rgba(239,68,68,0.08)';
            hint.textContent = 'هذا الطالب غير مرتبط بولي أمر — لا يمكن إصدار فاتورة حتى يتم الربط.';
            return;
        }
        hint.style.background = 'rgba(16,185,129,0.08)';
        hint.style.display = 'block';
        hint.textContent = `ولي الأمر: ${parentName || '—'} — الهاتف: ${parentPhone || '—'}`;
    }

    async function submitInvoice() {
        const studentId = document.getElementById('invoiceStudentId').value;
        const amount = document.getElementById('invoiceAmount').value;
        const desc = document.getElementById('invoiceDesc').value;
        const dueDate = document.getElementById('invoiceDueDate').value;

        if (!studentId || !amount || !dueDate) {
            showToast('يرجى تعبئة الحقول المطلوبة', 'error');
            return;
        }

        if (parseFloat(amount) <= 0) {
            showToast('المبلغ يجب أن يكون أكبر من صفر', 'error');
            return;
        }

        const btn = document.querySelector('#addInvoiceModal .btn-primary');
        const originalBtnHtml = btn ? btn.innerHTML : '';
        if (btn) { btn.disabled = true; btn.textContent = 'جارٍ المعالجة...'; }

        try {
            await apiFetch('/payments', 'POST', {
                studentId: parseInt(studentId),
                amount: parseFloat(amount),
                description: desc,
                dueDate: dueDate
            });
            closeModal('addInvoiceModal');
            showToast('تم إصدار الفاتورة بنجاح', 'success');
            fetchPayments();
        } catch (err) {
            showToast(err.message, 'error');
        } finally {
            if (btn) { btn.disabled = false; btn.innerHTML = originalBtnHtml || 'إصدار الفاتورة'; }
        }
    }

    async function payInvoice(id, btn) {
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await apiFetch(`/payments/${id}/pay`, 'POST');
            showToast('تم الدفع بنجاح!', 'success');
            fetchParentFees();
        } catch (err) {
            showToast(err.message, 'error');
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false, (window.Icon ? window.Icon('credit-card', {size:14}) : '') + ' سداد الآن');
        }
    }

    function confirmReceived(id) {
        global.confirmDelete('تأكيد استلام مبلغ هذه الفاتورة نقداً؟', async () => {
            try {
                await apiFetch(`/payments/${id}/pay`, 'POST');
                showToast('تم تسجيل الدفع', 'success');
                fetchPayments();
            } catch (err) { showToast(err.message, 'error'); }
        });
    }

    global.confirmReceived = confirmReceived;
    global.fetchPayments = fetchPayments;
    global.fetchParentFees = fetchParentFees;
    global.showNewInvoiceModal = showNewInvoiceModal;
    global.onInvoiceStudentChanged = onInvoiceStudentChanged;
    global.submitInvoice = submitInvoice;
    global.payInvoice = payInvoice;
    global.changePaymentsPage = changePaymentsPage;
    global.filterPaymentsByStatus = filterPaymentsByStatus;
    global.searchPayments = searchPayments;
})(window);
