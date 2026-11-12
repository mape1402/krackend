(() => {
    const sections = [...document.querySelectorAll('[data-roadmap-section]')];
    if (!sections.length) return;
    const message = (title, text, type = 'info') => window.OrchestratorMessages?.notify({ title, message: text, type });
    const toast = document.querySelector('[data-roadmap-toast]');
    let toastTimer;
    const showToast = text => {
        if (!toast) return;
        toast.textContent = text;
        toast.hidden = false;
        window.clearTimeout(toastTimer);
        toastTimer = window.setTimeout(() => { toast.hidden = true; }, 3000);
    };
    try {
        if (sessionStorage.getItem('krackend-roadmap-copied')) {
            sessionStorage.removeItem('krackend-roadmap-copied');
            showToast('Roadmap element created.');
        }
    } catch { /* Browser storage is optional. */ }

    const urlFor = (handler, values = {}) => {
        const url = new URL(window.location.href);
        url.searchParams.set('handler', handler);
        Object.entries(values).forEach(([key, value]) => url.searchParams.set(key, value || ''));
        return url;
    };
    const responseJson = async response => {
        const body = await response.json();
        if (!response.ok) throw new Error(body.message || 'The operation failed.');
        return body;
    };
    const loadPackage = async (section, type, id) => {
        const url = urlFor('RoadmapElement', { elementType: type, elementId: id, versionId: section.dataset.versionId });
        const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
        return (await responseJson(response)).package;
    };
    const paste = async (section, packageText) => {
        let packageData;
        try { packageData = JSON.parse(packageText); } catch { throw new Error('The clipboard does not contain a roadmap element.'); }
        if (packageData.kind !== 'krackend-roadmap-element' || packageData.schemaVersion !== 1 || packageData.elementType !== section.dataset.roadmapSection)
            throw new Error(`Copy a ${section.dataset.roadmapSection} before pasting here.`);
        const response = await fetch(urlFor('PasteRoadmapElement'), {
            method: 'POST', credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': document.querySelector('meta[name="request-verification-token"]')?.content || '' },
            body: JSON.stringify({ package: packageText, elementType: section.dataset.roadmapSection, versionId: section.dataset.versionId, stageId: section.dataset.stageId || '' })
        });
        await responseJson(response);
        try { sessionStorage.setItem('krackend-roadmap-copied', '1'); } catch { /* Browser storage is optional. */ }
        window.location.reload();
    };
    const readClipboard = async () => {
        try {
            if (navigator.clipboard?.readText) return await navigator.clipboard.readText();
        } catch { /* Offer a manual paste when browser permission is unavailable. */ }
        return window.prompt('Paste the copied roadmap element here (Ctrl+V):') || '';
    };
    const doPaste = async section => {
        const text = await readClipboard();
        if (text) await paste(section, text);
    };
    const run = operation => Promise.resolve().then(operation).catch(error => message('Roadmap', error.message || 'The operation failed.', 'error'));
    document.addEventListener('zoorce:speed-dial:select', event => {
        const target = { 'paste-trigger': 'trigger', 'paste-stage': 'stage', 'paste-task': 'task' }[event.detail?.value];
        if (!target || !['roadmap-actions-dial', 'task-actions-dial'].includes(event.target.id)) return;
        const section = sections.find(item => item.dataset.roadmapSection === target && item.dataset.editable === 'true');
        if (section) run(() => doPaste(section));
    });
    document.addEventListener('click', event => {
        const button = event.target.closest('.js-roadmap-copy, .js-roadmap-clone');
        if (!button) return;
        event.preventDefault();
        event.stopPropagation();
        const section = button.closest('[data-roadmap-section]');
        if (!section) return;
        run(async () => {
            const data = await loadPackage(section, button.dataset.elementType, button.dataset.elementId);
            if (button.classList.contains('js-roadmap-clone')) return paste(section, data);
            try {
                if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
                await navigator.clipboard.writeText(data);
                showToast(`${button.dataset.elementType[0].toUpperCase()}${button.dataset.elementType.slice(1)} copied to clipboard.`);
            } catch { window.prompt('Copy the roadmap element (Ctrl+C):', data); }
        });
    });

})();
