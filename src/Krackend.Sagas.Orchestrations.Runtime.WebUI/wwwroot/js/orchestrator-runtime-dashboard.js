(() => {
    const root = document.querySelector('[data-runtime-dashboard], [data-runtime-statistics]');
    const config = window.KrackendRuntimeDashboard;
    if (!root || !config) return;

    const periodButtons = [...root.querySelectorAll('[data-dashboard-period-value]')];
    const rankingLimitButtons = [...root.querySelectorAll('[data-dashboard-ranking-limit-value]')];
    const isManualDashboard = root.dataset.dashboardManualLoad === 'true';
    const requests = new Map();
    let reconnectTimer;
    let failuresTimer;
    let closed = false;
    const state = {
        hours: Number(root.dataset.hours) || 168,
        orchestrationKey: root.dataset.orchestrationKey || '',
        orchestrationVersion: root.dataset.orchestrationVersion || '',
        cohortEpoch: 0,
        activityEpoch: 0,
        topOrchestrationsEpoch: 0,
        failureEpoch: 0,
        loadGeneration: 0,
        rankingLimit: 5,
        values: null,
        activity: null,
        ranking: [],
        tracking: !isManualDashboard
    };

    // Created/Compensating are execution internals.  The aggregate view exposes
    // them as one comprehensible operational state: Running.
    const activeStatuses = ['Created', 'Running', 'Waiting', 'WaitingResponse', 'Retrying', 'Compensating'];
    const failedStatuses = ['Stopped', 'CompletedWithErrors', 'Aborted', 'Failed'];
    const statusDescriptors = [
        { key: 'running', label: 'Running', tone: 'progress', color: 'var(--z-info)' },
        { key: 'idle', label: 'Idle', tone: 'idle', color: 'var(--z-warning)' },
        { key: 'completed', label: 'Completed', tone: 'completed', color: 'var(--z-success)' },
        { key: 'deadLettered', label: 'Dead lettered', tone: 'dead-lettered', color: 'var(--z-warning)' },
        { key: 'failed', label: 'Failed', tone: 'failed', color: 'var(--z-danger)' }
    ];
    const supportedPeriods = new Set([1, 6, 24, 168, 720, 2160]);
    const eventValue = (event, name) => event?.[name] ?? event?.[`${name.charAt(0).toUpperCase()}${name.slice(1)}`];

    const statusValue = (statuses, name) => Number(statuses?.[name] || 0);
    const statusTotal = (statuses, names) => names.reduce((total, name) => total + statusValue(statuses, name), 0);
    const isAbort = error => error?.name === 'AbortError';

    function requestUrl(source) {
        const url = new URL(source, window.location.href);
        url.searchParams.delete('days');
        url.searchParams.set('hours', String(state.hours));
        if (state.orchestrationKey) url.searchParams.set('orchestrationKey', state.orchestrationKey);
        else url.searchParams.delete('orchestrationKey');
        if (state.orchestrationVersion) url.searchParams.set('orchestrationVersion', state.orchestrationVersion);
        else url.searchParams.delete('orchestrationVersion');
        return url;
    }

    async function fetchJson(name, source) {
        requests.get(name)?.abort();
        const controller = new AbortController();
        requests.set(name, controller);
        try {
            const url = requestUrl(source);
            if (name === 'activity') url.searchParams.set('cohortOnly', 'true');
            const response = await fetch(url, {
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
        if (window.KrackendRuntimeCharts?.replace(id, series)) return;
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

    function dashboardNumber(value) {
        return Number(value || 0).toLocaleString();
    }

    function dashboardShare(value, total) {
        return total ? `${Math.round(value * 100 / total)}%` : '0%';
    }

    function statusSeries(values) {
        return statusDescriptors.map(status => ({
            name: status.label,
            color: status.color,
            points: [{ label: status.label, value: values[status.key] }]
        }));
    }

    function centerChartCanvas(chartId) {
        const canvas = document.getElementById(chartId)?.querySelector('[data-z-chart-canvas]');
        canvas?.setAttribute('preserveAspectRatio', 'xMidYMid meet');
    }

    function renderStatusCenter(total) {
        const visual = document.getElementById('runtime-status-chart')?.querySelector('[data-z-chart-visual]');
        if (!visual) return;
        let center = visual.querySelector('[data-dashboard-status-center]');
        if (!center) {
            center = document.createElement('div');
            center.className = 'od-dashboard-status-center';
            center.dataset.dashboardStatusCenter = '';
            visual.append(center);
        }
        const value = document.createElement('strong');
        value.textContent = dashboardNumber(total);
        const label = document.createElement('span');
        label.textContent = 'Total instances';
        center.replaceChildren(value, label);
        const position = () => {
            const ring = visual.querySelector('.z-native-chart-ring-track');
            if (ring) {
                const visualBounds = visual.getBoundingClientRect();
                const ringBounds = ring.getBoundingClientRect();
                center.style.left = `${ringBounds.left - visualBounds.left + ringBounds.width / 2}px`;
                center.style.top = `${ringBounds.top - visualBounds.top + ringBounds.height / 2}px`;
                return;
            }

            const canvas = visual.querySelector('[data-z-chart-canvas]');
            if (!canvas) return;
            const visualBounds = visual.getBoundingClientRect();
            const canvasBounds = canvas.getBoundingClientRect();
            center.style.left = `${canvasBounds.left - visualBounds.left + canvasBounds.width / 2}px`;
            center.style.top = `${canvasBounds.top - visualBounds.top + canvasBounds.height / 2}px`;
        };
        position();
        if (!center.statusCenterResizeObserver && 'ResizeObserver' in window) {
            center.statusCenterResizeObserver = new ResizeObserver(position);
            center.statusCenterResizeObserver.observe(visual);
        }
    }

    function renderStatusSummary(total) {
        const summary = root.querySelector('[data-dashboard-status-total]');
        const value = root.querySelector('[data-dashboard-status-total-value]');
        if (!summary || !value) return;
        if (isManualDashboard) {
            summary.hidden = true;
            return;
        }
        summary.hidden = total === 0;
        value.textContent = dashboardNumber(total);
    }

    function renderStatusTooltip(values) {
        const tooltip = root.querySelector('[data-dashboard-status-tooltip]');
        const visual = document.getElementById('runtime-status-chart')?.querySelector('[data-z-chart-visual]');
        if (!tooltip || !visual) return;

        const heading = document.createElement('strong');
        heading.className = 'od-dashboard-status-tooltip-title';
        heading.textContent = 'Status distribution';
        const caption = document.createElement('span');
        caption.className = 'od-dashboard-status-tooltip-caption';
        caption.textContent = `${dashboardNumber(values.total)} total instances`;
        const rows = document.createElement('div');
        rows.className = 'od-dashboard-status-tooltip-rows';
        rows.replaceChildren(...statusDescriptors.map(status => {
            const value = values[status.key];
            const row = document.createElement('div');
            row.className = `od-dashboard-status-row is-${status.tone}`;
            const marker = document.createElement('span');
            marker.className = 'od-dashboard-status-marker';
            marker.setAttribute('aria-hidden', 'true');
            const label = document.createElement('span');
            label.className = 'od-dashboard-status-label';
            label.textContent = status.label;
            const count = document.createElement('strong');
            count.textContent = `${dashboardNumber(value)} · ${dashboardShare(value, values.total)}`;
            row.append(marker, label, count);
            return row;
        }));
        tooltip.replaceChildren(heading, caption, rows);

        visual.tabIndex = 0;
        visual.setAttribute('aria-describedby', tooltip.id);
        if (visual.dataset.statusTooltipBound) return;

        const show = () => { tooltip.hidden = false; };
        const hide = () => { tooltip.hidden = true; };
        visual.addEventListener('pointerenter', show);
        visual.addEventListener('pointerleave', hide);
        visual.addEventListener('focusin', show);
        visual.addEventListener('focusout', hide);
        visual.addEventListener('keydown', event => {
            if (event.key === 'Escape') hide();
        });
        visual.dataset.statusTooltipBound = 'true';
    }

    function asDashboardValues(cohort) {
        const statuses = cohort.currentStatuses || {};
        const values = {
            total: Number(cohort.total || 0),
            running: statusTotal(statuses, activeStatuses),
            idle: statusValue(statuses, 'Idle'),
            completed: statusValue(statuses, 'Completed') + statusValue(statuses, 'Compensated'),
            deadLettered: statusValue(statuses, 'DeadLettered'),
            failed: statusTotal(statuses, failedStatuses)
        };
        const classified = values.running + values.idle + values.completed + values.deadLettered + values.failed;
        values.total = Math.max(values.total, classified);
        return values;
    }

    function effectivenessTone(rate) {
        if (rate >= 95) return { key: 'healthy', color: 'var(--z-success)' };
        if (rate >= 90) return { key: 'warning', color: 'var(--z-warning)' };
        return { key: 'danger', color: 'var(--z-danger)' };
    }

    function renderEffectiveness(values) {
        const terminal = values.completed + values.deadLettered + values.failed;
        const chart = document.getElementById('runtime-effectiveness-chart');
        const detail = root.querySelector('[data-dashboard-effectiveness-detail]');
        const rate = terminal ? Math.round(values.completed * 1000 / terminal) / 10 : null;
        const tone = rate === null ? null : effectivenessTone(rate);
        if (chart) {
            chart.dataset.effectivenessTone = tone?.key || 'neutral';
            chart.dataset.effectivenessEmpty = String(rate === null);
        }
        replaceChart('runtime-effectiveness-chart', rate === null ? [] : [{
            name: 'Success rate',
            color: tone.color,
            points: [{ label: 'Success rate', value: rate }]
        }]);
        setPanelEmpty('effectiveness', terminal === 0);
        if (detail) {
            detail.hidden = terminal === 0;
            if (rate === null) {
                detail.textContent = 'No terminal results in the selected period.';
            } else {
                const completed = document.createElement('strong');
                completed.textContent = `${dashboardNumber(values.completed)} of ${dashboardNumber(terminal)} terminal results successful`;
                detail.title = `${dashboardNumber(values.running + values.idle)} still active`;
                detail.replaceChildren(completed);
            }
        }
        setLoading('effectiveness', false);
        clearError('effectiveness');
    }

    function renderCohort(cohort) {
        const values = asDashboardValues(cohort);
        state.values = values;
        renderCohortValues(values);
    }

    function renderCohortValues(values) {
        Object.entries(values).forEach(([key, value]) => setMetric(key, value, values.total));
        setMetricLoading(false);

        replaceChart('runtime-status-chart', statusSeries(values));
        window.requestAnimationFrame(() => window.requestAnimationFrame(() => {
            centerChartCanvas('runtime-status-chart');
            renderStatusCenter(values.total);
        }));
        renderStatusSummary(values.total);
        const statusDetail = root.querySelector('[data-dashboard-status-detail]');
        if (statusDetail) {
            const incidents = values.deadLettered + values.failed;
            statusDetail.hidden = values.total === 0;
            statusDetail.textContent = `${dashboardNumber(incidents)} terminal incidents · ${dashboardShare(incidents, values.total)} of the cohort`;
        }
        renderStatusTooltip(values);
        setPanelEmpty('status', values.total === 0);
        setLoading('status', false);
        clearError('status');

        renderEffectiveness(values);

        const finalized = values.completed + values.deadLettered + values.failed;
        replaceChart('runtime-funnel-chart', [{
            name: 'Instances',
            points: [
                { label: 'Started', value: values.total },
                { label: 'Completed', value: values.completed }
            ]
        }]);
        window.requestAnimationFrame(() => window.requestAnimationFrame(() => {
            centerChartCanvas('runtime-funnel-chart');
        }));
        const outcome = root.querySelector('[data-dashboard-outcome]');
        setPanelEmpty('funnel', values.total === 0);
        if (outcome) {
            outcome.hidden = values.total === 0;
            if (!values.total) {
                outcome.textContent = 'No instances started in this period.';
            } else {
                const terminal = document.createElement('strong');
                if (isManualDashboard) {
                    terminal.textContent = `${dashboardNumber(values.completed)} of ${dashboardNumber(values.total)} completed successfully`;
                    outcome.title = `${dashboardNumber(values.running + values.idle)} still active · ${dashboardNumber(values.failed)} failed · ${dashboardNumber(values.deadLettered)} dead-lettered`;
                    outcome.replaceChildren(terminal);
                } else {
                    terminal.textContent = `${dashboardNumber(finalized)} reached a terminal outcome`;
                    const detail = document.createElement('span');
                    detail.textContent = `${dashboardNumber(values.running + values.idle)} still active · ${dashboardNumber(values.failed)} failed · ${dashboardNumber(values.deadLettered)} dead-lettered`;
                    outcome.replaceChildren(terminal, detail);
                }
            }
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
        if (bucket === 'minute') result.setUTCSeconds(0, 0);
        else if (bucket === 'hour') result.setUTCMinutes(0, 0, 0);
        else if (bucket === 'day') result.setUTCHours(0, 0, 0, 0);
        else {
            result.setUTCHours(0, 0, 0, 0);
            result.setUTCDate(result.getUTCDate() - ((result.getUTCDay() + 6) % 7));
        }
        return result;
    }

    function bucketStep(date, bucket) {
        const next = new Date(date.getTime());
        if (bucket === 'minute') next.setUTCMinutes(next.getUTCMinutes() + 1);
        else if (bucket === 'hour') next.setUTCHours(next.getUTCHours() + 1);
        else if (bucket === 'day') next.setUTCDate(next.getUTCDate() + 1);
        else next.setUTCDate(next.getUTCDate() + 7);
        return next;
    }

    function bucketLabel(date, bucket) {
        if (bucket === 'minute' || bucket === 'hour') return date.toLocaleTimeString(undefined, { timeZone: 'UTC', hour: '2-digit', minute: '2-digit', hour12: false });
        if (bucket === 'week') return `Week of ${date.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' })}`;
        return date.toLocaleDateString(undefined, { timeZone: 'UTC', month: 'short', day: 'numeric' });
    }

    function setActivityEmpty(empty) {
        const emptyState = root.querySelector('[data-dashboard-activity-empty]');
        if (emptyState) {
            emptyState.hidden = !empty;
            if (empty && state.orchestrationKey) {
                const title = emptyState.querySelector('strong'), description = emptyState.querySelector('span');
                if (title) title.textContent = 'No transitions in this period';
                if (description) description.textContent = 'Choose a wider range to inspect earlier activity.';
            }
        }
        root.querySelector('[data-dashboard-panel="activity"]')?.classList.toggle('is-empty', empty);
    }

    function setRankingEmpty(empty) {
        const emptyState = root.querySelector('[data-dashboard-ranking-empty]');
        if (emptyState) emptyState.hidden = !empty;
    }

    function setPanelEmpty(panel, empty) {
        const emptyState = root.querySelector(`[data-dashboard-empty="${panel}"]`);
        if (emptyState) emptyState.hidden = !empty;
        root.querySelector(`[data-dashboard-panel="${panel}"]`)?.classList.toggle('is-empty', empty);
    }

    function renderTopOrchestrations(orchestrations) {
        const rows = Array.isArray(orchestrations) ? orchestrations : [];
        state.ranking = rows;
        setRankingEmpty(rows.length === 0);
        replaceChart('runtime-top-orchestrations-chart', rows.length === 0 ? [] : [{
            name: 'Started instances',
            points: rows.map(row => ({ label: String(row.orchestrationKey || ''), value: Number(row.startedInstances || 0) }))
        }]);
        setLoading('top-orchestrations', false);
        clearError('top-orchestrations');
    }

    function renderActivity(result) {
        state.activity = result;
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
        const statuses = [
            ['Completed', ['Completed', 'Compensated'], 'var(--z-success)'],
            ['Dead lettered', ['DeadLettered'], 'var(--z-warning)'],
            ['Failed', failedStatuses, 'var(--z-danger)']
        ];
        window.KrackendRuntimeSparklines?.render(root, buckets, counts);
        const countFor = (date, statusNames) => statusNames.reduce((sum, status) => sum + (counts.get(`${date.getTime()}|${status}`) || 0), 0);
        const total = buckets.reduce((sum, date) => sum + statuses.reduce((n, [, names]) => n + countFor(date, names), 0), 0);
        setActivityEmpty(total === 0);
        replaceChart('runtime-activity-chart', total === 0 ? [] : statuses.map(([name, statusNames, color]) => ({
            name,
            color,
            points: buckets.map(date => ({ label: bucketLabel(date, bucket), value: countFor(date, statusNames),
                partial: date < start || bucketStep(date, bucket) > before }))
        })));
        setLoading('activity', false);
        clearError('activity');
    }

    async function loadCohort({ showLoading = false } = {}) {
        const epoch = ++state.cohortEpoch;
        if (showLoading) {
            setMetricLoading(true);
            replaceChart('runtime-status-chart', []);
            replaceChart('runtime-effectiveness-chart', []);
            replaceChart('runtime-funnel-chart', []);
            const detail = root.querySelector('[data-dashboard-status-detail]'); if (detail) detail.hidden = true;
            setLoading('status', true);
            setLoading('funnel', true);
            setLoading('effectiveness', true);
        }
        try {
            const cohort = await fetchJson('cohort', root.dataset.cohortUrl);
            if (epoch !== state.cohortEpoch) return false;
            renderCohort(cohort);
            return true;
        } catch (error) {
            if (isAbort(error) || epoch !== state.cohortEpoch) return false;
            setMetricLoading(false);
            state.values = null;
            root.querySelectorAll('[data-dashboard-metric]').forEach(element => { element.textContent = '—'; });
            root.querySelectorAll('[data-dashboard-metric-share]').forEach(element => { element.textContent = 'Unavailable'; });
            showError('status', error);
            showError('funnel', error);
            showError('effectiveness', error);
            return false;
        }
    }

    async function loadActivity({ showLoading = false } = {}) {
        const epoch = ++state.activityEpoch;
        if (showLoading) {
            window.KrackendRuntimeSparklines?.clear(root);
            setActivityEmpty(false);
            replaceChart('runtime-activity-chart', []);
            setLoading('activity', true);
        }
        try {
            const activity = await fetchJson('activity', root.dataset.activityUrl);
            if (epoch !== state.activityEpoch) return;
            renderActivity(activity);
        } catch (error) {
            if (isAbort(error) || epoch !== state.activityEpoch) return;
            window.KrackendRuntimeSparklines?.clear(root, 'Changes unavailable');
            replaceChart('runtime-activity-chart', []);
            showError('activity', error);
        }
    }

    async function loadTopOrchestrations({ showLoading = false } = {}) {
        if (!root.dataset.topOrchestrationsUrl) return;
        const epoch = ++state.topOrchestrationsEpoch;
        if (showLoading) {
            setRankingEmpty(false);
            replaceChart('runtime-top-orchestrations-chart', []);
            setLoading('top-orchestrations', true);
        }
        try {
            const source = new URL(root.dataset.topOrchestrationsUrl, window.location.href);
            source.searchParams.set('limit', String(state.rankingLimit));
            const ranking = await fetchJson('top-orchestrations', source);
            if (epoch !== state.topOrchestrationsEpoch) return;
            renderTopOrchestrations(ranking);
        } catch (error) {
            if (isAbort(error) || epoch !== state.topOrchestrationsEpoch) return;
            showError('top-orchestrations', error);
        }
    }

    function loadDashboard({ showLoading = false } = {}) {
        const generation = ++state.loadGeneration;
        const scope = { orchestrationKey: state.orchestrationKey, orchestrationVersion: state.orchestrationVersion, hours: state.hours };
        const loads = [
            loadCohort({ showLoading }),
            loadActivity({ showLoading })
        ];
        if (root.dataset.topOrchestrationsUrl) loads.push(loadTopOrchestrations({ showLoading }));
        if (root.dataset.failuresUrl) loads.push(loadFailures({ showLoading }));
        return Promise.allSettled(loads).then(results => {
            if (generation !== state.loadGeneration || closed) return results;
            // Consumers such as Statistics own their progress button.  They
            // must complete only when every independently loaded panel has
            // settled, never on an arbitrary UI timer.
            root.dispatchEvent(new CustomEvent('krackend:dashboard-loaded', {
                detail: {
                    ...scope,
                    results
                }
            }));
            return results;
        });
    }

    async function loadFailures({ showLoading = false } = {}) {
        const epoch = ++state.failureEpoch;
        const empty = root.querySelector('[data-dashboard-failures-empty]');
        if (showLoading) {
            replaceChart('runtime-failures-chart', []);
            if (empty) { empty.hidden = false; empty.querySelector('strong').textContent = 'Loading failure events…'; }
            const footer = root.querySelector('[data-failure-detail]'); if (footer) footer.hidden = true;
            root.querySelectorAll('[data-failures-history], [data-failure-unclassified]').forEach(element => { element.hidden = true; });
            clearError('failures');
        }
        try {
            const report = await fetchJson('failures', root.dataset.failuresUrl);
            if (epoch !== state.failureEpoch) return;
            const points = (Array.isArray(report.items) ? report.items : []).map(row => ({ label: row.category,
                value: Number(row.count || 0) })).sort((a, b) => b.value - a.value);
            replaceChart('runtime-failures-chart', points.length ? [{ name: 'Failure events', points }] : []);
            if (empty) { empty.querySelector('strong').textContent = 'No failure events in this period'; empty.hidden = points.length > 0; }
            const panel = root.querySelector('[data-dashboard-panel="failures"]');
            if (panel) {
                let footer = panel.querySelector('[data-failure-detail]');
                if (!footer) { footer = document.createElement('footer'); footer.className = 'reference-chart-footer'; footer.dataset.failureDetail = ''; panel.append(footer); }
                const total = points.reduce((sum, point) => sum + point.value, 0);
                const leading = points.slice(0, 2).reduce((sum, point) => sum + point.value, 0);
                footer.hidden = total === 0;
                footer.textContent = `${total ? Math.round(leading * 100 / total) : 0}% in the top two categories · ${dashboardNumber(total)} recorded events`;
            }
            const history = root.querySelector('[data-failures-history]');
            if (history) history.hidden = report.historyComplete;
            const unclassified = root.querySelector('[data-failure-unclassified]');
            if (unclassified) {
                const count = Number(report.unclassifiedCount || 0);
                unclassified.hidden = count === 0;
                unclassified.textContent = count ? `${dashboardNumber(count)} events cannot be assigned to a scope and may include this selection.` : '';
            }
            if (!report.historyComplete && !failuresTimer) {
                failuresTimer = window.setTimeout(() => { failuresTimer = 0; void loadFailures(); }, 30000);
            }
            clearError('failures');
        } catch (error) {
            if (!isAbort(error) && epoch === state.failureEpoch) { if (empty) empty.hidden = true; showError('failures', error); }
        }
    }

    function syncPeriodButtons() {
        periodButtons.forEach(button => {
            const selected = Number(button.dataset.dashboardPeriodValue) === state.hours;
            button.setAttribute('aria-pressed', String(selected));
        });
    }

    function syncRankingLimitButtons() {
        rankingLimitButtons.forEach(button => {
            const selected = Number(button.dataset.dashboardRankingLimitValue) === state.rankingLimit;
            button.setAttribute('aria-pressed', String(selected));
        });
    }

    function updateAddress() {
        const url = new URL(window.location.href);
        url.searchParams.delete('days');
        url.searchParams.set('hours', String(state.hours));
        window.history.replaceState({}, '', url);
    }

    function selectPeriod(hours) {
        if (!supportedPeriods.has(hours) || hours === state.hours) return;
        state.hours = hours;
        syncPeriodButtons();
        updateAddress();
        if (isManualDashboard && !state.orchestrationKey) return;
        void loadDashboard({ showLoading: true });
    }

    function selectRankingLimit(limit) {
        if (![5, 10, 15].includes(limit) || limit === state.rankingLimit) return;
        state.rankingLimit = limit;
        syncRankingLimitButtons();
        void loadTopOrchestrations({ showLoading: true });
    }

    periodButtons.forEach(button => button.addEventListener('click', () => selectPeriod(Number(button.dataset.dashboardPeriodValue))));
    rankingLimitButtons.forEach(button => button.addEventListener('click', () => selectRankingLimit(Number(button.dataset.dashboardRankingLimitValue))));

    root.addEventListener('krackend:dashboard-load', event => {
        const detail = event.detail || {};
        state.orchestrationKey = String(detail.orchestrationKey || '').trim();
        state.orchestrationVersion = String(detail.orchestrationVersion || '').trim();
        if (detail.hours && supportedPeriods.has(Number(detail.hours))) {
            state.hours = Number(detail.hours);
            syncPeriodButtons();
        }
        void loadDashboard({ showLoading: true });
    });

    root.addEventListener('krackend:dashboard-invalidate', () => {
        state.loadGeneration++; state.cohortEpoch++; state.activityEpoch++; state.failureEpoch++;
        requests.forEach(controller => controller.abort());
        window.clearTimeout(failuresTimer); failuresTimer = 0;
        window.clearTimeout(statisticsTimer); statisticsTimer = 0;
        state.orchestrationKey = ''; state.orchestrationVersion = ''; state.values = null;
        ['runtime-status-chart', 'runtime-effectiveness-chart', 'runtime-activity-chart', 'runtime-failures-chart'].forEach(id => replaceChart(id, []));
        root.querySelectorAll('[data-dashboard-metric]').forEach(element => { element.textContent = '—'; });
        root.querySelectorAll('[data-dashboard-metric-share]').forEach(element => { element.textContent = 'Select a scope'; });
        setMetricLoading(false);
        window.KrackendRuntimeSparklines?.clear(root, 'Select a scope');
        ['status', 'effectiveness', 'activity', 'failures'].forEach(section => { setLoading(section, false); clearError(section); });
        setPanelEmpty('status', true); setPanelEmpty('effectiveness', true); setActivityEmpty(true);
        const activity = root.querySelector('[data-dashboard-activity-empty]');
        if (activity) { activity.querySelector('strong').textContent = 'Select a scope to load activity'; activity.querySelector('span').textContent = 'Statistics remain idle until an orchestration is selected.'; }
        const empty = root.querySelector('[data-dashboard-failures-empty]');
        if (empty) { empty.hidden = false; empty.querySelector('strong').textContent = 'Select a scope to load failure events'; }
        root.querySelectorAll('[data-failures-history], [data-failure-unclassified], [data-failure-detail], [data-dashboard-status-detail], [data-dashboard-effectiveness-detail]').forEach(element => { element.hidden = true; });
    });

    let statisticsTimer;
    function scheduleStatisticsRefresh() {
        if (statisticsTimer || !state.tracking) return;
        statisticsTimer = window.setTimeout(() => {
            statisticsTimer = 0;
            if (state.tracking && !closed) {
                const loads = [loadCohort(), loadActivity()];
                if (root.dataset.topOrchestrationsUrl) loads.push(loadTopOrchestrations());
                void Promise.allSettled(loads);
            }
        }, 15000);
    }

    function queueLiveEvent(event) {
        const transitionType = String(eventValue(event, 'transitionType') || '');
        if (root.dataset.failuresUrl && /(?:Failed|TimedOut)$/.test(transitionType) && !failuresTimer) {
            failuresTimer = window.setTimeout(() => { failuresTimer = 0; void loadFailures(); }, 15000);
        }
    }

    syncPeriodButtons();
    syncRankingLimitButtons();
    let connection;
    const notifyLiveState = stateName => {
        const indicator = root.querySelector('[data-dashboard-live]');
        if (indicator) indicator.textContent = ({ connected: 'Live', local: 'Local only', reconnecting: 'Reconnecting', inactive: 'Offline' })[stateName] || stateName;
        root.dispatchEvent(new CustomEvent('krackend:dashboard-live-state', { detail: { tracking: state.tracking, state: stateName } }));
    };
    const retry = () => {
        if (closed || !state.tracking) return;
        window.clearTimeout(reconnectTimer);
        reconnectTimer = window.setTimeout(start, 5000);
    };
    const subscribe = async () => connection.invoke('WatchRuntime');
    // Coalesce notifications and reload committed projections, rather than
    // guessing cohort totals from individual transitions.
    function createConnection() {
        if (connection || !window.signalR) return connection;
        connection = new signalR.HubConnectionBuilder().withUrl(config.hubPath).withAutomaticReconnect().build();
        connection.on('runtime.transition', queueLiveEvent);
        connection.on('runtime.statistics.updated', scheduleStatisticsRefresh);
        connection.on('runtime.resync', () => void loadDashboard());
        connection.on('runtime.fanout.health', synchronized => notifyLiveState(synchronized ? 'connected' : 'local'));
        connection.onreconnecting(() => notifyLiveState('reconnecting'));
        connection.onreconnected(async () => {
            try {
                const synchronized = await subscribe();
                notifyLiveState(synchronized === false ? 'local' : 'connected');
                void loadDashboard();
            } catch {
                notifyLiveState('reconnecting');
                await connection.stop();
            }
        });
        connection.onclose(() => {
            notifyLiveState(state.tracking ? 'reconnecting' : 'inactive');
            retry();
        });
        return connection;
    }
    async function start() {
        if (!state.tracking) return;
        if (!createConnection()) return;
        try {
            await connection.start();
            const synchronized = await subscribe();
            notifyLiveState(synchronized === false ? 'local' : 'connected');
        } catch {
            notifyLiveState('reconnecting');
            if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop();
            retry();
        }
    }

    async function stop() {
        window.clearTimeout(reconnectTimer);
        if (connection && connection.state !== signalR.HubConnectionState.Disconnected) {
            await connection.stop();
        }
        notifyLiveState('inactive');
    }

    root.addEventListener('krackend:dashboard-tracking', event => {
        state.tracking = Boolean(event.detail?.enabled);
        if (state.tracking) { void loadDashboard(); void start(); }
        else void stop();
    });

    if (state.tracking) {
        void loadDashboard({ showLoading: true });
        void start();
    }

    window.addEventListener('pagehide', () => {
        closed = true;
        window.clearTimeout(statisticsTimer);
        window.clearTimeout(reconnectTimer);
        window.clearTimeout(failuresTimer);
        requests.forEach(controller => controller.abort());
        void stop();
    }, { once: true });

})();
