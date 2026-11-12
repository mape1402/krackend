(() => {
    const current = () => {
        const url = new URL(location.href);
        url.searchParams.delete('returnUrl');
        url.searchParams.delete('handler');
        return url.pathname + url.search;
    };
    const catalog = () => document.querySelector('[data-catalog], [data-version-catalog], [data-artifact-catalog], [data-design-node-catalog], [data-runtime-catalog], [data-instance-history]');
    const stateFields = '[data-grid-search], [data-catalog-search], [data-catalog-sort], [data-version-search], [data-artifact-search], [data-node-search], [data-instance-search], [data-history-search], [data-history-from], [data-history-through], [data-history-orchestration], [data-history-version], [data-history-status]';
    function saveState() {
        const root = catalog();
        if (!root) return;
        const values = [...root.querySelectorAll(stateFields)].map(element => ({
            attribute: element.getAttributeNames().find(name => name.startsWith('data-')), value: element.value
        })).filter(item => item.attribute);
        try { sessionStorage.setItem(`krackend-catalog:${current()}`, JSON.stringify(values)); } catch { /* Optional state. */ }
    }
    document.addEventListener('DOMContentLoaded', () => {
        const root = catalog();
        if (!root) return;
        try {
            const values = JSON.parse(sessionStorage.getItem(`krackend-catalog:${current()}`) || '[]');
            values.forEach(({ attribute, value }) => {
                if (!/^data-[a-z-]+$/.test(attribute)) return;
                const element = root.querySelector(`[${attribute}]`);
                if (!element || element.value === value) return;
                element.value = value;
                element.dispatchEvent(new Event(element.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true }));
            });
            if (values.length && root.matches('[data-instance-history]')) root.querySelector('[data-history-filters]')?.requestSubmit();
        } catch { /* Optional state. */ }
    }, { once: true });
    document.addEventListener('click', event => {
        const link = event.target.closest('main a[href]');
        if (!link || link.hasAttribute('download') || link.target || link.hasAttribute('data-z-topbar-back')) return;
        const raw = link.getAttribute('href');
        if (!raw || raw.startsWith('#') || raw.startsWith('javascript:')) return;
        const target = new URL(link.href, location.href);
        if (target.origin !== location.origin || target.searchParams.has('handler')) return;
        const existing = new URL(location.href).searchParams.get('returnUrl');
        const sameSectionView = target.pathname === location.pathname && target.searchParams.has('section');
        if (sameSectionView && !existing) return;
        const origin = sameSectionView ? existing : current();
        if (origin.length <= 2048) target.searchParams.set('returnUrl', origin);
        saveState();
        link.href = target.pathname + target.search + target.hash;
    }, true);
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== 'post' || !form.closest('main')) return;
        saveState();
        const existing = new URL(location.href).searchParams.get('returnUrl');
        const isReview = !!document.querySelector('.od-review-detail');
        if (isReview && !existing) return;
        const origin = isReview ? existing : current();
        if (origin.length > 2048) return;
        let input = form.querySelector('input[name="returnUrl"]');
        if (!input) { input = document.createElement('input'); input.type = 'hidden'; input.name = 'returnUrl'; form.append(input); }
        input.value = origin;
    }, true);
})();
