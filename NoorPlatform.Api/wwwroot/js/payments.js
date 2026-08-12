/**
 * منصة نور — إدارة المدفوعات والرسوم
 */
(function (global) {
    'use strict';

    // ─── إصلاح: حالة ترقيم الصفحات (Pagination State) ───
    let paymentsPage = 1;
    const paymentsPageSize = 50;

    async function fetchPayments(page) {
        if (page) paymentsPage = page;
        try {
            // ─── إصلاح: إرسال بارامترات الترقيم للـ API المحدّث ───
            const data = await apiFetch(`/payments?page=${paymentsPage}&pageSize=${paymentsPageSize}`);
            const items = data.items || data;
            const grid = document.getElementById('paymentsGrid');
            if (!grid) return;

            if (!items.length) {
                grid.innerHTML = '<div class="empty-state">لا توجد فواتير</div>';
                renderPaymentsPagination(data);
                return;
            }

            grid.innerHTML = items.map(p => {
                const card = document.createElement('div');
                card.className = 'card';

                // Header
                const header = document.createElement('div');
                header.className = 'card-header';
                const h3 = document.createElement('h3');
                h3.textContent = p.studentName;
                const badge = document.createElement('span');
                badge.className = `status-badge ${p.status === 'Paid' ? 'sb-paid' : p.status === 'Pending' ? 'sb-pending' : 'sb-unpaid'}`;
                badge.textContent = p.status === 'Paid' ? 'مدفوعة' : p.status === 'Pending' ? 'بانتظار الدفع' : p.status === 'Overdue' ? 'متأخرة' : p.status;
                header.appendChild(h3);
                header.appendChild(badge);

                // Body
                const body = document.createElement('div');
                body.className = 'card-body';
                body.innerHTML = `
                    <p><strong>المبلغ:</strong> ${escapeHtml(String(p.amount))} ريال</p>
                    <p><strong>البيان:</strong> ${escapeHtml(p.description)}</p>
                    <p><strong>الاستحقاق:</strong> ${formatDateEnGb(p.dueDate)}</p>`;

                card.appendChild(header);
                card.appendChild(body);
                return card.outerHTML;
            }).join('');

            // ─── عرض أزرار التنقل بين الصفحات ───
            renderPaymentsPagination(data);
        } catch (err) {
            console.error(err);
        }
    }

    function renderPaymentsPagination(data) {
        const total = data.total || 0;
        const totalPages = Math.max(1, Math.ceil(total / paymentsPageSize));
        let paginationEl = document.getElementById('paymentsPagination');

        if (!paginationEl) {
            const grid = document.getElementById('paymentsGrid');
            if (!grid) return;
            paginationEl = document.createElement('div');
            paginationEl.id = 'paymentsPagination';
            paginationEl.style.cssText = 'display:flex;justify-content:center;align-items:center;gap:12px;padding:16px 0;grid-column:1/-1;';
            grid.parentElement?.appendChild(paginationEl);
        }

        if (totalPages <= 1) {
            paginationEl.innerHTML = '';
            return;
        }

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
                const card = document.createElement('div');
                card.className = 'card';

                const header = document.createElement('div');
                header.className = 'card-header';
                const h3 = document.createElement('h3');
                h3.textContent = p.studentName;
                const badge = document.createElement('span');
                badge.className = `status-badge ${p.status === 'Paid' ? 'sb-paid' : p.status === 'Pending' ? 'sb-pending' : 'sb-unpaid'}`;
                badge.textContent = p.status === 'Paid' ? 'مدفوعة' : p.status === 'Pending' ? 'بانتظار الدفع' : p.status === 'Overdue' ? 'متأخرة' : p.status;
                header.appendChild(h3);
                header.appendChild(badge);

                const body = document.createElement('div');
                body.className = 'card-body';
                body.innerHTML = `
                    <p><strong>المبلغ:</strong> ${escapeHtml(String(p.amount))} ريال</p>
                    <p><strong>البيان:</strong> ${escapeHtml(p.description)}</p>
                    <p><strong>الاستحقاق:</strong> ${formatDateEnGb(p.dueDate)}</p>`;

                if (p.status !== 'Paid') {
                    const btn = document.createElement('button');
                    btn.className = 'btn btn-primary';
                    btn.style.cssText = 'margin-top:10px; width:100%; justify-content:center';
                    btn.textContent = '💳 سداد الآن (تجريبي)';
                    btn.title = 'هذا سداد تجريبي لأغراض العرض فقط — لا يوجد بوابة دفع فعلية بعد';
                    btn.addEventListener('click', function () {
                        if (!confirm('⚠️ هذا سداد تجريبي فقط ولا يحوّل أي مبلغ فعلياً. سيتم تعليم الفاتورة كمدفوعة في النظام مباشرة.\n\nهل تريد المتابعة؟')) return;
                        payInvoice(p.id, this);
                    });
                    body.appendChild(btn);
                }

                card.appendChild(header);
                card.appendChild(body);
                return card.outerHTML;
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
                select.appendChild(opt);
            });
            document.getElementById('invoiceAmount').value = '';
            document.getElementById('invoiceDesc').value = '';
            document.getElementById('invoiceDueDate').value = '';
            document.getElementById('addInvoiceModal').classList.add('open');
        } catch (err) {
            showToast('❌ حدث خطأ في جلب الطلاب');
        }
    }

    async function submitInvoice() {
        const studentId = document.getElementById('invoiceStudentId').value;
        const amount = document.getElementById('invoiceAmount').value;
        const desc = document.getElementById('invoiceDesc').value;
        const dueDate = document.getElementById('invoiceDueDate').value;

        if (!studentId || !amount || !dueDate) {
            showToast('❌ يرجى تعبئة الحقول المطلوبة');
            return;
        }

        // ─── إصلاح: التحقق من القيمة قبل الإرسال (تتوافق مع Backend) ───
        if (parseFloat(amount) <= 0) {
            showToast('❌ المبلغ يجب أن يكون أكبر من صفر');
            return;
        }

        const btn = document.querySelector('#addInvoiceModal .btn-primary');
        if (btn) {
            btn.disabled = true;
            btn.textContent = 'جارٍ المعالجة...';
        }

        try {
            await apiFetch('/payments', 'POST', {
                studentId: parseInt(studentId),
                amount: parseFloat(amount),
                description: desc,
                dueDate: dueDate
            });
            closeModal('addInvoiceModal');
            showToast('✅ تم إصدار الفاتورة بنجاح');
            fetchPayments();
        } catch (err) {
            showToast('❌ ' + err.message);
        } finally {
            if (btn) {
                btn.disabled = false;
                // Since this might be a generic primary button in a modal, resetting text to original is best, but let's assume "حفظ" or "إصدار الفاتورة"
                btn.textContent = 'حفظ';
            }
        }
    }

    async function payInvoice(id, btn) {
        try {
            if (global.setBtnLoading) global.setBtnLoading(btn, true);
            await apiFetch(`/payments/${id}/pay`, 'POST');
            showToast('✅ تم الدفع بنجاح!');
            fetchParentFees();
        } catch (err) {
            showToast('❌ ' + err.message);
        } finally {
            if (global.setBtnLoading) global.setBtnLoading(btn, false, '💳 سداد الآن');
        }
    }

    global.fetchPayments = fetchPayments;
    global.fetchParentFees = fetchParentFees;
    global.showNewInvoiceModal = showNewInvoiceModal;
    global.submitInvoice = submitInvoice;
    global.payInvoice = payInvoice;
    global.changePaymentsPage = changePaymentsPage;
})(window);
