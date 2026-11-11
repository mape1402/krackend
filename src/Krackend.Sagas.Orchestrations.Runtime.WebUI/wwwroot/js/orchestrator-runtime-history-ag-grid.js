(() => {
    const root = document.querySelector('[data-instance-history]');
    if (!root) return;
    const grid = root.querySelector('#historyGrid');
    if (!window.agGrid) { grid.textContent = 'The history grid could not load.'; return; }
    const from = root.querySelector('[data-history-from]'), through = root.querySelector('[data-history-through]');
    const search = root.querySelector('[data-history-search]'), error = root.querySelector('[data-history-error]');
    let activeFrom = from.value, activeThrough = '', revision = 0, searchTimer;
    const date = value => { if (!value) return '—'; const text = /(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? value : `${value}Z`; const d = new Date(text); return Number.isNaN(d.getTime()) ? '—' : d.toISOString().replace('T', ' ').slice(0, 19); };
    const theme = () => {
        const css = getComputedStyle(document.documentElement);
        const color = (key, fallback) => css.getPropertyValue(key).trim() || fallback;
        return agGrid.themeQuartz.withParams({ accentColor: color('--z-primary', '#087a57'), backgroundColor: color('--z-surface', '#fff'), foregroundColor: color('--z-text', '#15251f'), borderColor: color('--z-border', '#d6e2dc'), fontSize: 12, spacing: 6 });
    };
    const datasource = { getRows: async params => {
        const requestRevision = revision, url = new URL(root.dataset.source, location.href);
        url.searchParams.set('page', String(Math.floor(params.startRow / 25) + 1)); url.searchParams.set('pageSize', '25');
        if (activeFrom) url.searchParams.set('startedFrom', `${activeFrom}T00:00:00Z`);
        if (activeThrough) { const end = new Date(`${activeThrough}T00:00:00Z`); end.setUTCDate(end.getUTCDate() + 1); url.searchParams.set('startedBefore', end.toISOString()); }
        if (search.value.trim()) url.searchParams.set('search', search.value.trim());
        const sort = params.sortModel?.[0]; if (sort) { url.searchParams.set('sort', sort.colId); url.searchParams.set('descending', String(sort.sort === 'desc')); }
        try {
            const response = await fetch(url, { headers: { Accept: 'application/json' } }); if (!response.ok) throw new Error('History unavailable');
            const result = await response.json(); if (revision !== requestRevision) { params.failCallback(); return; }
            params.successCallback(result.items, Number(result.totalCount)); root.querySelector('[data-history-count]').textContent = `${Number(result.totalCount).toLocaleString()} matching instances`;
        } catch { params.failCallback(); if (revision === requestRevision) root.querySelector('[data-history-count]').textContent = 'Unable to load history'; }
    } };
    const api = agGrid.createGrid(grid, { theme: theme(), rowModelType: 'infinite', datasource, cacheBlockSize: 25, maxBlocksInCache: 20, maxConcurrentDatasourceRequests: 1,
        blockLoadDebounceMillis: 100, rowHeight: 45, headerHeight: 34, suppressMultiSort: true, getRowId: p => String(p.data.id),
        defaultColDef: { sortable: true, resizable: true, minWidth: 100 }, columnDefs: [
            { field: 'orchestrationLabel', headerName: 'Orchestration', flex: 1, minWidth: 220 },
            { field: 'correlationId', headerName: 'Correlation ID', width: 200 }, { field: 'sagaId', headerName: 'Saga ID', width: 200 },
            { field: 'status', headerName: 'Status', width: 130 },
            { field: 'startedOnUtc', headerName: 'Started (UTC)', width: 170, valueFormatter: p => date(p.value) },
            { field: 'lastUpdatedOnUtc', headerName: 'Updated (UTC)', width: 170, valueFormatter: p => date(p.value) },
            { headerName: 'Actions', width: 95, sortable: false, cellRenderer: p => { if (!p.data) return ''; const a = document.createElement('a');
                a.href = root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(p.data.id)); a.className = 'z-button z-button-ghost';
                a.textContent = 'View'; a.setAttribute('aria-label', `View instance ${p.data.id}`); return a; } }
        ] });
    function refresh() { revision++; api.setGridOption('datasource', datasource); }
    root.querySelector('[data-history-filters]').addEventListener('submit', event => {
        event.preventDefault(); const invalid = !!(from.value && through.value && from.value > through.value);
        error.hidden = !invalid; from.setAttribute('aria-invalid', String(invalid)); through.setAttribute('aria-invalid', String(invalid));
        if (!invalid) { activeFrom = from.value; activeThrough = through.value; refresh(); }
    });
    search.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(refresh, 300); });
    const themeObserver = new MutationObserver(() => api.setGridOption('theme', theme()));
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme', 'data-bs-theme', 'data-krackend-theme'] });
    window.addEventListener('pagehide', () => { clearTimeout(searchTimer); themeObserver.disconnect(); api.destroy(); }, { once: true });
})();
