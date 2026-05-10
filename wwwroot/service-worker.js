// Salone Sales — minimal app-shell service worker
// Note: Blazor Server uses SignalR, so true offline mode requires Blazor WASM.
// This SW caches static assets so the app shell loads instantly and works on flaky connections.

const CACHE = 'salone-sales-v6';  // bump to invalidate cached CSS/JS after redesign
const SHELL = [
    '/',
    '/app.css',
    '/salone.css',
    '/app.js',
    '/manifest.webmanifest',
    '/favicon.png',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    'https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.css',
    'https://cdn.jsdelivr.net/npm/chart.js@4.4.1/dist/chart.umd.min.js'
];

self.addEventListener('install', (e) => {
    e.waitUntil(
        caches.open(CACHE).then((c) => c.addAll(SHELL.map(u => new Request(u, { mode: u.startsWith('http') ? 'no-cors' : 'same-origin' }))))
            .catch(() => { /* tolerate failures during dev */ })
    );
    self.skipWaiting();
});

self.addEventListener('activate', (e) => {
    e.waitUntil(
        caches.keys().then(keys => Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k))))
    );
    self.clients.claim();
});

self.addEventListener('fetch', (e) => {
    const url = new URL(e.request.url);

    // Never cache SignalR/_framework/_blazor traffic
    if (url.pathname.startsWith('/_blazor') ||
        url.pathname.startsWith('/_framework') ||
        url.pathname.startsWith('/export/')) return;

    // Network-first for HTML
    if (e.request.mode === 'navigate' || e.request.headers.get('accept')?.includes('text/html')) {
        e.respondWith(
            fetch(e.request).catch(() => caches.match('/'))
        );
        return;
    }

    // Cache-first for static assets
    e.respondWith(
        caches.match(e.request).then(cached => cached || fetch(e.request).then(resp => {
            if (resp && resp.status === 200 && resp.type === 'basic') {
                const clone = resp.clone();
                caches.open(CACHE).then(c => c.put(e.request, clone));
            }
            return resp;
        }).catch(() => cached))
    );
});
