const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('live instances appear newest first without replacing the infinite datasource', async () => {
    const fields = new Map(), listeners = new Map(), loaded = new Map(), requests = [];
    const field = () => ({ value: '', textContent: '', hidden: false, dataset: {}, classList: { toggle() {} },
        setAttribute() {}, addEventListener(name, callback) { this[name] = callback; } });
    const root = { dataset: { source: '/instances', countsSource: '/counts', activitySource: '/activity' },
        querySelector(selector) { if (selector === '#instancesGrid') return { querySelector: () => null };
            if (!fields.has(selector)) fields.set(selector, field()); return fields.get(selector); }, querySelectorAll: () => [] };
    const document = { documentElement: {}, querySelector: () => root, createElement: field };
    let options, resets = 0, total = 0, catchupRows = null;
    const api = { forEachNode(fn) { loaded.forEach(fn); }, getRowNode(id) { return [...loaded.values()].find(node => node.data?.id === id); },
        getDisplayedRowAtIndex(index) { if (!loaded.has(index)) loaded.set(index, { setData(row) { this.data = row; } }); return loaded.get(index); },
        setRowCount(value) { total = value; }, refreshInfiniteCache() {}, setGridOption(name) { if (name === 'datasource') resets++; }, refreshCells() {}, destroy() {} };
    const agGrid = { themeQuartz: { withParams: () => ({}) }, createGrid(_, config) { options = config; return api; } };
    const window = { agGrid, addEventListener(name, fn) { listeners.set(name, fn); },
        KrackendInstanceInspector: { create: () => ({ open() {}, close() {}, update() {}, isCurrent: () => false }) } };
    class MutationObserver { observe() {} disconnect() {} }
    const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.Runtime.WebUI/wwwroot/js/orchestrator-runtime-ag-grid.js'), 'utf8');
    vm.runInNewContext(source, { window, document, agGrid, MutationObserver, URL, AbortController, Date, setTimeout, clearTimeout,
        navigator: {}, location: { href: 'https://localhost/' }, getComputedStyle: () => ({ getPropertyValue: () => '' }),
        fetch: async url => { requests.push(String(url)); return { ok: true, json: async () => String(url).includes('/instances')
            ? catchupRows ? { items: catchupRows, totalCount: catchupRows.length, hasMore: false }
                : { items: Array.from({ length: 24 }, (_, i) => ({ id: `old-${i}`, startedOnUtc: '2020-01-01T00:00:00Z' })), totalCount: 24 } : {} }; } });
    const getRows = startRow => new Promise((resolve, reject) => options.datasource.getRows({ startRow, endRow: startRow + 25,
        successCallback: (items, count) => resolve({ items, count }), failCallback: reject }));
    const first = await getRows(0);
    assert.equal(first.count, 24);
    assert.equal(new URL(requests.find(url => url.includes('/instances'))).searchParams.get('sort'), 'startedOnUtc');
    assert.equal(new URL(requests.find(url => url.includes('/instances'))).searchParams.get('descending'), 'true');
    assert.equal(options.defaultColDef.sortable, false);
    const when = Date.now() + 1000;
    const event = (id, offset, transition = 'InstanceStarted', status = 'Running') => ({ id: `${id}-${transition}`, orchestrationInstanceId: id,
        transitionType: transition, instanceStatus: status, startedOnUtc: new Date(when + offset).toISOString(),
        lastUpdatedOnUtc: new Date(when + offset + (status === 'Completed' ? 10 : 0)).toISOString(), orchestrationDefinitionKey: 'workflow' });
    const emit = data => listeners.get('krackend:instances-changed')({ detail: { event: data } });
    emit(event('new-b', 2)); emit(event('new-a', 1));
    await new Promise(resolve => setTimeout(resolve, 180));
    assert.equal(total, 26);
    const readsBeforeRefresh = requests.filter(url => url.includes('/instances')).length;
    const updated = await getRows(0);
    assert.equal(requests.filter(url => url.includes('/instances')).length, readsBeforeRefresh);
    assert.equal(updated.items[0].id, 'new-b');
    assert.equal(updated.items[1].id, 'new-a');
    assert.equal(updated.items[2].id, 'old-0');
    assert.equal(resets, 0);
    const second = await getRows(25);
    assert.equal(second.items[0].id, 'old-23');
    emit(event('new-a', 1, 'InstanceCompleted', 'Completed'));
    await new Promise(resolve => setTimeout(resolve, 180));
    const afterCompletion = await getRows(0);
    assert.equal(afterCompletion.items[0].id, 'new-b');
    assert.equal(afterCompletion.items[1].id, 'new-a');
    assert.equal(afterCompletion.items[1].status, 'Completed');
    assert.equal(resets, 0);
    catchupRows = [{ id: 'new-b', status: 'Running', lastUpdatedOnUtc: new Date(when + 2).toISOString() },
        { id: 'missed', status: 'Running', startedOnUtc: new Date(when + 3).toISOString() }];
    listeners.get('krackend:instances-changed')({ detail: { resync: true } });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(total, 27);
    catchupRows = null;
    const afterReconnect = await getRows(0);
    assert.equal(afterReconnect.items[0].id, 'missed');
    assert.equal(afterReconnect.items[1].id, 'new-b');
    assert.equal(afterReconnect.items[2].id, 'new-a');
    assert.equal(resets, 0);
    listeners.get('pagehide')({ persisted: false });
});
