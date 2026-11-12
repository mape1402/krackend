(() => {
    const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const parseDate = value => {
        if (!value) return null;
        const text = /(?:Z|[+-]\d\d:\d\d)$/i.test(value) ? value : `${value}Z`;
        const result = new Date(text);
        return Number.isNaN(result.getTime()) ? null : result;
    };
    const formatDate = value => parseDate(value)?.toLocaleString() || '—';
    const duration = row => {
        const start = parseDate(row.startedOnUtc);
        if (!start) return '—';
        const end = ['Running', 'Idle'].includes(row.status) ? new Date() : parseDate(row.completedOnUtc || row.failedOnUtc || row.stoppedOnUtc || row.compensatedOnUtc);
        if (!end) return '—';
        const seconds = Math.max(0, Math.floor((end - start) / 1000));
        return seconds < 60 ? `${seconds}s` : seconds < 3600 ? `${Math.floor(seconds / 60)}m ${seconds % 60}s` : `${Math.floor(seconds / 3600)}h ${Math.floor(seconds % 3600 / 60)}m`;
    };
    function create(root, workspace, onClose = () => {}) {
        const panel = root.querySelector('[data-inspector]');
        let selected = null, controller = null, revision = 0;
        const query = selector => panel.querySelector(selector);
        const stages = query('[data-inspector-stages]');
        const more = query('[data-inspector-more]');
        const retry = query('[data-inspector-retry]');
        function render(row) {
            selected = row;
            query('[data-inspector-content]').hidden = false;
            query('[data-inspector-title]').textContent = row.orchestrationLabel || row.orchestrationDefinitionKey || '—';
            const status = String(row.status || 'Unknown');
            query('[data-inspector-status]').innerHTML = `<span class="status-pill status-${escape(status.toLowerCase())}">${escape(status)}</span>`;
            query('[data-inspector-updated]').textContent = `Updated ${formatDate(row.lastUpdatedOnUtc)}`;
            const known = row.totalTaskCount != null && row.completedTaskCount != null;
            const percent = !known ? null : row.totalTaskCount === 0 ? (status === 'Completed' ? 100 : 0) : Math.max(0, Math.min(100, Math.round(100 * row.completedTaskCount / row.totalTaskCount)));
            query('[data-inspector-progress-label]').textContent = percent == null ? 'Not available' : `${percent}% · ${row.completedTaskCount}/${row.totalTaskCount} tasks`;
            query('[data-inspector-progress]').style.width = `${percent ?? 0}%`;
            const fields = [['Saga ID', row.sagaId], ['Correlation ID', row.correlationId], ['Version', row.orchestrationVersion], ['Trace ID', row.traceId], ['Started', formatDate(row.startedOnUtc)], ['Current / last task', row.currentTaskKey || row.lastExecutedTaskKey]];
            query('[data-inspector-metadata]').innerHTML = fields.map(([key, value]) => { const text = value == null || value === '' ? '—' : value; return `<div><dt>${escape(key)}</dt><dd title="${escape(text)}">${escape(text)}</dd></div>`; }).join('');
            query('[data-inspector-duration]').textContent = duration(row);
            const detail = root.dataset.detailTemplate.replace('__instance_id__', encodeURIComponent(row.id));
            query('[data-inspector-detail]').href = detail;
            const activity = new URL(detail, location.href); activity.searchParams.set('section', 'activity');
            query('[data-inspector-activity]').href = activity.toString();
        }
        async function loadStages() {
            if (!selected) return;
            controller?.abort();
            const request = new AbortController(), epoch = ++revision, id = String(selected.id);
            controller = request; more.hidden = true; retry.hidden = true; stages.textContent = 'Loading recorded stages…';
            const url = new URL(root.dataset.stagesSource, location.href); url.searchParams.set('instanceId', id);
            try {
                const response = await fetch(url, { headers: { Accept: 'application/json' }, signal: request.signal });
                if (!response.ok) throw new Error('Stage summaries unavailable');
                const result = await response.json();
                if (epoch !== revision || String(selected?.id) !== id) return;
                const items = Array.isArray(result.items) ? result.items : [];
                if (!items.length) { stages.textContent = 'No recorded stages.'; more.hidden = !result.hasMore; return; }
                stages.innerHTML = items.map(item => {
                    const state = item.wasSkipped ? 'Skipped' : String(item.status || 'Unknown');
                    const style = /^(Completed|Compensated)$/i.test(state) ? 'done' : /^(Failed|TimedOut)$/i.test(state) ? 'failed' : /^(Running|Waiting|Compensating)$/i.test(state) ? 'active' : '';
                    const icon = style === 'done' ? 'check-lg' : style === 'failed' ? 'x-lg' : style === 'active' ? 'three-dots' : 'dash';
                    return `<div class="execution-step ${style}"><span class="step-dot"><i class="bi bi-${icon}" aria-hidden="true"></i></span><span class="step-copy"><strong>${escape(item.stageKey)}</strong><small>${escape(state)} · ${escape(formatDate(item.startedOnUtc))}</small></span></div>`;
                }).join('');
                more.hidden = !result.hasMore;
            } catch (error) {
                if (error.name !== 'AbortError' && epoch === revision && String(selected?.id) === id) { stages.textContent = 'Unable to load recorded stages.'; retry.hidden = false; }
            } finally { if (controller === request) controller = null; }
        }
        function open(row) {
            const changed = String(selected?.id || '') !== String(row.id);
            render(row); workspace.classList.add('inspector-open'); panel.inert = false; panel.setAttribute('aria-hidden', 'false');
            if (changed) void loadStages();
        }
        function close() {
            if (!selected) return;
            revision++; controller?.abort(); controller = null; selected = null;
            workspace.classList.remove('inspector-open'); panel.inert = true; panel.setAttribute('aria-hidden', 'true'); onClose();
        }
        query('[data-close-inspector]').addEventListener('click', close);
        retry.addEventListener('click', () => { void loadStages(); });
        return { open, close, update(row) { if (selected && String(selected.id) === String(row.id)) render(row); },
            isCurrent(id) { return !!selected && String(selected.id) === String(id); }, refreshStages: loadStages };
    }
    window.KrackendInstanceInspector = { create };
})();
