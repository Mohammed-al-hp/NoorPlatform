// sw.js — Service Worker لمنصة نور
// يُخزّن الواجهة مؤقتاً للعمل بشكل جزئي بدون إنترنت

const CACHE_NAME = 'noor-v2';
const STATIC_ASSETS = [
  '/',
  '/index.html',
  '/manifest.json',
  '/icon-192.png',
  '/icon-512.png'
];

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
      Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
    )
  );
  self.clients.claim();
});

// ─── الطلبات: Network First لـ API، Stale-While-Revalidate للواجهة ───
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);

  // طلبات API — دائماً من الشبكة مع رسالة خطأ واضحة
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(
      fetch(event.request).catch(() =>
        new Response(JSON.stringify({ message: 'لا يوجد اتصال بالإنترنت' }),
          { status: 503, headers: { 'Content-Type': 'application/json; charset=utf-8' } })
      )
    );
    return;
  }

  // الواجهة — Stale-While-Revalidate (سرعة + تحديث)
  event.respondWith(
    caches.match(event.request).then(cached => {
      const networkFetch = fetch(event.request).then(response => {
        if (response.ok) {
          const clone = response.clone();
          caches.open(CACHE_NAME).then(cache => cache.put(event.request, clone));
        }
        return response;
      }).catch(() => cached || caches.match('/index.html'));

      return cached || networkFetch;
    })
  );
});
