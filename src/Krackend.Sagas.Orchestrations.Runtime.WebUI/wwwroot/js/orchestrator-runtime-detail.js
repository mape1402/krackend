(() => {
    const root = document.querySelector('.od-instance-trace');
    if (!root) return;
    const copy = async value => {
        if (navigator.clipboard?.writeText) return navigator.clipboard.writeText(value);
        const input = document.createElement('textarea');
        input.value = value; input.readOnly = true; input.style.cssText = 'position:fixed;opacity:0';
        document.body.append(input); input.select(); const copied = document.execCommand('copy'); input.remove();
        if (!copied) throw new Error('Clipboard unavailable');
    };
    root.addEventListener('click', async event => {
        const button = event.target.closest('[data-copy-value]');
        if (!button) return;
        try { await copy(button.dataset.copyValue); button.classList.add('is-copied'); setTimeout(() => button.classList.remove('is-copied'), 1100); }
        catch { button.title = 'Clipboard unavailable'; }
    });
    const drawer = root.querySelector('[data-transition-drawer]');
    const drawerToggle = root.querySelector('[data-transition-drawer-toggle]');
    const setDrawer = open => {
        if (!drawer) return;
        drawer.hidden = !open;
        drawerToggle?.setAttribute('aria-expanded', String(open));
    };
    drawerToggle?.addEventListener('click', () => setDrawer(drawer?.hidden));
    root.querySelector('[data-transition-drawer-close]')?.addEventListener('click', () => setDrawer(false));
    root.querySelector('[data-recover-dialog-open]')?.addEventListener('click', event =>
        document.getElementById('recoverSagaDialog')?.open?.(event.currentTarget));
    root.addEventListener('zoorce:timeline:select', event => {
        const id = event.detail?.itemId;
        const panels = root.querySelectorAll('[data-transition-details]');
        let found = false;
        panels.forEach(panel => { const selected = panel.dataset.transitionDetails === id; panel.hidden = !selected; found ||= selected; });
        const placeholder = root.querySelector('[data-transition-placeholder]');
        if (placeholder) placeholder.hidden = found;
        if (found) setDrawer(true);
    });

    // Pretty-print valid JSON with safe token nodes. Content arrives Razor-encoded
    // and this renderer never assigns JSON as HTML, so diagnostic payloads remain
    // copyable without opening an XSS surface.
    const appendJsonToken = (fragment, text, className) => {
        const span = document.createElement('span');
        span.className = className;
        span.textContent = text;
        fragment.append(span);
    };
    const renderJson = (view, source) => {
        const formatted = JSON.stringify(JSON.parse(source), null, 2);
        const fragment = document.createDocumentFragment();
        const token = /("(?:\\.|[^"\\])*")(?=\s*:)|("(?:\\.|[^"\\])*")|(-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|\b(true|false|null)\b/g;
        let cursor = 0;
        for (let match; (match = token.exec(formatted));) {
            fragment.append(document.createTextNode(formatted.slice(cursor, match.index)));
            if (match[1]) appendJsonToken(fragment, match[1], 'json-key');
            else if (match[2]) appendJsonToken(fragment, match[2], 'json-string');
            else if (match[3]) appendJsonToken(fragment, match[3], 'json-number');
            else appendJsonToken(fragment, match[4], match[4] === 'null' ? 'json-null' : 'json-boolean');
            cursor = match.index + match[0].length;
        }
        fragment.append(document.createTextNode(formatted.slice(cursor)));
        view.replaceChildren(fragment);
    };
    root.querySelectorAll('[data-json-view]').forEach(view => {
        const source = view.textContent.trim();
        if (!source) { view.textContent = 'No data recorded.'; return; }
        try { renderJson(view, source); }
        catch { /* Non-JSON diagnostic data remains readable as supplied. */ }
    });

    const eventValue = (event, name) => event?.[name] ?? event?.[`${name.charAt(0).toUpperCase()}${name.slice(1)}`];
    const toTimestamp = value => {
        const timestamp = new Date(value).getTime();
        return Number.isNaN(timestamp) ? 0 : timestamp;
    };
    const formatUtc = value => {
        const text = typeof value === 'string' && !/(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? `${value}Z` : value;
        const date = new Date(text);
        return Number.isNaN(date.getTime()) ? '—' : date.toISOString().replace('T', ' ').substring(0, 19) + ' UTC';
    };
    const statusClass = status => ({
        Running: 'od-status-running', Idle: 'od-status-warning', Completed: 'od-status-active',
        DeadLettered: 'od-status-warning', Failed: 'od-status-danger'
    }[String(status || '')] || 'od-status-inactive');
    const toPublicStatus = status => {
        const value = String(status || 'Running');
        if (['Created', 'Running', 'Waiting', 'WaitingResponse', 'AwaitingResponse', 'Retrying', 'Compensating'].includes(value)) return 'Running';
        if (value === 'Idle') return 'Idle';
        if (['Completed', 'Compensated'].includes(value)) return 'Completed';
        if (value === 'DeadLettered') return 'DeadLettered';
        if (['Failed', 'Stopped', 'Aborted', 'CompletedWithErrors', 'TimedOut', 'Cancelled'].includes(value)) return 'Failed';
        return value;
    };
    const applyLiveEvent = event => {
        const eventInstance = eventValue(event, 'orchestrationInstanceId');
        if (String(eventInstance || '') !== root.dataset.instanceId) return;
        const occurred = eventValue(event, 'lastUpdatedOnUtc') || eventValue(event, 'occurredOnUtc');
        const prior = toTimestamp(root.dataset.liveEventTimestamp);
        if (toTimestamp(occurred) && toTimestamp(occurred) < prior) return;
        root.dataset.liveEventTimestamp = occurred || root.dataset.liveEventTimestamp || '';

        const status = toPublicStatus(eventValue(event, 'instanceStatus'));
        const statusElement = root.querySelector('[data-instance-status]');
        if (statusElement && status) {
            statusElement.textContent = status;
            statusElement.className = `od-status ${statusClass(status)}`;
        }
        const updated = root.querySelector('[data-instance-last-updated]');
        if (updated && occurred) updated.textContent = formatUtc(occurred);

        const notice = root.querySelector('[data-instance-live-update]');
        const noticeText = root.querySelector('[data-instance-live-update-text]');
        if (notice && noticeText) {
            noticeText.textContent = `Live update · ${eventValue(event, 'transitionType') || 'transition'} at ${formatUtc(eventValue(event, 'occurredOnUtc'))}`;
            notice.hidden = false;
        }
    };

    // The detail follows one saga only. A runtime-wide group makes an open
    // detail page process every message in a busy node and was the reason the
    // former indicator never represented useful freshness.
    if (window.signalR && root.dataset.liveHub) {
        const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.liveHub).withAutomaticReconnect().build();
        connection.on('runtime.transition', applyLiveEvent);
        connection.onreconnected(() => connection.invoke('WatchInstance', root.dataset.instanceId).catch(() => { /* Detail remains usable offline. */ }));
        connection.start().then(() => connection.invoke('WatchInstance', root.dataset.instanceId)).catch(() => { /* Detail remains usable offline. */ });
    }
})();
