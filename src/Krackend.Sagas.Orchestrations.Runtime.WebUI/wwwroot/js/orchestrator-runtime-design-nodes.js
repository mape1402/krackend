(() => {
    const root = document.querySelector('[data-design-node-catalog]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const gridView = root.querySelector('[data-node-grid]');
    const cardView = root.querySelector('[data-node-cards-view]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const search = root.querySelector('[data-node-search]');
    const cards = root.querySelector('[data-node-cards]');
    const empty = root.querySelector('[data-node-empty]');
    const status = root.querySelector('[data-node-status]');
    const more = root.querySelector('[data-node-more]');
    const sentinel = root.querySelector('[data-node-sentinel]');
    const records = new Map();
    let page = 0, hasMore = true, generation = 0, request = null, timer;

    const make = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };
    const date = value => value ? value.replace('T', ' ').slice(0, 19) : '-';
    const bind = (element, item) => {
        element.dataset.designNode = 'true';
        Object.entries(item).forEach(([key, value]) => {
            if (value != null && key !== 'actions') element.dataset[key] = String(value);
        });
        element.dataset.statusLabel = item.status;
        element.dataset.distributionModeLabel = item.distributionModeLabel;
        element.dataset.inboundCredentialStatusLabel = item.inboundCredentialStatus;
        element.dataset.outboundCredentialStatusLabel = item.outboundCredentialStatus;
        element.dataset.createdOnUtc = date(item.createdOnUtc);
        element.dataset.updatedOnUtc = date(item.updatedOnUtc);
    };
    const iconButton = (icon, label, className) => {
        const button = make('button', `btn btn-sm btn-outline-secondary ${className || ''}`);
        button.type = 'button'; button.title = label; button.setAttribute('aria-label', label);
        const glyph = make('i', `bi ${icon}`); glyph.setAttribute('aria-hidden', 'true'); button.append(glyph);
        return button;
    };
    const actionForm = (url, values, label) => {
        const form = make('form', 'od-inline-form'); form.method = 'post'; form.action = url;
        const token = document.querySelector('#design-node-token input[name="__RequestVerificationToken"]');
        if (token) form.append(token.cloneNode());
        Object.entries(values).forEach(([name, value]) => {
            const input = document.createElement('input'); input.type = 'hidden'; input.name = name; input.value = value; form.append(input);
        });
        const button = make('button', 'dropdown-item', label); button.type = 'submit'; form.append(button);
        return form;
    };
    const actions = item => {
        const group = make('div', 'd-flex gap-2 align-items-center'); group.dataset.nodeActions = '';
        group.append(iconButton('bi-gear', `Configure ${item.name}`, 'js-open-design-node-setup-card'),
            iconButton('bi-eye', `View ${item.name}`, 'js-open-design-node-view'));
        const dropdown = make('div', 'dropdown');
        const toggle = iconButton('bi-three-dots-vertical', `Actions for ${item.name}`);
        toggle.setAttribute('data-bs-toggle', 'dropdown'); toggle.setAttribute('aria-expanded', 'false');
        const menu = make('ul', 'dropdown-menu dropdown-menu-end');
        const add = form => { const li = document.createElement('li'); li.append(form); menu.append(li); };
        if (item.status === 'Suspend') {
            add(actionForm(root.dataset.statusUrl, { designNodeId: item.id, status: 'Pending' }, 'Mark pending'));
            add(actionForm(root.dataset.statusUrl, { designNodeId: item.id, status: 'Enabled' }, 'Enable node'));
        } else if (item.status === 'Pending' || item.status === 'Enabled') {
            add(actionForm(root.dataset.statusUrl, { designNodeId: item.id, status: 'Suspend' }, 'Suspend node'));
        }
        const pull = item.distributionMode === 'RuntimeFetchesFromDesign' || item.distributionMode === 'HybridSync';
        if (item.status === 'Enabled' && pull) {
            add(actionForm(root.dataset.checkUrl, { designNodeId: item.id }, 'Check connection'));
            if (item.outboundCredentialStatus === 'Active' && item.endpointBaseUri && item.remoteRuntimeNodeId) {
                const li = document.createElement('li');
                const button = make('button', 'dropdown-item js-review-pending-releases', 'Review releases');
                button.type = 'button'; button.dataset.sourceKey = item.key; li.append(button); menu.append(li);
            }
        }
        Array.from(group.children).reverse().forEach(child => {
            const entry = document.createElement('li');
            child.classList.remove('btn', 'btn-sm', 'btn-outline-secondary');
            child.classList.add('dropdown-item');
            const label = document.createElement('span'); label.textContent = child.title; child.append(label);
            entry.append(child); menu.prepend(entry);
        });
        dropdown.append(toggle, menu); group.replaceChildren(dropdown);
        return group;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell && !cell.querySelector('[data-node-actions]')) { bind(row, item); cell.replaceChildren(actions(item)); }
    });
    new MutationObserver(decorate).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:loaded', event => {
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item)); decorate();
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));

    const addCard = item => {
        const card = make('article', 'od-runtime-instance-card'); bind(card, item);
        const header = make('div', 'od-runtime-instance-header'); header.append(window.KrackendCatalogUI.cardIcon('hdd-network'), make('h3', '', item.name), actions(item));
        card.append(header, make('p', 'od-runtime-card-meta', item.key), window.KrackendCatalogUI.status(item.status));
        cards.append(card);
    };
    const load = async reset => {
        if (reset) { generation++; request?.abort(); request = null; page = 0; hasMore = true; cards.replaceChildren(); }
        if (request || !hasMore) return;
        const current = generation, controller = new AbortController(); request = controller;
        status.textContent = 'Loading design nodes...'; more.hidden = true;
        try {
            const url = new URL(root.dataset.source, location.href);
            url.searchParams.set('page', String(page + 1)); url.searchParams.set('pageSize', '25'); url.searchParams.set('search', search.value.trim());
            const response = await fetch(url, { signal: controller.signal }); if (!response.ok) throw new Error();
            const result = await response.json(); if (current !== generation) return;
            result.items.forEach(addCard); page++; hasMore = result.hasMore;
            empty.hidden = cards.childElementCount !== 0; more.hidden = !hasMore;
            status.textContent = `Showing ${cards.childElementCount} of ${result.totalCount}`;
            if (hasMore && result.items.length) requestAnimationFrame(() => {
                const rect = sentinel.getBoundingClientRect();
                if (current === generation && !cardView.hidden && rect.top <= window.innerHeight + 200 && rect.bottom >= -200) load(false);
            });
        } catch (error) {
            if (error.name !== 'AbortError') { status.textContent = 'Design nodes could not be loaded. Try again.'; more.hidden = false; hasMore = true; }
        } finally { if (request === controller) request = null; }
    };
    search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => load(true), 250); });
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {
        if (!cardView.hidden && entries.some(x => x.isIntersecting)) load(false);
    }, { rootMargin: '200px' }).observe(sentinel);
    const setView = button => {
        const showCards = button.dataset.nodeView === 'cards';
        gridView.hidden = showCards; cardView.hidden = !showCards;
        root.querySelectorAll('[data-node-view]').forEach(x => x.setAttribute('aria-pressed', String(x === button)));
        try { localStorage.setItem('krackend-runtime-design-node-view', button.dataset.nodeView); } catch { }
        if (showCards) { search.value = gridSearch.value; load(true); }
        else if (gridSearch.value !== search.value) { gridSearch.value = search.value; gridSearch.dispatchEvent(new Event('input', { bubbles: true })); }
    };
    root.querySelectorAll('[data-node-view]').forEach(button => button.addEventListener('click', () => setView(button)));
    try { if (localStorage.getItem('krackend-runtime-design-node-view') === 'cards') setView(root.querySelector('[data-node-view="cards"]')); } catch { }

    const pending = document.getElementById('pendingReleasesCatalog');
    const pendingDialog = document.getElementById('pendingReleasesModal');
    const pendingItems = pending?.querySelector('[data-pending-items]');
    const pendingStatus = pending?.querySelector('[data-pending-status]');
    const pendingMore = pending?.querySelector('[data-pending-more]');
    const pendingSentinel = pending?.querySelector('[data-pending-sentinel]');
    const applyForm = document.getElementById('pendingReleaseApplyForm');
    let pendingSource = '', pendingPage = 0, pendingHasMore = false, pendingLoading = false, pendingGeneration = 0;
    const loadPending = async () => {
        if (!pendingSource || pendingLoading || !pendingHasMore) return;
        const current = pendingGeneration;
        let loaded = false;
        pendingLoading = true; pendingMore.disabled = true; pendingStatus.textContent = 'Loading releases...';
        try {
            const url = new URL(pending.dataset.source, location.href);
            url.searchParams.set('sourceKey', pendingSource); url.searchParams.set('page', String(pendingPage + 1));
            const response = await fetch(url);
            if (!response.ok) {
                let message = 'Pending releases could not be loaded. Please try again.';
                try { message = (await response.json()).message || message; } catch { }
                throw new Error(message);
            }
            const result = await response.json(); if (current !== pendingGeneration) return;
            result.items.forEach(item => {
                const card = make('article', 'od-pending-release-card');
                const details = document.createElement('div');
                details.append(make('h3', '', `${item.orchestrationDefinitionKey} v${item.version}`),
                    make('p', 'od-item-meta', `${({ 'orchestration-version.new': 'New version', 'orchestration-version-snapshot': 'New version', 'orchestration-version.deprecated': 'Deprecate', 'orchestration-version.archived': 'Archive' })[item.artifactType] || item.artifactType || 'Artifact'} · ${item.releaseStrategy || 'Strategy unavailable'}`),
                    make('p', 'od-item-meta', item.releaseId ? `Release: ${item.releaseId}` : 'Release identifier unavailable'),
                    make('p', 'od-item-meta', `Promoted by ${item.promotedBy} on ${date(item.promotedOnUtc)} UTC`),
                    make('p', 'od-item-meta', `Correlation: ${item.correlationId}`),
                    make('p', 'od-item-meta', `Checksum: ${item.checksum}`));
                const button = make('button', 'z-button z-button-primary', 'Apply'); button.type = 'button';
                button.addEventListener('click', () => {
                    applyForm.elements.sourceKey.value = pendingSource;
                    applyForm.elements.releaseTargetId.value = item.releaseTargetId;
                    applyForm.requestSubmit();
                });
                card.append(details, button); pendingItems.append(card);
            });
            pendingPage++; pendingHasMore = result.hasMore; pendingMore.hidden = !pendingHasMore; loaded = true;
            pendingMore.textContent = 'Load more releases';
            pendingStatus.textContent = result.totalCount ? `Showing ${pendingItems.childElementCount} of ${result.totalCount}` : 'No pending releases for this design node.';
        } catch (error) {
            if (current === pendingGeneration) { pendingStatus.textContent = error.message || 'Pending releases could not be loaded. Please try again.'; pendingMore.hidden = false; pendingMore.textContent = 'Retry'; }
        } finally {
            if (current === pendingGeneration) {
                pendingLoading = false; pendingMore.disabled = false;
                if (loaded && pendingHasMore && pendingSentinel) requestAnimationFrame(() => {
                    const rect = pendingSentinel.getBoundingClientRect();
                    if (current === pendingGeneration && rect.width && rect.top <= window.innerHeight + 200 && rect.bottom >= -200) loadPending();
                });
            }
        }
    };
    const openPending = (sourceKey, opener) => {
        pendingGeneration++; pendingSource = sourceKey; pendingPage = 0; pendingHasMore = true; pendingLoading = false;
        pendingItems.replaceChildren(); pendingMore.hidden = true;
        pending.querySelector('[data-pending-source-label]').textContent = `Source: ${sourceKey}`;
        pendingDialog?.open?.(opener); loadPending();
    };
    root.addEventListener('click', event => {
        const button = event.target.closest('.js-review-pending-releases');
        if (button && pending) openPending(button.dataset.sourceKey, button);
    });
    pendingMore?.addEventListener('click', loadPending);
    if (pendingSentinel && 'IntersectionObserver' in window)
        new IntersectionObserver(entries => { if (entries.some(entry => entry.isIntersecting)) loadPending(); }, { rootMargin: '200px' }).observe(pendingSentinel);
    if (pending?.dataset.initialSource) openPending(pending.dataset.initialSource);
})();
