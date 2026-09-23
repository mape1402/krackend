(() => {
    const selector = 'input[data-schema-contract-key="true"]';
    const attachedAttribute = 'data-schema-contract-autocomplete-attached';
    const cache = new Map();

    function createEndpoint(kind, term) {
        const url = new URL(window.location.href);
        url.searchParams.set('handler', 'SchemaContracts');
        url.searchParams.set('contractKind', kind || 'Unspecified');
        url.searchParams.set('term', term || '');
        url.searchParams.set('take', '50');
        return `${url.pathname}${url.search}`;
    }

    async function searchContracts(input) {
        const kind = input.dataset.schemaContractKind || 'Unspecified';
        const term = input.value || '';
        const cacheKey = `${kind}|${term}`;
        if (cache.has(cacheKey)) {
            return cache.get(cacheKey);
        }

        const response = await fetch(createEndpoint(kind, term), {
            headers: {
                Accept: 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Schema contracts could not be loaded.');
        }

        const items = await response.json();
        const result = Array.isArray(items) ? items : [];
        cache.set(cacheKey, result);
        return result;
    }

    function getHost(input) {
        const host = input.parentElement;
        if (host) {
            host.classList.add('od-autocomplete-host');
        }

        return host;
    }

    function getList(input) {
        const existingId = input.dataset.schemaAutocompleteListId;
        if (existingId) {
            const existing = document.getElementById(existingId);
            if (existing) {
                return existing;
            }
        }

        const host = getHost(input);
        const list = document.createElement('div');
        list.id = `${input.id || input.name || 'schema-contract'}_SchemaContractList`;
        list.className = 'od-autocomplete-list d-none';
        list.setAttribute('role', 'listbox');
        list.setAttribute('aria-label', 'Available schema contracts');
        input.dataset.schemaAutocompleteListId = list.id;
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-controls', list.id);

        host?.appendChild(list);
        return list;
    }

    function clearList(input) {
        const list = getList(input);
        list.replaceChildren();
    }

    function showList(input) {
        const list = getList(input);
        list.classList.remove('d-none');
    }

    function hideList(input) {
        const list = getList(input);
        list.classList.add('d-none');
    }

    function renderStatus(input, message) {
        const list = getList(input);
        list.replaceChildren();

        const item = document.createElement('div');
        item.className = 'od-autocomplete-status';
        item.textContent = message;
        list.appendChild(item);
        showList(input);
    }

    function renderOptions(input, items) {
        const list = getList(input);
        list.replaceChildren();

        if (items.length === 0) {
            renderStatus(input, 'No schemas found');
            return;
        }

        items.forEach(item => {
            if (!item?.contractKey) {
                return;
            }

            const option = document.createElement('button');
            option.type = 'button';
            option.className = 'od-autocomplete-item';
            option.setAttribute('role', 'option');
            option.dataset.contractKey = item.contractKey || '';
            option.dataset.contractVersion = item.contractVersion || '';
            option.dataset.contractKind = item.contractKind || '';
            option.dataset.providerKey = item.providerKey || '';

            const title = document.createElement('span');
            title.className = 'od-autocomplete-title';
            title.textContent = item.contractKey;

            const meta = document.createElement('span');
            meta.className = 'od-autocomplete-meta';
            meta.textContent = [
                item.contractKind,
                item.contractVersion ? `v${item.contractVersion}` : '',
                item.providerKey
            ].filter(value => value).join(' · ');

            option.append(title, meta);
            option.addEventListener('mousedown', event => event.preventDefault());
            option.addEventListener('click', () => selectContract(input, option));
            list.appendChild(option);
        });

        showList(input);
    }

    function selectContract(input, option) {
        input.value = option.dataset.contractKey || '';
        input.dispatchEvent(new Event('change', { bubbles: true }));

        const versionTarget = getTarget(input.dataset.schemaVersionTarget || '');
        const version = option.dataset.contractVersion || '';
        if (versionTarget && version) {
            versionTarget.value = version;
            versionTarget.dispatchEvent(new Event('change', { bubbles: true }));
        }

        hideList(input);
    }

    function getTarget(id) {
        return id ? document.getElementById(id) : null;
    }

    function debounce(callback, delay) {
        let timeout;
        return (...args) => {
            window.clearTimeout(timeout);
            timeout = window.setTimeout(() => callback(...args), delay);
        };
    }

    function attach(input) {
        if (!input || input.getAttribute(attachedAttribute) === 'true') {
            return;
        }

        input.setAttribute(attachedAttribute, 'true');
        getList(input);

        const refresh = async () => {
            renderStatus(input, 'Loading schemas...');

            try {
                const items = await searchContracts(input);
                renderOptions(input, items);
            } catch {
                renderStatus(input, 'Schemas could not be loaded');
            }
        };
        const refreshDebounced = debounce(refresh, 180);

        input.addEventListener('focus', refresh);
        input.addEventListener('input', refreshDebounced);
        input.addEventListener('keydown', event => {
            if (event.key === 'Escape') {
                hideList(input);
            }
        });
        input.addEventListener('blur', () => {
            window.setTimeout(() => hideList(input), 120);
        });
    }

    function attachAll(root = document) {
        root.querySelectorAll(selector).forEach(attach);
    }

    document.addEventListener('DOMContentLoaded', () => attachAll());
    document.addEventListener('shown.bs.modal', event => attachAll(event.target));

    window.OrchestratorSchemaContracts = {
        attachAll
    };
})();
