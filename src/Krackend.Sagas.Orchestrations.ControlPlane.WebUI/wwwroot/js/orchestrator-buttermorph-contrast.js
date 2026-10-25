(() => {
    document.addEventListener('load', event => {
        const frame = event.target;
        if (!(frame instanceof HTMLIFrameElement) || frame.id !== 'buttermorph-host-frame') return;
        try {
            const body = frame.contentDocument?.body;
            if (!body) return;
            const foreground = getComputedStyle(document.documentElement).getPropertyValue('--krackend-on-primary').trim();
            if (foreground) body.style.setProperty('--bm-on-primary', foreground);
        } catch {
            // An external host cannot be styled by the parent page.
        }
    }, true);
})();
