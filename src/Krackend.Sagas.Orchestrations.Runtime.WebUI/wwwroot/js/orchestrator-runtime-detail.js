(() => {
    const root = document.querySelector('.od-instance-trace');
    if (!root) return;
    root.addEventListener('zoorce:timeline:select', event => {
        const id = event.detail?.itemId;
        const panels = root.querySelectorAll('[data-transition-details]');
        let found = false;
        panels.forEach(panel => { const selected = panel.dataset.transitionDetails === id; panel.hidden = !selected; found ||= selected; });
        const placeholder = root.querySelector('[data-transition-placeholder]');
        if (placeholder) placeholder.hidden = found;
    });
})();
