(() => {
    const status = value => {
        const label = String(value ?? '').trim();
        const normalized = label.replace(/[^a-z]/gi, '').toLowerCase();
        const success = ['active', 'enabled', 'completed', 'deployed', 'applied', 'approved', 'ready', 'acknowledged', 'succeeded'];
        const warning = ['pending', 'pendingdeployment', 'pendingdeprecation', 'pendingarchive', 'inreview', 'inprogress', 'running', 'waiting', 'queued', 'processing', 'todeploy', 'todeprecate', 'toarchive'];
        const danger = ['failed', 'deadlettered', 'rejected', 'error', 'cancelled'];
        const badge = document.createElement('span');
        badge.className = `od-list-status${success.includes(normalized) ? ' od-list-status--success' : warning.includes(normalized) ? ' od-list-status--warning' : danger.includes(normalized) ? ' od-list-status--danger' : ''}`;
        badge.textContent = label;
        return badge;
    };
    const cardIcon = name => {
        const icon = document.createElement('i');
        icon.className = `bi bi-${name} od-card-icon`;
        icon.setAttribute('aria-hidden', 'true');
        return icon;
    };
    window.KrackendCatalogUI = { status, cardIcon };
    const start = () => {
        const roots = document.querySelectorAll('[data-catalog], [data-version-catalog], [data-runtime-catalog], [data-artifact-catalog], [data-design-node-catalog]');
        let frame = 0;
        const resize = () => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => roots.forEach(root => root.querySelectorAll('[data-z-data-grid]').forEach(grid => {
                if (!grid.getClientRects().length) return;
                const viewport = grid.querySelector('[data-z-grid-viewport]');
                if (!viewport) return;
                const footerHeight = grid.querySelector('.z-grid-footer')?.getBoundingClientRect().height ?? 0;
                const available = Math.max(220, Math.floor(window.innerHeight - viewport.getBoundingClientRect().top - footerHeight - 12));
                viewport.style.setProperty('--z-grid-viewport-height', `${available}px`);
            })));
        };
        const decorate = root => root.querySelectorAll('[data-z-grid-field="status"], [data-z-grid-field="statusLabel"], [data-z-grid-field="approval"], [data-z-grid-field="application"], [data-z-grid-field="lifecycleStatus"]').forEach(cell => {
            if (cell.firstElementChild || !cell.textContent.trim() || cell.textContent.trim() === '-') return;
            cell.replaceChildren(status(cell.textContent));
        });
        roots.forEach(root => {
            new MutationObserver(() => { decorate(root); resize(); }).observe(root, { childList:true, subtree:true, attributes:true, attributeFilter:['hidden'] });
            decorate(root);
        });
        window.addEventListener('resize', resize);
        resize();
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();
