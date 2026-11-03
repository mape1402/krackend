(() => {
    const sections = [...document.querySelectorAll('[data-roadmap-section]')];
    if (!sections.length) return;
    const message = (title, text, type = 'info') => window.OrchestratorMessages?.notify({ title, message: text, type });
    const notice = 'Tasks are copied outside parallel groups. Only literal true/false conditions are kept; transformations, navigation rules and parallel groups are not copied.';
    try {
        if (sessionStorage.getItem('krackend-roadmap-copied')) {
            sessionStorage.removeItem('krackend-roadmap-copied');
            message('Roadmap element created', notice);
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
    document.addEventListener('click', event => {
        const button = event.target.closest('.js-roadmap-copy, .js-roadmap-clone, .js-roadmap-paste');
        if (!button) return;
        event.preventDefault();
        event.stopPropagation();
        const section = button.closest('[data-roadmap-section]');
        if (!section) return;
        run(async () => {
            if (button.classList.contains('js-roadmap-paste')) return doPaste(section);
            const data = await loadPackage(section, button.dataset.elementType, button.dataset.elementId);
            if (button.classList.contains('js-roadmap-clone')) return paste(section, data);
            try {
                if (!navigator.clipboard?.writeText) throw new Error('Clipboard unavailable');
                await navigator.clipboard.writeText(data);
                message('Copied', notice);
            } catch { window.prompt('Copy the roadmap element (Ctrl+C):', data); }
        });
    });

    const menu = document.createElement('div');
    menu.className = 'dropdown-menu show';
    menu.hidden = true;
    menu.style.display = 'none';
    menu.style.position = 'fixed';
    menu.style.zIndex = '2000';
    const pasteButton = document.createElement('button');
    pasteButton.type = 'button';
    pasteButton.className = 'dropdown-item';
    menu.appendChild(pasteButton);
    document.body.appendChild(menu);
    let activeSection;
    sections.forEach(section => section.addEventListener('contextmenu', event => {
        if (section.dataset.editable !== 'true' || event.target.closest('input, textarea, select, [contenteditable="true"]')) return;
        event.preventDefault();
        activeSection = section;
        pasteButton.textContent = `Paste ${section.dataset.roadmapSection}`;
        menu.style.left = `${Math.min(event.clientX, window.innerWidth - 160)}px`;
        menu.style.top = `${Math.min(event.clientY, window.innerHeight - 48)}px`;
        menu.hidden = false;
        menu.style.display = 'block';
        pasteButton.focus();
    }));
    const hideMenu = () => { menu.hidden = true; menu.style.display = 'none'; };
    pasteButton.addEventListener('click', () => { hideMenu(); run(() => doPaste(activeSection)); });
    document.addEventListener('click', event => { if (!menu.contains(event.target)) hideMenu(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape') hideMenu(); });
})();
