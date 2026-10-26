(() => {
    const root = document.querySelector('[data-runtime-dashboard]');
    const config = window.KrackendRuntimeDashboard;
    if (!root || !config) return;

    const filterForm = root.querySelector('[data-dashboard-filter]');
    const period = filterForm?.elements.namedItem('days');
    const orchestration = root.querySelector('[data-dashboard-orchestration]');
    const refreshButton = root.querySelector('#dashboardRefreshButton');
    const requests = new Map();
    let refreshTimer;
    let reconnectTimer;
    let closed = false;
    let dashboardRefreshInFlight = false;
    const state = {
        days: Number(root.dataset.days) || 7,
        orchestrationKey: root.dataset.orchestrationKey || ''
    };

    const statusValue = (statuses, name) => Number(statuses?.[name] || 0);
    const statusTotal = (statuses, names) => names.reduce((total, name) => total + statusValue(statuses, name), 0);
    const activeStatuses = ['Created', 'Running', 'Waiting', 'Compensating', 'DeadLettered'];
    const terminalStatuses = ['Stopped', 'Compensated', 'Completed', 'CompletedWithErrors', 'Aborted', 'Failed'];

    function requestUrl(source) {
        const url = new URL(source, window.location.href);
        url.searchParams.set('days', String(state.days));
        if (state.orchestrationKey) url.searchParams.set('orchestrationKey', state.orchestrationKey);
        else url.searchParams.delete('orchestrationKey');
        return url;
    }

    async function fetchJson(name, source, scoped = true) {
        requests.get(name)?.abort();
        const controller = new AbortController();
        requests.set(name, controller);
        const url = scoped ? requestUrl(source) : new URL(source, window.location.href);
        try {
            const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error(await response.text() || 'Dashboard data is unavailable.');
            return await response.json();
        } finally {
            if (requests.get(name) === controller) requests.delete(name);
        }
    }

    function setLoading(section, loading) {
        const element = root.querySelector(`[data-dashboard-loading="${section}"]`);
        if (element) element.hidden = !loading;
        if (loading) clearError(section);
    }

    function setMetricLoading(loading) {
        root.querySelectorAll('[data-dashboard-metric-skeleton]').forEach(element => { element.hidden = !loading; });
        root.querySelectorAll('[data-dashboard-metric-data]').forEach(element => { element.hidden = loading; });
    }

    function showError(section, error) {
        setLoading(section, false);
        const element = root.querySelector(`[data-dashboard-error="${section}"]`);
        if (!element) return;
        element.textContent = 'This panel could not load. It will retry when new statistics arrive.';
        element.hidden = false;
        console.warn(`Krackend dashboard ${section} load failed.`, error);
    }

    function clearError(section) {
        const element = root.querySelector(`[data-dashboard-error="${section}"]`);
        if (element) {
            element.textContent = '';
            element.hidden = true;
        }
    }

    function replaceChart(id, series) {
        document.getElementById(id)?.dispatchEvent(new CustomEvent('zoorce:chart:replace', { detail: { series } }));
    }

    function renderCohort(cohort) {
        const statuses = cohort.currentStatuses || {};
        const total = Number(cohort.total || 0);
        const inProgress = statusTotal(statuses, ['Created', 'Running', 'Compensating']);
        const waiting = statusValue(statuses, 'Waiting');
        const completed = statusValue(statuses, 'Completed');
        const deadLettered = statusValue(statuses, 'DeadLettered');
        const failed = statusTotal(statuses, ['Failed', 'Aborted', 'CompletedWithErrors']);
        const values = { total, running: inProgress, waiting, completed, deadLettered, failed };
        Object.entries(values).forEach(([key, value]) => {
            const metric = root.querySelector(`[data-dashboard-metric="${key}"]`);
            const share = root.querySelector(`[data-dashboard-metric-share="${key}"]`);
            if (metric) metric.textContent = value.toLocaleString();
            if (share) share.textContent = key === 'total' ? 'selected cohort' : total ? `${Math.round(value * 100 / total)}% of started` : 'no instances';
        });
        setMetricLoading(false);

        const visibleStatuses = Object.entries(statuses)
            .filter(([, value]) => Number(value) > 0)
            .map(([label, value]) => ({ label: formatStatus(label), value: Number(value) }));
        replaceChart('runtime-status-chart', [{ name: 'Instances', points: visibleStatuses.length ? visibleStatuses : [{ label: 'No instances', value: 0 }] }]);
        setLoading('status', false);
        clearError('status');

        const settled = Math.max(0, total - statusTotal(statuses, activeStatuses));
        replaceChart('runtime-funnel-chart', [{
            name: 'Instances',
            points: [
                { label: 'Started', value: total },
                { label: 'Finalized', value: settled },
                { label: 'Completed', value: completed }
            ]
        }]);
        const outstanding = statusTotal(statuses, activeStatuses);
        const outcome = root.querySelector('[data-dashboard-outcome]');
        if (outcome) {
            outcome.hidden = false;
            outcome.replaceChildren(outcomeChip(outstanding, 'in progress', 'running'), outcomeChip(deadLettered, 'dead lettered', 'warning'), outcomeChip(failed, 'failed', 'danger'));
        }
        setLoading('funnel', false);
        clearError('funnel');
    }

    function outcomeChip(value, label, tone) {
        const item = document.createElement('span');
        item.className = `od-dashboard-outcome-chip is-${tone}`;
        item.textContent = `${Number(value).toLocaleString()} ${label}`;
        return item;
    }

    function formatStatus(status) {
        return String(status || '').replace(/([a-z])([A-Z])/g, '$1 $2');
    }

    function asUtcDate(value) {
        if (!value) return null;
        const text = /(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? value : `${value}Z`;
        const date = new Date(text);
        return Number.isNaN(date.getTime()) ? null : date;
    }

    function bucketStart(date, bucket) {
        const result = new Date(date.getTime());
        if (bucket === 'hour') result.setUTCMinutes(0, 0, 0);
        else if (bucket === 'day') result.setUTCHours(0, 0, 0, 0);
        else {
            result.setUTCHours(0, 0, 0, 0);
            result.setUTCDate(result.getUTCDate() - ((result.getUTCDay() + 6) % 7));
        }
        return result;
    }

    function bucketStep(date, bucket) {
        const next = new Date(date.getTime());
        if (bucket === 'hour') next.setUTCHours(next.getUTCHours() + 1);
        else if (bucket === 'day') next.setUTCDate(next.getUTCDate() + 1);
        else next.setUTCDate(next.getUTCDate() + 7);
        return next;
    }

    function bucketLabel(date, bucket) {
        if (bucket === 'hour') return date.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit', hour12: false });
        if (bucket === 'week') return `Week of ${date.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' })}`;
        return date.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' });
    }

    function renderActivity(result) {
        const bucket = String(result.bucket || 'Day').toLowerCase();
        const start = asUtcDate(result.fromUtc);
        const before = asUtcDate(result.beforeUtc);
        if (!start || !before) throw new Error('The activity range is invalid.');
        const counts = new Map();
        (result.activity || []).forEach(item => {
            const date = asUtcDate(item.bucketUtc);
            if (!date) return;
            counts.set(`${date.getTime()}|${item.status}`, Number(item.count || 0));
        });
        const buckets = [];
        for (let current = bucketStart(start, bucket); current < before; current = bucketStep(current, bucket)) {
            buckets.push(current);
        }
        const statuses = [['Completed', 'Completed'], ['Dead lettered', 'DeadLettered'], ['Failed', 'Failed']];
        replaceChart('runtime-activity-chart', statuses.map(([name, status]) => ({
            name,
            points: buckets.map(date => ({ label: bucketLabel(date, bucket), value: counts.get(`${date.getTime()}|${status}`) || 0 }))
        })));
        setLoading('activity', false);
        clearError('activity');
    }

    function renderHealth(health) {
        const element = root.querySelector('[data-dashboard-health]');
        if (!element) return;
        if (!health.backfillCompleted) element.textContent = 'Preparing history';
        else if (Number(health.pendingObservations || 0) > 0) element.textContent = 'Updating projection';
        else element.textContent = '';
    }

    function formatUtcTime(value) {
        const date = asUtcDate(value);
        return date ? date.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit', hour12: false }) + ' UTC' : 'recently';
    }

    async function loadFilters() {
        try {
            const result = await fetchJson('filters', root.dataset.filtersUrl, false);
            const selected = state.orchestrationKey;
            orchestration.replaceChildren(new Option('All orchestrations', ''));
            (result.orchestrationKeys || []).forEach(key => orchestration.add(new Option(key, key, false, key === selected)));
            orchestration.disabled = false;
        } catch (error) {
            orchestration.disabled = false;
            console.warn('Krackend dashboard filter options could not load.', error);
        }
    }

    async function loadCohort() {
        setMetricLoading(true);
        setLoading('status', true);
        setLoading('funnel', true);
        try { renderCohort(await fetchJson('cohort', root.dataset.cohortUrl)); }
        catch (error) {
            setMetricLoading(false);
            showError('status', error);
            showError('funnel', error);
        }
    }

    async function loadActivity() {
        setLoading('activity', true);
        try { renderActivity(await fetchJson('activity', root.dataset.activityUrl)); }
        catch (error) { showError('activity', error); }
    }

    async function loadHealth() {
        try { renderHealth(await fetchJson('health', root.dataset.healthUrl, false)); }
        catch (error) { console.warn('Krackend dashboard projection health could not load.', error); }
    }

    async function loadDashboard({ includeFilters = false, showProgress = false } = {}) {
        if (dashboardRefreshInFlight) return;
        dashboardRefreshInFlight = true;
        if (showProgress && refreshButton) {
            refreshButton.disabled = true;
            refreshButton.dataset.zProgressState = 'active';
            refreshButton.dataset.zProgressIndeterminate = 'true';
            refreshButton.setAttribute('aria-busy', 'true');
        }
        const loaders = [loadCohort(), loadActivity(), loadHealth()];
        if (includeFilters) loaders.push(loadFilters());
        try {
            await Promise.allSettled(loaders);
        } finally {
            dashboardRefreshInFlight = false;
            if (showProgress && refreshButton) {
                refreshButton.disabled = false;
                refreshButton.dataset.zProgressState = 'idle';
                refreshButton.dataset.zProgressIndeterminate = 'false';
                refreshButton.removeAttribute('aria-busy');
            }
        }
    }

    function updateAddress() {
        const url = new URL(window.location.href);
        url.searchParams.set('days', String(state.days));
        if (state.orchestrationKey) url.searchParams.set('orchestrationKey', state.orchestrationKey);
        else url.searchParams.delete('orchestrationKey');
        window.history.replaceState({}, '', url);
    }

    filterForm?.addEventListener('submit', event => {
        event.preventDefault();
        state.days = Number(period.value) || 7;
        state.orchestrationKey = orchestration.value || '';
        updateAddress();
        void loadDashboard({ showProgress: true });
    });
    refreshButton?.addEventListener('click', event => {
        event.preventDefault();
        filterForm?.requestSubmit();
    });

    function scheduleStatisticsRefresh() {
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(() => void loadDashboard(), 250);
    }

    if (!window.signalR) {
        void loadDashboard({ includeFilters: true });
        return;
    }

    const connection = new signalR.HubConnectionBuilder().withUrl(config.hubPath).withAutomaticReconnect().build();
    const retry = () => {
        if (!closed) {
            window.clearTimeout(reconnectTimer);
            reconnectTimer = window.setTimeout(start, 5000);
        }
    };
    const subscribe = async () => {
        await connection.invoke('WatchRuntime');
    };
    connection.on('runtime.statistics.updated', scheduleStatisticsRefresh);
    connection.on('runtime.resync', scheduleStatisticsRefresh);
    connection.onreconnected(() => subscribe().catch(() => connection.stop()));
    connection.onclose(() => retry());
    async function start() {
        try { await connection.start(); await subscribe(); }
        catch {
            if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop();
            retry();
        }
    }

    window.addEventListener('pagehide', () => {
        closed = true;
        window.clearTimeout(refreshTimer);
        window.clearTimeout(reconnectTimer);
        requests.forEach(controller => controller.abort());
        connection.stop();
    }, { once: true });

    void loadDashboard({ includeFilters: true });
    start();
})();
