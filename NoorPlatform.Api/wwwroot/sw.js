// sw.js — Service Worker لمنصة نور (v3)
// يُخزّن الواجهة مؤقتاً للعمل بشكل جزئي بدون إنترنت

const CACHE_NAME = 'noor-v3';
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/icon-192.png',
  '/icon-512.png'
];

const FONT_CACHE = 'noor-fonts-v1';
const FONT_ORIGINS = ['https://fonts.googleapis.com', 'https://fonts.gstatic.com'];

// ─── تثبيت: تخزين الأصول الأساسية ───
self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS))
  );
  self.skipWaiting();
});

// ─── تفعيل: حذف الكاش القديم ───
self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys =>
      Promise.all(
        keys
          .filter(k => k !== CACHE_NAME && k !== FONT_CACHE)
          .map(k => caches.delete(k))
      )
    )
  );
  self.clients.claim();
});

// ─── الطلبات ───
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // طلبات API — Network Only مع رسالة خطأ واضحة
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(event.request).catch(() =>
        new Response(JSON.stringify({ message: 'لا يوجد اتصال بالإنترنت' }),
          { status: 503, headers: { 'Content-Type': 'application/json; charset=utf-8' } })
      )
    );
    return;
  }

  // Google Fonts — Cache First (ثابتة ولا تتغير)
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

  // CDN resources (html2pdf) — Cache First
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

  // الواجهة — Stale-While-Revalidate (سرعة + تحديث)
  event.respondWith(
    caches.match(event.request).then(cached => {
      const networkFetch = fetch(event.request).then(response => {
        if (response.ok && event.request.method === 'GET') {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
        }
        return response;
      }).catch(() => cached || caches.match('/index.html'));

      return cached || networkFetch;
    })
  );
});
