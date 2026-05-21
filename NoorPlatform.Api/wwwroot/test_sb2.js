        // ===== API CONFIG =====
        const API_URL = '/api';
        let TOKEN = localStorage.getItem('noor_token');
        let USER = {};
        try { USER = JSON.parse(localStorage.getItem('noor_user') || '{}'); } catch { USER = {}; }

        function escapeHtml(str) {
            if (str == null) return '';
            return String(str)
                .replace(/&/g, '&amp;')
                .replace(/</g, '&lt;')
                .replace(/>/g, '&gt;')
                .replace(/"/g, '&quot;')
                .replace(/'/g, '&#39;');
        }

        function isTokenExpired(token) {
            try {
                const payload = JSON.parse(atob(token.split('.')[1]));
                return payload.exp * 1000 < Date.now();
            } catch { return true; }
        }

        // ===== AUTH =====
        function checkAuth() {
            if (TOKEN && isTokenExpired(TOKEN)) {
                logout();
                return;
            }
            if (!TOKEN) {
                document.getElementById('loginScreen').style.display = 'flex';
            } else {
                document.getElementById('loginScreen').style.display = 'none';
                updateUserInfo();
                applyRoleUI(); // Enforce strict RBAC on frontend

                // Only load admin/teacher data if authorized
                if (USER.role === 'Admin' || USER.role === 'Teacher') {
                    fetchStats();
                    fetchLeaderboard();
                    fetchAnnouncements();
                    fetchStudents();
                    fetchTeachers();
                    fetchCircles();
                }

                // Restore hash state if applicable
                var hash = location.hash.replace('#', '');
                if (hash && document.getElementById('page-' + hash)) {
                    setTimeout(function () {
                        var navEl = document.querySelector('[onclick*="navigate(\'' + hash + '\'"]');
                        if (navEl) navigate(hash, navEl);
                    }, 100);
                }
            }
        }

        function toggleAuthMode(mode) {
            if (mode === 'register') {
                document.getElementById('loginForm').style.display = 'none';
                document.getElementById('registerForm').style.display = 'block';
            } else {
                document.getElementById('loginForm').style.display = 'block';
                document.getElementById('registerForm').style.display = 'none';
            }
        }

        async function handleLogin() {
            const phoneInput = document.getElementById('loginPhone');
            const passInput = document.getElementById('loginPassword');
            clearValidation(document.getElementById('loginForm'));
            const isValid = validateForm([
                { id: 'loginPhone', required: true, requiredMsg: 'يرجى إدخال رقم الجوال', pattern: /^05\d{8}$/, patternMsg: 'الرقم يجب أن يبدأ بـ 05 ويتكون من 10 أرقام' },
                { id: 'loginPassword', required: true, requiredMsg: 'يرجى إدخال كلمة المرور', minLength: 6, minLengthMsg: 'كلمة المرور يجب أن تكون 6 أحرف على الأقل' }
            ]);
            if (!isValid) return;

            const loginBtn = document.querySelector('#loginForm .btn-primary');
            btnLoading(loginBtn, true);

            try {
                const res = await fetch(`${API_URL}/auth/login`, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ phone: phoneInput.value.trim(), password: passInput.value })
                });

                if (res.status === 503 || res.status === 502 || res.status === 500) {
                    throw new Error('الخادم غير متوفر حالياً. يرجى التأكد من تشغيل الخادم.');
                }

                if (!res.ok) {
                    const errData = await res.json().catch(() => ({}));
                    throw new Error(errData.message || 'رقم الجوال أو كلمة المرور غير صحيحة');
                }

                const data = await res.json();
                TOKEN = data.token;
                USER = {
                    fullName: data.user?.fullName || data.fullName,
                    role: data.user?.role || data.role,
                    phone: data.user?.phone
                };
                localStorage.setItem('noor_token', TOKEN);
                localStorage.setItem('noor_user', JSON.stringify(USER));

                if (data.mustChangePassword) {
                    openModal('changePasswordModal');
                    showToast('⚠️ يرجى تغيير كلمة المرور المؤقتة');
                } else {
                    showToast('✅ تم تسجيل الدخول بنجاح');
                    checkAuth();
                }
            } catch (err) {
                showToast('❌ ' + err.message);
            } finally {
                btnLoading(loginBtn, false);
            }
        }

        async function submitChangePassword() {
            const current = document.getElementById('currentPasswordInput').value;
            const newPass = document.getElementById('newPasswordInput').value;
            if (!current || newPass.length < 8) {
                showToast('⚠️ كلمة المرور الجديدة 8 أحرف على الأقل');
                return;
            }
            try {
                await apiFetch('/auth/change-password', 'POST', { currentPassword: current, newPassword: newPass });
                closeModal('changePasswordModal');
                showToast('✅ تم تغيير كلمة المرور');
                checkAuth();
            } catch (e) {
                showToast('❌ ' + (e.message || 'فشل التغيير'));
            }
        }