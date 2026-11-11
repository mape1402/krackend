const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function history() {
    const handlers = {};
    const field = value => ({ value, hidden: false, textContent: '', setAttribute() {}, removeAttribute() {},
        addEventListener(type, callback) { this[type] = callback; } });
    const controls = {
        '[data-history-filters]': field(''), '[data-history-from]': field('2026-10-01'), '[data-history-through]': field(''),
        '[data-history-orchestration]': field(''), '[data-history-version]': field(''), '[data-history-status]': field(''),
        '[data-history-search]': field(''), '[data-history-error]': field(''), '[data-history-message]': field(''),
        '[data-history-count]': field(''), '[data-history-clear]': field(''), '#historyGrid': field('')
    };
    const root = { dataset: { source: '/instances?handler=List', defaultFrom: '2026-10-01', detailTemplate: '/detail/__instance_id__' },
        querySelector: selector => controls[selector] };
    const calls = [];
    let gridOptions, refreshes = 0;
    const api = { setGridOption(name) { if (name === 'datasource') refreshes++; }, destroy() {} };
    const agGrid = { themeQuartz: { withParams: () => ({}) }, createGrid(element, options) { gridOptions = options; return api; } };
    const document = { querySelector: () => root, documentElement: {}, createElement: () => ({ setAttribute() {} }) };
    const window = { agGrid, addEventListener(type, callback) { handlers[type] = callback; } };
    const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.Runtime.WebUI/wwwroot/js/orchestrator-runtime-history-ag-grid.js'), 'utf8');
    class MutationObserver { observe() {} disconnect() {} }
    vm.runInNewContext(source, { window, document, agGrid, MutationObserver, URL, AbortController, Date, setTimeout, clearTimeout,
        navigator: {}, getComputedStyle: () => ({ getPropertyValue: () => '' }), location: { href: 'https://localhost/runtime/history' },
        fetch: async url => { calls.push(url); return { ok: true, json: async () => ({ items: [], totalCount: 0 }) }; } });
    return { controls, calls, get refreshes() { return refreshes; }, get options() { return gridOptions; },
        submit() { controls['[data-history-filters]'].submit({ preventDefault() {} }); },
        clear() { controls['[data-history-clear]'].click(); },
        load() { return gridOptions.datasource.getRows({ startRow: 25, sortModel: [], successCallback() {}, failCallback() {} }); } };
}

test('history requests bounded blocks without pagination controls', async () => {
    const h = history();
    assert.equal(h.options.rowModelType, 'infinite');
    assert.notEqual(h.options.pagination, true);
    assert.equal(h.options.cacheBlockSize, 25);
    await h.load();
    assert.equal(h.calls.at(-1).searchParams.get('page'), '2');
    assert.equal(h.calls.at(-1).searchParams.get('pageSize'), '25');
});

test('history submits combined filters with inclusive UTC end and clears them', async () => {
    const h = history();
    h.controls['[data-history-through]'].value = '2026-10-05';
    h.controls['[data-history-orchestration]'].value = ' orders ';
    h.controls['[data-history-version]'].value = ' 1.2.3 ';
    h.controls['[data-history-status]'].value = 'completed';
    h.controls['[data-history-search]'].value = ' saga ';
    h.submit(); await h.load();
    let url = h.calls.at(-1);
    assert.equal(url.searchParams.get('page'), '2');
    assert.equal(url.searchParams.get('startedFrom'), '2026-10-01T00:00:00Z');
    assert.equal(url.searchParams.get('startedBefore'), '2026-10-06T00:00:00.000Z');
    assert.equal(url.searchParams.get('orchestrationKey'), 'orders');
    assert.equal(url.searchParams.get('orchestrationVersion'), '1.2.3');
    assert.equal(url.searchParams.get('statusGroup'), 'completed');
    assert.equal(url.searchParams.get('search'), 'saga');
    h.clear(); await h.load(); url = h.calls.at(-1);
    assert.equal(url.searchParams.get('startedFrom'), '2026-10-01T00:00:00Z');
    assert.equal(url.searchParams.has('orchestrationKey'), false);
    assert.equal(url.searchParams.has('startedBefore'), false);
    assert.equal(url.searchParams.has('search'), false);
});

test('invalid date range does not replace the applied filters', async () => {
    const h = history();
    h.controls['[data-history-from]'].value = '2026-10-10';
    h.controls['[data-history-through]'].value = '2026-10-05';
    h.submit();
    assert.equal(h.controls['[data-history-error]'].hidden, false);
    assert.equal(h.refreshes, 0);
    await h.load();
    assert.equal(h.calls.at(-1).searchParams.get('startedFrom'), '2026-10-01T00:00:00Z');
    assert.equal(h.calls.at(-1).searchParams.has('startedBefore'), false);
});

test('the final representable UTC day does not overflow the exclusive end', async () => {
    const h = history();
    h.controls['[data-history-through]'].value = '9999-12-31';
    h.submit(); await h.load();
    assert.equal(h.calls.at(-1).searchParams.has('startedBefore'), false);
});
