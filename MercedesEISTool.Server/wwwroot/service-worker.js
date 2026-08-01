const CACHE_NAME = 'mercedes-eis-toolkit-v1';
const ASSETS = ['/', '/app', '/app.css', '/app.js', '/manifest.json', '/login.html', '/brand-long.png', '/brand-cropped.png', '/icons/icon-192.png', '/icons/icon-512.png', '/icons/favicon-16.png', '/icons/favicon-32.png', '/icons/favicon-48.png', '/icons/favicon.ico'];

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
  event.respondWith(fetch(event.request).catch(() => caches.match(event.request).then((cached) => cached || caches.match('/app'))));
});
