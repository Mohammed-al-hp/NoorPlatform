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
            attendance: { date: new Date(), circleId: null },
            library: { items: [] },
            lastAccountCredentials: null
        },
        utils: {},
        api: {},
        ui: {}
    };

    global.NoorApp = app;

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

    app.loadSession = loadSession;
    app.saveSession = saveSession;
    app.clearSession = clearSession;
    app.isTokenExpired = isTokenExpired;

    loadSession();

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
