(() => {
    window.KrackendButterMorphContext = async context => {
        try {
            const url = new URL(window.location.href);
            url.searchParams.set('handler', 'ButterMorphContext');
            url.searchParams.set('context', context);
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) throw new Error('The current schema context could not be loaded.');
            const result = await response.json();
            if (!result.context) throw new Error('The current schema context is invalid.');
            return result.context;
        } catch {
            window.OrchestratorMessages?.notify({
                title: 'ButterMorph unavailable',
                message: 'The current schema context could not be loaded. Please try again.',
                type: 'error'
            });
            return null;
        }
    };
})();
