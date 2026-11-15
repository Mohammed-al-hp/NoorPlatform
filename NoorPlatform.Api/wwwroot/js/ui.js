/**
 * منصة نور — واجهة المستخدم المشتركة
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;

    /** أعلى نافذة منبثقة مفتوحة حالياً (آخرها في DOM أو صاحبة أعلى z-index). */
    function getTopOpenModal() {
        const open = Array.from(document.querySelectorAll('.modal-overlay.open'));
        if (!open.length) return null;
        return open.reduce((top, el) => {
            const z = parseInt(getComputedStyle(el).zIndex, 10) || 0;
            const tz = parseInt(getComputedStyle(top).zIndex, 10) || 0;
            return z >= tz ? el : top;
        });
    }

    function clearModalAlert(overlay) {
        overlay?.querySelectorAll('.modal-inline-alert').forEach(n => n.remove());
    }

    /**
     * يعرض رسالة الخطأ داخل النافذة المنبثقة نفسها فوق الحقول — حتى لا تختفي خلف الخلفية
     * المعتمة ولا يضطر المستخدم للبحث عنها على الشاشات الصغيرة.
     */
    function showModalAlert(overlay, msg, type) {
        const modal = overlay?.querySelector('.modal');
        if (!modal) return;
        clearModalAlert(overlay);
        const box = document.createElement('div');
        box.className = 'modal-inline-alert modal-inline-alert-' + (type || 'error');
        box.setAttribute('role', 'alert');
        const text = document.createElement('span');
        text.textContent = msg;
        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'modal-inline-alert-close';
        close.textContent = '×';
        close.setAttribute('aria-label', 'إغلاق');
        close.addEventListener('click', () => box.remove());
        box.appendChild(text);
        box.appendChild(close);
        const anchor = modal.querySelector('.modal-body') || modal.querySelector('form') || modal;
        anchor.insertBefore(box, anchor.firstChild);
        try { box.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); } catch { box.scrollIntoView(); }
    }

    function showToast(msg, type) {
        const c = document.getElementById('toastContainer');
        if (!c) return;
        // الحاوية يجب أن تكون ابناً مباشراً لـ body حتى لا تحصرها أي stacking context تحت النوافذ
        if (c.parentElement !== document.body) document.body.appendChild(c);
        const resolvedType = type || 'success';
        if (resolvedType === 'error' || resolvedType === 'warning') {
            const overlay = getTopOpenModal();
            if (overlay) showModalAlert(overlay, msg, resolvedType);
        }
        const iconNames = { success: 'check-circle', error: 'x-circle', warning: 'alert-triangle', info: 'info' };

        const t = document.createElement('div');
        t.className = 'toast toast-' + resolvedType;

        const body = document.createElement('div');
        body.className = 'toast-body';
        const icon = document.createElement('span');
        icon.className = 'toast-icon';
        icon.innerHTML = global.Icon ? global.Icon(iconNames[resolvedType] || 'info', { size: 18 }) : '';
        const text = document.createElement('span');
        text.textContent = msg;
        body.appendChild(icon);
        body.appendChild(text);

        const closeBtn = document.createElement('button');
        closeBtn.className = 'toast-close';
        closeBtn.textContent = '×';
        closeBtn.setAttribute('aria-label', 'إغلاق');
        closeBtn.addEventListener('click', () => t.remove());

        const progress = document.createElement('div');
        progress.className = 'toast-progress';
        const progressFill = document.createElement('div');
        progressFill.className = 'toast-progress-fill';
        progressFill.style.animationDuration = '3.2s';
        progress.appendChild(progressFill);

        t.appendChild(body);
        t.appendChild(closeBtn);
        t.appendChild(progress);
        c.appendChild(t);
        setTimeout(() => t.remove(), 3200);
    }

    function openModal(id) {
        const el = document.getElementById(id);
        if (el) { clearModalAlert(el); el.classList.add('open'); }
        if (id === 'addStudentModal' && typeof global.populateExistingParentSelect === 'function') {
            global.populateExistingParentSelect();
        }
    }

    function closeModal(id) {
        const el = document.getElementById(id);
        if (el) { el.classList.remove('open'); clearModalAlert(el); }
    }

    function openSidebar() {
        document.getElementById('sidebar')?.classList.add('open');
        document.getElementById('sidebarOverlay')?.classList.add('open');
    }

    function closeSidebar() {
        document.getElementById('sidebar')?.classList.remove('open');
        document.getElementById('sidebarOverlay')?.classList.remove('open');
    }

    function toggleSidebar() {
        const sidebar = document.getElementById('sidebar');
        if (!sidebar) return;
        if (sidebar.classList.contains('open')) closeSidebar();
        else openSidebar();
    }

    function setModalBody(modalBodyId, html, modalTitle) {
        const body = document.getElementById(modalBodyId);
        if (!body) {
            showToast('❌ تعذر فتح نافذة العرض');
            return false;
        }
        body.innerHTML = html;
        if (modalTitle) {
            const titleEl = document.getElementById('profileModalTitle');
            if (titleEl) titleEl.innerHTML = modalTitle;
        }
        return true;
    }

    function btnLoading(btn, loading) {
        if (!btn) return;
        if (loading) {
            btn.dataset.origText = btn.innerHTML;
            btn.disabled = true;
            const spinner = global.Icon ? global.Icon('loader', { size: 16, className: 'spin-icon' }) : '';
            btn.innerHTML = spinner + ' جاري المعالجة...';
        } else {
            btn.disabled = false;
            if (btn.dataset.origText) btn.innerHTML = btn.dataset.origText;
        }
    }

    function clearValidation(container) {
        if (!container) return;
        container.querySelectorAll('.field-error').forEach(el => el.remove());
        container.querySelectorAll('.input-error').forEach(el => el.classList.remove('input-error'));
    }

    function getUtils() {
        if (typeof global.getNoorUtils === 'function') return global.getNoorUtils();
        return global.NoorUtils || app().utils || global;
    }

    function validateForm(rules) {
        const U = getUtils();
        const isValidPhone = typeof U.isValidLibyanPhone === 'function'
            ? (v) => U.isValidLibyanPhone(v)
            : (v) => (typeof global.isValidLibyanPhone === 'function'
                ? global.isValidLibyanPhone(v)
                : /^09\d{8}$/.test(String(v || '').replace(/\D/g, '')));
        const msgLibyan = typeof U.libyanPhonePatternMsg === 'function'
            ? U.libyanPhonePatternMsg()
            : 'رقم هاتف غير صالح';
        let valid = true;

        rules.forEach(rule => {
            const el = rule.id ? document.getElementById(rule.id) : document.querySelector(rule.selector);
            if (!el) return;
            const parent = el.closest('.form-group') || el.parentElement;
            let err = '';

            const val = (el.value || '').trim();
            if (rule.required && !val) err = rule.requiredMsg || 'حقل مطلوب';
            else if (rule.minLength && val.length < rule.minLength) err = rule.minLengthMsg || 'قصير جداً';
            else if (rule.patternLibyan || rule.patternLibyan === true) {
                if (!isValidPhone(val)) err = rule.patternMsg || msgLibyan;
            } else if (rule.pattern && !rule.pattern.test(val)) err = rule.patternMsg || 'قيمة غير صالحة';

            if (err) {
                valid = false;
                el.classList.add('input-error');
                el.classList.remove('input-success');
                const existing = parent?.querySelector('.error-msg, .field-error');
                if (existing) existing.remove();
                if (parent) {
                    const span = document.createElement('div');
                    span.className = 'error-msg';
                    span.textContent = err;
                    parent.appendChild(span);
                }
            } else if (val) {
                el.classList.remove('input-error');
                el.classList.add('input-success');
                parent?.querySelectorAll('.error-msg, .field-error').forEach(n => n.remove());
            }
        });
        return valid;
    }

    function setGlobalLoading(show, text) {
        const el = document.getElementById('globalLoading');
        const lbl = document.getElementById('loadingText');
        if (!el) return;
        el.style.display = show ? 'flex' : 'none';
        if (lbl && text) lbl.textContent = text;
    }

    const ui = {
        showToast,
        showModalAlert,
        clearModalAlert,
        openModal,
        closeModal,
        openSidebar,
        closeSidebar,
        toggleSidebar,
        setModalBody,
        btnLoading,
        clearValidation,
        validateForm,
        setGlobalLoading
    };

    // سجّل على window أولاً حتى لا يفشل onclick إذا تعثّر ربط NoorApp.ui
    global.showToast = showToast;
    global.openModal = openModal;
    global.closeModal = closeModal;
    global.openSidebar = openSidebar;
    global.closeSidebar = closeSidebar;
    global.toggleSidebar = toggleSidebar;
    global.setModalBody = setModalBody;
    global.btnLoading = btnLoading;
    global.clearValidation = clearValidation;
    global.validateForm = validateForm;

    try {
        const root = app();
        if (root) root.ui = ui;
    } catch (e) {
        console.warn('NoorApp.ui bind failed', e);
    }
})(window);