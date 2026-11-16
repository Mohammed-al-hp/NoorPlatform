/**
 * منصة نور — أدوات القوائم:
 *  1) الترتيب السريع (أبجدي، الأكثر طلاباً، الأكبر عمراً...) لصفحات الطلاب والمحفّظين والحلقات.
 *  2) تحويل الجداول إلى بطاقات أنيقة على شاشات الجوال (Mobile Cards):
 *     صورة رمزية بالحرف الأول، رقاقات للصفات، وأزرار إجراءات ملوّنة (عرض / تعديل / حذف).
 *     يعمل تلقائياً على كل جدول يُرسم في الصفحة دون تعديل كل وحدة على حدة.
 */
(function (global) {
    'use strict';

    // ═══════════════ 1) الترتيب ═══════════════
    const collator = new Intl.Collator('ar');
    const byName = key => (a, b) => collator.compare(String(a[key] || ''), String(b[key] || ''));
    // تاريخ الميلاد الأقدم = الأكبر عمراً؛ القيم الفارغة في آخر القائمة دائماً
    const byDate = (key, dir) => (a, b) => {
        const x = a[key] ? Date.parse(a[key]) : NaN;
        const y = b[key] ? Date.parse(b[key]) : NaN;
        if (isNaN(x) && isNaN(y)) return 0;
        if (isNaN(x)) return 1;
        if (isNaN(y)) return -1;
        return dir * (x - y);
    };
    const byNumberDesc = key => (a, b) => (Number(b[key]) || 0) - (Number(a[key]) || 0);

    const SORTERS = {
        students: {
            alpha: byName('fullName'),
            oldest: byDate('dateOfBirth', 1),
            youngest: byDate('dateOfBirth', -1),
            progress: byNumberDesc('progress'),
            attendance: byNumberDesc('attendance')
        },
        teachers: {
            alpha: byName('fullName'),
            students: byNumberDesc('studentCount'),
            oldest: byDate('birthDate', 1)
        },
        circles: {
            alpha: byName('name'),
            students: byNumberDesc('studentCount')
        }
    };

    function sortList(kind, list, mode) {
        const fn = SORTERS[kind]?.[mode];
        if (!fn || !Array.isArray(list)) return list;
        return list.slice().sort(fn);
    }

    // ═══════════════ 2) بطاقات الجوال للجداول ═══════════════
    const TITLE_HEADERS = ['الاسم', 'الطالب', 'المحفظ', 'المحفّظ', 'ولي الأمر', 'اسم الطالب', 'المستخدم', 'البيان'];
    const CHIP_HEADERS = ['الحالة', 'الدور', 'التقييم', 'الحلقة', 'المستوى', 'الحساب', 'النوع', 'تغيير كلمة المرور'];
    const HIDE_HEADERS = ['#'];

    function classifyButton(btn) {
        if (btn.classList.contains('btn-view') || btn.classList.contains('btn-edit') || btn.classList.contains('btn-delete')) return;
        const text = (btn.textContent + ' ' + (btn.title || '') + ' ' + (btn.getAttribute('aria-label') || '')).trim();
        if (/حذف|أرشفة|تعطيل|إلغاء/.test(text)) btn.classList.add('mc-btn-delete');
        else if (/تعديل|تحرير/.test(text)) btn.classList.add('mc-btn-edit');
        else if (/عرض|تفاصيل|ملف/.test(text)) btn.classList.add('mc-btn-view');
        else btn.classList.add('mc-btn-neutral');
    }

    function enhanceTable(table) {
        if (table.dataset.noCards !== undefined || table.closest('[data-no-cards]')) return;
        const headers = Array.from(table.querySelectorAll('thead th')).map(th => th.textContent.trim());
        if (headers.length < 2) return;
        table.classList.add('mc-table');

        table.querySelectorAll('tbody tr').forEach(tr => {
            if (tr.dataset.mcDone === String(tr.cells.length) + '|' + tr.textContent.length) return;
            const cells = Array.from(tr.cells);
            if (cells.length === 1) { cells[0].classList.add('mc-full'); return; }

            let titleDone = false;
            cells.forEach((td, i) => {
                const label = headers[i] || '';
                td.dataset.label = label;
                td.classList.remove('mc-title', 'mc-chip', 'mc-actions', 'mc-hide');
                const hasButtons = td.querySelector('button, .btn, a.btn');
                const textOnly = td.textContent.replace(/\s+/g, '');

                if (HIDE_HEADERS.includes(label)) {
                    td.classList.add('mc-hide');
                } else if (/إجراء|الإجراءات|عمليات/.test(label) || (hasButtons && i === cells.length - 1)) {
                    td.classList.add('mc-actions');
                    td.querySelectorAll('button, .btn').forEach(classifyButton);
                } else if (!titleDone && (TITLE_HEADERS.includes(label) || (i === 0 && !HIDE_HEADERS.includes(label)) || (i === 1 && HIDE_HEADERS.includes(headers[0])))) {
                    td.classList.add('mc-title');
                    titleDone = true;
                    if (!td.querySelector('.mc-avatar') && textOnly) {
                        const avatar = document.createElement('span');
                        avatar.className = 'mc-avatar';
                        avatar.setAttribute('aria-hidden', 'true');
                        avatar.textContent = td.textContent.trim().charAt(0);
                        td.insertBefore(avatar, td.firstChild);
                    }
                } else if (CHIP_HEADERS.includes(label) || td.querySelector('.status-badge, .badge')) {
                    td.classList.add('mc-chip');
                }
            });
            tr.dataset.mcDone = String(tr.cells.length) + '|' + tr.textContent.length;
        });
    }

    function enhanceAll() {
        document.querySelectorAll('.content table').forEach(enhanceTable);
    }

    let pending = null;
    function scheduleEnhance() {
        if (pending) return;
        pending = requestAnimationFrame(() => { pending = null; enhanceAll(); });
    }

    function start() {
        enhanceAll();
        const root = document.querySelector('.main') || document.body;
        new MutationObserver(muts => {
            for (const m of muts) {
                if (m.addedNodes.length && !(m.target.classList && m.target.classList.contains('mc-title'))) {
                    scheduleEnhance();
                    return;
                }
            }
        }).observe(root, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();

    global.NoorListTools = { sort: sortList, enhanceTables: enhanceAll };
})(window);
