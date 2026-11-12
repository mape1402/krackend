(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;
    const grid = root.querySelector('#instancesGrid');
    if (!window.agGrid) { grid.textContent = 'The instance grid could not load.'; return; }
    const search = root.querySelector('[data-instance-search]');
    const workspace = root.querySelector('[data-workspace]');
    const inspector = window.KrackendInstanceInspector.create(root, workspace, () => { selected = null; scheduleInspectorFit(); });
    const counts = { total: 0, running: 0, idle: 0, completed: 0, deadLettered: 0, failed: 0 };
    const pending = [], seen = new Set(), selectedRows = new Map();
    let hours = 24, statusGroup = '', compact = false, selected = null, countsRequest, sparkRequest, searchTimer, liveTimer, countsTimer, sparkTimer, inspectorTimer, pendingInspectorId, inspectorFitTimer;
    let revision = 0;
    let snapshotEnd = new Date(), snapshotTotal = null, flushedAppends = 0, layoutRevision = 0;
    const appended = [], appendedById = new Map();
    const snapshotPages = new Map(), snapshotRows = new Map();
    const value = (event, key) => event?.[key] ?? event?.[key[0].toUpperCase() + key.slice(1)];
    const publicStatus = status => ({ Created: 'Running', Waiting: 'Running', Compensating: 'Running',
        Compensated: 'Completed', Stopped: 'Failed', Aborted: 'Failed', CompletedWithErrors: 'Failed' }[status] || status || 'Running');
    const keyFor = status => status ? ({ Running: 'running', Idle: 'idle', Completed: 'completed', DeadLettered: 'deadLettered', Failed: 'failed' }[publicStatus(status)]) : null;
    const date = input => { if (!input) return null; const text = /(?:Z|[+-]\d\d:\d\d)$/i.test(input) ? input : `${input}Z`; const d = new Date(text); return Number.isNaN(d.getTime()) ? null : d; };
    const relativeDate = input => {
        const value = date(input); if (!value) return '—';
        const seconds = Math.max(0, Math.floor((Date.now() - value.getTime()) / 1000));
        const relative = seconds < 60 ? `${seconds}s ago` : seconds < 3600 ? `${Math.floor(seconds / 60)}m ago` : seconds < 86400 ? `${Math.floor(seconds / 3600)}h ago` : `${Math.floor(seconds / 86400)}d ago`;
        return `<div class="relative-time"><strong>${relative}</strong><small>${escape(value.toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }))}</small></div>`;
    };
    const duration = row => {
        const start = date(row.startedOnUtc); if (!start) return '—';
        const active = ['Running', 'Idle'].includes(publicStatus(row.status));
        const end = active ? new Date() : date(row.completedOnUtc || row.failedOnUtc || row.stoppedOnUtc || row.compensatedOnUtc);
        if (!end) return '—';
        const seconds = Math.max(0, Math.floor((end - start) / 1000));
        return seconds < 60 ? `${seconds}s` : seconds < 3600 ? `${Math.floor(seconds / 60)}m ${seconds % 60}s` : `${Math.floor(seconds / 3600)}h ${Math.floor(seconds % 3600 / 60)}m`;
    };
    const escape = text => String(text ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const progress = row => row.totalTaskCount == null || row.completedTaskCount == null ? null
        : row.totalTaskCount === 0 ? (row.status === 'Completed' ? 100 : 0)
        : Math.max(0, Math.min(100, Math.round(100 * row.completedTaskCount / row.totalTaskCount)));
    function rowStatus(row) { const s = publicStatus(row?.status); return `<span class="status-pill status-${escape(s.toLowerCase())}">${escape(s)}</span>`; }
    function scheduleInspectorFit() {
        clearTimeout(inspectorFitTimer);
        inspectorFitTimer = setTimeout(() => { if (api && !api.isDestroyed()) api.sizeColumnsToFit(); }, 250);
    }
    function renderInspector(row) { const wasOpen = !!selected; selected = row; inspector.open(row); if (!wasOpen) scheduleInspectorFit(); }
    function theme() {
        const css = getComputedStyle(root);
        const c = (key, fallback) => css.getPropertyValue(key).trim() || fallback;
        return agGrid.themeQuartz.withParams({ accentColor: c('--primary', '#087a57'), backgroundColor: c('--surface', '#fff'),
            foregroundColor: c('--text', '#15251f'), borderColor: c('--border', '#d6e2dc'), fontSize: 10, spacing: 6 });
    }
    function loadedRows(gridApi) {
        const rows = [];
        gridApi.forEachNode(node => { if (node.data) rows.push(node.data); });
        return rows;
    }
    function listUrl(startRow) {
        const url = new URL(root.dataset.source, location.href);
        url.searchParams.set('page', String(Math.floor(startRow / 25) + 1));
        url.searchParams.set('pageSize', '25');
        url.searchParams.set('startedFrom', new Date(snapshotEnd.getTime() - hours * 3600000).toISOString());
        url.searchParams.set('startedBefore', snapshotEnd.toISOString());
        url.searchParams.set('sort', 'startedOnUtc'); url.searchParams.set('descending', 'true');
        if (search.value.trim()) url.searchParams.set('search', search.value.trim());
        if (statusGroup) url.searchParams.set('statusGroup', statusGroup);
        return url;
    }
    function updateSelection(gridApi) {
        root.querySelector('[data-selection-bar]').hidden = selectedRows.size === 0;
        root.querySelector('[data-selection-count]').textContent = String(selectedRows.size);
        const header = grid.querySelector('[data-select-loaded]'), rows = loadedRows(gridApi);
        if (header) {
            const checked = rows.filter(row => selectedRows.has(String(row.id))).length;
            header.checked = rows.length > 0 && checked === rows.length;
            header.indeterminate = checked > 0 && checked < rows.length;
            header.disabled = rows.length === 0;
        }
    }
    class SelectionHeader {
        init(params) {
            this.element = document.createElement('input'); this.element.type = 'checkbox';
            this.element.dataset.selectLoaded = ''; this.element.setAttribute('aria-label', 'Select loaded rows');
            this.element.title = 'Select loaded rows';
            this.element.addEventListener('change', () => {
                const checked = this.element.checked;
                root.querySelector('[data-selection-message]').textContent = '';
                loadedRows(params.api).forEach(row => checked ? selectedRows.set(String(row.id), row) : selectedRows.delete(String(row.id)));
                params.api.refreshCells({ columns: ['selection'], force: true });
                updateSelection(params.api);
            });
        }
        getGui() { return this.element; }
    }
    const columnDefs = [
        { colId: 'selection', width: 42, minWidth: 42, maxWidth: 42, pinned: 'left', sortable: false, resizable: false, headerComponent: SelectionHeader,
            cellRenderer: p => { if (!p.data) return ''; const checkbox = document.createElement('input'); checkbox.type = 'checkbox';
                checkbox.checked = selectedRows.has(String(p.data.id)); checkbox.setAttribute('aria-label', `Select ${p.data.sagaId || p.data.id}`);
                checkbox.addEventListener('click', e => e.stopPropagation()); checkbox.addEventListener('change', () => {
                    if (checkbox.checked) selectedRows.set(String(p.data.id), p.data); else selectedRows.delete(String(p.data.id)); updateSelection(p.api);
                }); return checkbox; } },
        { field: 'orchestrationLabel', headerName: 'Orchestration', pinned: 'left', width: 195,
            cellRenderer: p => p.data ? `<div class="orchestration-cell"><span class="orchestration-glyph"><i class="bi bi-bezier2"></i></span><span class="orchestration-copy"><strong>${escape(p.data.orchestrationDefinitionKey)}</strong><small>${escape(p.data.orchestrationVersion || '')}</small></span></div>` : '' },
        ...[['correlationId', 'Correlation ID'], ['sagaId', 'Saga ID']].map(([field, headerName]) => ({ field, headerName, width: 145,
            cellRenderer: p => p.data ? `<div class="mono-cell"><span>${escape(p.value || '—')}</span><button class="copy-id" type="button" aria-label="Copy ${headerName}"><i class="bi bi-copy"></i></button></div>` : '' })),
        { field: 'status', headerName: 'Status', width: 110, cellRenderer: p => p.data ? rowStatus(p.data) : '' },
        { field: 'progress', headerName: 'Progress', width: 105, sortable: false,
            cellRenderer: p => { if (!p.data) return ''; const n = progress(p.data); return n == null ? '—' : `<div class="progress-cell"><div class="progress-cell-track"><span style="width:${n}%"></span></div><small>${n}%</small></div>`; } },
        { field: 'currentStageKey', headerName: 'Current workflow', minWidth: 150, flex: 1, sortable: false,
            cellRenderer: p => p.data ? `<div class="workflow-cell"><strong>${escape(p.data.currentStageKey || p.data.lastExecutedStageKey || '—')}</strong><small>${escape(p.data.currentTaskKey || p.data.lastExecutedTaskKey || '')}</small></div>` : '' },
        { field: 'retryCount', headerName: 'Retries', width: 80, sortable: false },
        { field: 'startedOnUtc', headerName: 'Started', width: 105, cellRenderer: p => p.data ? relativeDate(p.value) : '' },
        { field: 'lastUpdatedOnUtc', headerName: 'Updated', width: 105, cellRenderer: p => p.data ? relativeDate(p.value) : '' },
        { headerName: 'Duration', width: 80, sortable: false, cellRenderer: p => p.data ? duration(p.data) : '' }
    ];
    let api;
    const datasource = { getRows: async params => {
        const requestRevision = revision, requestLayout = layoutRevision, liveCount = appended.length;
        try {
            let items = [];
            for (let index = params.startRow; index < Math.min(params.endRow, liveCount); index++)
                items.push(appended[liveCount - index - 1]);
            let offset = Math.max(0, params.startRow - liveCount);
            while ((snapshotTotal === null || offset < snapshotTotal) && (items.length < params.endRow - params.startRow || snapshotTotal === null)) {
                const page = Math.floor(offset / 25);
                let rowsInPage = snapshotPages.get(page);
                if (rowsInPage) { snapshotPages.delete(page); snapshotPages.set(page, rowsInPage); }
                else {
                    const response = await fetch(listUrl(offset), { headers: { Accept: 'application/json' } });
                    if (!response.ok) throw new Error('Instances unavailable');
                    const result = await response.json();
                    if (requestRevision !== revision || requestLayout !== layoutRevision) { params.failCallback(); return; }
                    if (snapshotTotal === null) snapshotTotal = Number(result.totalCount);
                    rowsInPage = result.items;
                    snapshotPages.set(page, rowsInPage);
                    for (const row of rowsInPage) snapshotRows.set(String(row.id), row);
                    if (snapshotPages.size > 22) {
                        const oldest = snapshotPages.keys().next().value;
                        for (const row of snapshotPages.get(oldest)) if (snapshotRows.get(String(row.id)) === row) snapshotRows.delete(String(row.id));
                        snapshotPages.delete(oldest);
                    }
                }
                const pageOffset = offset % 25;
                const take = Math.min(params.endRow - params.startRow - items.length, snapshotTotal - offset);
                if (take <= 0) break;
                const rows = rowsInPage.slice(pageOffset, pageOffset + take);
                items.push(...rows);
                if (!rows.length) break;
                offset += rows.length;
            }
            if (requestRevision !== revision || requestLayout !== layoutRevision) { params.failCallback(); return; }
            const total = snapshotTotal + appended.length;
            params.successCallback(items, total);
            flushedAppends = appended.length;
            root.querySelector('[data-runtime-loaded-copy]').textContent = `${total.toLocaleString()} matching instances`;
        } catch (error) { params.failCallback(); if (requestRevision === revision) root.querySelector('[data-runtime-loaded-copy]').textContent = 'Unable to load instances'; }
    } };
    api = agGrid.createGrid(grid, { theme: theme(), rowModelType: 'infinite', datasource, cacheBlockSize: 25, maxBlocksInCache: 20,
        maxConcurrentDatasourceRequests: 1, blockLoadDebounceMillis: 100, columnDefs, defaultColDef: { sortable: false, resizable: true, minWidth: 80 },
        rowHeight: 45, headerHeight: 34, getRowId: p => String(p.data.id), suppressMultiSort: true,
        onModelUpdated: e => updateSelection(e.api),
        onCellClicked: e => { if (!e.data) return; if (e.event?.target?.closest('.copy-id')) { navigator.clipboard?.writeText(String(e.value || '')); return; } if (!e.event?.target?.closest('input, button, a')) renderInspector(e.data); },
        onCellKeyDown: e => { if (e.data && e.event?.key === 'Enter' && !e.event.target?.closest('input, button, a')) { e.event.preventDefault(); renderInspector(e.data); } } });
    function refresh() { revision++; layoutRevision++; snapshotEnd = new Date(); snapshotTotal = null; flushedAppends = 0; appended.length = 0; appendedById.clear(); snapshotPages.clear(); snapshotRows.clear(); api.setGridOption('datasource', datasource); }
    function renderCounts() {
        Object.entries(counts).forEach(([key, n]) => { root.querySelector(`[data-monitor-count="${key}"]`).textContent = n.toLocaleString();
            root.querySelector(`[data-monitor-share="${key}"]`).textContent = key === 'total' ? `${hours}h` : counts.total ? `${Math.round(n * 100 / counts.total)}%` : '0%'; });
    }
    async function refreshSparks() {
        sparkRequest?.abort();
        if (search.value.trim()) { window.KrackendRuntimeSparklines?.clear(root, 'Changes unavailable for search'); return; }
        window.KrackendRuntimeSparklines?.clear(root);
        const controller = new AbortController(); sparkRequest = controller;
        const url = new URL(root.dataset.activitySource, location.href); url.searchParams.set('hours', String(hours)); url.searchParams.set('cohortOnly', 'true');
        try {
            const response = await fetch(url, { signal: controller.signal });
            if (response.ok) {
                const result = await response.json();
                if (sparkRequest === controller && !search.value.trim()) window.KrackendRuntimeSparklines?.renderResult(root, result);
            }
            else if (sparkRequest === controller) window.KrackendRuntimeSparklines?.clear(root, 'Changes unavailable');
        } catch (error) { if (error.name !== 'AbortError' && sparkRequest === controller) window.KrackendRuntimeSparklines?.clear(root, 'Changes unavailable'); }
        finally { if (sparkRequest === controller) sparkRequest = null; }
    }
    async function refreshCounts() {
        countsRequest?.abort(); const controller = new AbortController(); countsRequest = controller;
        const url = new URL(root.dataset.countsSource, location.href); url.searchParams.set('recentHours', String(hours));
        if (search.value.trim()) url.searchParams.set('search', search.value.trim());
        try {
            const response = await fetch(url, { signal: controller.signal }); if (!response.ok) throw new Error('Counts unavailable');
            const data = await response.json(), count = key => Number(data[key] || 0);
            counts.running = count('Created') + count('Running') + count('Waiting') + count('Compensating');
            counts.idle = count('Idle'); counts.completed = count('Completed') + count('Compensated');
            counts.deadLettered = count('DeadLettered'); counts.failed = count('Failed') + count('Stopped') + count('Aborted') + count('CompletedWithErrors');
            counts.total = Object.entries(counts).filter(([k]) => k !== 'total').reduce((sum, [, n]) => sum + n, 0);
            renderCounts();
        } catch (error) { if (error.name !== 'AbortError') root.querySelectorAll('[data-monitor-count]').forEach(e => { e.textContent = '—'; }); }
        finally { if (countsRequest === controller) { countsRequest = null; root.querySelectorAll('[data-monitor-metric-skeleton]').forEach(e => e.hidden = true); root.querySelectorAll('[data-monitor-metric-data]').forEach(e => e.hidden = false); } }
    }
    search.addEventListener('input', () => { clearTimeout(searchTimer); searchTimer = setTimeout(() => { inspector.close(); refresh(); void refreshCounts(); void refreshSparks(); }, 300); });
    root.querySelectorAll('[data-runtime-scope-value]').forEach(button => button.addEventListener('click', () => {
        hours = Number(button.dataset.runtimeScopeValue); root.querySelectorAll('[data-runtime-scope-value]').forEach(b => b.setAttribute('aria-pressed', String(b === button))); inspector.close(); refresh(); void refreshCounts(); void refreshSparks();
    }));
    root.querySelectorAll('[data-status-group]').forEach(button => button.addEventListener('click', () => {
        statusGroup = button.dataset.statusGroup; root.querySelectorAll('[data-status-group]').forEach(b => { const active = b.dataset.statusGroup === statusGroup; b.setAttribute('aria-pressed', String(active)); b.classList.toggle('is-active', active); }); inspector.close(); refresh();
    }));
    root.querySelector('[data-runtime-refresh]').addEventListener('click', () => { inspector.close(); refresh(); void refreshCounts(); void refreshSparks(); });
    root.querySelector('[data-density]').addEventListener('click', () => { compact = !compact; api.setGridOption('rowHeight', compact ? 35 : 45); api.resetRowHeights(); });
    root.querySelector('[data-fit-columns]').addEventListener('click', () => api.sizeColumnsToFit());
    root.querySelector('[data-inspect-selected]').addEventListener('click', () => { const first = selectedRows.values().next().value; if (first) renderInspector(first); });
    root.querySelector('[data-copy-selected]').addEventListener('click', async event => {
        const button = event.currentTarget, ids = [...selectedRows.values()].map(row => row.sagaId || row.id).join('\n');
        if (!ids) return;
        try { await navigator.clipboard.writeText(ids); button.textContent = 'Copied'; }
        catch { button.textContent = 'Clipboard unavailable'; }
        setTimeout(() => { button.innerHTML = '<i class="bi bi-copy"></i> Copy IDs'; }, 2000);
    });
    root.querySelector('[data-clear-selection]').addEventListener('click', () => { selectedRows.clear(); api.refreshCells({ columns: ['selection'], force: true }); updateSelection(api); });
    const scheduleCounts = () => { if (!countsTimer) countsTimer = setTimeout(() => { countsTimer = 0; void refreshCounts(); }, 15000); };
    function appendRow(row) {
        const id = String(row.id);
        if (appendedById.has(id)) return;
        appendedById.set(id, appended.length); appended.push(row);
    }
    function flushAppends() {
        if (snapshotTotal === null || flushedAppends === appended.length) return;
        const added = appended.length - flushedAppends;
        const total = snapshotTotal + appended.length;
        const viewport = grid.querySelector('.ag-body-vertical-scroll-viewport');
        const scrollTop = viewport?.scrollTop || 0;
        layoutRevision++;
        api.setRowCount(total, true);
        if (viewport && scrollTop > 0) viewport.scrollTop = scrollTop + added * (compact ? 35 : 45);
        flushedAppends = appended.length;
        api.refreshInfiniteCache();
        root.querySelector('[data-runtime-loaded-copy]').textContent = `${total.toLocaleString()} matching instances`;
    }
    function appendInstance(event, id) {
        if (appendedById.has(id)) return;
        const started = date(value(event, 'startedOnUtc'));
        if (!started || started < snapshotEnd) return;
        const status = publicStatus(value(event, 'instanceStatus'));
        const group = String(keyFor(status) || '').toLowerCase();
        if (statusGroup && !(statusGroup === 'attention' ? group === 'failed' || group === 'deadlettered' : group === statusGroup)) return;
        const term = search.value.trim().toLowerCase();
        if (term && !['orchestrationDefinitionKey', 'correlationId', 'sagaId', 'executionKey', 'orchestrationInstanceId']
            .some(key => String(value(event, key) || '').toLowerCase().includes(term))) return;
        const row = { id, orchestrationDefinitionKey: value(event, 'orchestrationDefinitionKey'), orchestrationVersion: value(event, 'orchestrationVersion'),
            sagaId: value(event, 'sagaId'), correlationId: value(event, 'correlationId'), executionKey: value(event, 'executionKey'), status,
            startedOnUtc: value(event, 'startedOnUtc'), lastUpdatedOnUtc: value(event, 'lastUpdatedOnUtc') || value(event, 'occurredOnUtc'),
            completedTaskCount: value(event, 'completedTaskCount'), totalTaskCount: value(event, 'totalTaskCount'),
            currentStageKey: value(event, 'currentStageKey'), currentTaskKey: value(event, 'currentTaskKey'),
            lastExecutedStageKey: value(event, 'lastExecutedStageKey'), lastExecutedTaskKey: value(event, 'lastExecutedTaskKey') };
        appendRow(row);
    }
    async function catchUp() {
        const requestRevision = revision, until = new Date();
        const url = listUrl(0);
        url.searchParams.set('startedFrom', snapshotEnd.toISOString());
        url.searchParams.set('startedBefore', until.toISOString());
        url.searchParams.set('pageSize', '100');
        url.searchParams.set('descending', 'false');
        if (snapshotEnd >= until) return;
        try {
            for (let page = 1; requestRevision === revision; page++) {
                url.searchParams.set('page', String(page));
                const response = await fetch(url, { headers: { Accept: 'application/json' } });
                if (!response.ok) return;
                const result = await response.json();
                if (requestRevision !== revision) return;
                for (const row of result.items) {
                    const id = String(row.id), index = appendedById.get(id);
                    if (index === undefined) appendRow(row);
                    else if ((date(row.lastUpdatedOnUtc)?.getTime() || 0) > (date(appended[index].lastUpdatedOnUtc)?.getTime() || 0)) {
                        appended[index] = row;
                        api.getRowNode(id)?.setData(row);
                    }
                }
                flushAppends();
                if (!result.hasMore) break;
            }
        } catch { /* Keep the current rows; the next reconnect can retry. */ }
    }
    function updateCountsFromEvent(event, current, transition) {
        if (!/^Instance/i.test(transition)) return;
        const started = date(value(event, 'startedOnUtc') || current?.startedOnUtc);
        if (started && started.getTime() < Date.now() - hours * 3600000) return;
        // Reconcile committed counts in a bounded batch; a transition alone
        // cannot establish whether the existing query already included it.
        scheduleCounts();
    }
    function applyEvent(event) {
        const eventId = String(value(event, 'id') || ''); if (!eventId || seen.has(eventId)) return;
        seen.add(eventId); if (seen.size > 4096) seen.delete(seen.values().next().value);
        const id = String(value(event, 'orchestrationInstanceId') || ''); if (!id) return;
        const node = api.getRowNode(id), appendedIndex = appendedById.get(id);
        const current = node?.data || (appendedIndex !== undefined ? appended[appendedIndex] : null) || snapshotRows.get(id) || (selected?.id === id ? selected : null);
        const transition = String(value(event, 'transitionType') || '');
        const incoming = date(value(event, 'lastUpdatedOnUtc') || value(event, 'occurredOnUtc'))?.getTime() || 0;
        const previous = date(current?.lastUpdatedOnUtc)?.getTime() || 0;
        if (incoming < previous) return;
        if (/^Instance/i.test(transition)) {
            updateCountsFromEvent(event, current, transition);
            const started = date(value(event, 'startedOnUtc') || current?.startedOnUtc);
            if ((!started || started.getTime() >= Date.now() - hours * 3600000) && !sparkTimer)
                sparkTimer = setTimeout(() => { sparkTimer = 0; void refreshSparks(); }, 15000);
            if (!current && /^(InstancePromoted|InstanceStarted)$/i.test(transition)) appendInstance(event, id);
        }
        if (!current) return;
        const next = { ...current, status: publicStatus(value(event, 'instanceStatus') || current.status),
            currentStageKey: value(event, 'currentStageKey') ?? current.currentStageKey, currentTaskKey: value(event, 'currentTaskKey') ?? current.currentTaskKey,
            lastExecutedStageKey: value(event, 'lastExecutedStageKey') ?? current.lastExecutedStageKey, lastExecutedTaskKey: value(event, 'lastExecutedTaskKey') ?? current.lastExecutedTaskKey,
            lastUpdatedOnUtc: value(event, 'lastUpdatedOnUtc') || value(event, 'occurredOnUtc') || current.lastUpdatedOnUtc,
            completedTaskCount: value(event, 'progressKnown') === false ? null : value(event, 'completedTaskCount') ?? current.completedTaskCount,
            totalTaskCount: value(event, 'progressKnown') === false ? null : value(event, 'totalTaskCount') ?? current.totalTaskCount };
        if (appendedIndex !== undefined) appended[appendedIndex] = next;
        const cached = snapshotRows.get(id); if (cached) Object.assign(cached, next);
        node?.setData(next); if (selectedRows.has(id)) selectedRows.set(id, next); if (selected?.id === id) {
            selected = next; inspector.update(next);
            if (/^Stage/i.test(transition)) {
                pendingInspectorId = id;
                if (!inspectorTimer) inspectorTimer = setTimeout(() => { inspectorTimer = 0; if (inspector.isCurrent(pendingInspectorId)) void inspector.refreshStages(); }, 1500);
            }
        }
    }
    window.addEventListener('krackend:instances-changed', event => {
        if (event.detail?.resync) { void catchUp(); void refreshCounts(); return; }
        const item = event.detail?.event; if (!item) return;
        const id = String(value(item, 'orchestrationInstanceId') || ''); if (!id) return;
        pending.push(item);
        if (!liveTimer) liveTimer = setTimeout(() => { liveTimer = 0;
            const batch = pending.splice(0); batch.sort((a, b) => (date(value(a, 'startedOnUtc'))?.getTime() || 0) - (date(value(b, 'startedOnUtc'))?.getTime() || 0));
            for (const item of batch) applyEvent(item);
            flushAppends();
        }, 120);
    });
    const themeObserver = new MutationObserver(() => api.setGridOption('theme', theme()));
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['class', 'style', 'data-theme', 'data-bs-theme', 'data-krackend-theme'] });
    void refreshCounts(); void refreshSparks();
    window.addEventListener('pagehide', event => { if (event.persisted) return; clearTimeout(searchTimer); clearTimeout(liveTimer); clearTimeout(countsTimer); clearTimeout(sparkTimer); clearTimeout(inspectorTimer); inspector.close(); clearTimeout(inspectorFitTimer); countsRequest?.abort(); sparkRequest?.abort(); themeObserver.disconnect(); api.destroy(); });
})();
