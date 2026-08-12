/**
 * منصة نور — طبقة API ومعالجة الأخطاء
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const U = () => app().utils;

    function handleApiError(error, options) {
        const opts = options || {};
        const status = error?.status;
        let message = error?.message || 'حدث خطأ غير متوقع';

        if (status === 401) {
            message = 'انتهت الجلسة، يرجى تسجيل الدخول مجدداً';
            if (!opts.skipLogout) {
                // ─── إصلاح: توجيه المستخدم فوراً لصفحة تسجيل الدخول عند انتهاء الجلسة ───
                if (typeof global.logout === 'function') global.logout();
                else global.location.href = '/index.html';
            }
        } else if (status === 403) {
            // ─── إصلاح: رسالة واضحة عند رفض الوصول (403 Forbidden) ───
            message = error.message || 'عذراً، لا تملك صلاحية الوصول لهذا المحتوى';
        } else if (status === 404) {
            message = error.message || 'العنصر المطلوب غير موجود';
        } else if (status === 500 || status === 502 || status === 503) {
            message = 'الخادم غير متوفر حالياً. حاول لاحقاً';
        } else if (message.includes('Failed to fetch') || message.includes('NetworkError')) {
            message = 'لا يوجد اتصال بالإنترنت أو الخادم غير متاح';
        }

        if (!opts.silent && app().ui?.showToast) {
            app().ui.showToast('❌ ' + message);
        }
        return message;
    }

    async function apiFetch(endpoint, method, body) {
        const state = app().state;
        if (!state.token) {
            const err = new Error('يرجى تسجيل الدخول');
            err.status = 401;
            throw err;
        }

        const options = {
            method: method || 'GET',
            headers: {
                Authorization: 'Bearer ' + state.token,
                'Content-Type': 'application/json'
            }
        };
        if (body && options.method !== 'GET') {
            options.body = JSON.stringify(body);
        }

        let res;
        try {
            res = await fetch(state.apiUrl + endpoint, options);
        } catch {
            const err = new Error('لا يوجد اتصال بالإنترنت أو الخادم غير متاح');
            err.status = 0;
            throw err;
        }

        if (res.status === 401 || res.headers.get('Token-Expired') === 'true') {
            const err = new Error('انتهت الجلسة، يرجى تسجيل الدخول مجدداً');
            err.status = 401;
            throw err;
        }

        // ─── إصلاح: التقاط كود 403 وعرض تنبيه واضح قبل رمي الخطأ ───
        if (res.status === 403) {
            const payload = await res.json().catch(() => ({}));
            const err = new Error(payload.message || 'عذراً، لا تملك صلاحية الوصول');
            err.status = 403;
            throw err;
        }

        if (!res.ok) {
            const payload = await res.json().catch(() => ({}));
            const err = new Error(payload.message || 'HTTP ' + res.status);
            err.status = res.status;
            throw err;
        }

        if (res.status === 204) return null;
        const text = await res.text();
        return text ? JSON.parse(text) : null;
    }

    async function apiFetchSafe(endpoint, method, body, options) {
        try {
            return await apiFetch(endpoint, method, body);
        } catch (e) {
            handleApiError(e, options);
            return null;
        }
    }

    async function apiFetchBlob(endpoint) {
        const state = app().state;
        if (!state.token) {
            const err = new Error('يرجى تسجيل الدخول');
            err.status = 401;
            throw err;
        }

        let res;
        try {
            res = await fetch(state.apiUrl + endpoint, {
                method: 'GET',
                headers: { Authorization: 'Bearer ' + state.token }
            });
        } catch {
            const err = new Error('لا يوجد اتصال بالإنترنت أو الخادم غير متاح');
            err.status = 0;
            throw err;
        }

        if (res.status === 401) {
            const err = new Error('انتهت الجلسة، يرجى تسجيل الدخول مجدداً');
            err.status = 401;
            throw err;
        }

        if (!res.ok) {
            const payload = await res.json().catch(() => ({}));
            const err = new Error(payload.message || 'HTTP ' + res.status);
            err.status = res.status;
            throw err;
        }

        const blob = await res.blob();
        return URL.createObjectURL(blob);
    }

    async function openPdfInNewTab(endpoint, options) {
        const opts = options || {};
        try {
            const blobUrl = await apiFetchBlob(endpoint);
            window.open(blobUrl, '_blank');
        } catch (e) {
            handleApiError(e, opts);
        }
    }

    const api = { apiFetch, apiFetchSafe, handleApiError, apiFetchBlob, openPdfInNewTab };
    app().api = api;

    global.apiFetch = apiFetch;
    global.handleApiError = handleApiError;
    global.openPdfInNewTab = openPdfInNewTab;
})(window);