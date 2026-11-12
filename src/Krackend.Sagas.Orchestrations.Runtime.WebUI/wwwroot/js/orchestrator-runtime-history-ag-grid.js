(() => {
    const root = document.querySelector('[data-instance-history]');
    if (!root) return;
    const grid = root.querySelector('#historyGrid');
    if (!window.agGrid) { grid.textContent = 'The history grid could not load.'; return; }
    const inspector = window.KrackendInstanceInspector.create(root, root.querySelector('[data-workspace]'));

    const form = root.querySelector('[data-history-filters]');
    const from = root.querySelector('[data-history-from]');
    const through = root.querySelector('[data-history-through]');
    const orchestration = root.querySelector('[data-history-orchestration]');
    const version = root.querySelector('[data-history-version]');
    const status = root.querySelector('[data-history-status]');
    const search = root.querySelector('[data-history-search]');
    const error = root.querySelector('[data-history-error]');
    const message = root.querySelector('[data-history-message]');
    const count = root.querySelector('[data-history-count]');
    let active = { from: from.value, through: '', orchestration: '', version: '', status: '', search: '' };
    let revision = 0, searchTimer, closed = false;
    const requests = new Set();
    const escape = text => String(text ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const date = value => {
        if (!value) return '—';
        const text = /(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? value : `${value}Z`;
        const result = new Date(text);
        return Number.isNaN(result.getTime()) ? '—' : result.toISOString().replace('T', ' ').slice(0, 19);
    };
    const theme = () => {
        const css = getComputedStyle(document.documentElement);
        const color = (key, fallback) => css.getPropertyValue(key).trim() || fallback;
        return agGrid.themeQuartz.withParams({ accentColor: color('--z-primary', '#087a57'), backgroundColor: color('--z-surface', '#fff'), foregroundColor: color('--z-text', '#15251f'), borderColor: color('--z-border', '#d6e2dc'), fontSize: 12, spacing: 6 });
    };
    function listUrl(start, sortModel) {
        const url = new URL(root.dataset.source, location.href);
        url.searchParams.set('page', String(Math.floor(start / 25) + 1));
        url.searchParams.set('pageSize', '25');
        if (active.from) url.searchParams.set('startedFrom', `${active.from}T00:00:00Z`);
        // There is no representable .NET instant after the last day of year 9999.
        if (active.through && active.through !== '9999-12-31') {
            const end = new Date(`${active.through}T00:00:00Z`);
            end.setUTCDate(end.getUTCDate() + 1);
            url.searchParams.set('startedBefore', end.toISOString());
        }
        if (active.search) url.searchParams.set('search', active.search);
        if (active.orchestration) url.searchParams.set('orchestrationKey', active.orchestration);
        if (active.version) url.searchParams.set('orchestrationVersion', active.version);
        if (active.status) url.searchParams.set('statusGroup', active.status);
        const sort = sortModel?.[0];
        if (sort) { url.searchParams.set('sort', sort.colId); url.searchParams.set('descending', String(sort.sort === 'desc')); }
        return url;
    }
    const datasource = { getRows: async params => {
        const requestRevision = revision, controller = new AbortController();
        requests.add(controller);
        try {
            const response = await fetch(listUrl(params.startRow, params.sortModel), { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error('History unavailable');
            const result = await response.json();
            if (closed || revision !== requestRevision) { params.failCallback(); return; }
            params.successCallback(result.items, Number(result.totalCount));
            count.textContent = `${Number(result.totalCount).toLocaleString()} matching instances`;
            const versionNote = active.version ? 'Instances without a recorded version cannot match the version filter.' : '';
            message.textContent = result.totalCount ? versionNote : ['No instances match these filters.', versionNote].filter(Boolean).join(' ');
        } catch (exception) {
            params.failCallback();
            if (!closed && revision === requestRevision && exception.name !== 'AbortError') {
                count.textContent = '';
                message.textContent = 'Unable to load history. Try applying the filters again.';
            }
        } finally { requests.delete(controller); }
    } };
    const columns = [
        { field: 'orchestrationLabel', headerName: 'Orchestration', pinned: 'left', width: 220, minWidth: 200,
            cellRenderer: p => p.data ? `<div class="orchestration-cell"><span class="orchestration-glyph"><i class="bi bi-bezier2"></i></span><span class="orchestration-copy"><strong>${escape(p.data.orchestrationDefinitionKey || p.value || '—')}</strong><small>${escape(p.data.orchestrationVersion || '—')}</small></span></div>` : '' },
        ...[['correlationId', 'Correlation ID'], ['sagaId', 'Saga ID']].map(([field, headerName]) => ({ field, headerName, flex: 1, minWidth: 130,
            cellRenderer: p => p.data ? `<div class="mono-cell"><span>${escape(p.value || '—')}</span><button class="copy-id" type="button" aria-label="Copy ${headerName}"><i class="bi bi-copy"></i></button></div>` : '' })),
        { field: 'status', headerName: 'Status', flex: .7, minWidth: 105, cellRenderer: p => p.data ? `<span class="status-pill status-${escape(String(p.value || '').toLowerCase())}">${escape(p.value || '—')}</span>` : '' },
        { field: 'startedOnUtc', headerName: 'Started (UTC)', flex: 1, minWidth: 145, sort: 'desc', valueFormatter: p => date(p.value) },
        { field: 'lastUpdatedOnUtc', headerName: 'Updated (UTC)', flex: 1, minWidth: 145, valueFormatter: p => date(p.value) }
    ];
    const api = agGrid.createGrid(grid, { theme: theme(), rowModelType: 'infinite', datasource, cacheBlockSize: 25, maxBlocksInCache: 20,
        maxConcurrentDatasourceRequests: 1,
        blockLoadDebounceMillis: 100, rowHeight: 45, headerHeight: 34, suppressMultiSort: true, getRowId: p => String(p.data.id),
        defaultColDef: { sortable: true, resizable: true, minWidth: 100 }, columnDefs: columns,
        onCellClicked: async event => {
            if (!event.data) return;
            if (event.event?.target?.closest('.copy-id')) {
                try { await navigator.clipboard.writeText(String(event.value || '')); message.textContent = 'ID copied.'; }
                catch { message.textContent = 'Unable to copy the ID.'; }
            } else if (!event.event?.target?.closest('input, button, a')) inspector.open(event.data);
        },
        onCellKeyDown: event => { if (event.data && event.event?.key === 'Enter' && !event.event.target?.closest('input, button, a')) { event.event.preventDefault(); inspector.open(event.data); } }
    });
    function refresh() {
        revision++;
        inspector.close();
        requests.forEach(controller => controller.abort());
        message.textContent = ''; count.textContent = '';
        api.setGridOption('datasource', datasource);
    }
    function clearValidation() {
        error.hidden = true;
        from.removeAttribute('aria-invalid'); through.removeAttribute('aria-invalid');
    }
    form.addEventListener('submit', event => {
        event.preventDefault();
        const invalid = !!(from.value && through.value && from.value > through.value);
        error.hidden = !invalid;
        from.setAttribute('aria-invalid', String(invalid)); through.setAttribute('aria-invalid', String(invalid));
        if (invalid) return;
        clearTimeout(searchTimer);
        active = { from: from.value, through: through.value, orchestration: orchestration.value.trim(), version: version.value.trim(),
            status: status.value, search: search.value.trim() };
        refresh();
    });
    root.querySelector('[data-history-clear]').addEventListener('click', () => {
        clearTimeout(searchTimer);
        from.value = root.dataset.defaultFrom; through.value = ''; orchestration.value = ''; version.value = ''; status.value = ''; search.value = '';
        active = { from: from.value, through: '', orchestration: '', version: '', status: '', search: '' };
        clearValidation(); refresh();
    });
    search.addEventListener('input', () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => { active = { ...active, search: search.value.trim() }; refresh(); }, 300);
    });
    const themeObserver = new MutationObserver(() => api.setGridOption('theme', theme()));
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme', 'data-bs-theme', 'data-krackend-theme'] });
    window.addEventListener('pagehide', event => {
        if (event.persisted) return;
        closed = true; clearTimeout(searchTimer); inspector.close(); requests.forEach(controller => controller.abort()); themeObserver.disconnect(); api.destroy();
    });
})();
