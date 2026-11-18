/**
 * منصة نور — قائمة الحساب (زر الصورة الرمزية أعلى الصفحة)
 * تعرض اسم المستخدم ودوره، مع: تغيير كلمة المرور، الوضع الليلي، تسجيل الخروج.
 */
(function (global) {
    'use strict';

    const ic = (name, size) => (global.Icon ? global.Icon(name, { size: size || 16 }) : '');
    let menu = null;

    function currentUser() {
        return (global.NoorApp && global.NoorApp.state && global.NoorApp.state.user) || {};
    }

    function roleLabel(user) {
        if (typeof global.NoorRoleLabel === 'function') return global.NoorRoleLabel(user);
        const labels = { Admin: 'مشرف', Teacher: 'محفّظ', Student: 'طالب', Parent: 'ولي أمر' };
        return user.roleLabel || labels[user.role] || '';
    }

    function escapeText(s) {
        const d = document.createElement('div');
        d.textContent = s == null ? '' : String(s);
        return d.innerHTML;
    }

    function build() {
        menu = document.createElement('div');
        menu.className = 'account-menu';
        menu.id = 'accountMenu';
        menu.setAttribute('role', 'menu');
        document.body.appendChild(menu);

        document.addEventListener('click', (e) => {
            if (!menu.classList.contains('open')) return;
            if (menu.contains(e.target) || e.target.closest('#topbarAvatar')) return;
            close();
        });
        document.addEventListener('keydown', (e) => { if (e.key === 'Escape') close(); });
        let lastWidth = global.innerWidth;
        global.addEventListener('resize', () => { if (global.innerWidth !== lastWidth) { lastWidth = global.innerWidth; close(); } });
    }

    function render() {
        const user = currentUser();
        const isDark = document.body.classList.contains('dark-mode');
        menu.innerHTML = `
            <div class="account-menu-head">
                <strong>${escapeText(user.fullName || 'مستخدم')}</strong>
                <span>${escapeText(roleLabel(user))}${user.phone ? ' — ' + escapeText(user.phone) : ''}</span>
            </div>
            <button type="button" role="menuitem" data-act="mushaf">${ic('book-open')} المصحف (قالون)</button>
            <button type="button" role="menuitem" data-act="password">${ic('lock')} تغيير كلمة المرور</button>
            <button type="button" role="menuitem" data-act="dark">${ic(isDark ? 'sun' : 'moon')} ${isDark ? 'الوضع النهاري' : 'الوضع الليلي'}</button>
            <button type="button" role="menuitem" data-act="logout" class="danger">${ic('log-out')} تسجيل الخروج</button>`;
        menu.querySelectorAll('[data-act]').forEach(btn => btn.addEventListener('click', () => act(btn.dataset.act)));
    }

    function position(anchor) {
        const r = anchor.getBoundingClientRect();
        menu.style.top = (r.bottom + 8) + 'px';
        // تُحاذى حافتها اليمنى مع الصورة الرمزية فتمتد نحو محتوى الصفحة (يسار) ولا تغطي القائمة الجانبية
        const width = menu.offsetWidth || 240;
        let left = r.right - width;
        if (left < 8) left = Math.min(r.left, global.innerWidth - width - 8);
        menu.style.left = left + 'px';
    }

    function open() {
        const anchor = document.getElementById('topbarAvatar');
        if (!anchor) return;
        if (!menu) build();
        render();
        menu.classList.add('open');
        position(anchor);
        anchor.setAttribute('aria-expanded', 'true');
        menu.querySelector('button')?.focus();
    }

    function close() {
        if (!menu) return;
        menu.classList.remove('open');
        document.getElementById('topbarAvatar')?.setAttribute('aria-expanded', 'false');
    }

    function toggle() {
        if (menu && menu.classList.contains('open')) close(); else open();
    }

    function openVoluntaryPasswordChange() {
        ['currentPasswordInput', 'newPasswordInput', 'confirmNewPasswordInput'].forEach(id => {
            const el = document.getElementById(id);
            if (el) el.value = '';
        });
        const err = document.getElementById('changePasswordError');
        if (err) { err.style.display = 'none'; err.textContent = ''; }
        const intro = document.getElementById('changePasswordIntro');
        if (intro) intro.textContent = 'أدخل كلمة المرور الحالية ثم كلمة المرور الجديدة (8 أحرف على الأقل).';
        const cancel = document.getElementById('changePasswordCancel');
        if (cancel) cancel.style.display = '';
        const submit = document.getElementById('changePasswordSubmit');
        if (submit) submit.textContent = 'حفظ كلمة المرور';
        global.NoorApp?.ui?.openModal?.('changePasswordModal');
    }

    /** يعيد نافذة كلمة المرور لوضع «التغيير الإجباري» (بلا زر إلغاء). */
    function resetPasswordModal() {
        const intro = document.getElementById('changePasswordIntro');
        if (intro) intro.textContent = 'يجب تغيير كلمة المرور المؤقتة قبل المتابعة.';
        const cancel = document.getElementById('changePasswordCancel');
        if (cancel) cancel.style.display = 'none';
        const submit = document.getElementById('changePasswordSubmit');
        if (submit) submit.textContent = 'حفظ ومتابعة';
    }

    function closePasswordModal() {
        global.NoorApp?.ui?.closeModal?.('changePasswordModal');
        resetPasswordModal();
    }

    function act(action) {
        close();
        if (action === 'mushaf') global.NoorMushaf?.open({});
        else if (action === 'password') openVoluntaryPasswordChange();
        else if (action === 'dark' && typeof global.toggleDarkMode === 'function') global.toggleDarkMode();
        else if (action === 'logout' && typeof global.logout === 'function') global.logout();
    }

    function init() {
        const anchor = document.getElementById('topbarAvatar');
        if (!anchor || anchor.dataset.menuBound) return;
        anchor.dataset.menuBound = '1';
        anchor.setAttribute('role', 'button');
        anchor.setAttribute('tabindex', '0');
        anchor.setAttribute('aria-haspopup', 'menu');
        anchor.setAttribute('aria-expanded', 'false');
        anchor.setAttribute('title', 'حسابي');
        anchor.addEventListener('click', (e) => { e.stopPropagation(); toggle(); });
        anchor.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggle(); }
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

    global.NoorAccountMenu = { open, close, toggle, resetPasswordModal, closePasswordModal };
})(window);
