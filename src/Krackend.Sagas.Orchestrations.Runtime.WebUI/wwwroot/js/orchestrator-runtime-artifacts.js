(() => {
    const root = document.querySelector('[data-artifact-catalog]');
    if (!root) return;
    const kind = root.dataset.kind;
    const grid = root.querySelector('[data-z-data-grid]');
    const gridView = root.querySelector('[data-artifact-grid]');
    const cardView = root.querySelector('[data-artifact-cards-view]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const search = root.querySelector('[data-artifact-search]');
    const cards = root.querySelector('[data-artifact-cards]');
    const empty = root.querySelector('[data-artifact-empty]');
    const status = root.querySelector('[data-artifact-status]');
    const more = root.querySelector('[data-artifact-more]');
    const sentinel = root.querySelector('[data-artifact-sentinel]');
    const records = new Map();
    let page = 0, hasMore = true, generation = 0, request = null, timer;
    const make = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    };
    const iconButton = (icon, label) => {
        const button = make('button', 'btn btn-sm btn-outline-secondary');
        button.type = 'button'; button.title = label; button.setAttribute('aria-label', label);
        const glyph = make('i', `bi ${icon}`); glyph.setAttribute('aria-hidden', 'true'); button.append(glyph);
        return button;
    };
    const actions = item => {
        if (kind === 'groups') {
            const link = make('a', 'btn btn-sm btn-outline-secondary');
            const url = new URL(root.dataset.indexUrl, location.href); url.searchParams.set('orchestrationKey', item.key);
            link.href = url.toString(); link.title = 'View versions'; link.setAttribute('aria-label', `View versions for ${item.key}`);
            const icon = make('i', 'bi bi-eye'); icon.setAttribute('aria-hidden', 'true'); link.append(icon);
            return link;
        }
        const group = make('div', 'd-flex gap-2');
        const history = iconButton('bi-clock-history', `View history for version ${item.version}`);
        history.addEventListener('click', () => showHistory(item)); group.append(history);
        if (item.isActive && item.status === 'Ready') {
            const standup = iconButton('bi-broadcast-pin', `Request standup for version ${item.version}`);
            standup.addEventListener('click', () => {
                const form = document.getElementById('artifact-standup-form'); form.elements.artifactId.value = item.id; form.requestSubmit();
            });
            group.append(standup);
        }
        return group;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell && !cell.querySelector('[data-artifact-actions]')) {
            const content = actions(item); content.dataset.artifactActions = ''; cell.replaceChildren(content);
        }
    });
    new MutationObserver(decorate).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:loaded', event => {
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorate();
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));

    const addCard = item => {
        const card = make('article', 'od-runtime-instance-card');
        const header = make('div', 'od-runtime-instance-header');
        header.append(make('h3', '', kind === 'groups' ? item.key : `Version ${item.version}`), actions(item));
        card.append(header);
        if (kind === 'groups') card.append(make('p', 'od-runtime-card-meta', `${item.versionCount} versions · ${item.activeCount} active`));
        else [item.lifecycleStatus, item.status].filter(Boolean).forEach(value => card.append(window.KrackendCatalogUI.status(value)));
        cards.append(card);
    };
    const load = async reset => {
        if (reset) { generation++; request?.abort(); request = null; page = 0; hasMore = true; cards.replaceChildren(); }
        if (request || !hasMore) return;
        const current = generation, controller = new AbortController(); request = controller;
        status.textContent = 'Loading artifacts...'; more.hidden = true;
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
            if (error.name !== 'AbortError') { status.textContent = 'Artifacts could not be loaded. Try again.'; more.hidden = false; hasMore = true; }
        } finally { if (request === controller) request = null; }
    };
    search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(() => load(true), 250); });
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {
        if (!cardView.hidden && entries.some(x => x.isIntersecting)) load(false);
    }, { rootMargin: '200px' }).observe(sentinel);
    const setView = button => {
        const showCards = button.dataset.artifactView === 'cards';
        gridView.hidden = showCards; cardView.hidden = !showCards;
        root.querySelectorAll('[data-artifact-view]').forEach(x => x.setAttribute('aria-pressed', String(x === button)));
        try { localStorage.setItem('krackend-runtime-artifact-view', button.dataset.artifactView); } catch { }
        if (showCards) { search.value = gridSearch.value; load(true); }
        else if (gridSearch.value !== search.value) { gridSearch.value = search.value; gridSearch.dispatchEvent(new Event('input', { bubbles: true })); }
    };
    root.querySelectorAll('[data-artifact-view]').forEach(button => button.addEventListener('click', () => setView(button)));
    try { if (localStorage.getItem('krackend-runtime-artifact-view') === 'cards') setView(root.querySelector('[data-artifact-view="cards"]')); } catch { }

    const historyModalElement = document.getElementById('artifactHistoryModal');
    const historyDetails = document.getElementById('artifactHistoryDetails');
    const historyItems = document.getElementById('artifactHistoryItems');
    const historyStatus = document.getElementById('artifactHistoryStatus');
    const historyMore = document.getElementById('artifactHistoryMore');
    const historyScroll = historyModalElement.querySelector('[data-z-dialog-focus]');
    let historyId = '', historyPage = 0, historyHasMore = true, historyLoading = false, historyGeneration = 0;
    const loadHistory = async () => {
        if (historyLoading || !historyHasMore) return;
        const current = historyGeneration; historyLoading = true; historyStatus.textContent = 'Loading history...'; historyMore.hidden = true;
        try {
            const url = new URL(root.dataset.historySource, location.href);
            url.searchParams.set('artifactId', historyId); url.searchParams.set('page', String(historyPage + 1));
            const response = await fetch(url); if (!response.ok) throw new Error();
            const result = await response.json(); if (current !== historyGeneration) return;
            result.items.forEach(item => {
                const entry = make('div', 'mb-2');
                entry.append(make('strong', '', item.artifactType), make('div', '', `${item.lifecycleStatus} · ${item.runtimeStatus} · ${item.applied} UTC`),
                    make('small', '', `Release: ${item.releaseId || 'Not available'} · Strategy: ${item.releaseStrategy || '-'}`));
                historyItems.append(entry);
            });
            historyPage++; historyHasMore = result.hasMore; historyMore.hidden = !historyHasMore;
            historyStatus.textContent = historyPage === 1 && !result.items.length ? 'No release history available.' : '';
            if (result.items.length && historyHasMore) requestAnimationFrame(() => {
                if (current === historyGeneration && !historyModalElement.hidden &&
                    historyScroll.scrollTop + historyScroll.clientHeight >= historyScroll.scrollHeight - 48) loadHistory();
            });
        } catch { if (current === historyGeneration) { historyStatus.textContent = 'History could not be loaded. Try again.'; historyMore.hidden = false; } }
        finally { if (current === historyGeneration) historyLoading = false; }
    };
    const showHistory = item => {
        if (!historyModalElement?.open) return;
        historyId = item.id; historyPage = 0; historyHasMore = true; historyLoading = false; historyGeneration++;
        historyItems.replaceChildren(); historyDetails.replaceChildren();
        const details = make('dl', 'od-artifact-details');
        [['Version', item.version], ['Lifecycle', item.lifecycleStatus], ['Runtime state', item.status], ['Ingress', item.isActive ? 'Active' : 'Removed'],
            ['Generation', item.ingressGeneration], ['Deployed', `${item.deployed} UTC`], ['Latest release', item.lastReleaseId || '-'],
            ['Control artifact', item.lastControlPlaneArtifactId || '-'], ['Checksum', item.checksum],
            ['Activated', item.activatedOnUtc || '-'], ['Projection started', item.projectionStartedOnUtc || '-'],
            ['Projection completed', item.projectionCompletedOnUtc || '-'], ['Projection failed', item.projectionFailedOnUtc || '-'],
            ['Projection error', item.projectionError || '-']]
            .forEach(([label, value]) => details.append(make('dt', '', label), make('dd', '', String(value ?? '-'))));
        historyDetails.append(details); historyModalElement.open(); loadHistory();
    };
    historyMore.addEventListener('click', loadHistory);
    historyScroll.addEventListener('scroll', event => {
        const body = event.currentTarget; if (body.scrollTop + body.clientHeight >= body.scrollHeight - 48) loadHistory();
    });
})();
