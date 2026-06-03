/**
 * منصة نور — واجهة المستخدم المشتركة
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;

    function showToast(msg, type) {
        const c = document.getElementById('toastContainer');
        if (!c) return;
        const t = document.createElement('div');
        t.className = 'toast' + (type ? ' toast-' + type : '');
        t.textContent = msg;
        c.appendChild(t);
        setTimeout(() => t.remove(), 3200);
    }

    function openModal(id) {
        const el = document.getElementById(id);
        if (el) el.classList.add('open');
    }

    function closeModal(id) {
        const el = document.getElementById(id);
        if (el) el.classList.remove('open');
    }

    function closeSidebar() {
        document.getElementById('sidebar')?.classList.remove('open');
        document.getElementById('sidebarOverlay')?.classList.remove('open');
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
            if (titleEl) titleEl.textContent = modalTitle;
        }
        return true;
    }

    function btnLoading(btn, loading) {
        if (!btn) return;
        if (loading) {
            btn.dataset.origText = btn.innerHTML;
            btn.disabled = true;
            btn.innerHTML = '⏳ جاري المعالجة...';
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
            : 'رقم جوال غير صالح';
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
        openModal,
        closeModal,
        closeSidebar,
        setModalBody,
        btnLoading,
        clearValidation,
        validateForm,
        setGlobalLoading
    };

    app().ui = ui;
    global.showToast = showToast;
    global.openModal = openModal;
    global.closeModal = closeModal;
    global.closeSidebar = closeSidebar;
    global.setModalBody = setModalBody;
    global.btnLoading = btnLoading;
    global.clearValidation = clearValidation;
    global.validateForm = validateForm;
})(window);
