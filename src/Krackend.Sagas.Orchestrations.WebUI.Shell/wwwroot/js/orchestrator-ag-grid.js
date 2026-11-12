(() => {
    function mount(root, options = {}) {
        if (root.krackendGrid) return root.krackendGrid;
        const host = root.querySelector('[data-grid-host]');
        const search = root.querySelector('[data-grid-search]');
        const status = root.querySelector('[data-grid-state]');
        if (!window.agGrid) { status.textContent = 'The grid could not load.'; return null; }
        const config = JSON.parse(root.querySelector('[data-krackend-grid-config]').textContent);
        const size = Math.max(1, Number(config.pageSize) || 25);
        const requests = new Set(), loaded = new Set();
        let revision = 0, timer;
        const theme = () => {
            const css = getComputedStyle(document.documentElement);
            const color = (key, fallback) => css.getPropertyValue(key).trim() || fallback;
            return agGrid.themeQuartz.withParams({ accentColor: color('--z-primary', '#087a57'),
                backgroundColor: color('--z-surface', '#fff'), foregroundColor: color('--z-text', '#15251f'),
                borderColor: color('--z-border', '#d6e2dc'), fontSize: 10, spacing: 6 });
        };
        const columns = config.columns.map(column => ({ field: column.field, headerName: column.title,
            sortable: !!column.sortable, resizable: true, flex: 1, minWidth: column.field === 'actions' ? 100 : 120,
            cellRenderer: params => {
                if (!params.data) return '';
                const result = options.renderCell?.(column.field, params.data, params);
                if (result != null) {
                    if (result instanceof Element) result.querySelectorAll('[data-bs-toggle="dropdown"]').forEach(button => button.setAttribute('data-bs-boundary', 'viewport'));
                    return result;
                }
                if (['status', 'statusLabel', 'lifecycleStatus', 'approval', 'application', 'activation'].includes(column.field)
                    && params.value != null && String(params.value).trim() && window.KrackendCatalogUI) {
                    const badge = window.KrackendCatalogUI.status(column.field === 'statusLabel' ? params.data.status ?? params.value : params.value);
                    badge.textContent = String(params.value);
                    badge.classList.add('od-grid-status');
                    return badge;
                }
                const text = document.createElement('span');
                text.textContent = params.value == null ? '' : String(params.value);
                return text;
            } }));
        const source = { getRows: async params => {
            const current = revision, controller = new AbortController(); requests.add(controller);
            const url = new URL(config.binding.source, location.href);
            url.searchParams.set('page', String(Math.floor(params.startRow / size) + 1));
            url.searchParams.set('pageSize', String(size));
            url.searchParams.set('search', search.value.trim());
            const sort = params.sortModel?.[0];
            if (sort) { url.searchParams.set('sort', sort.colId); url.searchParams.set('descending', String(sort.sort === 'desc')); }
            try {
                const response = await fetch(url, { signal: controller.signal, headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error('Request failed');
                const result = await response.json();
                if (current !== revision) { params.failCallback(); return; }
                const rows = Array.isArray(result.items) ? result.items : [];
                const total = Number(result.totalCount);
                rows.forEach((row, i) => loaded.add(String(row.id ?? params.startRow + i)));
                params.successCallback(rows, Number.isFinite(total) ? total : result.hasMore ? undefined : params.startRow + rows.length);
                status.textContent = !rows.length && params.startRow === 0 ? 'No matching records.' : '';
                options.onLoaded?.(result, loaded.size);
            } catch (error) {
                params.failCallback();
                if (error.name !== 'AbortError' && current === revision) status.textContent = 'Unable to load records. Try again.';
            } finally { requests.delete(controller); }
        } };
        const api = agGrid.createGrid(host, { theme: theme(), rowModelType: 'infinite', datasource: source,
            cacheBlockSize: size, maxBlocksInCache: 20, maxConcurrentDatasourceRequests: 1,
            blockLoadDebounceMillis: 100, rowHeight: 45, headerHeight: 34,
            columnDefs: columns, suppressMultiSort: true,
            onCellClicked: event => options.onCellClicked?.(event),
            onCellKeyDown: event => options.onCellKeyDown?.(event) });
        root.addEventListener('show.bs.dropdown', event => {
            const dropdown = event.target.closest('.dropdown');
            const menu = dropdown?.querySelector('.dropdown-menu');
            if (!menu) return;
            const available = host.getBoundingClientRect().bottom - event.target.getBoundingClientRect().bottom;
            dropdown.classList.toggle('dropup', available < menu.children.length * 40 + 20);
        });
        const refresh = () => { revision++; requests.forEach(request => request.abort()); loaded.clear(); status.textContent = ''; api.setGridOption('datasource', source); };
        search.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(refresh, 250); });
        const observer = new MutationObserver(() => api.setGridOption('theme', theme()));
        observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme', 'data-bs-theme', 'data-krackend-theme'] });
        window.addEventListener('pagehide', event => { if (!event.persisted) { clearTimeout(timer); requests.forEach(request => request.abort()); observer.disconnect(); api.destroy(); } });
        return root.krackendGrid = { api, refresh };
    }
    window.KrackendCatalogGrid = { mount };
    document.addEventListener('DOMContentLoaded', () => document.querySelectorAll('[data-krackend-grid]').forEach(grid => mount(grid)), { once: true });
})();
