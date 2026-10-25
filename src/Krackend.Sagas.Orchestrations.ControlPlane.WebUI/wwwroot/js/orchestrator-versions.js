(() => {
    const root = document.querySelector('[data-version-catalog]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const gridView = root.querySelector('[data-version-grid]');
    const cardView = root.querySelector('[data-version-cards-view]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const search = root.querySelector('[data-version-search]');
    const cards = root.querySelector('[data-version-cards]');
    const empty = root.querySelector('[data-version-empty]');
    const status = root.querySelector('[data-version-status]');
    const more = root.querySelector('[data-version-more]');
    const sentinel = root.querySelector('[data-version-sentinel]');
    const records = new Map();
    let page = 0, hasMore = true, generation = 0, request = null, timer;
    const make = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };
    const roadmapUrl = id => root.dataset.roadmapUrl.replace('__versionId__', encodeURIComponent(id));
    const actionLabels = { RequestDeployment: 'Send to deployment review', RequestDeprecation: 'Request deprecation',
        RequestArchive: 'Request archive', OpenReview: 'Open review', Abandon: 'Abandon draft' };
    const actions = item => {
        const group = make('div', 'd-flex gap-2'); group.dataset.versionActions = '';
        const link = make('a', 'btn btn-sm btn-outline-secondary'); link.href = roadmapUrl(item.id);
        link.title = 'Open roadmap'; link.setAttribute('aria-label', `Open roadmap for ${item.version}`);
        const icon = make('i', 'bi bi-diagram-3'); icon.setAttribute('aria-hidden', 'true'); link.append(icon); group.append(link);
        if (!item.actions.length) return group;
        const dropdown = make('div', 'dropdown'); const toggle = make('button', 'btn btn-sm btn-outline-secondary');
        toggle.type = 'button'; toggle.title = 'Version actions'; toggle.setAttribute('aria-label', `Actions for ${item.version}`);
        toggle.setAttribute('data-bs-toggle', 'dropdown'); toggle.setAttribute('aria-expanded', 'false');
        const dots = make('i', 'bi bi-three-dots-vertical'); dots.setAttribute('aria-hidden', 'true'); toggle.append(dots);
        const menu = make('ul', 'dropdown-menu dropdown-menu-end');
        item.actions.forEach(action => {
            const li = document.createElement('li'); const form = document.createElement('form');
            form.method = 'post'; form.action = root.dataset.actionUrl;
            const token = document.querySelector('#version-catalog-token input[name="__RequestVerificationToken"]');
            if (token) form.append(token.cloneNode());
            Object.entries({ orchestrationId: root.dataset.orchestrationId, versionId: item.id, action }).forEach(([name, value]) => {
                const input = document.createElement('input'); input.type = 'hidden'; input.name = name; input.value = value; form.append(input);
            });
            const button = make('button', `dropdown-item${action === 'Abandon' ? ' text-danger' : ''}`, actionLabels[action] || action);
            button.type = 'submit'; button.formNoValidate = true; form.append(button); li.append(form); menu.append(li);
        });
        dropdown.append(toggle, menu); group.append(dropdown); return group;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey); const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell && !cell.querySelector('[data-version-actions]')) cell.replaceChildren(actions(item));
    });
    new MutationObserver(decorate).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:loaded', event => {
        if (!event.detail.append) records.clear(); event.detail.result.items.forEach(item => records.set(String(item.id), item)); decorate();
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
    const addCard = item => {
        const card = make('article', 'od-item-card od-version-card');
        const header = make('div', 'od-item-top'); const identity = document.createElement('div');
        identity.append(make('h3', 'od-item-title', item.version), make('p', 'od-item-meta', item.versionLabel || ''));
        const controls = make('div', 'd-flex align-items-start gap-2');
        const state = item.status.startsWith('Pending') ? 'inreview' :
            ({ Draft: 'draft', Deployed: 'deployed', Deprecated: 'deprecated', Archived: 'archived', Abandoned: 'archived' }[item.status] || 'draft');
        controls.append(make('span', `od-status od-status-${state}`, item.statusLabel), actions(item));
        header.append(identity, controls); card.append(header, make('p', 'od-item-description', item.description || ''),
            make('p', 'od-item-meta od-item-time mt-auto', `Created: ${item.createdOnUtc || '-'}`));
        card.addEventListener('click', event => { if (!event.target.closest('a,button,input,form,.dropdown')) location.href = roadmapUrl(item.id); });
        cards.append(card);
    };
    const load = async reset => {
        if (reset) { generation++; request?.abort(); request = null; page = 0; hasMore = true; cards.replaceChildren(); }
        if (request || !hasMore) return;
        const current = generation, controller = new AbortController(); request = controller;
        status.textContent = 'Loading versions...'; more.hidden = true;
        try {
            const url = new URL(root.dataset.source, location.href);
            url.searchParams.set('page', String(page + 1)); url.searchParams.set('pageSize', '25'); url.searchParams.set('search', search.value.trim());
            const response = await fetch(url, { signal: controller.signal }); if (!response.ok) throw new Error();
            const result = await response.json(); if (current !== generation) return;
            result.items.forEach(addCard); page++; hasMore = result.hasMore; empty.hidden = cards.childElementCount !== 0;
            more.hidden = !hasMore; status.textContent = `Showing ${cards.childElementCount} of ${result.totalCount}`;
            if (hasMore && result.items.length) requestAnimationFrame(() => {
                const rect = sentinel.getBoundingClientRect();
                if (current === generation && !cardView.hidden && rect.top <= window.innerHeight + 200 && rect.bottom >= -200) load(false);
            });
        } catch (error) {
            if (error.name !== 'AbortError') { status.textContent = 'Versions could not be loaded. Try again.'; more.hidden = false; hasMore = true; }
        } finally { if (request === controller) request = null; }
    };
    search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => load(true), 250); });
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {
        if (!cardView.hidden && entries.some(x => x.isIntersecting)) load(false);
    }, { rootMargin: '200px' }).observe(sentinel);
    const setView = button => {
        const showCards = button.dataset.versionView === 'cards'; gridView.hidden = showCards; cardView.hidden = !showCards;
        root.querySelectorAll('[data-version-view]').forEach(x => x.setAttribute('aria-pressed', String(x === button)));
        try { localStorage.setItem('krackend-version-view', button.dataset.versionView); } catch { }
        if (showCards) { search.value = gridSearch.value; load(true); }
        else if (gridSearch.value !== search.value) { gridSearch.value = search.value; gridSearch.dispatchEvent(new Event('input', { bubbles: true })); }
    };
    root.querySelectorAll('[data-version-view]').forEach(button => button.addEventListener('click', () => setView(button)));
    try { if (localStorage.getItem('krackend-version-view') === 'cards') setView(root.querySelector('[data-version-view="cards"]')); } catch { }
})();
