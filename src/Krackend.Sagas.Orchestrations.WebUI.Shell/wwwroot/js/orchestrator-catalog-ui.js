(() => {
    const badge = (value, tone = '') => {
        const element = document.createElement('span');
        const normalizedTone = String(tone || '').trim().toLowerCase();
        element.className = `z-badge${['success', 'warning', 'danger'].includes(normalizedTone) ? ` z-badge-${normalizedTone}` : ''}`;
        element.textContent = String(value ?? '').trim();
        return element;
    };
    const status = value => {
        const label = String(value ?? '').trim();
        const normalized = label.replace(/[^a-z]/gi, '').toLowerCase();
        const success = ['active', 'enabled', 'completed', 'deployed', 'applied', 'approved', 'ready', 'acknowledged', 'succeeded'];
        const warning = ['pending', 'pendingdeployment', 'pendingdeprecation', 'pendingarchive', 'inreview', 'inprogress', 'running', 'waiting', 'queued', 'processing', 'todeploy', 'todeprecate', 'toarchive'];
        const danger = ['failed', 'deadlettered', 'rejected', 'error', 'cancelled'];
        return badge(label, success.includes(normalized) ? 'success' : warning.includes(normalized) ? 'warning' : danger.includes(normalized) ? 'danger' : '');
    };
    const cardIcon = name => {
        const icon = document.createElement('i');
        icon.className = `bi bi-${name} od-card-icon`;
        icon.setAttribute('aria-hidden', 'true');
        return icon;
    };
    window.KrackendCatalogUI = { badge, status, cardIcon };
    const start = () => {
        const roots = document.querySelectorAll('[data-catalog], [data-version-catalog], [data-runtime-catalog], [data-artifact-catalog], [data-design-node-catalog]');
        let frame = 0;
        const resize = () => {
            cancelAnimationFrame(frame);
            frame = requestAnimationFrame(() => roots.forEach(root => root.querySelectorAll('[data-z-data-grid]').forEach(grid => {
                if (!grid.getClientRects().length) return;
                const viewport = grid.querySelector('[data-z-grid-viewport]');
                if (!viewport) return;
                const footer = grid.querySelector('.z-grid-footer');
                const footerHeight = footer?.parentElement === grid ? footer.getBoundingClientRect().height : 0;
                let available = Math.max(220, Math.floor(window.innerHeight - viewport.getBoundingClientRect().top - footerHeight));
                viewport.style.setProperty('--z-grid-viewport-height', `${available}px`);
                const overflow = Math.max(0, document.documentElement.scrollHeight - document.documentElement.clientHeight);
                if (overflow) {
                    available = Math.max(220, available - overflow);
                    viewport.style.setProperty('--z-grid-viewport-height', `${available}px`);
                }
            })));
        };
        const decorate = root => root.querySelectorAll('[data-z-grid-field="status"], [data-z-grid-field="statusLabel"], [data-z-grid-field="approval"], [data-z-grid-field="application"], [data-z-grid-field="lifecycleStatus"]').forEach(cell => {
            if (cell.firstElementChild || !cell.textContent.trim() || cell.textContent.trim() === '-') return;
            cell.replaceChildren(status(cell.textContent));
        });
        roots.forEach(root => {
            root.querySelectorAll('[data-z-data-grid]').forEach(grid => {
                const toolbar = grid.querySelector('.z-toolbar');
                const footer = grid.querySelector('.z-grid-footer');
                if (toolbar && footer && footer.parentElement === grid) toolbar.append(footer);
            });
            new MutationObserver(() => { decorate(root); resize(); }).observe(root, { childList:true, subtree:true, attributes:true, attributeFilter:['hidden'] });
            decorate(root);
        });
        window.addEventListener('resize', resize);
        resize();
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})();
