(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;
    const gridView = root.querySelector('[data-runtime-grid-view]');
    const cardView = root.querySelector('[data-runtime-card-view]');
    const grid = root.querySelector('[data-z-data-grid]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const search = root.querySelector('[data-runtime-search]');
    const sort = root.querySelector('[data-runtime-sort]');
    const cards = root.querySelector('[data-runtime-cards]');
    const empty = root.querySelector('[data-runtime-empty]');
    const status = root.querySelector('[data-runtime-status]');
    const more = root.querySelector('[data-runtime-more]');
    const sentinel = root.querySelector('[data-runtime-sentinel]');
    const updates = root.querySelector('[data-runtime-updates]');
    const records = new Map();
    let page = 0, hasMore = false, request = null, generation = 0, searchTimer = 0, refreshTimer = 0;
    let gridResult = null, gridRetryTimer = 0, gridRefreshing = false, gridRefreshPending = false;

    const formatDate = value => {
        const date = new Date(value);
        return value && !Number.isNaN(date.getTime()) ? date.toISOString().replace('T', ' ').substring(0, 19) : '-';
    };
    const copyable = (value, label) => {
        const container = document.createElement('span');
        container.className = 'od-id-copy';
        const text = document.createElement('span');
        text.className = 'od-copy-value';
        text.textContent = value || '-';
        container.append(text);
        if (value) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'od-copy-button';
            button.dataset.copyValue = value;
            button.title = `Copy ${label}`;
            button.setAttribute('aria-label', `Copy ${label}`);
            const icon = document.createElement('i');
            icon.className = 'bi bi-copy';
            icon.setAttribute('aria-hidden', 'true');
            button.append(icon);
            container.append(button);
        }
        return container;
    };

    const refreshGrid = () => {
        if (gridRefreshing || grid.getAttribute('aria-busy') === 'true') {
            gridRefreshPending = true;
            return;
        }
        gridRefreshing = true;
        grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
    };

    const detailButton = item => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'btn btn-outline-secondary btn-sm';
        button.dataset.openInstance = item.id;
        button.title = 'View instance';
        button.setAttribute('aria-label', `View instance ${item.correlationId || item.id}`);
        const icon = document.createElement('i');
        icon.className = 'bi bi-eye';
        icon.setAttribute('aria-hidden', 'true');
        button.append(icon);
        return button;
    };
    const decorateGrid = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell && !cell.querySelector('[data-open-instance]')) cell.replaceChildren(detailButton(item));
        if (!item) return;
        for (const [field, label] of [['correlationId', 'correlation ID'], ['sagaId', 'saga ID']]) {
            const target = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (target && !target.querySelector('.od-id-copy')) target.replaceChildren(copyable(item[field], label));
        }
        for (const field of ['startedOnUtc', 'lastUpdatedOnUtc']) {
            const target = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (target && target.textContent !== formatDate(item[field])) target.textContent = formatDate(item[field]);
        }
        const statusCell = row.querySelector('[data-z-grid-field="status"]');
        if (statusCell && !statusCell.querySelector('.od-status')) {
            const badge = document.createElement('span');
            badge.className = `od-status ${item.statusClass || ''}`;
            badge.textContent = item.status;
            statusCell.replaceChildren(badge);
        }
    });
    new MutationObserver(decorateGrid).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:error', () => {
        gridRefreshing = false;
        if (gridRefreshPending) updates.hidden = false;
        gridRefreshPending = false;
    });
    grid.addEventListener('zoorce:grid:loaded', event => {
        gridRefreshing = false;
        gridResult = event.detail.result;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorateGrid();
        clearTimeout(gridRetryTimer);
        gridRetryTimer = setTimeout(() => {
            const sorted = grid.querySelector('[data-z-grid-sort][aria-sort="ascending"], [data-z-grid-sort][aria-sort="descending"]');
            const field = sorted?.dataset.zGridSort ?? '';
            const descending = sorted?.getAttribute('aria-sort') === 'descending';
            if (gridResult && (gridResult.search !== gridSearch.value.trim() || gridResult.sort !== field || gridResult.descending !== descending))
                refreshGrid();
            else if (gridRefreshPending) {
                gridRefreshPending = false;
                refreshGrid();
            }
        }, 220);
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));

    const appendCard = item => {
        const card = document.createElement('article');
        card.className = 'od-runtime-instance-card';
        const header = document.createElement('div');
        header.className = 'od-runtime-instance-header';
        const title = document.createElement('h3');
        title.textContent = item.orchestrationLabel || item.orchestrationDefinitionKey;
        header.append(window.KrackendCatalogUI.cardIcon('activity'), title, detailButton(item));
        const badge = document.createElement('span');
        badge.className = `od-status ${item.statusClass || ''}`;
        badge.textContent = item.status;
        const correlation = document.createElement('p');
        correlation.className = 'od-runtime-card-meta';
        correlation.append(copyable(item.correlationId, 'Correlation ID'));
        card.append(header, correlation, badge);
        cards.append(card);
    };
    const load = async reset => {
        if (reset) {
            generation++;
            request?.abort();
            request = null;
            page = 0;
            cards.replaceChildren();
        }
        if (request || (!reset && !hasMore)) return;
        const current = generation;
        const controller = new AbortController();
        request = controller;
        status.textContent = 'Loading instances...';
        cardView.setAttribute('aria-busy', 'true');
        try {
            const url = new URL(root.dataset.source, location.href);
            url.searchParams.set('page', String(page + 1));
            url.searchParams.set('pageSize', '25');
            url.searchParams.set('search', search.value.trim());
            const [field, direction] = sort.value.split(':');
            url.searchParams.set('sort', field);
            url.searchParams.set('descending', String(direction === 'desc'));
            const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error('Unable to load instances.');
            const result = await response.json();
            if (current !== generation) return;
            result.items.forEach(appendCard);
            page++;
            hasMore = result.hasMore;
            empty.hidden = cards.childElementCount !== 0;
            more.hidden = !hasMore;
            status.textContent = `Showing ${cards.childElementCount} of ${result.totalCount} instances`;
            if (hasMore && result.items.length) requestAnimationFrame(() => {
                const rect = sentinel.getBoundingClientRect();
                if (current === generation && !cardView.hidden && rect.top <= window.innerHeight + 200 && rect.bottom >= -200) load(false);
            });
        } catch (error) {
            if (error.name !== 'AbortError') {
                status.textContent = 'Unable to load instances. Please try again.';
                more.hidden = false;
                hasMore = true;
            }
        } finally {
            if (request === controller) {
                request = null;
                cardView.removeAttribute('aria-busy');
            }
        }
    };
    search.addEventListener('input', () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => load(true), 200);
    });
    sort.addEventListener('change', () => load(true));
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {
        if (!cardView.hidden && entries.some(entry => entry.isIntersecting)) load(false);
    }, { rootMargin: '200px' }).observe(sentinel);
    const setView = control => {
        const showCards = control.dataset.runtimeView === 'cards';
        gridView.hidden = showCards;
        cardView.hidden = !showCards;
        root.querySelectorAll('[data-runtime-view]').forEach(item => item.setAttribute('aria-pressed', String(item === control)));
        try { localStorage.setItem('krackend-runtime-instances-view', control.dataset.runtimeView); } catch { /* Preference is optional. */ }
        if (showCards) {
            const changed = search.value !== gridSearch.value;
            search.value = gridSearch.value;
            if (changed || (page === 0 && !request)) load(true);
        } else if (gridSearch.value !== search.value) {
            gridSearch.value = search.value;
            gridSearch.dispatchEvent(new Event('input', { bubbles: true }));
        } else {
            refreshGrid();
        }
    };
    root.querySelectorAll('[data-runtime-view]').forEach(control => control.addEventListener('click', () => setView(control)));
    const refresh = () => {
        updates.hidden = true;
        if (cardView.hidden) refreshGrid();
        else load(true);
    };
    root.querySelector('[data-runtime-refresh]').addEventListener('click', refresh);
    updates.addEventListener('click', refresh);
    try {
        if (localStorage.getItem('krackend-runtime-instances-view') === 'cards') setView(root.querySelector('[data-runtime-view="cards"]'));
    } catch { /* Preference is optional. */ }
    window.addEventListener('krackend:instances-changed', () => {
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(() => {
            if (!cardView.hidden && page <= 1) load(true);
            else if (!gridView.hidden && (grid.querySelector('[data-z-grid-viewport]')?.scrollTop ?? 0) === 0) refreshGrid();
            else updates.hidden = false;
        }, 1000);
    });
})();
