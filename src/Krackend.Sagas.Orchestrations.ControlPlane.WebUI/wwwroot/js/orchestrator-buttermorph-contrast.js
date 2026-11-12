(() => {
    // ButterMorph 2.1.5 still uses fixed light text in parts of its designer.
    // Keep the host palette and adapt those selectors to ButterMorph's own tokens.
    const rules = `
        body[data-bm-theme-mode] .bm-topbar h1,
        body[data-bm-theme-mode] .bm-dock-titlebar,
        body[data-bm-theme-mode] .bm-panel-title,
        body[data-bm-theme-mode] .event-editor > .platform-card-header,
        body[data-bm-theme-mode] .event-editor > .platform-card-header span {
            color: var(--bm-sidebar-text);
        }
        body[data-bm-theme-mode] .bm-topbar .bm-document-import-button,
        body[data-bm-theme-mode] .bm-topbar .bm-document-import-button:hover {
            color: var(--bm-sidebar-text);
        }
        body[data-bm-theme-mode] .bm-topbar .bm-view-button {
            background: transparent;
            border-color: transparent;
            color: var(--bm-sidebar-text);
        }
        body[data-bm-theme-mode] .bm-topbar .bm-view-button:hover,
        body[data-bm-theme-mode] .bm-workbench[data-active-view="Visual"] .bm-view-button[data-view="Visual"],
        body[data-bm-theme-mode] .bm-workbench[data-active-view="Dsl"] .bm-view-button[data-view="Dsl"] {
            background: var(--bm-sidebar-active);
            border-color: var(--bm-sidebar-accent-border);
            color: var(--bm-sidebar-text);
        }
        body[data-bm-theme-mode] :is(.bm-dock-pin, .bm-toolbox-pin):hover,
        body[data-bm-theme-mode] .bm-toolbox-actions button:hover {
            color: var(--bm-sidebar-text);
        }
        body[data-bm-theme-mode="dark"] .bm-array-mapping:not(.bm-drop-hover) {
            background-color: var(--bm-schema-panel-soft, var(--bm-surface));
        }
        body[data-bm-theme-mode] :is(.bm-message-action, .bm-target-form .bm-canvas-header button[type="submit"], .bm-button-primary, .btn-primary),
        body[data-bm-theme-mode="dark"] .bm-workbench button[type="submit"],
        body[data-bm-theme-mode] .object-schema-breadcrumb button.active,
        body[data-bm-theme-mode] input[type="file"]::file-selector-button {
            color: var(--bm-on-primary);
        }
        body[data-bm-theme-mode] :is(.bm-message-action, .bm-target-form .bm-canvas-header button[type="submit"], .bm-button-primary, .btn-primary):hover,
        body[data-bm-theme-mode] input[type="file"]:hover::file-selector-button {
            color: var(--bm-on-primary-hover, var(--bm-on-primary));
        }
    `;

    const apply = frame => {
        if (!(frame instanceof HTMLIFrameElement) || frame.id !== 'buttermorph-host-frame') return;
        try {
            const frameDocument = frame.contentDocument;
            if (!frameDocument?.head || !frameDocument.body) return;
            if (!frameDocument.getElementById('krackend-buttermorph-contrast')) {
                const style = frameDocument.createElement('style');
                style.id = 'krackend-buttermorph-contrast';
                style.textContent = rules;
                frameDocument.head.appendChild(style);
            }
            const colors = getComputedStyle(document.documentElement);
            for (const [source, target] of [
                ['--krackend-on-primary', '--bm-on-primary'],
                ['--krackend-on-primary-hover', '--bm-on-primary-hover']
            ]) {
                const value = colors.getPropertyValue(source).trim();
                if (value) frameDocument.body.style.setProperty(target, value);
            }
        } catch {
            // An external host cannot be styled by the parent page.
        }
    };

    document.addEventListener('load', event => {
        apply(event.target);
    }, true);
    document.addEventListener('zoorce:theme-change', () => apply(document.getElementById('buttermorph-host-frame')));
})();
