(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const search = grid.querySelector('[data-z-grid-search]');
    const scope = root.querySelector('[data-runtime-scope]');
    const pageSize = root.querySelector('[data-runtime-page-size]');
    const controls = root.querySelector('.od-runtime-view-controls');
    const records = new Map();
    let refreshTimer, countsRequest, refreshing = false, refreshPending = false, preservePending = false, scrollAnchor = null;
    grid.querySelector('.z-toolbar').append(controls);
    const bodyObserver = new MutationObserver(() => decorate());

    function observeGridBody() {
        bodyObserver.disconnect();
        const body = grid.querySelector('[data-z-grid-body]');
        if (body) bodyObserver.observe(body, { childList: true, subtree: true });
    }

    function mountGrid() {
        window.ZoorceDataGrid.mount(grid);
        observeGridBody();
    }

    const updateLoadedCopy = result => {
        const footer = grid.querySelector('.z-grid-footer');
        if (!footer || !result) return;
        const loaded = records.size;
        const total = Number(result.totalCount ?? result.totalRows ?? 0);
        const label = total > loaded
            ? `${loaded.toLocaleString()} of ${total.toLocaleString()} matching rows loaded`
            : total > 0
                ? `All ${loaded.toLocaleString()} matching rows loaded`
                : 'No matching rows';
        let copy = footer.querySelector('[data-runtime-loaded-copy]');
        if (!copy) {
            copy = document.createElement('span');
            copy.dataset.runtimeLoadedCopy = 'true';
            copy.className = 'od-grid-loaded-copy';
            footer.replaceChildren(copy);
        }
        copy.textContent = label;
    };

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
    const captureScrollAnchor = () => {
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        if (!viewport || viewport.scrollTop <= 0) return null;
        const viewportTop = viewport.getBoundingClientRect().top;
        const row = [...grid.querySelectorAll('tr[data-z-grid-key]')]
            .find(item => item.getBoundingClientRect().bottom > viewportTop);
        return row
            ? { key: row.dataset.zGridKey, offset: row.getBoundingClientRect().top - viewportTop, scrollTop: viewport.scrollTop }
            : { key: null, offset: 0, scrollTop: viewport.scrollTop };
    };
    const restoreScrollAnchor = () => {
        if (!scrollAnchor) return;
        const anchor = scrollAnchor;
        scrollAnchor = null;
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        if (!viewport) return;
        const row = anchor.key ? grid.querySelector(`tr[data-z-grid-key="${CSS.escape(anchor.key)}"]`) : null;
        if (row) viewport.scrollTop += row.getBoundingClientRect().top - viewport.getBoundingClientRect().top - anchor.offset;
        else viewport.scrollTop = anchor.scrollTop;
    };
    const refresh = ({ preserveScroll = false } = {}) => {
        if (refreshing || grid.getAttribute('aria-busy') === 'true') {
            refreshPending = true;
            preservePending ||= preserveScroll;
            return;
        }
        if (preserveScroll) scrollAnchor = captureScrollAnchor();
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
    observeGridBody();
    grid.addEventListener('zoorce:grid:error', () => {
        refreshing = false;
        scrollAnchor = null;
        if (refreshPending) {
            const preserveScroll = preservePending;
            refreshPending = false;
            preservePending = false;
            refresh({ preserveScroll });
        }
    });
    grid.addEventListener('zoorce:grid:loaded', event => {
        refreshing = false;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorate();
        updateLoadedCopy(event.detail.result);
        if (!event.detail.append) refreshCounts();
        window.requestAnimationFrame(restoreScrollAnchor);
        if (refreshPending) {
            const preserveScroll = preservePending;
            refreshPending = false;
            preservePending = false;
            refresh({ preserveScroll });
        }
    });
    scope.addEventListener('change', () => {
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent); config.binding.source = scopedUrl(root.dataset.source).toString();
        window.ZoorceDataGrid.dispose(grid); configElement.textContent = JSON.stringify(config);
        refreshing = false; refreshPending = false; mountGrid();
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    });
    pageSize?.addEventListener('change', () => {
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent);
        config.pageSize = Number(pageSize.value);
        window.ZoorceDataGrid.dispose(grid);
        configElement.textContent = JSON.stringify(config);
        records.clear();
        mountGrid();
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    });
    root.querySelector('[data-runtime-refresh]').addEventListener('click', () => refresh());
    window.addEventListener('krackend:instances-changed', () => {
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(() => {
            const preserveScroll = (grid.querySelector('[data-z-grid-viewport]')?.scrollTop ?? 0) > 0;
            refresh({ preserveScroll });
        }, 1000);
    });
    refresh();
})();
