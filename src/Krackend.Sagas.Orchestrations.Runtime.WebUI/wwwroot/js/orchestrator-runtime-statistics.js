(() => {
    const root = document.querySelector('[data-runtime-statistics]');
    const config = window.KrackendRuntimeDashboard;
    if (!root || !config) return;

    const orchestration = root.querySelector('[data-statistics-orchestration]');
    const version = root.querySelector('[data-statistics-version]');
    const orchestrationSuggestions = root.querySelector('[data-statistics-orchestration-suggestions]');
    const versionSuggestions = root.querySelector('[data-statistics-version-suggestions]');
    const load = root.querySelector('[data-statistics-load] button');
    const tracking = root.querySelector('[data-statistics-tracking]');
    const liveCopy = root.querySelector('[data-statistics-live-copy]');
    let debounce = 0;
    let isTracking = false;

    const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[character]));
    const setBusy = busy => {
        if (!load) return;
        load.disabled = busy;
        load.dataset.loading = String(busy);
        load.setAttribute('aria-busy', String(busy));
        load.dataset.zProgressState = busy ? 'in-progress' : 'idle';
        load.dataset.zProgressIndeterminate = String(busy);
        load.style.setProperty('--z-button-progress', busy ? '100%' : '0%');
    };
    const closeSuggestions = element => { if (element) { element.replaceChildren(); element.classList.add('d-none'); } };

    async function suggestions(source, search) {
        const url = new URL(source, window.location.href);
        url.searchParams.set('search', search);
        url.searchParams.set('take', '20');
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) return [];
        const result = await response.json();
        return Array.isArray(result.items) ? result.items : [];
    }

    function showSuggestions(container, items, onSelect) {
        if (!container) return;
        if (!items.length) return closeSuggestions(container);
        container.innerHTML = items.map(item => `<button type="button" class="od-autocomplete-item" data-value="${escapeHtml(item)}"><span class="od-autocomplete-title">${escapeHtml(item)}</span></button>`).join('');
        container.classList.remove('d-none');
        container.onclick = event => {
            const choice = event.target.closest('[data-value]');
            if (!choice) return;
            onSelect(choice.dataset.value || '');
            closeSuggestions(container);
        };
    }

    function queueSearch(input, source, container, onSelect, dependency = '') {
        window.clearTimeout(debounce);
        debounce = window.setTimeout(async () => {
            if (!source || (input === version && !dependency)) return closeSuggestions(container);
            const url = new URL(source, window.location.href);
            if (input === version) url.searchParams.set('orchestrationKey', dependency);
            const items = await suggestions(url, input.value.trim());
            showSuggestions(container, items, onSelect);
        }, 180);
    }

    orchestration?.addEventListener('input', () => {
        version.value = '';
        version.disabled = !orchestration.value.trim();
        closeSuggestions(versionSuggestions);
        queueSearch(orchestration, config.suggestionsUrl, orchestrationSuggestions, selected => {
            orchestration.value = selected;
            version.disabled = false;
        });
    });
    version?.addEventListener('input', () => queueSearch(version, config.versionsUrl, versionSuggestions, selected => { version.value = selected; }, orchestration.value.trim()));
    orchestration?.addEventListener('focus', () => queueSearch(orchestration, config.suggestionsUrl, orchestrationSuggestions, selected => { orchestration.value = selected; version.disabled = false; }));
    version?.addEventListener('focus', () => queueSearch(version, config.versionsUrl, versionSuggestions, selected => { version.value = selected; }, orchestration.value.trim()));

    async function loadScope() {
        const key = orchestration?.value.trim();
        if (!key) {
            orchestration?.focus();
            return;
        }
        setBusy(true);
        root.dispatchEvent(new CustomEvent('krackend:dashboard-load', { bubbles: false, detail: { orchestrationKey: key, orchestrationVersion: version?.value.trim() || '' } }));
    }

    load?.addEventListener('click', loadScope);
    root.addEventListener('krackend:dashboard-loaded', () => setBusy(false));
    tracking?.addEventListener('click', () => {
        isTracking = !isTracking;
        tracking.setAttribute('aria-pressed', String(isTracking));
        tracking.classList.toggle('is-active', isTracking);
        const label = tracking.querySelector('span');
        if (label) label.textContent = isTracking ? 'Tracking realtime' : 'Track realtime';
        if (liveCopy) liveCopy.textContent = isTracking ? 'Live changes for this scope are applied without reloading the dashboard.' : 'Realtime tracking is off until you enable it.';
        root.dispatchEvent(new CustomEvent('krackend:dashboard-tracking', { bubbles: false, detail: { enabled: isTracking } }));
    });
})();
