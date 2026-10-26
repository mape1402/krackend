(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const search = grid.querySelector('[data-z-grid-search]');
    const scope = root.querySelector('[data-runtime-scope]');
    const controls = root.querySelector('.od-runtime-view-controls');
    const updates = root.querySelector('[data-runtime-updates]');
    const records = new Map();
    let refreshTimer, countsRequest, refreshing = false, refreshPending = false;
    grid.querySelector('.z-toolbar').append(controls);

    const scopedUrl = source => {
        const url = new URL(source, location.href);
        if (scope.value === 'active') url.searchParams.set('activeOnly', 'true');
        else url.searchParams.set('recentHours', scope.value);
        return url;
    };
    const refreshCounts = async () => {
        countsRequest?.abort();
        const controller = new AbortController();
        countsRequest = controller;
        const url = scopedUrl(root.dataset.countsSource);
        url.searchParams.set('search', search.value.trim());
        try {
            const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error('Counts unavailable');
            const counts = await response.json();
            const value = key => Number(counts[key] || 0);
            const totals = { running: value('Created') + value('Running') + value('Compensating'), waiting: value('Waiting'),
                completed: value('Completed'), deadLettered: value('DeadLettered'),
                failed: value('Failed') + value('Aborted') + value('CompletedWithErrors') };
            Object.entries(totals).forEach(([key, count]) => { root.querySelector(`[data-monitor-count="${key}"]`).textContent = count.toLocaleString(); });
        } catch (error) {
            if (error.name !== 'AbortError') root.querySelectorAll('[data-monitor-count]').forEach(element => { element.textContent = '—'; });
        } finally { if (countsRequest === controller) countsRequest = null; }
    };
    const refresh = () => {
        updates.hidden = true;
        if (refreshing || grid.getAttribute('aria-busy') === 'true') { refreshPending = true; return; }
        refreshing = true;
        grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
    };
    const copyable = (value, label) => {
        const container = document.createElement('span'); container.className = 'od-id-copy';
        const text = document.createElement('span'); text.className = 'od-copy-value'; text.textContent = value || '-'; container.append(text);
        if (value) {
            const button = document.createElement('button'); button.type = 'button'; button.className = 'od-copy-button';
            button.dataset.copyValue = value; button.title = `Copy ${label}`; button.setAttribute('aria-label', `Copy ${label}`);
            const icon = document.createElement('i'); icon.className = 'bi bi-copy'; icon.setAttribute('aria-hidden', 'true');
            button.append(icon); container.append(button);
        }
        return container;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        if (!item) return;
        const actions = row.querySelector('[data-z-grid-field="actions"]');
        if (actions && !actions.querySelector('[data-instance-link]')) {
            const link = document.createElement('a'); link.className = 'z-button z-button-ghost';
            link.href = root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(item.id));
            link.dataset.instanceLink = item.id; link.title = 'View instance'; link.setAttribute('aria-label', `View instance ${item.correlationId || item.id}`);
            const icon = document.createElement('i'); icon.className = 'bi bi-eye'; icon.setAttribute('aria-hidden', 'true');
            link.append(icon); actions.replaceChildren(link);
        }
        for (const [field, label] of [['correlationId', 'correlation ID'], ['sagaId', 'saga ID']]) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (cell && !cell.querySelector('.od-id-copy')) cell.replaceChildren(copyable(item[field], label));
        }
        for (const field of ['startedOnUtc', 'lastUpdatedOnUtc']) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`), value = item[field];
            const text = typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value;
            const date = new Date(text);
            if (cell && value && !Number.isNaN(date.getTime())) {
                const formatted = date.toISOString().replace('T', ' ').substring(0, 19);
                if (cell.textContent !== formatted) cell.textContent = formatted;
            }
        }
        const status = row.querySelector('[data-z-grid-field="status"]');
        if (status && !status.querySelector('.od-status')) {
            const badge = document.createElement('span'); badge.className = `od-status ${item.statusClass || ''}`; badge.textContent = item.status;
            status.replaceChildren(badge);
        }
    });
    new MutationObserver(decorate).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:error', () => { refreshing = false; if (refreshPending) updates.hidden = false; refreshPending = false; });
    grid.addEventListener('zoorce:grid:loaded', event => {
        refreshing = false;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorate();
        if (!event.detail.append) refreshCounts();
        if (refreshPending) { refreshPending = false; refresh(); }
    });
    scope.addEventListener('change', () => {
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent); config.binding.source = scopedUrl(root.dataset.source).toString();
        window.ZoorceDataGrid.dispose(grid); configElement.textContent = JSON.stringify(config);
        refreshing = false; refreshPending = false; window.ZoorceDataGrid.mount(grid);
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    });
    root.querySelector('[data-runtime-refresh]').addEventListener('click', refresh);
    updates.addEventListener('click', refresh);
    window.addEventListener('krackend:instances-changed', () => {
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(() => {
            if ((grid.querySelector('[data-z-grid-viewport]')?.scrollTop ?? 0) === 0) refresh();
            else updates.hidden = false;
        }, 1000);
    });
    refresh();
})();
