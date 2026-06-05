/**
 * منصة نور — نواة التطبيق والحالة المشتركة
 */
(function (global) {
    'use strict';

    const app = {
        state: {
            apiUrl: '/api',
            token: null,
            user: {},
            circles: [],
            students: { all: [], waiting: [], filter: 'all', archive: false, waitingMode: false },
            attendance: { date: new Date(), circleId: null, pending: {}, records: [] },
            library: { items: [] },
            lastAccountCredentials: null
        },
        utils: {},
        api: {},
        ui: {}
    };

    global.NoorApp = app;

    // ─── إصلاح: إدارة بيئة الإنتاج والطباعة الآمنة (Security Cleanup) ───
    const isProduction = true; // يمكن ضبطها عبر إعدادات الـ Build أو البيئة
    function log(...args) {
        if (!isProduction) {
            console.log(...args);
        }
    }
    app.log = log;
    global.log = log;

    // ─── إصلاح: مؤشر حالة الشبكة (Offline Awareness) ───
    function initNetworkAwareness() {
        window.addEventListener('offline', () => {
            let bar = document.getElementById('offline-notification');
            if (!bar) {
                bar = document.createElement('div');
                bar.id = 'offline-notification';
                bar.style.cssText = 'position:fixed;top:0;left:0;right:0;background:var(--red,#dc2626);color:#fff;text-align:center;padding:12px;z-index:9999;font-weight:bold;box-shadow:0 4px 12px rgba(0,0,0,0.1);font-size:14px;';
                bar.innerHTML = '⚠️ لا يوجد اتصال بالإنترنت. المنصة تعمل الآن في وضع عدم الاتصال.';
                document.body.prepend(bar);
            }
            bar.style.display = 'block';
        });

        window.addEventListener('online', () => {
            const bar = document.getElementById('offline-notification');
            if (bar) {
                bar.style.background = 'var(--green,#16a34a)';
                bar.innerHTML = '✅ عاد الاتصال بالإنترنت.';
                setTimeout(() => { bar.style.display = 'none'; }, 3000);
            }
        });
    }

    function migrateAuthStorage() {
        const legacyToken = localStorage.getItem('noor_token');
        const legacyUser = localStorage.getItem('noor_user');
        if (legacyToken && !sessionStorage.getItem('noor_token')) {
            sessionStorage.setItem('noor_token', legacyToken);
            if (legacyUser) sessionStorage.setItem('noor_user', legacyUser);
        }
        localStorage.removeItem('noor_token');
        localStorage.removeItem('noor_user');
    }

    function loadSession() {
        migrateAuthStorage();
        app.state.token = sessionStorage.getItem('noor_token');
        try {
            app.state.user = JSON.parse(sessionStorage.getItem('noor_user') || '{}');
        } catch {
            app.state.user = {};
        }
    }

    function saveSession(token, user) {
        app.state.token = token;
        app.state.user = user || {};
        if (token) sessionStorage.setItem('noor_token', token);
        else sessionStorage.removeItem('noor_token');
        sessionStorage.setItem('noor_user', JSON.stringify(app.state.user));
    }

    function clearSession() {
        app.state.token = null;
        app.state.user = {};
        sessionStorage.removeItem('noor_token');
        sessionStorage.removeItem('noor_user');
        localStorage.removeItem('noor_token');
        localStorage.removeItem('noor_user');
    }

    function isTokenExpired(token) {
        if (!token) return true;
        try {
            const payload = JSON.parse(atob(token.split('.')[1]));
            return payload.exp * 1000 < Date.now();
        } catch {
            return true;
        }
    }

    // ─── إصلاح: تخزين بيانات المستخدم لتقليل الاستعلامات (Performance) ───
    async function getCachedProfile() {
        const cached = sessionStorage.getItem('noor_user_profile');
        const cachedTime = sessionStorage.getItem('noor_user_profile_time');
        const now = Date.now();
        
        // Cache valid for 5 minutes
        if (cached && cachedTime && (now - parseInt(cachedTime)) < 5 * 60 * 1000) {
            return JSON.parse(cached);
        }
        
        try {
            if (app.api && typeof app.api.apiFetch === 'function') {
                const profile = await app.api.apiFetch('/auth/profile');
                if (profile) {
                    sessionStorage.setItem('noor_user_profile', JSON.stringify(profile));
                    sessionStorage.setItem('noor_user_profile_time', now.toString());
                    return profile;
                }
            }
        } catch(e) {
            log('Failed to fetch profile', e);
        }
        return null;
    }

    app.loadSession = loadSession;
    app.saveSession = saveSession;
    app.clearSession = clearSession;
    app.isTokenExpired = isTokenExpired;
    app.getCachedProfile = getCachedProfile;

    loadSession();
    initNetworkAwareness();

    /** بعد تحميل utils.js يُستدعى getNoorUtils لمزامنة utils */
    global.syncNoorUtils = function () {
        if (typeof global.getNoorUtils === 'function') global.getNoorUtils();
    };

    /** جسر توافق مؤقت مع onclick القديم */
    Object.defineProperty(global, 'TOKEN', {
        get: () => app.state.token,
        set: (v) => {
            app.state.token = v;
            if (v) sessionStorage.setItem('noor_token', v);
            else sessionStorage.removeItem('noor_token');
        },
        configurable: true
    });
    Object.defineProperty(global, 'USER', {
        get: () => app.state.user,
        set: (v) => {
            app.state.user = v || {};
            sessionStorage.setItem('noor_user', JSON.stringify(app.state.user));
        },
        configurable: true
    });
    Object.defineProperty(global, 'API_URL', {
        get: () => app.state.apiUrl,
        configurable: true
    });
    Object.defineProperty(global, '_allStudentsData', {
        get: () => app.state.students.all,
        set: (v) => { app.state.students.all = v || []; },
        configurable: true
    });
    Object.defineProperty(global, '_circles', {
        get: () => app.state.circles,
        set: (v) => { app.state.circles = v || []; },
        configurable: true
    });
})(window);
