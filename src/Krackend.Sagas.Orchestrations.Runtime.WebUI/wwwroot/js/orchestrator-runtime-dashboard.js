(() => {
    const config = window.KrackendRuntimeDashboard;
    const indicator = document.querySelector('[data-dashboard-live-state]');
    const label = document.querySelector('[data-dashboard-live-label]');
    const list = document.querySelector('[data-dashboard-live-list]');
    const empty = document.querySelector('[data-dashboard-live-empty]');
    if (!config || !indicator || !label || !list) return;
    const setState = (state, text) => { indicator.dataset.liveState = state; label.textContent = text; };
    if (!window.signalR) { setState('disconnected', 'Offline'); return; }
    const connection = new signalR.HubConnectionBuilder().withUrl(config.hubPath).withAutomaticReconnect().build();
    let timer;
    let refreshTimer;
    let closed = false;
    const retry = () => { if (!closed) { clearTimeout(timer); timer = setTimeout(start, 5000); } };
    const refresh = () => {
        clearTimeout(refreshTimer);
        refreshTimer = setTimeout(async () => {
            try {
                const response = await fetch(config.statsPath, { headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error('Statistics unavailable');
                renderStatistics(await response.json());
            } catch { const freshness = document.querySelector('[data-dashboard-freshness]'); if (freshness) freshness.textContent = 'Statistics unavailable'; }
        }, 300);
    };
    const subscribe = async () => { const synchronized = await connection.invoke('WatchRuntime'); setState(synchronized === false ? 'degraded' : 'connected', synchronized === false ? 'Local only' : 'Live'); refresh(); };
    const replaceChart = (id, series) => document.getElementById(id)?.dispatchEvent(new CustomEvent('zoorce:chart:replace', { detail: { series } }));
    function renderStatistics(report) {
        const status = report.currentStatuses || {};
        const reached = report.reachedStatuses || {};
        const counts = [report.total, (status.Created || 0) + (status.Running || 0), status.Waiting || 0,
            status.Completed || 0, status.DeadLettered || 0, status.Failed || 0];
        counts.forEach((value, index) => { const element = document.querySelector(`[data-dashboard-metric="${index}"]`); if (element) element.textContent = Number(value).toLocaleString(); });
        const freshness = document.querySelector('[data-dashboard-freshness]');
        if (freshness) freshness.textContent = report.backfillCompleted ? `${report.pendingObservations} pending observations` : 'Preparing historical baseline';
        const start = new Date(report.fromUtc.endsWith('Z') ? report.fromUtc : `${report.fromUtc}Z`);
        const end = new Date(report.beforeUtc.endsWith('Z') ? report.beforeUtc : `${report.beforeUtc}Z`);
        const points = [];
        for (let date = new Date(start); date < end; date.setUTCDate(date.getUTCDate() + 1)) {
            points.push({ key: date.toISOString().slice(0, 10), label: date.toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' }) });
        }
        const activity = report.activity || [];
        replaceChart('runtime-activity-chart', [['Completed', 'Completed'], ['Dead lettered', 'DeadLettered'], ['Failed', 'Failed']].map(([name, state]) => ({
            name, points: points.map(day => ({ label: day.label, value: activity.filter(item => item.dayUtc.slice(0, 10) === day.key && item.status === state).reduce((sum, item) => sum + item.count, 0) }))
        })));
        replaceChart('runtime-status-chart', [{ name: 'Instances', points: Object.entries(status).map(([name, value]) => ({ label: name, value })) }]);
        replaceChart('runtime-funnel-chart', [{ name: 'Instances', points: [{ label: 'Started', value: report.total }, { label: 'Completed', value: reached.Completed || 0 }] }]);
    }
    connection.on('runtime.statistics.updated', refresh);
    connection.on('runtime.resync', refresh);
    connection.on('runtime.fanout.health', synchronized => setState(synchronized ? 'connected' : 'degraded', synchronized ? 'Live' : 'Local only'));
    connection.on('runtime.transition', event => {
        if (!event || (config.orchestrationKey && event.orchestrationDefinitionKey !== config.orchestrationKey)) return;
        const item = document.createElement('li');
        const detail = document.createElement('div');
        const title = document.createElement('strong');
        title.textContent = event.orchestrationDefinitionKey || 'Orchestration';
        const description = document.createElement('span');
        description.textContent = [event.stageKey, event.taskKey, event.transitionType].filter(Boolean).join(' · ');
        const time = document.createElement('time');
        const occurred = new Date(event.occurredOnUtc);
        time.textContent = Number.isNaN(occurred.getTime()) ? '' : occurred.toLocaleTimeString();
        detail.append(title, description);
        if (event.orchestrationInstanceId && config.instancePath) {
            const link = document.createElement('a');
            link.href = config.instancePath.replace('__instance_id__', encodeURIComponent(event.orchestrationInstanceId));
            link.setAttribute('aria-label', `Open ${event.orchestrationDefinitionKey || 'orchestration'} instance`);
            link.append(detail);
            item.append(link, time);
        } else item.append(detail, time);
        list.prepend(item);
        while (list.children.length > 10) list.lastElementChild.remove();
        if (empty) empty.hidden = true;
    });
    connection.onreconnecting(() => setState('connecting', 'Reconnecting'));
    connection.onreconnected(() => subscribe().catch(() => connection.stop()));
    connection.onclose(() => { setState('disconnected', 'Offline'); retry(); });
    async function start() {
        try { await connection.start(); await subscribe(); }
        catch { setState('disconnected', 'Offline'); if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop(); retry(); }
    }
    window.addEventListener('pagehide', () => { closed = true; clearTimeout(timer); clearTimeout(refreshTimer); connection.stop(); }, { once: true });
    start();
})();
