/**
 * منصة نور — تذكيرات الطالب الذكية:
 *  • «هل قرأت وردك اليوم؟» مرة واحدة يومياً (مع زر «نعم، قرأته» و«ذكّرني لاحقاً»).
 *  • تذكير بدخول وقت كل صلاة (مواقيت مصراتة من الخادم) مع زر لتسجيلها مباشرة.
 * تظهر كبطاقة متحركة أنيقة أعلى الشاشة، وتُفعَّل/تُعطَّل من «الإعدادات» لدى المشرف.
 */
(function (global) {
    'use strict';

    const app = () => global.NoorApp;
    const STORAGE_PREFIX = 'noor_reminder_';
    let prayerTimer = null;
    let todayTimes = null;

    function store(key, value) {
        try {
            if (value === undefined) return localStorage.getItem(STORAGE_PREFIX + key);
            localStorage.setItem(STORAGE_PREFIX + key, value);
        } catch { return null; }
    }

    function todayKey() {
        const d = new Date();
        return `${d.getFullYear()}-${d.getMonth() + 1}-${d.getDate()}`;
    }

    function icon(name, size) {
        return global.Icon ? global.Icon(name, { size: size || 20 }) : '';
    }

    function ensureHost() {
        let host = document.getElementById('noorReminderHost');
        if (!host) {
            host = document.createElement('div');
            host.id = 'noorReminderHost';
            host.className = 'reminder-host';
            host.setAttribute('aria-live', 'polite');
            document.body.appendChild(host);
        }
        return host;
    }

    /** يعرض بطاقة تذكير متحركة. actions: [{label, primary, onClick}] */
    function showReminder({ id, iconName, title, body, actions, tone }) {
        const host = ensureHost();
        if (document.getElementById('reminder-' + id)) return;
        const card = document.createElement('div');
        card.className = 'reminder-card tone-' + (tone || 'green');
        card.id = 'reminder-' + id;
        card.setAttribute('role', 'status');
        card.innerHTML = `
            <div class="reminder-icon">${icon(iconName || 'bell', 22)}</div>
            <div class="reminder-content">
                <div class="reminder-title">${title}</div>
                ${body ? `<div class="reminder-body">${body}</div>` : ''}
                <div class="reminder-actions"></div>
            </div>
            <button type="button" class="reminder-close" aria-label="إغلاق">${icon('x', 16)}</button>`;
        const close = () => {
            card.classList.add('leaving');
            setTimeout(() => card.remove(), 320);
        };
        card.querySelector('.reminder-close').onclick = close;
        const actionsEl = card.querySelector('.reminder-actions');
        (actions || []).forEach(a => {
            const b = document.createElement('button');
            b.type = 'button';
            b.className = 'btn ' + (a.primary ? 'btn-primary' : 'btn-outline');
            b.textContent = a.label;
            b.onclick = () => { try { a.onClick && a.onClick(); } finally { close(); } };
            actionsEl.appendChild(b);
        });
        host.appendChild(card);
        requestAnimationFrame(() => card.classList.add('visible'));
        // يختفي تلقائياً بعد دقيقة إن لم يتفاعل الطالب
        setTimeout(() => { if (card.isConnected) close(); }, 60000);
    }

    function wirdReminder() {
        const today = todayKey();
        if (store('wird_done') === today) return;
        const snoozeUntil = parseInt(store('wird_snooze') || '0', 10);
        if (Date.now() < snoozeUntil) return;
        showReminder({
            id: 'wird',
            iconName: 'book-open',
            tone: 'gold',
            title: 'هل قرأت وردك اليوم؟',
            body: 'خيركم من تعلّم القرآن وعلّمه — حافظ على وردك اليومي ولو قليلاً.',
            actions: [
                { label: 'نعم، قرأته', primary: true, onClick: () => store('wird_done', today) },
                { label: 'ذكّرني بعد ساعتين', onClick: () => store('wird_snooze', String(Date.now() + 2 * 3600 * 1000)) }
            ]
        });
    }

    function minutesOf(hhmm) {
        const [h, m] = String(hhmm || '').split(':').map(Number);
        return h * 60 + m;
    }

    function checkPrayerTimes() {
        if (!todayTimes?.prayers) return;
        const now = new Date();
        const nowMin = now.getHours() * 60 + now.getMinutes();
        const today = todayKey();
        todayTimes.prayers.forEach(p => {
            const at = minutesOf(p.time);
            // نافذة التذكير: من الأذان حتى 40 دقيقة بعده، مرة واحدة لكل صلاة يومياً
            if (nowMin >= at && nowMin <= at + 40 && store('prayer_' + p.prayer) !== today) {
                store('prayer_' + p.prayer, today);
                showReminder({
                    id: 'prayer-' + p.prayer,
                    iconName: 'moon',
                    tone: 'green',
                    title: `حان وقت صلاة ${p.name}`,
                    body: `أُذِّن لصلاة ${p.name} الساعة ${p.time} بتوقيت مصراتة. صلاة الجماعة في المسجد أعظم أجراً.`,
                    actions: [
                        { label: 'سجّل صلاتك', primary: true, onClick: () => global.navigate && global.navigate('myPrayer', document.querySelector('[onclick*="navigate(\'myPrayer\'"]')) },
                        { label: 'لاحقاً' }
                    ]
                });
            }
        });
    }

    async function loadTimes() {
        try {
            todayTimes = await app().api.apiFetch('/pedagogical/prayer-times');
        } catch { todayTimes = null; }
    }

    async function start() {
        const user = app()?.state?.user;
        if (!user || user.role !== 'Student') return;
        const settings = global.NoorPublicSettings
            || await fetch((app().state.apiUrl || '/api') + '/settings/public').then(r => r.ok ? r.json() : {}).catch(() => ({}));
        if (settings && settings.studentRemindersEnabled === false) return;

        await loadTimes();
        setTimeout(wirdReminder, 2500);
        checkPrayerTimes();
        clearInterval(prayerTimer);
        prayerTimer = setInterval(async () => {
            // تحديث المواقيت عند تغيّر اليوم
            const d = new Date();
            const ymd = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
            if (!todayTimes || todayTimes.date !== ymd) await loadTimes();
            checkPrayerTimes();
        }, 60000);
    }

    global.NoorReminders = { start, show: showReminder };
})(window);
