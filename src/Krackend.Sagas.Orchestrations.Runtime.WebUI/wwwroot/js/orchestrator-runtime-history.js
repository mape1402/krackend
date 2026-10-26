(() => {
    const root = document.querySelector('[data-instance-history]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const from = root.querySelector('[data-history-from]');
    const through = root.querySelector('[data-history-through]');
    const error = root.querySelector('[data-history-error]');
    grid.querySelector('.z-toolbar').append(root.querySelector('[data-history-filters]'));
    const records = new Map();
    const renderActions = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (!item || !cell || cell.querySelector('a')) return;
        const link = document.createElement('a');
        link.href = root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(item.id));
        link.className = 'z-button z-button-ghost';
        link.title = 'View instance';
        link.setAttribute('aria-label', `View instance ${item.correlationId || item.id}`);
        const icon = document.createElement('i');
        icon.className = 'bi bi-eye'; icon.setAttribute('aria-hidden', 'true');
        link.append(icon); cell.replaceChildren(link);
        const status = row.querySelector('[data-z-grid-field="status"]');
        if (status) {
            const chip = document.createElement('span');
            chip.className = `od-status ${item.statusClass || ''}`; chip.textContent = item.status;
            status.replaceChildren(chip);
        }
        for (const field of ['startedOnUtc', 'lastUpdatedOnUtc']) {
            const target = row.querySelector(`[data-z-grid-field="${field}"]`);
            const value = item[field];
            if (!target || !value) continue;
            const text = typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value;
            const date = new Date(text);
            if (!Number.isNaN(date.getTime())) target.textContent = date.toISOString().replace('T', ' ').substring(0, 19);
        }
    });
    new MutationObserver(renderActions).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:loaded', event => {
        records.clear(); event.detail.result.items.forEach(item => records.set(String(item.id), item)); renderActions();
    });
    root.querySelector('[data-history-filters]').addEventListener('submit', event => {
        event.preventDefault();
        const invalid = Boolean(from.value && through.value && from.value > through.value);
        error.hidden = !invalid; from.setAttribute('aria-invalid', String(invalid)); through.setAttribute('aria-invalid', String(invalid));
        if (invalid) return;
        const url = new URL(root.dataset.source, location.href);
        if (from.value) url.searchParams.set('startedFrom', `${from.value}T00:00:00Z`);
        if (through.value) {
            const end = new Date(`${through.value}T00:00:00Z`); end.setUTCDate(end.getUTCDate() + 1);
            url.searchParams.set('startedBefore', end.toISOString());
        }
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent); config.binding.source = url.toString();
        window.ZoorceDataGrid.dispose(grid); configElement.textContent = JSON.stringify(config); window.ZoorceDataGrid.mount(grid);
        const search = grid.querySelector('[data-z-grid-search]');
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
})();
