(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;

    const grid = root.querySelector('[data-z-data-grid]');
    const search = grid.querySelector('[data-z-grid-search]');
    const scopeButtons = [...root.querySelectorAll('[data-runtime-scope-value]')];
    const controls = root.querySelector('.od-runtime-view-controls');
    const toolbar = grid.querySelector('.z-toolbar');
    const refreshControl = root.querySelector('[data-runtime-refresh]');
    const searchControl = search.closest('.z-grid-search, .z-data-grid-search') || search.parentElement;
    const records = new Map();
    const pendingEvents = new Map();
    const seenEventIds = new Set();
    const metrics = { total: 0, running: 0, idle: 0, completed: 0, deadLettered: 0, failed: 0 };
    const maxRetainedRows = 500;
    const maxPendingEvents = 1024;
    const maxSeenEvents = 4096;
    let catalogTotal = 0;
    let recentHours = 24;
    let refreshing = false;
    let refreshPending = false;
    let preservePending = false;
    let scrollAnchor = null;
    let countsRequest;
    let liveEventTimer = 0;

    toolbar.append(controls);
    if (searchControl && refreshControl) searchControl.after(refreshControl);

    const bodyObserver = new MutationObserver(() => decorate());

    function observeGridBody() {
        bodyObserver.disconnect();
        const body = grid.querySelector('[data-z-grid-body]');
        if (body) bodyObserver.observe(body, { childList: true, subtree: true });
    }

    function mountGrid() {
        window.KrackendCatalogData?.registerGridSource?.(grid);
        window.ZoorceDataGrid.mount(grid);
        observeGridBody();
    }

    const eventValue = (event, name) => event?.[name] ?? event?.[`${name.charAt(0).toUpperCase()}${name.slice(1)}`];
    const toTimestamp = value => {
        const timestamp = new Date(value).getTime();
        return Number.isNaN(timestamp) ? 0 : timestamp;
    };
    const toUtcText = value => {
        const timestamp = new Date(value).getTime();
        return Number.isNaN(timestamp) ? new Date().toISOString() : new Date(timestamp).toISOString();
    };
    const toPublicStatus = value => {
        const status = String(value || 'Running');
        if (['Created', 'Running', 'Waiting', 'WaitingResponse', 'Retrying', 'Compensating'].includes(status)) return 'Running';
        if (status === 'Idle') return 'Idle';
        if (['Completed', 'Compensated'].includes(status)) return 'Completed';
        if (status === 'DeadLettered') return 'DeadLettered';
        if (['Failed', 'Stopped', 'Aborted', 'CompletedWithErrors', 'TimedOut', 'Cancelled'].includes(status)) return 'Failed';
        return status;
    };
    const statusClass = status => ({
        Running: 'od-status-running', Idle: 'od-status-warning', Completed: 'od-status-active',
        DeadLettered: 'od-status-warning', Failed: 'od-status-danger'
    }[status] || 'od-status-inactive');
    const metricKey = status => ({
        Running: 'running', Idle: 'idle', Completed: 'completed', DeadLettered: 'deadLettered', Failed: 'failed'
    }[toPublicStatus(status)] || null);
    const formatUtc = value => {
        const text = typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value;
        const date = new Date(text);
        return Number.isNaN(date.getTime()) ? '' : date.toISOString().replace('T', ' ').substring(0, 19);
    };
    const isInstanceStatusTransition = event => /^Instance/i.test(String(eventValue(event, 'transitionType') || ''));
    const isInstanceStart = event => /^(InstancePromoted|InstanceStarted)$/i.test(String(eventValue(event, 'transitionType') || ''));
    const orchestrationLabel = (definitionKey, version) => definitionKey ? (version ? `${definitionKey} v${version}` : definitionKey) : '';

    const updateLoadedCopy = result => {
        const copy = root.querySelector('[data-runtime-loaded-copy]');
        if (!copy) return;
        if (result) catalogTotal = Number(result.totalCount ?? result.totalRows ?? 0);
        const loaded = records.size;
        copy.textContent = catalogTotal > loaded
            ? `${loaded.toLocaleString()} of ${catalogTotal.toLocaleString()} matching rows loaded`
            : catalogTotal > 0 ? `${loaded.toLocaleString()} rows loaded` : 'No matching rows';
    };

    const setMetricLoading = loading => {
        root.querySelectorAll('[data-monitor-metric-skeleton]').forEach(element => { element.hidden = !loading; });
        root.querySelectorAll('[data-monitor-metric-data]').forEach(element => { element.hidden = loading; });
    };
    const renderMetrics = () => {
        Object.entries(metrics).forEach(([key, count]) => {
            const countElement = root.querySelector(`[data-monitor-count="${key}"]`);
            const shareElement = root.querySelector(`[data-monitor-share="${key}"]`);
            if (countElement) countElement.textContent = Number(count).toLocaleString();
            if (shareElement) {
                shareElement.textContent = key === 'total'
                    ? 'selected time range'
                    : metrics.total ? `${Math.round(count * 100 / metrics.total)}% of matching` : 'no instances';
            }
        });
    };
    const scopedUrl = source => {
        const url = new URL(source, location.href);
        url.searchParams.delete('activeOnly');
        url.searchParams.set('recentHours', String(recentHours));
        return url;
    };
    const refreshCounts = async ({ showLoading = false } = {}) => {
        if (showLoading) setMetricLoading(true);
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
            metrics.running = value('Created')
                + value('Running')
                + value('Waiting')
                + value('WaitingResponse')
                + value('Retrying')
                + value('Compensating');
            metrics.idle = value('Idle');
            metrics.completed = value('Completed') + value('Compensated');
            metrics.deadLettered = value('DeadLettered');
            metrics.failed = value('Failed') + value('Stopped') + value('Aborted') + value('CompletedWithErrors');
            metrics.total = metrics.running + metrics.idle + metrics.completed + metrics.deadLettered + metrics.failed;
            renderMetrics();
        } catch (error) {
            if (error.name !== 'AbortError') {
                root.querySelectorAll('[data-monitor-count]').forEach(element => { element.textContent = '—'; });
                root.querySelectorAll('[data-monitor-share]').forEach(element => { element.textContent = 'Counts unavailable'; });
            }
        } finally {
            if (countsRequest === controller) {
                countsRequest = null;
                setMetricLoading(false);
            }
        }
    };

    const captureScrollAnchor = () => {
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        if (!viewport || viewport.scrollTop <= 0) return null;
        const viewportTop = viewport.getBoundingClientRect().top;
        const row = [...grid.querySelectorAll('tr[data-z-grid-key]')].find(item => item.getBoundingClientRect().bottom > viewportTop);
        return row ? { key: row.dataset.zGridKey, offset: row.getBoundingClientRect().top - viewportTop, scrollTop: viewport.scrollTop }
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
    const setRefreshBusy = busy => {
        const button = refreshControl?.querySelector('button');
        if (!button) return;
        button.disabled = busy;
        button.setAttribute('aria-busy', String(busy));
        button.dataset.zProgressState = busy ? 'in-progress' : 'idle';
        button.dataset.zProgressIndeterminate = String(busy);
        button.style.setProperty('--z-button-progress', busy ? '100%' : '0%');
        refreshControl.classList.toggle('is-loading', busy);
    };
    const refresh = ({ preserveScroll = false, showMetricLoading = false } = {}) => {
        if (refreshing || grid.getAttribute('aria-busy') === 'true') {
            refreshPending = true;
            preservePending ||= preserveScroll;
            return;
        }
        if (preserveScroll) scrollAnchor = captureScrollAnchor();
        refreshing = true;
        setRefreshBusy(true);
        if (showMetricLoading) void refreshCounts({ showLoading: true });
        grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
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
            button.innerHTML = '<i class="bi bi-copy" aria-hidden="true"></i>';
            container.append(button);
        }
        return container;
    };
    const renderProgress = item => {
        const total = Number(item.totalTaskCount || 0);
        const complete = Math.min(total, Math.max(0, Number(item.completedTaskCount || 0)));
        const container = document.createElement('span');
        container.className = 'od-instance-progress';
        container.dataset.progressState = `${complete}/${total}`;
        if (!total) {
            container.textContent = '—';
            return container;
        }
        const meter = document.createElement('span');
        meter.className = 'od-instance-progress-meter';
        const fill = document.createElement('span');
        fill.style.width = `${Math.round(complete * 100 / total)}%`;
        meter.append(fill);
        const label = document.createElement('small');
        label.textContent = `${complete}/${total}`;
        container.title = `${complete} of ${total} enabled tasks completed`;
        container.append(meter, label);
        return container;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        if (!item) return;
        for (const [field, label] of [['correlationId', 'correlation ID'], ['sagaId', 'saga ID']]) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (cell && cell.querySelector('.od-copy-value')?.textContent !== (item[field] || '-')) cell.replaceChildren(copyable(item[field], label));
        }
        for (const field of ['startedOnUtc', 'lastUpdatedOnUtc']) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            const value = formatUtc(item[field]);
            if (cell && cell.textContent !== value) cell.textContent = value;
        }
        const status = row.querySelector('[data-z-grid-field="status"]');
        if (status && (status.querySelector('.od-status')?.textContent !== item.status || status.querySelector('.od-status')?.className !== `od-status ${item.statusClass || ''}`)) {
            const badge = document.createElement('span');
            badge.className = `od-status ${item.statusClass || ''}`;
            badge.textContent = item.status;
            status.replaceChildren(badge);
        }
        const progress = row.querySelector('[data-z-grid-field="progress"]');
        if (progress) {
            const total = Number(item.totalTaskCount || 0);
            const complete = Math.min(total, Math.max(0, Number(item.completedTaskCount || 0)));
            const state = `${complete}/${total}`;
            if (progress.firstElementChild?.dataset.progressState !== state) progress.replaceChildren(renderProgress(item));
        }
        row.classList.add('od-instance-row');
        row.tabIndex = 0;
        row.setAttribute('aria-label', `Open saga ${item.sagaId || item.id}`);
    });

    const copyText = async value => {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(value);
            return;
        }
        const input = document.createElement('textarea');
        input.value = value;
        input.setAttribute('readonly', '');
        input.style.cssText = 'position:fixed;opacity:0;pointer-events:none';
        document.body.append(input);
        input.select();
        const succeeded = document.execCommand('copy');
        input.remove();
        if (!succeeded) throw new Error('Clipboard unavailable');
    };
    grid.addEventListener('click', async event => {
        const copyButton = event.target.closest('[data-copy-value]');
        if (copyButton && grid.contains(copyButton)) {
            const value = copyButton.dataset.copyValue;
            if (!value) return;
            try {
                await copyText(value);
                copyButton.classList.add('is-copied');
                copyButton.title = 'Copied';
                window.setTimeout(() => { copyButton.classList.remove('is-copied'); copyButton.title = copyButton.getAttribute('aria-label'); }, 1400);
            } catch {
                copyButton.title = 'Unable to copy';
            }
            return;
        }
        if (event.target.closest('a,button,input,select,textarea')) return;
        const row = event.target.closest('tr[data-z-grid-key]');
        const item = row && records.get(row.dataset.zGridKey);
        if (item) location.assign(root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(item.id)));
    });
    grid.addEventListener('keydown', event => {
        if (event.key !== 'Enter' && event.key !== ' ') return;
        const row = event.target.closest('tr[data-z-grid-key]');
        const item = row && records.get(row.dataset.zGridKey);
        if (!item) return;
        event.preventDefault();
        location.assign(root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(item.id)));
    });

    const trimRecordState = () => {
        if (records.size <= maxRetainedRows) return;
        const visible = new Set([...grid.querySelectorAll('tr[data-z-grid-key]')].map(row => row.dataset.zGridKey));
        for (const key of records.keys()) {
            if (records.size <= maxRetainedRows) break;
            if (!visible.has(key)) records.delete(key);
        }
        while (records.size > maxRetainedRows) records.delete(records.keys().next().value);
    };
    observeGridBody();
    grid.addEventListener('zoorce:grid:error', () => {
        refreshing = false;
        setRefreshBusy(false);
        scrollAnchor = null;
    });
    grid.addEventListener('zoorce:grid:loaded', event => {
        refreshing = false;
        setRefreshBusy(false);
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        trimRecordState();
        decorate();
        updateLoadedCopy(event.detail.result);
        if (!event.detail.append) void refreshCounts({ showLoading: false });
        window.requestAnimationFrame(restoreScrollAnchor);
        if (refreshPending) {
            const preserveScroll = preservePending;
            refreshPending = false;
            preservePending = false;
            refresh({ preserveScroll });
        }
    });

    const matchesScope = item => {
        const term = search.value.trim().toLocaleLowerCase();
        if (!term) return true;
        return [item.id, item.orchestrationDefinitionKey, item.correlationId, item.sagaId, item.executionKey]
            .some(value => String(value || '').toLocaleLowerCase().includes(term));
    };
    const toLiveRow = (event, existing) => {
        const status = toPublicStatus(eventValue(event, 'instanceStatus') || existing?.status);
        const definitionKey = eventValue(event, 'orchestrationDefinitionKey') || existing?.orchestrationDefinitionKey || '';
        const version = eventValue(event, 'orchestrationVersion') || existing?.orchestrationVersion || '';
        return {
            ...existing,
            id: String(eventValue(event, 'orchestrationInstanceId') || existing?.id || ''),
            orchestrationDefinitionKey: definitionKey,
            orchestrationVersion: version,
            orchestrationLabel: orchestrationLabel(definitionKey, version),
            correlationId: eventValue(event, 'correlationId') || existing?.correlationId || '',
            sagaId: eventValue(event, 'sagaId') || existing?.sagaId || '',
            executionKey: eventValue(event, 'executionKey') || existing?.executionKey || '',
            status,
            statusClass: statusClass(status),
            currentStageKey: eventValue(event, 'stageKey') || existing?.currentStageKey || '',
            currentTaskKey: eventValue(event, 'taskKey') || existing?.currentTaskKey || '',
            startedOnUtc: toUtcText(eventValue(event, 'startedOnUtc') || existing?.startedOnUtc || eventValue(event, 'occurredOnUtc')),
            lastUpdatedOnUtc: toUtcText(eventValue(event, 'lastUpdatedOnUtc') || eventValue(event, 'occurredOnUtc')),
            reactiveRevision: String(eventValue(event, 'revision') || existing?.reactiveRevision || ''),
            // The reactive payload carries the server-side projection. Keep the
            // row's existing values only for older publishers that do not yet
            // include progress, never calculate task state in the browser.
            completedTaskCount: Number(eventValue(event, 'completedTaskCount') ?? existing?.completedTaskCount ?? 0),
            totalTaskCount: Number(eventValue(event, 'totalTaskCount') ?? existing?.totalTaskCount ?? 0)
        };
    };
    const patchVisibleRow = item => {
        const row = grid.querySelector(`tr[data-z-grid-key="${CSS.escape(item.id)}"]`);
        if (!row) return false;
        for (const field of ['orchestrationLabel', 'currentStageKey', 'currentTaskKey']) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (cell) cell.textContent = item[field] || '';
        }
        decorate();
        return true;
    };
    const adjustMetrics = (event, current, row) => {
        if (!isInstanceStatusTransition(event)) return;
        const after = metricKey(row.status);
        const before = metricKey(current?.status || eventValue(event, 'fromStatus'));
        if (!current && isInstanceStart(event)) {
            metrics.total += 1;
            if (after) metrics[after] += 1;
            renderMetrics();
            return;
        }
        if (!before || !after || before === after) return;
        metrics[before] = Math.max(0, metrics[before] - 1);
        metrics[after] += 1;
        renderMetrics();
    };
    const rememberEvent = event => {
        const id = String(eventValue(event, 'id') || '');
        if (!id || seenEventIds.has(id)) return false;
        seenEventIds.add(id);
        if (seenEventIds.size > maxSeenEvents) seenEventIds.delete(seenEventIds.values().next().value);
        return true;
    };
    const isOlderEvent = (event, current) => {
        if (!current) return false;
        const incomingTimestamp = toTimestamp(eventValue(event, 'lastUpdatedOnUtc') || eventValue(event, 'occurredOnUtc'));
        const currentTimestamp = toTimestamp(current.lastUpdatedOnUtc);
        if (incomingTimestamp < currentTimestamp) return true;
        if (incomingTimestamp > currentTimestamp) return false;
        const incomingRevision = String(eventValue(event, 'revision') || '');
        const currentRevision = String(current.reactiveRevision || '');
        if (!incomingRevision || !currentRevision) return false;
        const incomingNumber = Number(incomingRevision);
        const currentNumber = Number(currentRevision);
        if (Number.isFinite(incomingNumber) && Number.isFinite(currentNumber)) return incomingNumber <= currentNumber;
        return incomingRevision.localeCompare(currentRevision) <= 0;
    };
    const applyLiveEvent = event => {
        if (!rememberEvent(event)) return;
        const id = String(eventValue(event, 'orchestrationInstanceId') || '');
        if (!id) return;
        const current = records.get(id);
        if (isOlderEvent(event, current)) return;
        const row = toLiveRow(event, current);
        adjustMetrics(event, current, row);
        if (!matchesScope(row)) return;
        if (current) {
            records.set(id, row);
            patchVisibleRow(row);
            return;
        }
        // Existing, unloaded rows are never injected into the reader's current
        // viewport. Only a genuinely new saga gets its descending-start row.
        if (!isInstanceStart(event)) return;
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        if ((viewport?.scrollTop ?? 0) > 8) return;
        records.set(id, row);
        catalogTotal += 1;
        grid.dispatchEvent(new CustomEvent('zoorce:grid:insert', { detail: { item: row } }));
        trimRecordState();
        updateLoadedCopy();
    };
    const flushLiveEvents = () => {
        liveEventTimer = 0;
        const events = [...pendingEvents.values()];
        pendingEvents.clear();
        events.forEach(applyLiveEvent);
    };
    const queueLiveEvent = event => {
        const id = String(eventValue(event, 'orchestrationInstanceId') || '');
        if (!id) return;
        if (!pendingEvents.has(id) && pendingEvents.size >= maxPendingEvents) {
            // A single fallback reconciliation is safer than retaining an
            // unbounded in-browser state during an exceptional burst.
            refresh({ preserveScroll: true });
            return;
        }
        const current = pendingEvents.get(id);
        if (!current || !isOlderEvent(event, toLiveRow(current, null)))
            pendingEvents.set(id, event);
        if (!liveEventTimer) liveEventTimer = window.setTimeout(flushLiveEvents, 120);
    };
    window.addEventListener('krackend:instances-changed', event => {
        const detail = event.detail || {};
        const transition = String(eventValue(detail.event, 'transitionType') || '');
        const technical = new Set(['TaskDispatchSuppressed', 'TaskDispatchPhysicalCancellationFinalized', 'TaskDispatchPhysicalCancellationDeferred']);
        if (detail.event && !technical.has(transition) && eventValue(detail.event, 'instanceStatus')) queueLiveEvent(detail.event);
        // A server resync only occurs following a reconnect or a bounded queue
        // overflow; routine transitions must never reload the catalog.
        if (detail.resync) refresh({ preserveScroll: true });
    });
    const syncScopeButtons = () => scopeButtons.forEach(button =>
        button.setAttribute('aria-pressed', String(Number(button.dataset.runtimeScopeValue) === recentHours)));
    const changeScope = hours => {
        if (!Number.isFinite(hours) || hours === recentHours) return;
        recentHours = hours;
        syncScopeButtons();
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent);
        config.binding.source = scopedUrl(root.dataset.source).toString();
        window.ZoorceDataGrid.dispose(grid);
        configElement.textContent = JSON.stringify(config);
        window.KrackendCatalogData?.registerRemoteSource?.(config.binding.source);
        records.clear();
        catalogTotal = 0;
        refreshing = false;
        refreshPending = false;
        mountGrid();
        void refreshCounts({ showLoading: true });
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    };
    scopeButtons.forEach(button => button.addEventListener('click', () => changeScope(Number(button.dataset.runtimeScopeValue))));
    refreshControl?.addEventListener('click', () => refresh({ showMetricLoading: true }));
    syncScopeButtons();

    // Zoorce mounts grids as its script is evaluated. Dispose that opportunistic
    // mount and recreate it only after the catalog listeners above are attached;
    // otherwise a fast initial request can finish before this page captures the
    // loaded event and the grid remains visually empty.
    window.ZoorceDataGrid.dispose(grid);
    mountGrid();
    void refreshCounts({ showLoading: true });
})();
