const CACHE_NAME = 'diet-dost-v14';
const ASSETS = [
  '/',
  '/index.html',
  '/styles.css',
  '/manifest.json'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache => cache.addAll(ASSETS))
  );
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(
      keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k))
    ))
  );
  self.clients.claim();
});

self.addEventListener('fetch', event => {
  // Pass-through API requests
  if (event.request.url.includes('/api/')) {
    event.respondWith(
      fetch(event.request).catch(() => {
        return new Response(JSON.stringify({ offline: true, message: "Currently offline. Your logs will sync when connection restores." }), {
          headers: { 'Content-Type': 'application/json' }
        });
      })
    );
  } else if (event.request.url.includes('/js/') || event.request.url.includes('partials/') || event.request.url.includes('.css')) {
    // Network-first for JavaScript modules, partials, and CSS stylesheets to guarantee latest styles
    event.respondWith(
      fetch(event.request).catch(() => caches.match(event.request))
    );
  } else {
    event.respondWith(
      caches.match(event.request).then(cached => cached || fetch(event.request))
    );
  }
});
