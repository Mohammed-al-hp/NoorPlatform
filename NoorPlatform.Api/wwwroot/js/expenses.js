/**
 * منصة نور — إدارة المصروفات والعهد المالية
 */
(function (global) {
    'use strict';

    const NoorExpenses = {
        fetchExpenses: async function () {
            try {
                // Get summary first
                const summary = await apiFetch('/expenses/summary');
                if (summary) {
                    let salaries = 0;
                    let trusts = 0;
                    let count = 0;
                    if (summary.breakdown) {
                        summary.breakdown.forEach(b => {
                            if (b.category === 'Salary') salaries += b.total;
                            if (b.category === 'Trust') trusts += b.total;
                            count += b.count;
                        });
                    }
                    document.getElementById('expTotal').innerText = (summary.grandTotal || 0).toLocaleString() + ' د.ل';
                    document.getElementById('expSalaries').innerText = salaries.toLocaleString() + ' د.ل';
                    document.getElementById('expTrusts').innerText = trusts.toLocaleString() + ' د.ل';
                    document.getElementById('expCount').innerText = count;
                }

                // Get list of expenses
                const data = await apiFetch('/expenses?pageNumber=1&pageSize=100');
                this.renderExpenses(data.items || data);
            } catch (err) {
                console.error(err);
                if (typeof showToast === 'function') showToast('حدث خطأ أثناء جلب المصروفات', 'error');
            }
        },

        renderExpenses: function (expenses) {
            const tbody = document.getElementById('expensesTableBody');
            if (!tbody) return;

            if (!expenses || expenses.length === 0) {
                tbody.innerHTML = '<tr><td colspan="6" style="text-align:center;color:var(--text-muted)">لا توجد مصروفات مسجلة</td></tr>';
                return;
            }

            tbody.innerHTML = expenses.map(exp => {
                let badgeClass = 'status-pending';
                let catText = 'أخرى';
                
                // ExpenseCategory enum mapping
                switch(exp.category) {
                    case 'Salary': 
                    case 0:
                        badgeClass = 'status-excellent'; catText = 'رواتب'; break;
                    case 'Trust': 
                    case 1:
                        badgeClass = 'status-late'; catText = 'عهدة'; break;
                    case 'Maintenance': 
                    case 2:
                        badgeClass = 'status-absent'; catText = 'صيانة'; break;
                    case 'Reward': 
                    case 3:
                        badgeClass = 'status-good'; catText = 'مكافآت'; break;
                    default:
                        if (typeof exp.category === 'string') catText = exp.category;
                        break;
                }

                return `
                <tr>
                    <td><strong>${escapeHtml(exp.title)}</strong></td>
                    <td><span class="status-badge ${badgeClass}">${escapeHtml(catText)}</span></td>
                    <td style="font-weight:700; color:var(--text);">${exp.amount.toLocaleString()} د.ل</td>
                    <td><span style="font-size:12px;color:var(--text-muted);">${formatDateEnGb(exp.date)}</span></td>
                    <td>${exp.description ? escapeHtml(exp.description) : '<span style="color:var(--text-muted)">--</span>'}</td>
                    <td>
                        <div class="actions" style="display:flex;gap:6px">
                            <button class="btn btn-outline btn-delete" style="padding:4px 8px" onclick="NoorExpenses.deleteExpense(${exp.id})">
                                ${window.Icon ? window.Icon('trash-2', { size: 14 }) : 'حذف'}
                            </button>
                        </div>
                    </td>
                </tr>`;
            }).join('');
        },

        openAddModal: function () {
            document.getElementById('addExpenseForm').reset();
            document.getElementById('expDate').valueAsDate = new Date();
            openModal('addExpenseModal');
        },

        saveExpense: async function (e) {
            e.preventDefault();
            const title = document.getElementById('expTitle').value.trim();
            const amount = parseFloat(document.getElementById('expAmount').value);
            const category = document.getElementById('expCategory').value;
            const date = document.getElementById('expDate').value;
            const notes = document.getElementById('expNotes').value.trim();
            const btn = document.querySelector('#addExpenseModal .btn-primary');

            if (!title || !amount || isNaN(amount) || amount <= 0) {
                showToast('يرجى تعبئة العنوان والمبلغ بشكل صحيح', 'warning');
                return;
            }

            try {
                if (global.setBtnLoading) global.setBtnLoading(btn, true);
                
                await apiFetch('/expenses', 'POST', {
                    title: title,
                    amount: amount,
                    category: parseInt(category, 10) || 4,
                    date: date,
                    description: notes
                });
                
                closeModal('addExpenseModal');
                showToast('تم إضافة المصروف بنجاح', 'success');
                NoorExpenses.fetchExpenses();
            } catch (err) {
                showToast(err.message || 'فشل حفظ المصروف', 'error');
            } finally {
                if (global.setBtnLoading) global.setBtnLoading(btn, false);
            }
        },

        deleteExpense: function (id) {
            global.confirmDelete('هل أنت متأكد من حذف هذا المصروف؟', async () => {
                try {
                    await apiFetch(`/expenses/${id}`, 'DELETE');
                    showToast('تم حذف المصروف بنجاح', 'success');
                    NoorExpenses.fetchExpenses();
                } catch (err) {
                    showToast(err.message, 'error');
                }
            });
        }
    };

    global.NoorExpenses = NoorExpenses;

})(window);
