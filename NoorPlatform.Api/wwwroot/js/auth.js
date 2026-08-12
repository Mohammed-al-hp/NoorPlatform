/**
 * منصة نور — المصادقة والصلاحيات
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    function apiFetch(endpoint, method, body) {
        return app().api.apiFetch(endpoint, method, body);
    }
    function handleApiError(err, opts) {
        return app().api.handleApiError(err, opts);
    }
    const ui = () => app().ui;
    const U = () => (typeof global.getNoorUtils === 'function' ? global.getNoorUtils() : (global.NoorUtils || app().utils));

    // صفحات إدارية — لا يجوز للطالب/ولي الأمر فتحها (تسبب 403 عند التحميل التلقائي)
    // المصدر الموحّد معرَّف في modals.js (global.NoorStaffOnlyPages)
    const STAFF_ONLY_PAGES = new Set(global.NoorStaffOnlyPages || [
        'attendance', 'memorization', 'exams',
        'students', 'teachers', 'circles', 'payments',
        'reports', 'settings', 'users', 'parents'
    ]);

    function checkAuth() {
        const state = app().state;
        if (state.token && app().isTokenExpired(state.token)) {
            logout();
            return;
        }

        const loginScreen = document.getElementById('loginScreen');
        if (!state.token) {
            if (loginScreen) loginScreen.style.display = 'flex';
            return;
        }

        if (loginScreen) loginScreen.style.display = 'none';
        updateUserInfo();
        applyRoleUI();

        const role = state.user.role;
        if (role === 'Admin' || role === 'Teacher') {
            global.NoorDashboard?.fetchStats?.();
            global.NoorDashboard?.fetchLeaderboard?.();
            global.NoorDashboard?.fetchAnnouncements?.();
            global.NoorStudents?.fetchStudents?.();
            if (typeof fetchTeachers === 'function') fetchTeachers();
            if (typeof fetchCircles === 'function') fetchCircles();
        }

        const hash = location.hash.replace('#', '');
        const isStaff = role === 'Admin' || role === 'Teacher';
        // تجاهل hash الصفحات الإدارية للطالب/ولي الأمر حتى لا يُستدعى API بـ 403
        if (hash && document.getElementById('page-' + hash)) {
            if (!isStaff && STAFF_ONLY_PAGES.has(hash)) {
                history.replaceState({ page: role === 'Parent' ? 'parentView' : 'studentView' }, '', '#' + (role === 'Parent' ? 'parentView' : 'studentView'));
                return;
            }
            setTimeout(() => {
                const navEl = document.querySelector('[onclick*="navigate(\'' + hash + '\'"]');
                if (navEl) global.navigate(hash, navEl);
            }, 100);
        }
    }

    function applyRoleUI() {
        const role = app().state.user.role;
        const isAdmin = role === 'Admin';
        const isTeacher = role === 'Teacher';
        const isStudent = role === 'Student';
        const isParent = role === 'Parent';
        const staffSection = document.getElementById('staffSection');
        const adminOnlySection = document.getElementById('adminOnlySection');
        const studentSection = document.getElementById('studentSection');
        const parentSection = document.getElementById('parentSection');
        const libraryNavSection = document.getElementById('libraryNavSection');
        const libraryAdminActions = document.getElementById('libraryAdminActions');
        const trackingNavSection = document.getElementById('trackingNavSection');
        const isStaff = isAdmin || isTeacher;

        // صفحات المتابعة الإدارية (حضور / تسميع / اختبارات) — للمحفّظ والمشرف فقط
        if (trackingNavSection) trackingNavSection.style.display = isStaff ? 'block' : 'none';

        // ─── إصلاح: إخفاء أزرار الوسام/الشهادة الإدارية عن الطالب نفسه ───
        const quranMapActions = document.getElementById('quranMapAdminActions');
        if (quranMapActions) quranMapActions.style.display = isStaff ? 'flex' : 'none';

        // ─── منع المحفظ من اختيار (المحفظون فقط) عند إنشاء إعلان ───
        const annTargetTeachers = document.querySelector('#annTarget option[value="Teachers"]');
        if (annTargetTeachers) annTargetTeachers.style.display = isAdmin ? 'block' : 'none';

        // ─── إخفاء عناصر الداشبورد العامة عن الطالب وولي الأمر ───
        const dashStatGrid = document.querySelector('#page-dashboard .stat-grid');
        if (dashStatGrid) dashStatGrid.style.display = (isStudent || isParent) ? 'none' : 'grid';
        
        const dashLeaderboard = document.querySelector('#leaderboardWrap')?.closest('.table-card');
        if (dashLeaderboard) dashLeaderboard.style.display = (isStudent || isParent) ? 'none' : 'block';
        
        const dashQuickActions = document.querySelector('#page-dashboard .quick-actions');
        if (dashQuickActions) dashQuickActions.style.display = (isAdmin || isTeacher) ? 'flex' : 'none';

        // لوحة التحفيز أصبحت داخل الصفحة الرئيسية للطالب (#page-studentView)
        document.querySelectorAll('#page-dashboard .charts-row, #page-dashboard .table-card').forEach(el => {
            if (isStudent || isParent) el.style.display = 'none';
        });

        if (staffSection) staffSection.style.display = isStaff ? 'block' : 'none';
        if (adminOnlySection) adminOnlySection.style.display = isAdmin ? 'block' : 'none';
        if (isAdmin || isTeacher) {
            if (studentSection) studentSection.style.display = 'none';
            if (parentSection) parentSection.style.display = 'none';
            if (libraryNavSection) libraryNavSection.style.display = 'block';
            if (libraryAdminActions) libraryAdminActions.style.display = 'flex';
        } else if (isParent) {
            if (studentSection) studentSection.style.display = 'none';
            if (parentSection) parentSection.style.display = 'block';
            if (libraryNavSection) libraryNavSection.style.display = 'block';
            if (libraryAdminActions) libraryAdminActions.style.display = 'none';
        } else {
            if (studentSection) studentSection.style.display = 'block';
            if (parentSection) parentSection.style.display = 'none';
            if (libraryNavSection) libraryNavSection.style.display = 'block';
            if (libraryAdminActions) libraryAdminActions.style.display = 'none';
        }

        // الشريط السفلي والـ FAB: إخفاء روابط الإدارة عن الطالب
        document.querySelectorAll('#bottomNav .bottom-nav-item').forEach(item => {
            const onclick = item.getAttribute('onclick') || '';
            const isAdminNav = /navBottom\('(students|attendance|memorization)'/.test(onclick);
            if (isAdminNav) item.style.display = isStaff ? '' : 'none';
        });
        document.querySelectorAll('#fabMenu .fab-menu-item').forEach(item => {
            const onclick = item.getAttribute('onclick') || '';
            const isAdminFab = /navigate\('(students|attendance|memorization|circles)'/.test(onclick);
            if (isAdminFab) item.style.display = isStaff ? '' : 'none';
        });

        if (isStudent) {
            // الرئيسية للطالب = صفحة التحفيز (studentView)
            const homeNav = document.querySelector('#studentSection .nav-item');
            global.navigate('studentView', homeNav);
            if (typeof fetchStudentView === 'function') fetchStudentView();
            // توجيه أزرار «الرئيسية» لصفحة الطالب
            const bottomHome = document.querySelector('#bottomNav .bottom-nav-item');
            if (bottomHome) bottomHome.setAttribute('onclick', "navBottom('studentView', this)");
            const fabHome = document.querySelector('.fab-menu-item[onclick*="dashboard"]');
            if (fabHome) fabHome.setAttribute('onclick', "navigate('studentView',null);toggleFab()");
        } else if (isParent) {
            global.navigate('parentView', document.querySelector('#parentSection .nav-item'));
            if (typeof fetchParentView === 'function') fetchParentView();
        } else {
            global.navigate('dashboard', document.querySelector('.nav-item'));
        }
    }

    function updateUserInfo() {
        const user = app().state.user;
        const roleName = document.getElementById('roleName');
        const roleEmail = document.getElementById('roleEmail');
        const roleAvatar = document.getElementById('roleAvatar');
        if (roleName) roleName.textContent = user.fullName || '';
        if (roleEmail) roleEmail.textContent = user.role || '';
        if (roleAvatar) {
            roleAvatar.textContent = user.role === 'Admin' ? '👤' : user.role === 'Teacher' ? '👨‍🏫' : '👨‍🎓';
        }
        const topAv = document.getElementById('topbarAvatar');
        if (topAv && user.fullName) topAv.textContent = user.fullName.slice(0, 2);
    }

    function toggleAuthMode(mode) {
        const loginForm = document.getElementById('loginForm');
        const registerForm = document.getElementById('registerForm');
        if (!loginForm || !registerForm) return;
        if (mode === 'register') {
            loginForm.style.display = 'none';
            registerForm.style.display = 'block';
        } else {
            loginForm.style.display = 'block';
            registerForm.style.display = 'none';
        }
    }

    async function handleLogin() {
        const phoneInput = document.getElementById('loginPhone');
        const passInput = document.getElementById('loginPassword');
        ui().clearValidation(document.getElementById('loginForm'));
        const isValid = ui().validateForm([
            { id: 'loginPhone', required: true, requiredMsg: 'يرجى إدخال رقم الهاتف', patternLibyan: true },
            { id: 'loginPassword', required: true, requiredMsg: 'يرجى إدخال كلمة المرور', minLength: 6, minLengthMsg: 'كلمة المرور 6 أحرف على الأقل' }
        ]);
        if (!isValid) return;

        const loginBtn = document.querySelector('#loginForm .btn-primary');
        ui().btnLoading(loginBtn, true);

        try {
            const displayPhone = phoneInput.value.trim();
            const res = await fetch(app().state.apiUrl + '/auth/login', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    phone: displayPhone,
                    password: passInput.value
                })
            });

            if ([500, 502, 503].includes(res.status)) {
                throw Object.assign(new Error('الخادم غير متوفر'), { status: res.status });
            }

            if (!res.ok) {
                const errData = await res.json().catch(() => ({}));
                throw Object.assign(new Error(errData.message || 'بيانات الدخول غير صحيحة'), { status: res.status });
            }

            const data = await res.json();
            app().saveSession(data.token, {
                fullName: data.user?.fullName || data.fullName,
                role: data.user?.role || data.role,
                phone: data.user?.phone
            });

            // إخفاء شاشة الدخول فوراً في كلتا الحالتين
            const ls = document.getElementById('loginScreen');
            if (ls) ls.style.display = 'none';

            if (data.mustChangePassword) {
                ui().openModal('changePasswordModal');
                ui().showToast('⚠️ يرجى تغيير كلمة المرور المؤقتة');
            } else {
                const userName = data.user?.fullName || data.fullName || '';
                const firstName = userName.split(' ')[0] || 'مستخدماً';
                ui().showToast(`✅ أهلاً بك يا ${firstName}`);
                checkAuth();
            }
        } catch (err) {
            handleApiError(err, { skipLogout: true });
        } finally {
            ui().btnLoading(loginBtn, false);
        }
    }

    async function submitChangePassword() {
        const current = document.getElementById('currentPasswordInput')?.value;
        const newPass = document.getElementById('newPasswordInput')?.value;
        if (!current || !newPass || newPass.length < 8) {
            ui().showToast('⚠️ كلمة المرور الجديدة 8 أحرف على الأقل');
            return;
        }
        try {
            await apiFetch('/auth/change-password', 'POST', { currentPassword: current, newPassword: newPass });
            ui().closeModal('changePasswordModal');
            ui().showToast('✅ تم تغيير كلمة المرور');
            checkAuth();
        } catch (e) {
            handleApiError(e, { skipLogout: true });
        }
    }

    function logout() {
        app().clearSession();
        location.reload();
    }

    async function handleRegister() {
        ui().showToast('❌ التسجيل الذاتي غير متاح');
        toggleAuthMode('login');
    }

    const auth = {
        checkAuth,
        applyRoleUI,
        updateUserInfo,
        toggleAuthMode,
        handleLogin,
        submitChangePassword,
        logout,
        handleRegister
    };

    app().auth = auth;
    global.checkAuth = checkAuth;
    global.applyRoleUI = applyRoleUI;
    global.handleLogin = handleLogin;
    global.submitChangePassword = submitChangePassword;
    global.logout = logout;
    global.toggleAuthMode = toggleAuthMode;
    global.handleRegister = handleRegister;
    global.updateUserInfo = updateUserInfo;
})(window);
