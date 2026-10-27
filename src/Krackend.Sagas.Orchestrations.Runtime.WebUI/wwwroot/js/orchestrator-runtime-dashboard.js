(() => {
    const root = document.querySelector('[data-runtime-dashboard]');
    const config = window.KrackendRuntimeDashboard;
    if (!root || !config) return;

    const periodButtons = [...root.querySelectorAll('[data-dashboard-period-value]')];
    const requests = new Map();
    let refreshTimer;
    let reconnectTimer;
    let closed = false;
    const state = {
        days: Number(root.dataset.days) || 7,
        cohortEpoch: 0,
        activityEpoch: 0
    };

    const activeStatuses = ['Created', 'Running', 'Compensating'];
    const failedStatuses = ['Stopped', 'Compensated', 'CompletedWithErrors', 'Aborted', 'Failed'];
    const supportedPeriods = new Set([1, 7, 30, 90]);

    const statusValue = (statuses, name) => Number(statuses?.[name] || 0);
    const statusTotal = (statuses, names) => names.reduce((total, name) => total + statusValue(statuses, name), 0);
    const isAbort = error => error?.name === 'AbortError';

    function requestUrl(source) {
        const url = new URL(source, window.location.href);
        url.searchParams.set('days', String(state.days));
        return url;
    }

    async function fetchJson(name, source) {
        requests.get(name)?.abort();
        const controller = new AbortController();
        requests.set(name, controller);
        try {
            const response = await fetch(requestUrl(source), {
                headers: { Accept: 'application/json' },
                signal: controller.signal
            });
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
        if (!element) return;
        element.textContent = '';
        element.hidden = true;
    }

    function replaceChart(id, series) {
        document.getElementById(id)?.dispatchEvent(new CustomEvent('zoorce:chart:replace', { detail: { series } }));
    }

    function setMetric(key, value, total) {
        const metric = root.querySelector(`[data-dashboard-metric="${key}"]`);
        const share = root.querySelector(`[data-dashboard-metric-share="${key}"]`);
        if (metric) metric.textContent = Number(value).toLocaleString();
        if (!share) return;
        share.textContent = key === 'total'
            ? 'selected cohort'
            : total ? `${Math.round(value * 100 / total)}% of started` : 'no instances';
    }

    function asDashboardValues(cohort) {
        const statuses = cohort.currentStatuses || {};
        const values = {
            total: Number(cohort.total || 0),
            running: statusTotal(statuses, activeStatuses),
            waiting: statusValue(statuses, 'Waiting'),
            completed: statusValue(statuses, 'Completed'),
            deadLettered: statusValue(statuses, 'DeadLettered'),
            failed: statusTotal(statuses, failedStatuses)
        };
        const classified = values.running + values.waiting + values.completed + values.deadLettered + values.failed;
        values.total = Math.max(values.total, classified);
        return values;
    }

    function renderCohort(cohort) {
        const values = asDashboardValues(cohort);
        Object.entries(values).forEach(([key, value]) => setMetric(key, value, values.total));
        setMetricLoading(false);

        const distribution = [
            ['In progress', values.running],
            ['Waiting', values.waiting],
            ['Completed', values.completed],
            ['Dead lettered', values.deadLettered],
            ['Failed', values.failed]
        ].filter(([, value]) => value > 0);
        replaceChart('runtime-status-chart', [{
            name: 'Instances',
            points: distribution.length ? distribution.map(([label, value]) => ({ label, value })) : [{ label: 'No instances', value: 0 }]
        }]);
        setLoading('status', false);
        clearError('status');

        const finalized = values.completed + values.deadLettered + values.failed;
        replaceChart('runtime-funnel-chart', [{
            name: 'Instances',
            points: [
                { label: 'Started', value: values.total },
                { label: 'Finalized', value: finalized },
                { label: 'Completed', value: values.completed }
            ]
        }]);
        const outcome = root.querySelector('[data-dashboard-outcome]');
        if (outcome) {
            outcome.hidden = false;
            outcome.textContent = values.total
                ? `${values.running + values.waiting} in progress · ${values.deadLettered} dead-lettered · ${values.failed} failed`
                : 'No instances started in this period.';
        }
        setLoading('funnel', false);
        clearError('funnel');
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
            if (date) counts.set(`${date.getTime()}|${item.status}`, Number(item.count || 0));
        });
        const buckets = [];
        for (let current = bucketStart(start, bucket); current < before; current = bucketStep(current, bucket)) buckets.push(current);
        const statuses = [['Completed', 'Completed'], ['Dead lettered', 'DeadLettered'], ['Failed', 'Failed']];
        replaceChart('runtime-activity-chart', statuses.map(([name, status]) => ({
            name,
            points: buckets.map(date => ({ label: bucketLabel(date, bucket), value: counts.get(`${date.getTime()}|${status}`) || 0 }))
        })));
        setLoading('activity', false);
        clearError('activity');
    }

    async function loadCohort() {
        const epoch = ++state.cohortEpoch;
        setMetricLoading(true);
        setLoading('status', true);
        setLoading('funnel', true);
        try {
            const cohort = await fetchJson('cohort', root.dataset.cohortUrl);
            if (epoch !== state.cohortEpoch) return;
            renderCohort(cohort);
        } catch (error) {
            if (isAbort(error) || epoch !== state.cohortEpoch) return;
            setMetricLoading(false);
            showError('status', error);
            showError('funnel', error);
        }
    }

    async function loadActivity() {
        const epoch = ++state.activityEpoch;
        setLoading('activity', true);
        try {
            const activity = await fetchJson('activity', root.dataset.activityUrl);
            if (epoch !== state.activityEpoch) return;
            renderActivity(activity);
        } catch (error) {
            if (isAbort(error) || epoch !== state.activityEpoch) return;
            showError('activity', error);
        }
    }

    function loadDashboard() {
        return Promise.allSettled([loadCohort(), loadActivity()]);
    }

    function syncPeriodButtons() {
        periodButtons.forEach(button => {
            const selected = Number(button.dataset.dashboardPeriodValue) === state.days;
            button.setAttribute('aria-pressed', String(selected));
        });
    }

    function updateAddress() {
        const url = new URL(window.location.href);
        url.searchParams.set('days', String(state.days));
        window.history.replaceState({}, '', url);
    }

    function selectPeriod(days) {
        if (!supportedPeriods.has(days) || days === state.days) return;
        state.days = days;
        syncPeriodButtons();
        updateAddress();
        void loadDashboard();
    }

    periodButtons.forEach(button => button.addEventListener('click', () => selectPeriod(Number(button.dataset.dashboardPeriodValue))));

    function scheduleStatisticsRefresh() {
        window.clearTimeout(refreshTimer);
        refreshTimer = window.setTimeout(() => void loadDashboard(), 250);
    }

    syncPeriodButtons();
    void loadDashboard();
    if (!window.signalR) return;

    const connection = new signalR.HubConnectionBuilder().withUrl(config.hubPath).withAutomaticReconnect().build();
    const retry = () => {
        if (closed) return;
        window.clearTimeout(reconnectTimer);
        reconnectTimer = window.setTimeout(start, 5000);
    };
    const subscribe = async () => connection.invoke('WatchRuntime');
    connection.on('runtime.statistics.updated', scheduleStatisticsRefresh);
    connection.on('runtime.resync', scheduleStatisticsRefresh);
    connection.onreconnected(() => subscribe().catch(() => connection.stop()));
    connection.onclose(retry);
    async function start() {
        try {
            await connection.start();
            await subscribe();
        } catch {
            if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop();
            retry();
        }
    }

    window.addEventListener('pagehide', () => {
        closed = true;
        window.clearTimeout(refreshTimer);
        window.clearTimeout(reconnectTimer);
        requests.forEach(controller => controller.abort());
        void connection.stop();
    }, { once: true });

    void start();
})();
