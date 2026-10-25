(() => {
    const dialog = document.getElementById('reviewCatalogDialog');
    if (!dialog) return;
    let request = null;

    const statusTone = value => ({ Approved: 'success', Applied: 'success', Rejected: 'danger', Aborted: 'danger', Pending: 'warning' }[value] || 'info');
    const render = data => {
        dialog.querySelectorAll('[data-review-value]').forEach(element => {
            const key = element.dataset.reviewValue;
            const value = data[key] ?? '';
            element.textContent = value || '-';
            if (key === 'approval' || key === 'application') element.className = `z-status z-status-${statusTone(value)}`;
        });
        dialog.querySelectorAll('[data-review-optional]').forEach(element => {
            element.hidden = !data[element.dataset.reviewOptional];
        });
        const visibility = { approve: data.canApprove, reject: data.canApprove, abort: data.canAbort,
            deploy: data.canApply && data.operation === 'Deploy', apply: data.canApply && data.operation !== 'Deploy' };
        dialog.querySelectorAll('[data-review-action]').forEach(element => { element.hidden = !visibility[element.dataset.reviewAction]; });
        const roadmap = dialog.querySelector('[data-review-roadmap]');
        roadmap.href = data.roadmapUrl;
        document.querySelectorAll('[data-review-post]').forEach(form => {
            const action = new URL(data.detailsUrl, location.origin);
            action.searchParams.set('handler', form.dataset.reviewPost);
            form.action = action.toString();
            form.elements.reviewId.value = data.id;
        });
        ['catalogReviewRejectForm', 'catalogReviewAbortForm', 'catalogReviewDeployForm'].forEach(id => document.getElementById(id)?.reset());
        document.querySelectorAll('[data-review-post] [name="reviewId"]').forEach(input => { input.value = data.id; });
    };

    document.addEventListener('click', async event => {
        const link = event.target.closest('a[data-review-id]');
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault();
        request?.abort();
        request = new AbortController();
        const url = new URL(location.pathname, location.origin);
        url.searchParams.set('handler', 'Detail');
        url.searchParams.set('reviewId', link.dataset.reviewId);
        try {
            const response = await fetch(url, { signal: request.signal, headers: { Accept: 'application/json' } });
            if (!response.ok) throw new Error('The review could not be loaded.');
            render(await response.json());
            dialog.open(link);
        } catch (error) {
            if (error.name !== 'AbortError') window.OrchestratorMessages?.notify({ title: 'Review', message: 'The review could not be loaded. Try again.', type: 'error' });
        }
    });
    dialog.addEventListener('click', event => {
        const action = event.target.closest('[data-review-action]')?.dataset.reviewAction;
        const id = { reject: 'reviewCatalogRejectDialog', abort: 'reviewCatalogAbortDialog', deploy: 'reviewCatalogDeployDialog' }[action];
        if (id) document.getElementById(id)?.open(event.target);
    });
})();
