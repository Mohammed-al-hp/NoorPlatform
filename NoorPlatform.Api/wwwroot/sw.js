// sw.js — Service Worker لمنصة نور (v10)
const CACHE_NAME = 'noor-v27'; // v27: نص قالون دون إنترنت، اختيار الآيات بنصّها، عارض المصحف، العدّ المدني؛ v26: صفحة متابعة الأبناء، المحفوظ بالترتيب العكسي؛ v25: إزالة البريد، الدخول بالهاتف أو اسم المستخدم، نطاق المتصدرين؛ v24: صفحة الإعدادات بتبويبات، سلّم التقديرات، نقاط الحضور والتقديرات، عناصر التقييم الاختيارية؛ v23: قائمة الحساب، تاريخ جلسة التسميع، محادثات الرسائل، تنبيه صلاحية الإعدادات؛ v22: تحسينات الهاتف (mobile.css)؛ v21: أيقونة الآيفون وفتح التطبيق المثبّت على index.html؛ v20: الإشعارات، مراجعة اليوم، الخريطة الحرارية، التحليلات، التقرير الشهري
const STATIC_ASSETS = ['/', '/landing.html', '/index.html', '/manifest.json', '/css/landing.css', '/css/fonts.css', '/img/logo-mark.svg', '/icon-192.png', '/icon-512.png', '/apple-touch-icon.png', '/css/app.css', '/css/enhancements.css', '/css/responsive.css', '/js/asset-loader.js', '/js/app.js', '/js/utils.js', '/js/api.js', '/js/ui.js', '/js/app-ui-extensions.js', '/js/page-controller.js', '/js/attendance.js', '/js/parents.js', '/js/hifz.js', '/js/students.js', '/js/dashboard.js', '/js/payments.js', '/js/teachers.js', '/js/users.js', '/js/reports.js', '/js/modals.js', '/js/pedagogical.js', '/js/monthly-rating.js', '/js/list-tools.js', '/js/reminders.js', '/js/smart-features.js', '/js/account-menu.js', '/js/messages.js', '/js/quran-text.js', '/js/mushaf-viewer.js', '/fonts/UthmanicQaloun.woff2', '/css/smart-features.css', '/css/mobile.css', '/lib/html2pdf.bundle.min.js'];
const FONT_CACHE = 'noor-fonts-v1';
// صفحات المصحف ونص قالون: ذاكرة مستقلة لا تُمسح مع تحديثات المنصة (تعمل دون إنترنت بعد أول عرض)
const QURAN_CACHE = 'noor-quran-v1';
const FONT_ORIGINS = ['https://fonts.googleapis.com', 'https://fonts.gstatic.com'];
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache =>
      Promise.allSettled(STATIC_ASSETS.map(url => cache.add(url)))
    )
  );
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys
          .filter(k => k !== CACHE_NAME && k !== FONT_CACHE && k !== QURAN_CACHE)
          .map(k => caches.delete(k))
      )
    ).then(() => self.clients.matchAll({ type: 'window' }))
      .then(clients => clients.forEach(client => client.postMessage({ type: 'SW_UPDATED' })))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  if (url.pathname.startsWith('/uploads/')) {
    event.respondWith(fetch(event.request));
    return;
  }

  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(event.request).catch(() =>
        new Response(JSON.stringify({ message: 'لا يوجد اتصال بالإنترنت' }),
          { status: 503, headers: { 'Content-Type': 'application/json; charset=utf-8' } })
      )
    );
    return;
  }

  if (FONT_ORIGINS.some(origin => url.href.startsWith(origin))) {
    event.respondWith(
      caches.open(FONT_CACHE).then(cache =>
        cache.match(event.request).then(cached => {
          if (cached) return cached;
          return fetch(event.request).then(response => {
            if (response.ok && event.request.method === 'GET') cache.put(event.request, response.clone());
            return response;
          });
        })
      )
    );
    return;
  }

  if (url.pathname.startsWith('/mushaf/') || url.pathname.startsWith('/data/')) {
    event.respondWith(
      caches.open(QURAN_CACHE).then(cache =>
        cache.match(event.request).then(cached => {
          if (cached) return cached;
          return fetch(event.request).then(response => {
            if (response.ok && event.request.method === 'GET') cache.put(event.request, response.clone());
            return response;
          });
        })
      )
    );
    return;
  }

  if (url.hostname === 'cdnjs.cloudflare.com') {
    event.respondWith(
      caches.open(CACHE_NAME).then(cache =>
        cache.match(event.request).then(cached => {
          if (cached) return cached;
          return fetch(event.request).then(response => {
            if (response.ok && event.request.method === 'GET') cache.put(event.request, response.clone());
            return response;
          });
        })
      )
    );
    return;
  }

  if (url.pathname === '/' || url.pathname === '/index.html' || url.pathname === '/landing.html' || url.pathname.endsWith('/sw.js') || url.pathname.endsWith('/manifest.json')) {
    event.respondWith(
      fetch(event.request)
        .then(response => {
          if (response.ok && event.request.method === 'GET') {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => caches.match(event.request).then(r => r || caches.match('/index.html')))
    );
    return;
  }

  // ملفات JS/CSS: شبكة أولاً حتى لا تبقى دوال ناقصة من كاش قديم
  if (url.pathname.startsWith('/js/') || url.pathname.startsWith('/css/') || url.pathname.endsWith('.js') || url.pathname.endsWith('.css')) {
    event.respondWith(
      fetch(event.request)
        .then(response => {
          if (response.ok && event.request.method === 'GET') {
            const clone = response.clone();
            caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => caches.match(event.request))
    );
    return;
  }

  event.respondWith(
    caches.match(event.request).then(cached => {
      if (cached) return cached;
      return fetch(event.request).then(response => {
        if (response.ok && event.request.method === 'GET' && event.request.url.startsWith('http')) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
        }
        return response;
      }).catch(() => caches.match('/index.html'));
    })
  );
});
