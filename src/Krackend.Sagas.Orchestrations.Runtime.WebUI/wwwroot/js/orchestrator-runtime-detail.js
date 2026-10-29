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
    root.addEventListener('zoorce:timeline:select', event => {
        const id = event.detail?.itemId;
        const panels = root.querySelectorAll('[data-transition-details]');
        let found = false;
        panels.forEach(panel => { const selected = panel.dataset.transitionDetails === id; panel.hidden = !selected; found ||= selected; });
        const placeholder = root.querySelector('[data-transition-placeholder]');
        if (placeholder) placeholder.hidden = found;
        if (found) setDrawer(true);
    });

    // Pretty-print valid JSON without introducing a second editor dependency.
    // Content arrives Razor-encoded, so token markup is generated only from text.
    root.querySelectorAll('[data-json-view]').forEach(view => {
        const source = view.textContent.trim();
        if (!source) { view.textContent = 'No data recorded.'; return; }
        try { view.textContent = JSON.stringify(JSON.parse(source), null, 2); }
        catch { /* Non-JSON diagnostic data remains readable as supplied. */ }
    });

    // Details share the runtime SignalR connection.  A matching transition does
    // not reload the page while an operator is reading it; it marks freshness
    // discreetly and offers the browser's normal refresh path.
    if (window.signalR && root.dataset.liveHub) {
        const connection = new signalR.HubConnectionBuilder().withUrl(root.dataset.liveHub).withAutomaticReconnect().build();
        connection.on('runtime.transition', event => {
            const eventInstance = event?.orchestrationInstanceId ?? event?.OrchestrationInstanceId;
            if (String(eventInstance || '') !== root.dataset.instanceId) return;
            root.dataset.liveUpdate = 'true';
        });
        connection.start().then(() => connection.invoke('WatchRuntime')).catch(() => { /* Detail remains usable offline. */ });
    }
})();
