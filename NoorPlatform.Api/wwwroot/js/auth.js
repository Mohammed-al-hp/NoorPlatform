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
        'attendance', 'memorization', 'evaluation',
        'students', 'teachers', 'circles', 'payments',
        'reports', 'settings', 'users', 'parents', 'expenses'
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
        applyAdminPermissionUI();
        refreshCurrentUser();
        loadCenterName();
        // تذكيرات الطالب (الورد اليومي وأوقات الصلاة) — reminders.js يُحمَّل بعد هذا الملف
        setTimeout(() => global.NoorReminders?.start?.(), 0);

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
        const searchBox = document.querySelector('.sidebar .search-box');
        if (searchBox) searchBox.style.display = isStaff ? '' : 'none';
        // الطالب وولي الأمر: قراءة الإعلانات فقط + بلا لوحة التحكم
        document.querySelectorAll('[onclick*="addAnnModal"]').forEach(b => { b.style.display = isStaff ? '' : 'none'; });
        document.querySelectorAll('[onclick*="navigate(\'dashboard\'"], [onclick*="navBottom(\'dashboard\'"]').forEach(b => { if (!isStaff) b.style.display = 'none'; });


        // صفحات المتابعة الإدارية (حضور / تسميع / اختبارات) — للمحفّظ والمشرف فقط
        if (trackingNavSection) trackingNavSection.style.display = isStaff ? 'block' : 'none';
        // قسم «الرئيسية» (لوحة التحكم) للمحفّظ والمشرف؛ الطالب وولي الأمر لهما رئيسية في بوابتهما
        const homeNavSection = document.getElementById('homeNavSection');
        if (homeNavSection) homeNavSection.style.display = isStaff ? 'block' : 'none';

        // ─── إصلاح: إخفاء أزرار الوسام/الشهادة الإدارية عن الطالب نفسه ───
        const quranMapActions = document.getElementById('quranMapAdminActions');
        if (quranMapActions) quranMapActions.style.display = isStaff ? 'flex' : 'none';

        // ─── منع المحفظ من اختيار (المحفظون فقط) عند إنشاء إعلان ───
        const annTargetTeachers = document.querySelector('#annTarget option[value="Teachers"]');
        if (annTargetTeachers) annTargetTeachers.style.display = isAdmin ? 'block' : 'none';

        // ─── إخفاء عناصر الداشبورد العامة عن الطالب وولي الأمر ───
        const dashStatGrid = document.querySelector('#page-dashboard .stat-grid');
        if (dashStatGrid) dashStatGrid.style.display = (isStudent || isParent) ? 'none' : 'grid';

        // ─── بوابة المحفّظ: لا تظهر له بطاقة «المحفظون النشطون» (لا تخصه) ───
        const teachersCard = document.getElementById('dashTeachersCard');
        if (teachersCard) teachersCard.style.display = isTeacher ? 'none' : '';
        if (dashStatGrid) dashStatGrid.classList.toggle('stat-grid-3', isTeacher);
        const dashStudentsLabel = document.querySelector('#dashStudents + .stat-label');
        if (dashStudentsLabel) dashStudentsLabel.textContent = isTeacher ? 'طلاب حلقتي' : 'إجمالي الطلاب';
        const dashCirclesLabel = document.querySelector('#dashCircles + .stat-label');
        if (dashCirclesLabel) dashCirclesLabel.textContent = isTeacher ? 'حلقاتي' : 'الحلقات الفعالة';
        const dashNavLabel = document.querySelector('#homeNavSection .nav-item span');
        if (dashNavLabel) dashNavLabel.textContent = isTeacher ? 'بوابة المحفّظ' : 'لوحة التحكم والإحصائيات';
        // تسجيل الطلاب وتعديل بياناتهم للمشرفين فقط
        document.querySelectorAll('[onclick*="openModal(\'addStudentModal\')"], #btnAddStudent, #btnAddStudentHeader').forEach(b => {
            b.style.display = isAdmin ? '' : 'none';
        });
        document.body.classList.toggle('role-teacher', isTeacher);
        document.body.classList.toggle('role-admin', isAdmin);

        const dashLeaderboard = document.querySelector('#leaderboardWrap')?.closest('.table-card');
        if (dashLeaderboard) dashLeaderboard.style.display = (isStudent || isParent) ? 'none' : 'block';
        
        const dashQuickActions = document.querySelector('#page-dashboard .quick-actions');
        if (dashQuickActions) dashQuickActions.style.display = (isAdmin || isTeacher) ? 'flex' : 'none';

        // ─── إصلاح: إخفاء إجراءات حصرية للأدمن (إضافة محفّظ / إنشاء حلقة) عن المحفّظ ───
        document.querySelectorAll('.admin-only-action').forEach(btn => {
            btn.style.display = isAdmin ? '' : 'none';
        });
        // لوحة التحفيز أصبحت داخل الصفحة الرئيسية للطالب (#page-studentView)
        document.querySelectorAll('#page-dashboard .charts-row, #page-dashboard .table-card').forEach(el => {
            if (isStudent || isParent) el.style.display = 'none';
        });

        // ─── إصلاح: إخفاء صندوق الإشعارات اليومية العام (خاص بالمركز) عن الطالب/ولي الأمر ───
        const dailyNotifBox = document.getElementById('dailyNotifBox');
        if (dailyNotifBox) dailyNotifBox.style.display = (isStudent || isParent) ? 'none' : 'block';

        // ─── إصلاح: رابط "الرسائل" الأول خاص بصندوق وارد المحفّظ/الأدمن فقط ───
        const staffMessagesNav = document.getElementById('staffMessagesNav');
        if (staffMessagesNav) staffMessagesNav.style.display = isStaff ? '' : 'none';

        if (staffSection) staffSection.style.display = isTeacher && !isAdmin ? 'block' : 'none';
        if (adminOnlySection) adminOnlySection.style.display = isAdmin ? 'block' : 'none';
        const adminOpsSection = document.getElementById('adminOpsSection');
        if (adminOpsSection) adminOpsSection.style.display = isAdmin ? 'block' : 'none';
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
            const isAdminOnlyFab = /navigate\('circles'/.test(onclick);
            if (isAdminFab) item.style.display = (isAdminOnlyFab ? isAdmin : isStaff) ? '' : 'none';
        });

        if (isStudent) {
            // الرئيسية للطالب = صفحة التحفيز (studentView)
            const homeNav = document.querySelector('#studentSection .nav-item');
            global.navigate('studentView', homeNav);
            // توجيه أزرار «الرئيسية» لصفحة الطالب
            const bottomHome = document.querySelector('#bottomNav .bottom-nav-item');
            if (bottomHome) bottomHome.setAttribute('onclick', "navBottom('studentView', this)");
            const fabHome = document.querySelector('.fab-menu-item[onclick*="dashboard"]');
            if (fabHome) fabHome.setAttribute('onclick', "navigate('studentView',null);toggleFab()");
        } else if (isParent) {
            global.navigate('parentView', document.querySelector('#parentSection .nav-item'));
            const bottomHome = document.querySelector('#bottomNav .bottom-nav-item');
            if (bottomHome) bottomHome.setAttribute('onclick', "navBottom('parentView', this)");
            const fabHome = document.querySelector('.fab-menu-item[onclick*="dashboard"]');
            if (fabHome) fabHome.setAttribute('onclick', "navigate('parentView',null);toggleFab()");
        } else {
            global.navigate('dashboard', document.querySelector('.nav-item'));
        }

        if (isStudent || isParent) applyPortalNavVisibility();
    }

    async function applyPortalNavVisibility() {
        try {
            const cfg = await apiFetch('/pedagogical/portal-config');
            const show = cfg?.evaluationsVisible !== false;
            document.querySelectorAll('.nav-item').forEach(item => {
                const onclick = item.getAttribute('onclick') || '';
                if (onclick.includes("navigate('myEvaluations'")) {
                    item.style.display = show ? '' : 'none';
                }
            });
        } catch {
            /* الإعداد غير متاح — اترك الروابط ظاهرة */
        }
    }

    // ─── تسميات الأدوار بالعربية (لا تظهر Admin/Teacher في الواجهة) ───
    const ROLE_LABELS = { Admin: 'مشرف', Teacher: 'محفّظ', Student: 'طالب', Parent: 'ولي أمر' };
    function roleLabelFor(user) {
        if (!user) return '';
        if (user.role === 'Admin' && user.isSuperAdmin) return 'مدير عام';
        return user.roleLabel || ROLE_LABELS[user.role] || '';
    }

    /** هل يملك المشرف الحالي صلاحية معينة؟ (المدير العام يملك الكل) */
    function hasAdminPermission(perm) {
        const user = app().state.user || {};
        if (user.role !== 'Admin') return false;
        if (user.isSuperAdmin) return true;
        // جلسة قديمة بلا قائمة صلاحيات: لا نخفي شيئاً — الخادم هو المرجع
        if (!Array.isArray(user.adminPermissions)) return true;
        return user.adminPermissions.includes(perm);
    }

    /** يحدّث المستوى الإداري والصلاحيات وتسمية الدور من الخادم (تتغير دون إعادة الدخول). */
    async function refreshCurrentUser() {
        try {
            const me = await apiFetch('/auth/me');
            if (!me || !me.role) return;
            const current = app().state.user || {};
            app().saveSession(app().state.token, Object.assign({}, current, {
                fullName: me.fullName || current.fullName,
                role: me.role,
                roleLabel: me.roleLabel,
                isSuperAdmin: !!me.isSuperAdmin,
                adminPermissions: me.adminPermissions || []
            }));
            updateUserInfo();
            applyAdminPermissionUI();
        } catch { /* غير متصل — تبقى البيانات المخزنة */ }
    }

    /** اسم المركز الفعلي من الإعدادات بدل النص الثابت (الشريط الجانبي وشاشة الدخول). */
    async function loadCenterName() {
        try {
            const res = await fetch(app().state.apiUrl + '/settings/public');
            if (!res.ok) return;
            const data = await res.json();
            global.NoorPublicSettings = data;
            setCenterName(data.centerName);
        } catch { /* يبقى النص الافتراضي */ }
    }
    function setCenterName(name) {
        if (!name) return;
        const side = document.getElementById('sidebarCenterName');
        if (side) side.textContent = name;
        const login = document.getElementById('loginCenterName');
        if (login) login.textContent = name;
    }

    /** إخفاء ما لا يملك المشرف صلاحيته (الخادم يرفضه أصلاً — هذا لتنظيف الواجهة). */
    function applyAdminPermissionUI() {
        const user = app().state.user || {};
        const isAdmin = user.role === 'Admin';
        document.body.classList.toggle('is-super-admin', isAdmin && !!user.isSuperAdmin);
        if (!isAdmin) return;
        const finance = hasAdminPermission('ManageFinance');
        document.querySelectorAll('[onclick*="navigate(\'payments\'"], [onclick*="navigate(\'expenses\'"]').forEach(el => {
            el.style.display = finance ? '' : 'none';
        });
        document.querySelectorAll('.super-admin-only').forEach(el => {
            el.style.display = user.isSuperAdmin ? '' : 'none';
        });
        const settingsSave = document.getElementById('btnSaveSettings');
        if (settingsSave) settingsSave.disabled = !hasAdminPermission('ManageSettings');
    }

    function updateUserInfo() {
        const user = app().state.user;
        const roleName = document.getElementById('roleName');
        const roleEmail = document.getElementById('roleEmail');
        const roleAvatar = document.getElementById('roleAvatar');
        if (roleName) roleName.textContent = user.fullName || '';
        if (roleEmail) roleEmail.textContent = roleLabelFor(user);
        if (roleAvatar) {
            if (window.Icon) {
                var iconName = user.role === 'Admin' ? 'user' : user.role === 'Teacher' ? 'user-check' : 'graduation-cap';
                roleAvatar.innerHTML = window.Icon(iconName, { size: 22 });
            }
        }
        const topAv = document.getElementById('topbarAvatar');
        if (topAv && window.Icon) topAv.innerHTML = window.Icon('user', { size: 18 });
    }

    
    async function handleLogin() {
        const phoneInput = document.getElementById('loginPhone');
        const passInput = document.getElementById('loginPassword');
        ui().clearValidation(document.getElementById('loginForm'));
        const loginErrorBox = document.getElementById('loginError');
        if (loginErrorBox) { loginErrorBox.style.display = 'none'; loginErrorBox.textContent = ''; }
        const isValid = ui().validateForm([
            { id: 'loginPhone', required: true, requiredMsg: 'يرجى إدخال رقم الهاتف', pattern: /^(st\d{6}|[0-9٠-٩+\s]{9,15})$/i, patternMsg: 'أدخل رقم الهاتف أو اسم الدخول (مثل st123456)' },
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
                roleLabel: data.user?.roleLabel,
                isSuperAdmin: !!data.user?.isSuperAdmin,
                adminPermissions: data.user?.adminPermissions || [],
                phone: data.user?.phone
            });

            // إخفاء شاشة الدخول فوراً في كلتا الحالتين
            const ls = document.getElementById('loginScreen');
            if (ls) ls.style.display = 'none';

            if (data.mustChangePassword) {
                ui().openModal('changePasswordModal');
                ui().showToast('يرجى تغيير كلمة المرور المؤقتة', 'warning');
            } else {
                const userName = data.user?.fullName || data.fullName || '';
                const firstName = userName.split(' ')[0] || 'مستخدماً';
                ui().showToast(`أهلاً بك يا ${firstName}`, 'success');
                checkAuth();
            }
        } catch (err) {
            const loginErrorBox2 = document.getElementById('loginError');
            if (loginErrorBox2) {
                loginErrorBox2.textContent = err.message || 'بيانات الدخول غير صحيحة';
                loginErrorBox2.style.display = 'block';
            }
            handleApiError(err, { skipLogout: true, silent: true });
        } finally {
            ui().btnLoading(loginBtn, false);
        }
    }

    async function submitChangePassword() {
        const current = document.getElementById('currentPasswordInput')?.value;
        const newPass = document.getElementById('newPasswordInput')?.value;
        const confirmPass = document.getElementById('confirmNewPasswordInput')?.value;
        const errorBox = document.getElementById('changePasswordError');
        const showErr = (msg) => {
            if (errorBox) { errorBox.textContent = msg; errorBox.style.display = 'block'; }
            else ui().showToast(msg, 'warning');
        };
        if (errorBox) { errorBox.style.display = 'none'; errorBox.textContent = ''; }

        if (!current || !newPass || newPass.length < 8) {
            showErr('كلمة المرور الجديدة 8 أحرف على الأقل');
            return;
        }
        if (newPass !== confirmPass) {
            showErr('كلمة المرور الجديدة وتأكيدها غير متطابقين');
            return;
        }

        try {
            const cpRes = await apiFetch('/auth/change-password', 'POST', { currentPassword: current, newPassword: newPass });
            if (cpRes?.token) app().saveSession(cpRes.token, app().state.user);
            ui().closeModal('changePasswordModal');
            ui().showToast('تم تغيير كلمة المرور', 'success');
            checkAuth();
        } catch (e) {
            showErr(e.message || 'حدث خطأ أثناء تغيير كلمة المرور');
        }
    }

    /** قنوات رمز التحقق المفعّلة في الإعدادات: قناة واحدة = زر مباشر، قناتان = اختيار. */
    function applyOtpChannelOptions() {
        const channels = global.NoorPublicSettings?.otpChannels || ['WhatsApp'];
        const group = document.getElementById('fpChannelGroup');
        const btn = document.getElementById('fpSendBtn');
        if (group) group.style.display = channels.length > 1 ? '' : 'none';
        document.querySelectorAll('input[name="fpChannel"]').forEach(r => {
            r.closest('label').style.display = channels.includes(r.value) ? '' : 'none';
            r.checked = r.value === channels[0];
        });
        if (btn) btn.textContent = channels.length > 1
            ? 'إرسال رمز التحقق'
            : (channels[0] === 'Sms' ? 'إرسال رمز التحقق برسالة نصية' : 'إرسال رمز التحقق عبر واتساب');
    }

    function openForgotPasswordHelp() {
        const contactEl = document.getElementById('forgotPasswordContact');
        const btnsWrap = document.getElementById('forgotPasswordWhatsappBtns');
        const phones = (global.NoorPlatformConfig && global.NoorPlatformConfig.adminContactPhones) || [];

        if (contactEl) {
            contactEl.textContent = phones.length
                ? 'لم يصلك الرمز أو لا تملك رقماً مسجلاً؟ تواصل مع الإدارة عبر واتساب:'
                : 'لم يصلك الرمز أو لا تملك رقماً مسجلاً؟ تواصل مع مدير المنصة ليعيد تعيين كلمة المرور.';
        }
        if (btnsWrap) {
            btnsWrap.innerHTML = phones.map((phone, idx) => {
                const digits = phone.replace(/[^0-9]/g, '').replace(/^0/, '218');
                const label = phones.length > 1 ? `تواصل عبر واتساب (${idx + 1})` : 'تواصل عبر واتساب';
                return `<button type="button" class="btn btn-outline" style="width:100%;margin-bottom:8px" onclick="window.open('https://wa.me/${digits}','_blank')">${label}</button>`;
            }).join('');
        }
        const loginPhone = document.getElementById('loginPhone')?.value?.trim() || '';
        const fpPhone = document.getElementById('fpPhone');
        if (fpPhone && /^[0-9٠-٩]{10}$/.test(loginPhone)) fpPhone.value = loginPhone;
        applyOtpChannelOptions();
        fpGoToStep(1);
        ui().openModal('forgotPasswordModal');
    }

    // ─── استعادة كلمة المرور برمز التحقق (OTP) — 3 خطوات ───
    let fpTimer = null;
    const fp = { phone: '', code: '' };

    function fpShowError(msg) {
        const el = document.getElementById('fpError');
        if (!el) return ui().showToast(msg, 'error');
        el.textContent = msg;
        el.style.display = msg ? 'flex' : 'none';
        if (msg) el.scrollIntoView({ block: 'nearest' });
    }

    function fpShowInfo(msg) {
        const el = document.getElementById('fpInfo');
        if (!el) return;
        el.textContent = msg || '';
        el.style.display = msg ? 'block' : 'none';
    }

    function fpGoToStep(step) {
        [1, 2, 3].forEach(n => {
            const f = document.getElementById('fpStep' + n);
            if (f) f.style.display = n === step ? 'block' : 'none';
        });
        const label = document.getElementById('fpStepLabel');
        if (label) label.textContent = `الخطوة ${step} من 3`;
        fpShowError('');
        if (step === 1) fpShowInfo('');
        const focusId = { 1: 'fpPhone', 2: 'fpCode', 3: 'fpNewPassword' }[step];
        setTimeout(() => document.getElementById(focusId)?.focus(), 50);
    }

    function fpStartResendCountdown(seconds) {
        const btn = document.getElementById('fpResendBtn');
        if (!btn) return;
        clearInterval(fpTimer);
        let left = Math.max(0, seconds | 0);
        const tick = () => {
            btn.disabled = left > 0;
            btn.textContent = left > 0 ? `إعادة الإرسال (${left})` : 'إعادة الإرسال';
            if (left-- <= 0) clearInterval(fpTimer);
        };
        tick();
        fpTimer = setInterval(tick, 1000);
    }

    async function fpPost(path, body) {
        let res;
        try {
            res = await fetch(app().state.apiUrl + path, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            });
        } catch {
            throw Object.assign(new Error('لا يوجد اتصال بالإنترنت أو الخادم غير متاح'), { status: 0 });
        }
        const data = await res.json().catch(() => ({}));
        if (!res.ok) throw Object.assign(new Error(data.message || 'حدث خطأ، حاول مرة أخرى'), { status: res.status, data });
        return data;
    }

    async function fpRequestCode(isResend) {
        const phone = isResend ? fp.phone : (document.getElementById('fpPhone')?.value || '').trim();
        const U = global.NoorUtils || {};
        const valid = typeof U.isValidLibyanPhone === 'function' ? U.isValidLibyanPhone(phone) : /^09\d{8}$/.test(phone);
        if (!valid) return fpShowError('أدخل رقم هاتف صحيح يبدأ بـ 09 ويتكون من 10 أرقام');

        const btn = isResend ? document.getElementById('fpResendBtn') : document.querySelector('#fpStep1 .btn-primary');
        if (!isResend) ui().btnLoading(btn, true);
        const channel = document.querySelector('input[name="fpChannel"]:checked')?.value
            || (global.NoorPublicSettings?.otpChannels || ['WhatsApp'])[0];
        try {
            const data = await fpPost('/auth/forgot-password', { phone, channel });
            fp.phone = phone;
            fpGoToStep(2);
            fpShowInfo(data.message);
            fpStartResendCountdown(data.resendAfterSeconds || 60);
        } catch (e) {
            if (e.status === 429 && e.data?.retryAfterSeconds && fp.phone === phone) {
                fpGoToStep(2);
                fpStartResendCountdown(e.data.retryAfterSeconds);
            }
            fpShowError(e.message);
        } finally {
            if (!isResend) ui().btnLoading(btn, false);
        }
    }

    async function fpVerifyCode() {
        const code = (document.getElementById('fpCode')?.value || '').trim();
        if (!/^\d{6}$/.test(code)) return fpShowError('رمز التحقق يتكون من 6 أرقام');
        const btn = document.querySelector('#fpStep2 .btn-primary');
        ui().btnLoading(btn, true);
        try {
            await fpPost('/auth/verify-otp', { phone: fp.phone, code });
            fp.code = code;
            fpShowInfo('');
            fpGoToStep(3);
        } catch (e) {
            fpShowError(e.message);
        } finally {
            ui().btnLoading(btn, false);
        }
    }

    async function fpResetPassword() {
        const pass = document.getElementById('fpNewPassword')?.value || '';
        const confirmPass = document.getElementById('fpConfirmPassword')?.value || '';
        if (pass.length < 8 || !/\d/.test(pass)) return fpShowError('كلمة المرور 8 أحرف على الأقل وتحتوي رقماً واحداً على الأقل');
        if (pass !== confirmPass) return fpShowError('كلمة المرور وتأكيدها غير متطابقين');
        const btn = document.querySelector('#fpStep3 .btn-primary');
        ui().btnLoading(btn, true);
        try {
            const data = await fpPost('/auth/reset-password', { phone: fp.phone, code: fp.code, newPassword: pass });
            ['fpCode', 'fpNewPassword', 'fpConfirmPassword'].forEach(id => { const el = document.getElementById(id); if (el) el.value = ''; });
            clearInterval(fpTimer);
            ui().closeModal('forgotPasswordModal');
            const loginPhone = document.getElementById('loginPhone');
            if (loginPhone) loginPhone.value = fp.phone;
            ui().showToast(data.message || 'تم تعيين كلمة المرور الجديدة', 'success');
            document.getElementById('loginPassword')?.focus();
        } catch (e) {
            // انتهاء الصلاحية أو تجاوز المحاولات يتطلب طلب رمز جديد
            if (/اطلب رمزاً جديداً/.test(e.message)) fpGoToStep(1);
            fpShowError(e.message);
        } finally {
            ui().btnLoading(btn, false);
        }
    }

    function logout() {
        app().clearSession();
        location.reload();
    }


    const auth = {
        checkAuth,
        applyRoleUI,
        updateUserInfo,
        handleLogin,
        submitChangePassword,
        logout,
        openForgotPasswordHelp,
        roleLabelFor,
        hasAdminPermission,
        refreshCurrentUser,
        loadCenterName,
        setCenterName
    };

    app().auth = auth;
    global.NoorRoleLabel = roleLabelFor;
    global.NoorHasAdminPermission = hasAdminPermission;
    global.NoorSetCenterName = setCenterName;
    // شاشة الدخول تعرض اسم المركز قبل تسجيل الدخول
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', loadCenterName);
    else loadCenterName();
    global.checkAuth = checkAuth;
    global.applyRoleUI = applyRoleUI;
    global.handleLogin = handleLogin;
    global.submitChangePassword = submitChangePassword;
    global.logout = logout;
    global.updateUserInfo = updateUserInfo;
    global.openForgotPasswordHelp = openForgotPasswordHelp;
    global.fpRequestCode = fpRequestCode;
    global.fpVerifyCode = fpVerifyCode;
    global.fpResetPassword = fpResetPassword;
    global.fpGoToStep = fpGoToStep;
})(window);
