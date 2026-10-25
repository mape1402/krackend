(() => {
    const root = document.querySelector('[data-catalog]');
    if (!root) return;
    const kind = root.dataset.catalog;
    const label = root.dataset.label;
    const preferenceKey = `krackend-${kind}-view`;
    const grid = root.querySelector('[data-z-data-grid]');
    const gridView = root.querySelector('[data-catalog-grid]');
    const cardView = root.querySelector('[data-catalog-cards]');
    const cards = root.querySelector('[data-catalog-card-items]');
    const search = root.querySelector('[data-catalog-search]');
    const sort = root.querySelector('[data-catalog-sort]');
    const status = root.querySelector('[data-catalog-status]');
    const empty = root.querySelector('[data-catalog-empty]');
    const more = root.querySelector('[data-catalog-more]');
    const form = root.querySelector('[data-catalog-action-form]');
    const sentinel = root.querySelector('[data-catalog-sentinel]');
    const gridSearch = grid.querySelector('[data-z-grid-search]');
    const records = new Map();
    let page = 0, hasMore = false, request = null, searchTimer = 0, generation = 0;
    let gridResult = null, gridRefreshTimer = 0;

    const button = (icon, label) => {
        const element = document.createElement('button');
        element.type = 'button';
        element.className = 'btn btn-sm btn-outline-secondary';
        element.title = label;
        element.setAttribute('aria-label', label);
        const glyph = document.createElement('i');
        glyph.className = `bi ${icon}`;
        glyph.setAttribute('aria-hidden', 'true');
        element.append(glyph);
        return element;
    };
    const actions = item => {
        if (kind === 'releases') {
            const details = button('bi-eye', `Details for release ${item.id}`);
            details.classList.add('js-open-release-details');
            details.dataset.releaseId = item.id;
            return details;
        }
        if (kind === 'artifacts') {
            const details = button('bi-eye', `Details for ${item.displayName} version ${item.version}`);
            details.classList.add('js-open-artifact-details');
            details.dataset.id = item.id;
            return details;
        }
        if (kind === 'runtime-nodes') {
            const group = document.createElement('div');
            group.className = 'od-catalog-actions';
            group.dataset.runtimeNode = 'true';
            Object.entries(item.node).forEach(([key, value]) => { group.dataset[key] = value ?? ''; });
            const configure = button('bi-gear', `Configure ${item.displayName}`);
            configure.classList.add('js-open-runtime-node-setup-card');
            const details = button('bi-eye', `Details for ${item.displayName}`);
            details.classList.add('js-open-runtime-node-view');
            const dropdown = document.createElement('div');
            dropdown.className = 'dropdown';
            const toggle = button('bi-three-dots-vertical', `Actions for ${item.displayName}`);
            toggle.dataset.bsToggle = 'dropdown';
            toggle.setAttribute('aria-expanded', 'false');
            const menu = document.createElement('ul');
            menu.className = 'dropdown-menu dropdown-menu-end';
            const entry = (text, formId, fields = {}, danger = false) => {
                const li = document.createElement('li');
                const action = document.createElement('button');
                action.type = 'button';
                action.className = `dropdown-item${danger ? ' text-danger' : ''}`;
                action.textContent = text;
                action.addEventListener('click', () => {
                    const target = document.getElementById(formId);
                    target.elements.runtimeNodeId.value = item.id;
                    Object.entries(fields).forEach(([key, value]) => { target.elements[key].value = value; });
                    target.requestSubmit();
                });
                li.append(action);
                menu.append(li);
            };
            if (item.node.status === 'Pending' || item.node.status === 'Enabled')
                entry('Suspend node', 'runtime-node-status-form', { status: 'Suspend' });
            if (item.node.status === 'Suspend') {
                entry('Mark pending', 'runtime-node-status-form', { status: 'Pending' });
                entry('Enable node', 'runtime-node-status-form', { status: 'Enabled' });
            }
            if (item.node.status === 'Enabled' && ['DesignPublishesToRuntime', 'HybridSync'].includes(item.node.distributionMode))
                entry('Check connection', 'runtime-node-check-form');
            entry('Delete node', 'runtime-node-delete-form', {}, true);
            dropdown.append(toggle, menu);
            group.append(configure, details, dropdown);
            return group;
        }
        if (kind === 'teams') {
            const group = document.createElement('div');
            group.className = 'od-catalog-actions';
            const edit = button('bi-pencil', `Edit ${item.displayName}`);
            edit.classList.add('js-open-team-modal');
            Object.assign(edit.dataset, { mode: 'edit', id: item.id, key: item.key,
                displayName: item.displayName, description: item.description ?? '' });
            const members = button('bi-people', `Members of ${item.displayName}`);
            members.classList.add('js-open-members-modal');
            Object.assign(members.dataset, { teamId: item.id, teamName: item.displayName });
            const change = button(item.isActive ? 'bi-pause-circle' : 'bi-play-circle',
                `${item.isActive ? 'Disable' : 'Enable'} ${item.displayName}`);
            change.addEventListener('click', () => {
                const statusForm = document.getElementById('team-status-form');
                statusForm.elements.teamId.value = item.id;
                statusForm.elements.isActive.value = String(!item.isActive);
                statusForm.requestSubmit();
            });
            group.append(edit, members, change);
            return group;
        }
        if (kind === 'environments') {
            const group = document.createElement('div');
            group.dataset.environment = 'true';
            Object.assign(group.dataset, { id: item.id, name: item.displayName, code: item.key,
                description: item.description ?? '', isEnabled: String(item.isActive) });
            const edit = button('bi-pencil', `Edit ${item.displayName}`);
            edit.classList.add('js-edit-environment');
            group.append(edit);
            return group;
        }
        if (kind === 'reviews' || kind === 'release-orchestrations' || kind === 'assignments') {
            const link = document.createElement('a');
            link.className = 'btn btn-sm btn-outline-secondary';
            link.href = item.openUrl;
            link.title = kind === 'reviews' ? 'Open review' : kind === 'assignments' ? 'View attempts' : 'Open releases';
            link.setAttribute('aria-label', `${link.title} for ${item.displayName}`);
            const icon = document.createElement('i');
            icon.className = 'bi bi-eye';
            icon.setAttribute('aria-hidden', 'true');
            link.append(icon);
            return link;
        }
        if (kind === 'orchestrations') {
            const group = document.createElement('div');
            group.className = 'dropdown js-orchestration-card';
            Object.assign(group.dataset, {
                orchestrationId: item.id, orchestrationKey: item.key, orchestrationName: item.displayName,
                orchestrationDomainId: item.domainId ?? '', orchestrationDomainDisplay: item.domain ?? '',
                orchestrationOwnerTeamId: item.ownerTeamId ?? '', orchestrationOwnerTeamDisplay: item.ownerTeam ?? '',
                orchestrationDescription: item.description ?? '', orchestrationTags: (item.tags ?? []).join(',')
            });
            const toggle = button('bi-three-dots-vertical', `Actions for ${item.displayName}`);
            toggle.dataset.bsToggle = 'dropdown';
            toggle.setAttribute('aria-expanded', 'false');
            const menu = document.createElement('ul');
            menu.className = 'dropdown-menu dropdown-menu-end';
            const entry = element => { const li = document.createElement('li'); li.append(element); menu.append(li); };
            const open = document.createElement('a');
            open.className = 'dropdown-item';
            open.href = item.openUrl;
            open.textContent = 'Open';
            entry(open);
            const edit = document.createElement('button');
            edit.type = 'button';
            edit.className = 'dropdown-item js-open-orchestration-modal';
            edit.dataset.mode = 'edit';
            edit.textContent = 'Edit';
            entry(edit);
            const change = document.createElement('button');
            change.type = 'button';
            change.className = 'dropdown-item';
            change.textContent = item.isActive ? 'Set inactive' : 'Set active';
            change.addEventListener('click', () => {
                form.elements.orchestrationId.value = item.id;
                form.action = item.isActive ? form.dataset.inactiveUrl : form.dataset.activeUrl;
                form.requestSubmit();
            });
            entry(change);
            group.append(toggle, menu);
            return group;
        }
        if (kind === 'metadata') {
            const group = document.createElement('div');
            group.className = 'dropdown';
            const toggle = button('bi-three-dots-vertical', `Actions for ${item.displayName}`);
            toggle.dataset.bsToggle = 'dropdown';
            toggle.setAttribute('aria-expanded', 'false');
            const menu = document.createElement('ul');
            menu.className = 'dropdown-menu dropdown-menu-end';
            const designItem = document.createElement('li');
            const design = document.createElement('button');
            design.type = 'button';
            design.className = 'dropdown-item js-open-payload-schema-designer';
            design.dataset.id = item.id;
            design.textContent = 'Design schema';
            designItem.append(design);
            const deleteItem = document.createElement('li');
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'dropdown-item text-danger';
            remove.textContent = 'Delete metadata';
            remove.addEventListener('click', () => {
                form.elements.metadataDescriptorId.value = item.id;
                form.requestSubmit();
            });
            deleteItem.append(remove);
            menu.append(designItem, deleteItem);
            group.append(toggle, menu);
            return group;
        }
        const group = document.createElement('div');
        group.className = 'od-catalog-actions';
        const edit = button('bi-pencil', `Edit ${item.displayName}`);
        edit.classList.add('js-open-domain-modal');
        Object.assign(edit.dataset, { mode: 'edit', id: item.id, key: item.key,
            displayName: item.displayName, description: item.description ?? '' });
        const toggle = button(item.isActive ? 'bi-pause-circle' : 'bi-play-circle',
            `${item.isActive ? 'Disable' : 'Enable'} ${item.displayName}`);
        toggle.addEventListener('click', () => {
            form.elements.domainId.value = item.id;
            form.elements.isActive.value = String(!item.isActive);
            form.requestSubmit();
        });
        group.append(edit, toggle);
        return group;
    };
    const decorateGrid = () => grid.querySelectorAll('tr[data-z-grid-key]').forEach(row => {
        const item = records.get(row.dataset.zGridKey);
        const cell = row.querySelector('[data-z-grid-field="actions"]');
        if (item && cell && !cell.firstElementChild) cell.replaceChildren(actions(item));
        if (item && kind === 'artifacts') {
            const type = row.querySelector('[data-z-grid-field="artifactType"]');
            if (type && !type.firstElementChild) {
                const badge = document.createElement('span');
                badge.className = `od-status ${item.typeClass}`;
                badge.textContent = item.typeLabel;
                type.replaceChildren(badge);
            }
        }
        if (item && (kind === 'orchestrations' || kind === 'reviews' || kind === 'release-orchestrations')) {
            const name = row.querySelector(kind === 'reviews' ? '[data-z-grid-field="orchestration"]' : '[data-z-grid-field="displayName"]');
            if (name && !name.querySelector('a')) {
                const link = document.createElement('a');
                link.href = item.openUrl;
                link.textContent = item.displayName;
                name.replaceChildren(link);
            }
        }
    });
    new MutationObserver(decorateGrid).observe(grid.querySelector('[data-z-grid-body]'), { childList: true, subtree: true });
    grid.addEventListener('zoorce:grid:loaded', event => {
        gridResult = event.detail.result;
        if (!event.detail.append) records.clear();
        event.detail.result.items.forEach(item => records.set(String(item.id), item));
        decorateGrid();
        clearTimeout(gridRefreshTimer);
        gridRefreshTimer = setTimeout(() => {
            const sorted = grid.querySelector('[data-z-grid-sort][aria-sort="ascending"], [data-z-grid-sort][aria-sort="descending"]');
            const sort = sorted?.dataset.zGridSort ?? '';
            const descending = sorted?.getAttribute('aria-sort') === 'descending';
            if (gridResult && (gridResult.search !== gridSearch.value.trim() || gridResult.sort !== sort || gridResult.descending !== descending))
                grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
        }, 220);
    });
    grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));

    const appendCard = item => {
        const card = document.createElement('article');
        card.className = 'od-item-card';
        const top = document.createElement('div');
        top.className = 'od-item-top';
        const identity = document.createElement('div');
        const name = document.createElement('h2');
        name.className = 'od-item-title';
        if (kind === 'orchestrations' || kind === 'reviews' || kind === 'release-orchestrations') {
            const link = document.createElement('a');
            link.href = item.openUrl;
            link.textContent = item.displayName;
            name.append(link);
        } else name.textContent = item.displayName;
        const key = document.createElement('p');
        key.className = 'od-item-meta';
        key.textContent = item.key;
        identity.append(name, key);
        if (item.sourceKey) {
            const source = document.createElement('p');
            source.className = 'od-item-meta';
            source.textContent = `Source: ${item.sourceKey}`;
            identity.append(source);
        }
        top.append(identity, actions(item));
        if (item.status) {
            const badge = document.createElement('span');
            badge.className = `od-status ${kind === 'releases' || kind === 'assignments' ? (item.status === 'Completed' || item.status === 'Acknowledged' ? 'od-status-completed' : item.status === 'Failed' || item.status === 'Cancelled' ? 'od-status-failed' : 'od-status-queued') : item.isActive ? 'od-status-active' : 'od-status-inactive'}`;
            badge.textContent = item.status;
            card.append(top, badge);
        } else if (kind === 'artifacts') {
            const badge = document.createElement('span');
            badge.className = `od-status ${item.typeClass}`;
            badge.textContent = item.typeLabel;
            card.append(top, badge);
        } else card.append(top);
        const description = document.createElement('p');
        description.className = 'od-item-description';
        if (kind !== 'release-orchestrations' && kind !== 'assignments') {
            description.textContent = item.description || 'No description.';
            card.append(description);
        }
        if (kind === 'release-orchestrations') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `Releases: ${item.count}${item.latest ? ` | Latest: ${item.latest}` : ''}`;
            card.append(details);
        }
        if (kind === 'releases') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `${item.strategy} | ${item.requestedBy} | ${item.created}`;
            card.append(details);
        }
        if (kind === 'assignments') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `Release: ${item.release || '-'} | Activation: ${item.activation} | ${item.assigned}`;
            card.append(details);
        }
        if (kind === 'orchestrations') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = [item.domain && `Domain: ${item.domain}`, item.ownerTeam && `Team: ${item.ownerTeam}`].filter(Boolean).join(' · ');
            card.append(details);
        }
        if (kind === 'runtime-nodes') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = [item.environment, item.node.distributionModeLabel].filter(Boolean).join(' | ');
            card.append(details);
            const readiness = document.createElement('p');
            readiness.className = 'od-item-meta';
            readiness.textContent = `Credentials: ${item.credentials} | Endpoint: ${item.endpoint}`;
            card.append(readiness);
        }
        if (kind === 'artifacts') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `Created ${item.created} | Checksum: ${item.checksum}`;
            card.append(details);
        }
        if (kind === 'reviews') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `${item.approval} | ${item.application} | ${item.requested}`;
            card.append(details);
        }
        if (kind === 'environments') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `Updated ${item.updated}`;
            card.append(details);
        }
        if (kind === 'teams') {
            const details = document.createElement('p');
            details.className = 'od-item-meta';
            details.textContent = `Members: ${item.members}`;
            card.append(details);
        }
        cards.append(card);
    };
    const load = async reset => {
        if (reset) {
            generation++;
            request?.abort();
            request = null;
            page = 0;
            cards.replaceChildren();
        }
        if (request || (!reset && !hasMore)) return;
        const currentGeneration = generation;
        const controller = new AbortController();
        request = controller;
        status.textContent = `Loading ${label}...`;
        cardView.setAttribute('aria-busy', 'true');
        try {
            const url = new URL(root.dataset.source, location.href);
            url.searchParams.set('page', String(page + 1));
            url.searchParams.set('pageSize', '25');
            url.searchParams.set('search', search.value.trim());
            const [sortField, direction] = sort.value.split(':');
            url.searchParams.set('sort', sortField);
            url.searchParams.set('descending', String(direction === 'desc'));
            const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: controller.signal });
            if (!response.ok) throw new Error(`Unable to load ${label}.`);
            const result = await response.json();
            if (generation !== currentGeneration) return;
            result.items.forEach(appendCard);
            page++;
            hasMore = result.hasMore;
            empty.hidden = cards.childElementCount !== 0;
            more.hidden = !hasMore;
            status.textContent = `Showing ${cards.childElementCount} of ${result.totalCount} ${label}`;
            if (hasMore && result.items.length) requestAnimationFrame(() => {
                const rect = sentinel.getBoundingClientRect();
                if (currentGeneration === generation && !cardView.hidden && rect.top <= window.innerHeight + 200 && rect.bottom >= -200) load(false);
            });
        } catch (error) {
            if (error.name !== 'AbortError') {
                status.textContent = `Unable to load ${label}. Please try again.`;
                more.hidden = false;
                hasMore = true;
            }
        } finally {
            if (request === controller) {
                request = null;
                cardView.removeAttribute('aria-busy');
            }
        }
    };
    search.addEventListener('input', () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => load(true), 200);
    });
    sort.addEventListener('change', () => load(true));
    if (kind === 'runtime-nodes') document.addEventListener('krackend:runtime-node-updated', () => {
        grid.dispatchEvent(new CustomEvent('zoorce:grid:refresh'));
        if (!cardView.hidden) load(true);
        else page = 0;
    });
    more.addEventListener('click', () => load(false));
    if ('IntersectionObserver' in window) {
        new IntersectionObserver(entries => {
            if (!cardView.hidden && entries.some(entry => entry.isIntersecting)) load(false);
        }, { rootMargin: '200px' }).observe(sentinel);
    }
    const setView = control => {
        const showCards = control.dataset.catalogView === 'cards';
        gridView.hidden = showCards;
        cardView.hidden = !showCards;
        root.querySelectorAll('[data-catalog-view]').forEach(item =>
            item.setAttribute('aria-pressed', String(item === control)));
        try { localStorage.setItem(preferenceKey, control.dataset.catalogView); } catch { /* Preference is optional. */ }
        if (showCards) {
            const changed = search.value !== gridSearch.value;
            search.value = gridSearch.value;
            if (changed || (page === 0 && !request)) load(true);
        } else if (gridSearch.value !== search.value) {
            gridSearch.value = search.value;
            gridSearch.dispatchEvent(new Event('input', { bubbles: true }));
        }
    };
    root.querySelectorAll('[data-catalog-view]').forEach(control => control.addEventListener('click', () => setView(control)));
    try {
        if (localStorage.getItem(preferenceKey) === 'cards') setView(root.querySelector('[data-catalog-view="cards"]'));
    } catch { /* Preference is optional. */ }
})();
