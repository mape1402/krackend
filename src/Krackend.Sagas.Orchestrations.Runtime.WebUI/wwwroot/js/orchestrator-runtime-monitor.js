(() => {
    const config = window.KrackendRuntimeMonitor;
    const indicator = document.querySelector('[data-live-state]');
    const label = document.querySelector('[data-live-label]');
    if (!config || !indicator || !label) return;

    const setState = (state, text) => { indicator.dataset.liveState = state; label.textContent = text; };
    if (!window.signalR) { setState('disconnected', 'Offline'); return; }
    const connection = new signalR.HubConnectionBuilder().withUrl(config.hubPath).withAutomaticReconnect().build();
    let retryTimer;
    const retry = () => { window.clearTimeout(retryTimer); retryTimer = window.setTimeout(start, 5000); };
    const changed = detail => window.dispatchEvent(new CustomEvent('krackend:instances-changed', { detail }));
    const subscribe = async () => {
        const synchronized = await connection.invoke('WatchRuntime');
        setState(synchronized === false ? 'degraded' : 'connected', synchronized === false ? 'Local only' : 'Live');
        changed({ resync: true });
    };
    connection.on('runtime.transition', event => changed({ event }));
    connection.on('runtime.resync', () => changed({ resync: true }));
    connection.on('runtime.fanout.health', synchronized => setState(synchronized ? 'connected' : 'degraded', synchronized ? 'Live' : 'Local only'));
    connection.onreconnecting(() => setState('connecting', 'Reconnecting'));
    connection.onreconnected(() => subscribe().catch(() => connection.stop()));
    connection.onclose(() => { setState('disconnected', 'Offline'); retry(); });
    async function start() {
        try { await connection.start(); await subscribe(); }
        catch {
            setState('disconnected', 'Offline');
            if (connection.state !== signalR.HubConnectionState.Disconnected) await connection.stop();
            retry();
        }
    }
    start();
})();
