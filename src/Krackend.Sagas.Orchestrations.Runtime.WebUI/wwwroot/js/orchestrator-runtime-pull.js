(() => {
    const root = document.querySelector('[data-pull-flow]');
    if (!root) return;
    const get = name => root.querySelector(`[data-pull-${name}]`);
    const items = get('items'), status = get('status'), more = get('more'), sentinel = get('sentinel');
    const panels = ['list', 'detail', 'confirm', 'result'];
    let page = 0, hasMore = true, loading = false, generation = 0, selected = null, applying = false, retryArtifactId = '';
    const text = (tag, value, className) => {
        const element = document.createElement(tag);
        element.textContent = value || '-';
        if (className) element.className = className;
        return element;
    };
    const typeLabel = type => ({
        'orchestration-version.new': 'New version',
        'orchestration-version-snapshot': 'New version',
        'orchestration-version.deprecated': 'Deprecate',
        'orchestration-version.archived': 'Archive'
    })[type] || type || 'Artifact';
    const effect = type => ({
        'New version': 'Install a new orchestration version in this runtime.',
        'Deprecate': 'Deprecate the installed orchestration version.',
        'Archive': 'Archive the deprecated orchestration version.'
    })[typeLabel(type)] || 'Apply this artifact to the runtime.';
    const badgeClass = type => ({ 'New version': 'z-badge-success', 'Deprecate': 'z-badge-warning', 'Archive': 'z-badge-danger' })[typeLabel(type)] || '';
    const formatDate = value => {
        if (!value || String(value).startsWith('0001-01-01')) return '-';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
    };
    const show = panel => {
        get('wizard').hidden = panel !== 'detail' && panel !== 'confirm';
        if (panel === 'detail' || panel === 'confirm') {
            window.ZoorceWizards?.get('pullReleaseWizard')?.applySnapshot({ currentStepId: panel, steps: [
                { id: 'detail', isNavigable: false, state: panel === 'detail' ? 'Active' : 'Completed' },
                { id: 'confirm', isNavigable: false, state: panel === 'confirm' ? 'Active' : 'Pending' }
            ] });
        }
        panels.forEach(name => { get(name).hidden = name !== panel; });
        if (panel !== 'list') root.querySelector(`[data-pull-${panel}] h2`)?.focus();
    };
    const facts = (target, values) => {
        target.replaceChildren();
        values.forEach(([label, value]) => {
            const group = document.createElement('div');
            group.append(text('dt', label), text('dd', value));
            target.append(group);
        });
    };
    const openDetail = item => {
        selected = item;
        get('type').textContent = typeLabel(item.artifactType);
        get('type').className = `z-badge ${badgeClass(item.artifactType)}`;
        get('name').textContent = `${item.orchestrationDefinitionKey || '-'} · ${item.version || '-'}`;
        get('effect').textContent = effect(item.artifactType);
        facts(get('facts'), [['Release', item.releaseId], ['Strategy', item.releaseStrategy],
            ['Promoted by', item.promotedBy], ['Promoted on', formatDate(item.promotedOnUtc)]]);
        facts(get('technical'), [['Target', item.releaseTargetId], ['Correlation', item.correlationId], ['Checksum', item.checksum]]);
        show('detail');
    };
    const addItem = item => {
        const row = document.createElement('article'); row.className = 'pull-row';
        const identity = document.createElement('div');
        identity.append(text('strong', item.orchestrationDefinitionKey), text('span', `Version ${item.version || '-'} · ${item.releaseId || 'Release unavailable'}`, 'pull-muted'));
        const meta = document.createElement('div'); meta.className = 'pull-row-meta';
        meta.append(text('span', typeLabel(item.artifactType), `z-badge ${badgeClass(item.artifactType)}`), text('span', item.releaseStrategy || 'Strategy unavailable', 'pull-muted'), text('span', formatDate(item.promotedOnUtc), 'pull-muted'));
        const button = text('button', 'View details', 'z-button z-button-ghost'); button.type = 'button';
        button.setAttribute('aria-label', `View ${item.orchestrationDefinitionKey || 'release'} ${item.version || ''}`);
        button.addEventListener('click', () => openDetail(item));
        row.append(identity, meta, button); items.append(row);
    };
    const load = async reset => {
        if (reset) { generation++; page = 0; hasMore = true; loading = false; items.replaceChildren(); }
        if (loading || !hasMore) return;
        const current = generation; loading = true; more.hidden = true; status.textContent = 'Loading releases...';
        get('skeleton').hidden = items.childElementCount !== 0;
        try {
            const url = new URL(root.dataset.listUrl, location.href);
            url.searchParams.set('sourceKey', root.dataset.sourceKey); url.searchParams.set('page', String(page + 1));
            const response = await fetch(url);
            if (!response.ok) {
                let message = 'Releases could not be loaded. Check the connection and try again.';
                try { message = (await response.json()).message || message; } catch { }
                throw new Error(message);
            }
            const result = await response.json();
            if (current !== generation) return;
            result.items.forEach(addItem); page++; hasMore = result.hasMore;
            status.textContent = result.totalCount ? `Showing ${items.childElementCount} of ${result.totalCount}` : 'No pending releases for this design node.';
            more.hidden = !hasMore; more.textContent = 'Load more';
        } catch (error) {
            if (current !== generation) return;
            status.textContent = error.message || 'Releases could not be loaded.';
            more.hidden = false; more.textContent = 'Retry';
        } finally {
            if (current === generation) {
                loading = false; get('skeleton').hidden = true;
                if (hasMore && more.hidden === false && more.textContent !== 'Retry') requestAnimationFrame(() => {
                    const rect = sentinel.getBoundingClientRect();
                    if (rect.width && rect.top <= innerHeight + 200) load(false);
                });
            }
        }
    };
    const result = (title, message, badge) => {
        get('result-title').textContent = title; get('result-message').textContent = message;
        get('result-status').textContent = badge;
        const url = new URL(root.dataset.artifactsUrl, location.href);
        if (selected?.orchestrationDefinitionKey) url.searchParams.set('orchestrationKey', selected.orchestrationDefinitionKey);
        get('history').href = url; show('result');
    };
    const displayDeployment = data => {
        retryArtifactId = data.acknowledgementPending ? data.runtimeArtifactId || '' : '';
        get('retry').hidden = !retryArtifactId;
        if (data.acknowledgementPending) result('Confirmation pending', 'The runtime accepted the release, but the control plane could not be notified. Retry confirmation without deploying the artifact again.', 'Attention');
        else if (!data.accepted) result('Release rejected', data.message || 'The runtime rejected this release.', 'Rejected');
        else if (['Ready', 'Retired'].includes(data.status)) result('Release applied', data.message || 'The release was applied.', data.status);
        else result('Release accepted', `${data.message || 'The runtime accepted this release.'} Check runtime artifacts for activation progress.`, data.status || 'Pending');
    };
    get('refresh').addEventListener('click', () => load(true));
    get('more').addEventListener('click', () => load(false));
    get('list-back').addEventListener('click', () => show('list'));
    get('detail-back').addEventListener('click', () => show('detail'));
    get('done').addEventListener('click', () => { show('list'); load(true); });
    get('prepare').addEventListener('click', () => {
        get('confirm-text').textContent = `${typeLabel(selected.artifactType)}: ${selected.orchestrationDefinitionKey} · ${selected.version}. ${effect(selected.artifactType)} Strategy: ${selected.releaseStrategy || 'not specified'}.`;
        show('confirm');
    });
    const applyButton = get('apply').querySelector('button');
    applyButton.addEventListener('click', async () => {
        if (applying || !selected) return;
        applying = true; applyButton.disabled = true; get('detail-back').disabled = true;
        applyButton.dataset.zProgressState = 'active'; applyButton.dataset.zProgressIndeterminate = 'true'; applyButton.setAttribute('aria-busy', 'true');
        get('progress').textContent = 'Downloading and applying the release...';
        try {
            const body = new FormData(get('form'));
            body.set('sourceKey', root.dataset.sourceKey); body.set('releaseTargetId', selected.releaseTargetId);
            const response = await fetch(root.dataset.applyUrl, { method: 'POST', body });
            if (!response.ok) throw new Error('The result could not be confirmed. Check runtime artifacts before retrying.');
            const data = await response.json();
            displayDeployment(data);
        } catch (error) { get('retry').hidden = true; result('Result unknown', error.message || 'Check runtime artifacts before retrying.', 'Attention'); }
        finally { applying = false; applyButton.disabled = false; get('detail-back').disabled = false; applyButton.dataset.zProgressState = 'idle'; applyButton.dataset.zProgressIndeterminate = 'false'; applyButton.setAttribute('aria-busy', 'false'); get('progress').textContent = ''; }
    });
    get('retry').addEventListener('click', async () => {
        if (applying || !selected || !retryArtifactId) return;
        applying = true; get('retry').disabled = true; get('done').disabled = true; get('retry').textContent = 'Retrying...';
        try {
            const body = new FormData(get('form'));
            body.set('sourceKey', root.dataset.sourceKey); body.set('releaseTargetId', selected.releaseTargetId); body.set('runtimeArtifactId', retryArtifactId);
            const response = await fetch(root.dataset.retryUrl, { method: 'POST', body });
            if (!response.ok) throw new Error('Confirmation is still pending. Check the runtime artifact and connection.');
            displayDeployment(await response.json());
        } catch (error) { result('Confirmation pending', error.message, 'Attention'); }
        finally { applying = false; get('retry').disabled = false; get('done').disabled = false; get('retry').textContent = 'Retry confirmation'; }
    });
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {
        if (!get('list').hidden && entries.some(entry => entry.isIntersecting)) load(false);
    }, { rootMargin: '200px' }).observe(sentinel);
    load(true);
})();
