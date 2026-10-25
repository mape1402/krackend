(() => {
    const root = document.querySelector('[data-domain-catalog]');
    if (!root) return;
    const grid = root.querySelector('#domains-grid');
    const gridView = root.querySelector('[data-domain-grid]');
    const cardView = root.querySelector('[data-domain-cards]');
    const cards = root.querySelector('[data-domain-card-items]');
    const search = root.querySelector('[data-domain-search]');
    const status = root.querySelector('[data-domain-status]');
    const empty = root.querySelector('[data-domain-empty]');
    const more = root.querySelector('[data-domain-more]');
    const form = root.querySelector('[data-domain-status-form]');
    const sentinel = root.querySelector('[data-domain-sentinel]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const records = new Map();
    let page = 0, hasMore = false, request = null, searchTimer = 0, generation = 0;
    let gridResult = null, gridRefreshTimer = 0;

    const button = (icon, label) => {
        const element = document.createElement('button');
        element.type = 'button';
        element.className = 'btn btn-sm btn-outline-secondary';
        element.title = label;
        element.setAttribute('aria-label', label);
        const glyph = document.createElement('i');
        glyph.className = `bi ${icon}`;
        glyph.setAttribute('aria-hidden', 'true');
        element.append(glyph);
        return element;
    };
    const actions = item => {
        const group = document.createElement('div');
        group.className = 'od-catalog-actions';
        const edit = button('bi-pencil', `Edit ${item.displayName}`);
        edit.classList.add('js-open-domain-modal');
        Object.assign(edit.dataset, { mode: 'edit', id: item.id, key: item.key,
            displayName: item.displayName, description: item.description ?? '' });
        const toggle = button(item.isActive ? 'bi-pause-circle' : 'bi-play-circle',
            `${item.isActive ? 'Disable' : 'Enable'} ${item.displayName}`);
        toggle.addEventListener('click', () => {
            form.elements.domainId.value = item.id;
            form.elements.isActive.value = String(!item.isActive);
            form.requestSubmit();
        });
        group.append(edit, toggle);
        return group;
    };
    const decorateGrid = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell) cell.replaceChildren(actions(item));
    });
    grid.addEventListener('zoorce:grid:loaded', event => {
        gridResult = event.detail.result;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorateGrid();
        clearTimeout(gridRefreshTimer);
        gridRefreshTimer = setTimeout(() => {
            const sorted = grid.querySelector('[data-z-grid-sort][aria-sort="ascending"], [data-z-grid-sort][aria-sort="descending"]');
            const sort = sorted?.dataset.zGridSort ?? '';
            const descending = sorted?.getAttribute('aria-sort') === 'descending';
            if (gridResult && (gridResult.search !== gridSearch.value.trim() || gridResult.sort !== sort || gridResult.descending !== descending))
                grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
        }, 220);
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));

    const appendCard = item => {
        const card = document.createElement('article');
        card.className = 'od-item-card';
        const top = document.createElement('div');
        top.className = 'od-item-top';
        const identity = document.createElement('div');
        const name = document.createElement('h2');
        name.className = 'od-item-title';
        name.textContent = item.displayName;
        const key = document.createElement('p');
        key.className = 'od-item-meta';
        key.textContent = item.key;
        identity.append(name, key);
        top.append(identity, actions(item));
        const badge = document.createElement('span');
        badge.className = `od-status ${item.isActive ? 'od-status-active' : 'od-status-inactive'}`;
        badge.textContent = item.status;
        const description = document.createElement('p');
        description.className = 'od-item-description';
        description.textContent = item.description || 'No description.';
        card.append(top, badge, description);
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
        const currentGeneration = generation;
        const controller = new AbortController();
        request = controller;
        status.textContent = 'Loading domains...';
        cardView.setAttribute('aria-busy', 'true');
        try {
            const url = new URL(root.dataset.source, location.href);
            url.searchParams.set('page', String(page + 1));
            url.searchParams.set('pageSize', '25');
            url.searchParams.set('search', search.value.trim());
            const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error('Unable to load domains.');
            const result = await response.json();
            if (generation !== currentGeneration) return;
            result.items.forEach(appendCard);
            page++;
            hasMore = result.hasMore;
            empty.hidden = cards.childElementCount !== 0;
            more.hidden = !hasMore;
            status.textContent = `Showing ${cards.childElementCount} of ${result.totalCount} domains`;
        } catch (error) {
            if (error.name !== 'AbortError') {
                status.textContent = 'Unable to load domains. Please try again.';
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
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) {
        new IntersectionObserver(entries => {
            if (!cardView.hidden && entries.some(entry => entry.isIntersecting)) load(false);
        }, { rootMargin: '200px' }).observe(sentinel);
    }
    const setView = control => {
        const showCards = control.dataset.domainView === 'cards';
        gridView.hidden = showCards;
        cardView.hidden = !showCards;
        root.querySelectorAll('[data-domain-view]').forEach(item =>
            item.setAttribute('aria-pressed', String(item === control)));
        try { localStorage.setItem('krackend-domains-view', control.dataset.domainView); } catch { /* Preference is optional. */ }
        if (showCards) {
            const changed = search.value !== gridSearch.value;
            search.value = gridSearch.value;
            if (changed || (page === 0 && !request)) load(true);
        } else if (gridSearch.value !== search.value) {
            gridSearch.value = search.value;
            gridSearch.dispatchEvent(new Event('input', { bubbles: true }));
        }
    };
    root.querySelectorAll('[data-domain-view]').forEach(control => control.addEventListener('click', () => setView(control)));
    try {
        if (localStorage.getItem('krackend-domains-view') === 'cards') setView(root.querySelector('[data-domain-view="cards"]'));
    } catch { /* Preference is optional. */ }
})();
