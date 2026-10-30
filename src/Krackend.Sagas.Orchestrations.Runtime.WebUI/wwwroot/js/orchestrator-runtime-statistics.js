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
    const liveIndicator = root.querySelector('[data-statistics-live-indicator]');
    let debounce = 0;
    let isTracking = false;
    let scopeLoaded = false;

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
    const setTrackingAvailability = () => {
        if (!tracking) return;
        tracking.disabled = !scopeLoaded;
        tracking.setAttribute('aria-disabled', String(!scopeLoaded));
    };
    const setLiveIndicator = state => {
        if (!liveIndicator) return;
        const connected = state === 'connected' && isTracking && scopeLoaded;
        liveIndicator.hidden = !connected;
        liveIndicator.dataset.state = state || 'inactive';
    };
    const setTracking = enabled => {
        isTracking = Boolean(enabled) && scopeLoaded;
        if (tracking) {
            tracking.setAttribute('aria-pressed', String(isTracking));
            tracking.classList.toggle('is-active', isTracking);
            const label = tracking.querySelector('span');
            if (label) label.textContent = isTracking ? 'Tracking live' : 'Track live';
        }
        setLiveIndicator(isTracking ? 'reconnecting' : 'inactive');
        root.dispatchEvent(new CustomEvent('krackend:dashboard-tracking', { bubbles: false, detail: { enabled: isTracking } }));
    };
    const invalidateScope = () => {
        if (isTracking) setTracking(false);
        scopeLoaded = false;
        setTrackingAvailability();
    };

    async function suggestions(source, search) {
        const url = new URL(source, window.location.href);
        url.searchParams.set('search', search);
        url.searchParams.set('take', '20');
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        if (!response.ok) return [];
        const result = await response.json();
        return Array.isArray(result.items) ? result.items : [];
    }

    function closeAllSuggestions(except = null) {
        [orchestrationSuggestions, versionSuggestions].forEach(container => {
            if (container && container !== except) closeSuggestions(container);
        });
    }

    function showSuggestions(container, items, onSelect) {
        if (!container) return;
        if (!items.length) return closeSuggestions(container);
        closeAllSuggestions(container);
        container.innerHTML = items.map(item => `<button type="button" class="od-autocomplete-item" data-value="${escapeHtml(item)}"><span class="od-autocomplete-title">${escapeHtml(item)}</span></button>`).join('');
        container.classList.remove('d-none');
        container.onpointerdown = event => {
            const choice = event.target.closest('[data-value]');
            if (!choice) return;
            event.preventDefault();
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
        invalidateScope();
        version.value = '';
        version.disabled = !orchestration.value.trim();
        closeSuggestions(versionSuggestions);
        queueSearch(orchestration, config.suggestionsUrl, orchestrationSuggestions, selected => {
            orchestration.value = selected;
            version.disabled = false;
            invalidateScope();
        });
    });
    version?.addEventListener('input', () => {
        invalidateScope();
        queueSearch(version, config.versionsUrl, versionSuggestions, selected => { version.value = selected; invalidateScope(); }, orchestration.value.trim());
    });
    orchestration?.addEventListener('focus', () => queueSearch(orchestration, config.suggestionsUrl, orchestrationSuggestions, selected => {
        orchestration.value = selected;
        version.disabled = false;
        invalidateScope();
    }));
    version?.addEventListener('focus', () => queueSearch(version, config.versionsUrl, versionSuggestions, selected => { version.value = selected; invalidateScope(); }, orchestration.value.trim()));
    [orchestration, version].forEach(input => input?.addEventListener('blur', () => window.setTimeout(() => {
        if (!input.closest('.od-statistics-field')?.contains(document.activeElement)) closeAllSuggestions();
    }, 0)));
    const closeSuggestionsOutside = event => {
        if (!event.target.closest('[data-statistics-orchestration], [data-statistics-version], [data-statistics-orchestration-suggestions], [data-statistics-version-suggestions]')) {
            closeAllSuggestions();
        }
    };
    document.addEventListener('pointerdown', closeSuggestionsOutside);
    document.addEventListener('click', closeSuggestionsOutside);
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape') closeAllSuggestions();
    });

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
    root.addEventListener('krackend:dashboard-loaded', event => {
        setBusy(false);
        const detail = event.detail || {};
        const requestedKey = orchestration?.value.trim() || '';
        const cohortLoaded = Array.isArray(detail.results) && detail.results[0]?.status === 'fulfilled';
        scopeLoaded = Boolean(requestedKey && detail.orchestrationKey === requestedKey && cohortLoaded);
        setTrackingAvailability();
    });
    root.addEventListener('krackend:dashboard-live-state', event => setLiveIndicator(event.detail?.state));
    tracking?.addEventListener('click', () => {
        if (!scopeLoaded) return;
        setTracking(!isTracking);
    });

    setTrackingAvailability();
})();
