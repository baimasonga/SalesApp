// Theme: load from localStorage on first paint to avoid flash
(function () {
    try {
        const t = localStorage.getItem('sl-theme') || 'light';
        document.documentElement.setAttribute('data-theme', t);
    } catch { }
})();

window.salone = {
    setTheme: function (theme) {
        try { localStorage.setItem('sl-theme', theme); } catch { }
        document.documentElement.setAttribute('data-theme', theme);
    },
    getTheme: function () {
        return document.documentElement.getAttribute('data-theme') || 'light';
    },
    registerShortcuts: function (dotnetRef) {
        if (window.__sloneShortcutsRegistered) return;
        window.__sloneShortcutsRegistered = true;
        document.addEventListener('keydown', (e) => {
            // Cmd/Ctrl+K — command palette (always available)
            if ((e.metaKey || e.ctrlKey) && (e.key === 'k' || e.key === 'K')) {
                dotnetRef.invokeMethodAsync('OpenCmdK');
                e.preventDefault();
                return;
            }
            // ignore when typing
            const tag = (e.target && e.target.tagName) || '';
            if (['INPUT', 'TEXTAREA', 'SELECT'].includes(tag) || e.target.isContentEditable) return;

            if (e.key === 'n' || e.key === 'N') { window.location.href = '/sales/new'; e.preventDefault(); }
            if (e.key === 'd' || e.key === 'D') { window.location.href = '/'; e.preventDefault(); }
            if (e.key === 'r' || e.key === 'R') { window.location.href = '/reports'; e.preventDefault(); }
            if (e.key === '?') { dotnetRef.invokeMethodAsync('ShowShortcuts'); e.preventDefault(); }
        });
    },
    _charts: {},
    createChart: function (canvas, type, labels, data, color, currency) {
        if (!window.Chart) return null;
        const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
        const text = isDark ? '#cbd5e1' : '#475569';
        const grid = isDark ? 'rgba(255,255,255,.06)' : 'rgba(15,23,42,.06)';

        const palette = ['#0072c6', '#10b981', '#8b5cf6', '#f97316', '#ef4444', '#14b8a6', '#6366f1', '#f59e0b'];
        const labelArr = Array.from(labels);
        const dataArr = Array.from(data);

        const ctx = canvas.getContext('2d');
        let bg, border;
        if (type === 'doughnut') {
            bg = labelArr.map((_, i) => palette[i % palette.length]);
            border = isDark ? '#0b1220' : '#fff';
        } else if (type === 'line') {
            const grad = ctx.createLinearGradient(0, 0, 0, 240);
            grad.addColorStop(0, color + '55');
            grad.addColorStop(1, color + '00');
            bg = grad;
            border = color;
        } else {
            bg = color;
            border = color;
        }

        const cfg = {
            type: type,
            data: {
                labels: labelArr,
                datasets: [{
                    label: 'Value',
                    data: dataArr,
                    backgroundColor: bg,
                    borderColor: border,
                    borderWidth: type === 'doughnut' ? 2 : 2,
                    borderRadius: type === 'bar' ? 6 : 0,
                    fill: type === 'line',
                    tension: .35,
                    pointRadius: type === 'line' ? 0 : undefined,
                    pointHoverRadius: type === 'line' ? 5 : undefined
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: type === 'doughnut', position: 'bottom', labels: { color: text, font: { size: 11 } } },
                    tooltip: {
                        callbacks: {
                            label: (c) => {
                                const v = c.parsed.y ?? c.parsed;
                                return currency ? `NLe ${Number(v).toLocaleString('en-US', { minimumFractionDigits: 2 })}` :
                                                  Number(v).toLocaleString();
                            }
                        }
                    }
                },
                scales: type === 'doughnut' ? {} : {
                    x: { ticks: { color: text, font: { size: 10 } }, grid: { display: false } },
                    y: {
                        ticks: {
                            color: text, font: { size: 10 },
                            callback: (v) => currency ? 'NLe ' + (v >= 1000 ? (v / 1000).toFixed(0) + 'k' : v) : v
                        },
                        grid: { color: grid, drawBorder: false }
                    }
                }
            }
        };
        const id = 'c_' + Math.random().toString(36).slice(2);
        this._charts[id] = new window.Chart(canvas, cfg);
        return id;
    },
    updateChart: function (id, labels, data) {
        const chart = this._charts[id];
        if (!chart) return;
        chart.data.labels = Array.from(labels);
        chart.data.datasets[0].data = Array.from(data);
        chart.update();
    },
    destroyChart: function (id) {
        const chart = this._charts[id];
        if (chart) { chart.destroy(); delete this._charts[id]; }
    },
    downloadFile: function (filename, contentType, content) {
        const blob = new Blob([content], { type: contentType });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url; a.download = filename;
        document.body.appendChild(a); a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
};

// PWA service-worker registration
if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
        navigator.serviceWorker.register('/service-worker.js').catch(() => { });
    });
}

// Online/offline detection — toggles a banner at the top of the page
(function () {
    function ensureBanner() {
        let el = document.getElementById('offline-banner');
        if (el) return el;
        el = document.createElement('div');
        el.id = 'offline-banner';
        el.innerHTML = '<i class="bi bi-wifi-off"></i> <span>You\\'re offline. Some features need a connection — we\\'ll auto-reconnect.</span>';
        document.body.appendChild(el);
        return el;
    }
    function update() {
        const el = ensureBanner();
        el.classList.toggle('is-visible', !navigator.onLine);
    }
    window.addEventListener('online', update);
    window.addEventListener('offline', update);
    document.addEventListener('DOMContentLoaded', update);
    update();
})();
