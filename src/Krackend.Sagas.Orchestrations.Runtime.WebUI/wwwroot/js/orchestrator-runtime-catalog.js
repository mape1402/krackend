(() => {
    const root = document.querySelector('[data-runtime-catalog]');
    if (!root) return;
    const grid = root.querySelector('[data-z-data-grid]');
    const search = grid.querySelector('[data-z-grid-search]');
    const scopeButtons = [...root.querySelectorAll('[data-runtime-scope-value]')];
    const controls = root.querySelector('.od-runtime-view-controls');
    const records = new Map();
    const pendingEvents = new Map();
    let countsTimer, liveEventTimer, countsRequest;
    let refreshing = false, refreshPending = false, preservePending = false, scrollAnchor = null;
    let countsLoadingForNextLoad = true, catalogLoaded = false, catalogTotal = 0;
    let recentHours = 24;
    grid.querySelector('.z-toolbar').append(controls);
    const bodyObserver = new MutationObserver(() => decorate());

    function observeGridBody() {
        bodyObserver.disconnect();
        const body = grid.querySelector('[data-z-grid-body]');
        if (body) bodyObserver.observe(body, { childList: true, subtree: true });
    }

    function mountGrid() {
        window.ZoorceDataGrid.mount(grid);
        observeGridBody();
    }

    const updateLoadedCopy = result => {
        const copy = root.querySelector('[data-runtime-loaded-copy]');
        if (!copy) return;
        const loaded = records.size;
        if (result) catalogTotal = Number(result.totalCount ?? result.totalRows ?? 0);
        const total = catalogTotal;
        const label = total > loaded
            ? `${loaded.toLocaleString()} of ${total.toLocaleString()} matching rows loaded`
            : total > 0
                ? `${loaded.toLocaleString()} rows loaded`
                : 'No matching rows';
        copy.textContent = label;
    };

    const setMetricLoading = loading => {
        root.querySelectorAll('[data-monitor-metric-skeleton]').forEach(element => { element.hidden = !loading; });
        root.querySelectorAll('[data-monitor-metric-data]').forEach(element => { element.hidden = loading; });
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
            const totals = { running: value('Created') + value('Running') + value('Compensating'), idle: value('Idle') + value('Waiting'),
                completed: value('Completed'), deadLettered: value('DeadLettered'),
                failed: value('Failed') + value('Aborted') + value('CompletedWithErrors') };
            const total = Object.values(totals).reduce((sum, count) => sum + count, 0);
            Object.entries(totals).forEach(([key, count]) => {
                root.querySelector(`[data-monitor-count="${key}"]`).textContent = count.toLocaleString();
                const share = root.querySelector(`[data-monitor-share="${key}"]`);
                if (share) share.textContent = total ? `${Math.round(count * 100 / total)}% of matching` : 'no instances';
            });
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
    const scheduleCountsRefresh = () => {
        window.clearTimeout(countsTimer);
        countsTimer = window.setTimeout(() => void refreshCounts(), 450);
    };
    const captureScrollAnchor = () => {
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        if (!viewport || viewport.scrollTop <= 0) return null;
        const viewportTop = viewport.getBoundingClientRect().top;
        const row = [...grid.querySelectorAll('tr[data-z-grid-key]')]
            .find(item => item.getBoundingClientRect().bottom > viewportTop);
        return row
            ? { key: row.dataset.zGridKey, offset: row.getBoundingClientRect().top - viewportTop, scrollTop: viewport.scrollTop }
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
    const refresh = ({ preserveScroll = false, showMetricLoading = false } = {}) => {
        if (refreshing || grid.getAttribute('aria-busy') === 'true') {
            refreshPending = true;
            preservePending ||= preserveScroll;
            return;
        }
        if (preserveScroll) scrollAnchor = captureScrollAnchor();
        countsLoadingForNextLoad = showMetricLoading;
        refreshing = true;
        setRefreshBusy(true);
        grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
    };
    const setRefreshBusy = busy => {
        const control = root.querySelector('[data-runtime-refresh]');
        const button = control?.querySelector('button');
        if (!button) return;
        button.disabled = busy;
        button.setAttribute('aria-busy', String(busy));
        control.classList.toggle('is-loading', busy);
    };
    const copyable = (value, label) => {
        const container = document.createElement('span'); container.className = 'od-id-copy';
        const text = document.createElement('span'); text.className = 'od-copy-value'; text.textContent = value || '-'; container.append(text);
        if (value) {
            const button = document.createElement('button'); button.type = 'button'; button.className = 'od-copy-button';
            button.dataset.copyValue = value; button.title = `Copy ${label}`; button.setAttribute('aria-label', `Copy ${label}`);
            const icon = document.createElement('i'); icon.className = 'bi bi-copy'; icon.setAttribute('aria-hidden', 'true');
            button.append(icon); container.append(button);
        }
        return container;
    };
    const decorate = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        if (!item) return;
        const actions = row.querySelector('[data-z-grid-field="actions"]');
        if (actions && !actions.querySelector('[data-instance-link]')) {
            const link = document.createElement('a'); link.className = 'z-button z-button-ghost';
            link.href = root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(item.id));
            link.dataset.instanceLink = item.id; link.title = 'View instance'; link.setAttribute('aria-label', `View instance ${item.correlationId || item.id}`);
            const icon = document.createElement('i'); icon.className = 'bi bi-eye'; icon.setAttribute('aria-hidden', 'true');
            link.append(icon); actions.replaceChildren(link);
        }
        for (const [field, label] of [['correlationId', 'correlation ID'], ['sagaId', 'saga ID']]) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (cell && cell.querySelector('.od-copy-value')?.textContent !== (item[field] || '-')) cell.replaceChildren(copyable(item[field], label));
        }
        for (const field of ['startedOnUtc', 'lastUpdatedOnUtc']) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`), value = item[field];
            const text = typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value;
            const date = new Date(text);
            if (cell && value && !Number.isNaN(date.getTime())) {
                const formatted = date.toISOString().replace('T', ' ').substring(0, 19);
                if (cell.textContent !== formatted) cell.textContent = formatted;
            }
        }
        const status = row.querySelector('[data-z-grid-field="status"]');
        if (status && (status.querySelector('.od-status')?.textContent !== item.status || status.querySelector('.od-status')?.className !== `od-status ${item.statusClass || ''}`)) {
            const badge = document.createElement('span'); badge.className = `od-status ${item.statusClass || ''}`; badge.textContent = item.status;
            status.replaceChildren(badge);
        }
    });
    grid.addEventListener('click', async event => {
        const button = event.target.closest('[data-copy-value]');
        if (!button || !grid.contains(button)) return;
        const value = button.dataset.copyValue;
        if (!value) return;
        try {
            await navigator.clipboard.writeText(value);
            button.classList.add('is-copied');
            button.title = 'Copied';
            window.setTimeout(() => { button.classList.remove('is-copied'); button.title = button.getAttribute('aria-label'); }, 1400);
        } catch {
            button.title = 'Unable to copy';
        }
    });
    observeGridBody();
    grid.addEventListener('zoorce:grid:error', () => {
        refreshing = false;
        setRefreshBusy(false);
        scrollAnchor = null;
        if (refreshPending) {
            const preserveScroll = preservePending;
            refreshPending = false;
            preservePending = false;
            refresh({ preserveScroll });
        }
    });
    grid.addEventListener('zoorce:grid:loaded', event => {
        refreshing = false;
        setRefreshBusy(false);
        catalogLoaded = true;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorate();
        updateLoadedCopy(event.detail.result);
        if (!event.detail.append) {
            const showLoading = countsLoadingForNextLoad;
            countsLoadingForNextLoad = false;
            void refreshCounts({ showLoading });
        }
        window.requestAnimationFrame(restoreScrollAnchor);
        if (refreshPending) {
            const preserveScroll = preservePending;
            refreshPending = false;
            preservePending = false;
            refresh({ preserveScroll });
        }
    });
    const eventValue = (event, name) => event?.[name] ?? event?.[`${name.charAt(0).toUpperCase()}${name.slice(1)}`];
    const toUtcText = value => {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? new Date().toISOString() : date.toISOString();
    };
    const toTimestamp = value => {
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? 0 : date.getTime();
    };
    const orchestrationLabel = (definitionKey, version) => definitionKey
        ? (version ? `${definitionKey} v${version}` : definitionKey)
        : '';
    const statusClass = status => {
        if (['Completed', 'Compensated'].includes(status)) return 'od-status-active';
        if (status === 'Running') return 'od-status-running';
        if (['Idle', 'Waiting', 'WaitingResponse', 'Compensating'].includes(status)) return 'od-status-waiting';
        if (['Retrying', 'CompletedWithErrors', 'DeadLettered'].includes(status)) return 'od-status-warning';
        if (['Failed', 'TimedOut', 'Cancelled', 'Aborted'].includes(status)) return 'od-status-danger';
        return 'od-status-inactive';
    };
    const matchesScope = item => {
        const term = search.value.trim().toLocaleLowerCase();
        if (!term) return true;
        return [item.id, item.orchestrationDefinitionKey, item.correlationId, item.sagaId, item.executionKey]
            .some(value => String(value || '').toLocaleLowerCase().includes(term));
    };
    const toLiveRow = (event, existing) => {
        const status = String(eventValue(event, 'instanceStatus') || existing?.status || 'Created');
        const occurredOnUtc = toUtcText(eventValue(event, 'occurredOnUtc'));
        const definitionKey = eventValue(event, 'orchestrationDefinitionKey') || existing?.orchestrationDefinitionKey || '';
        const version = eventValue(event, 'orchestrationVersion') || existing?.orchestrationVersion || '';
        const isCompleted = ['Completed', 'Compensated'].includes(status);
        const isFailed = ['Failed', 'Stopped', 'Aborted', 'CompletedWithErrors', 'DeadLettered'].includes(status);
        return {
            ...existing,
            id: String(eventValue(event, 'orchestrationInstanceId') || existing?.id || ''),
            orchestrationDefinitionKey: definitionKey,
            orchestrationVersion: version,
            orchestrationLabel: existing?.orchestrationLabel || orchestrationLabel(definitionKey, version),
            correlationId: eventValue(event, 'correlationId') || existing?.correlationId || '',
            sagaId: eventValue(event, 'sagaId') || existing?.sagaId || '',
            executionKey: eventValue(event, 'executionKey') || existing?.executionKey || '',
            status,
            statusClass: statusClass(status),
            currentStageKey: eventValue(event, 'stageKey') || existing?.currentStageKey || '',
            currentTaskKey: eventValue(event, 'taskKey') || existing?.currentTaskKey || '',
            startedOnUtc: existing?.startedOnUtc || occurredOnUtc,
            lastUpdatedOnUtc: occurredOnUtc,
            idleSinceUtc: status === 'Idle' ? occurredOnUtc : existing?.idleSinceUtc || null,
            completedOnUtc: isCompleted ? occurredOnUtc : existing?.completedOnUtc || null,
            failedOnUtc: isFailed ? occurredOnUtc : existing?.failedOnUtc || null
        };
    };
    const publishPatch = item => {
        if (!window.ZoorceData?.invalidate) return false;
        window.ZoorceData.invalidate({
            scope: 'runtime.instances',
            change: { key: item.id, item },
            correlationId: item.id
        });
        // Zoorce patches primitive cell values. Reapply the catalog affordances
        // after its DOM write without loading or reordering the collection.
        window.requestAnimationFrame(decorate);
        return true;
    };
    const patchVisibleRow = item => {
        const row = grid.querySelector(`tr[data-z-grid-key="${CSS.escape(item.id)}"]`);
        if (!row) return false;
        for (const field of ['orchestrationLabel', 'currentStageKey', 'currentTaskKey']) {
            const cell = row.querySelector(`[data-z-grid-field="${field}"]`);
            if (cell) cell.textContent = item[field] || '';
        }
        window.requestAnimationFrame(decorate);
        return true;
    };
    const applyLiveEvent = event => {
        const id = String(eventValue(event, 'orchestrationInstanceId') || '');
        if (!id) return;
        const current = records.get(id);
        const occurredOnUtc = eventValue(event, 'occurredOnUtc');
        if (current && toTimestamp(occurredOnUtc) < toTimestamp(current.lastUpdatedOnUtc)) return;
        const row = toLiveRow(event, current);
        if (!matchesScope(row)) return;
        if (current) {
            records.set(id, row);
            if (!publishPatch(row)) patchVisibleRow(row);
            return;
        }
        const viewport = grid.querySelector('[data-z-grid-viewport]');
        // A scrolling user owns the viewport. Keep newly arrived rows for the
        // next deliberate refresh instead of inserting above their reading position.
        if ((viewport?.scrollTop ?? 0) > 8) {
            return;
        }
        records.set(id, row);
        catalogTotal += 1;
        grid.dispatchEvent(new CustomEvent('zoorce:grid:insert', { detail: { item: row } }));
        updateLoadedCopy();
    };
    const flushLiveEvents = () => {
        liveEventTimer = 0;
        const events = [...pendingEvents.values()];
        pendingEvents.clear();
        events.forEach(applyLiveEvent);
        if (events.length) scheduleCountsRefresh();
    };
    const queueLiveEvent = event => {
        const id = String(eventValue(event, 'orchestrationInstanceId') || '');
        if (!id) return;
        const current = pendingEvents.get(id);
        if (!current || toTimestamp(eventValue(event, 'occurredOnUtc')) >= toTimestamp(eventValue(current, 'occurredOnUtc')))
            pendingEvents.set(id, event);
        if (liveEventTimer) return;
        liveEventTimer = window.setTimeout(flushLiveEvents, 180);
    };
    window.addEventListener('krackend:instances-changed', event => {
        const detail = event.detail || {};
        const transition = String(eventValue(detail.event, 'transitionType') || '');
        const technical = new Set(['TaskDispatchSuppressed', 'TaskDispatchPhysicalCancellationFinalized', 'TaskDispatchPhysicalCancellationDeferred']);
        if (detail.event && !technical.has(transition) && eventValue(detail.event, 'instanceStatus')) queueLiveEvent(detail.event);
        // A server-requested resync is exceptional and is the sole realtime path
        // allowed to reload the catalog.
        if (detail.resync) refresh({ preserveScroll: true });
    });
    const syncScopeButtons = () => scopeButtons.forEach(button =>
        button.setAttribute('aria-pressed', String(Number(button.dataset.runtimeScopeValue) === recentHours)));
    const changeScope = hours => {
        if (!Number.isFinite(hours) || hours === recentHours) return;
        recentHours = hours;
        syncScopeButtons();
        const configElement = grid.querySelector('[data-z-grid-config]');
        const config = JSON.parse(configElement.textContent); config.binding.source = scopedUrl(root.dataset.source).toString();
        window.ZoorceDataGrid.dispose(grid); configElement.textContent = JSON.stringify(config);
        refreshing = false; refreshPending = false; countsLoadingForNextLoad = true; mountGrid();
        if (search.value.trim()) search.dispatchEvent(new Event('input', { bubbles: true }));
    };
    scopeButtons.forEach(button => button.addEventListener('click', () => changeScope(Number(button.dataset.runtimeScopeValue))));
    root.querySelector('[data-runtime-refresh]').addEventListener('click', () => refresh({ showMetricLoading: false }));
    syncScopeButtons();
    refresh({ showMetricLoading: true });
})();
