const CACHE_NAME = 'mercedes-eis-toolkit-v3';
const ASSETS = ['/', '/app', '/app.css', '/app.js', '/manifest.json', '/login.html', '/app.html', '/brand-long.png', '/brand-cropped.png', '/icons/icon-192.png', '/icons/icon-512.png', '/icons/favicon-16.png', '/icons/favicon-32.png', '/icons/favicon-48.png', '/icons/favicon.ico'];
const SHELL_ASSET_PATHS = new Set(['/', '/app', '/app.css', '/app.js', '/app.html', '/login.html', '/manifest.json']);

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(ASSETS)));
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(caches.keys().then((keys) => Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))));
  self.clients.claim();
});

self.addEventListener('fetch', (event) => {
  if (event.request.method !== 'GET') return;

  const requestUrl = new URL(event.request.url);
  if (requestUrl.origin !== self.location.origin) return;

  if (requestUrl.pathname.startsWith('/api/')) {
    event.respondWith(fetch(event.request).catch(() => caches.match('/app')));
    return;
  }

  if (event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(() => caches.match('/app').then((cached) => cached || caches.match('/login.html'))));
    return;
  }

  if (SHELL_ASSET_PATHS.has(requestUrl.pathname)) {
    event.respondWith(fetch(event.request).then((response) => {
      const copy = response.clone();
      caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
      return response;
    }).catch(() => caches.match(event.request).then((cached) => cached || caches.match('/app'))));
    return;
  }

  event.respondWith(caches.match(event.request).then((cached) => {
    if (cached) {
      return cached;
    }

    return fetch(event.request).then((response) => {
      const copy = response.clone();
      caches.open(CACHE_NAME).then((cache) => cache.put(event.request, copy));
      return response;
    }).catch(() => caches.match('/app'));
  }));
});
