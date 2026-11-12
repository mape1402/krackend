const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('infinite instance selection only selects loaded rows without fetching more', async () => {
    const fields = new Map();
    const field = () => ({ value: '', textContent: '', hidden: false, dataset: {}, style: {}, classList: { add() {}, remove() {}, toggle() {} },
        setAttribute(name, value) { this[name] = value; }, addEventListener(name, callback) { this[name] = callback; } });
    const nodes = [{ data: { id: 'a', sagaId: 'a' } }, { data: { id: 'b', sagaId: 'b' } }];
    let header, options, fetches = 0;
    const api = { forEachNode(callback) { nodes.forEach(callback); }, refreshCells() {}, setGridOption() {}, destroy() {} };
    const grid = { querySelector: selector => selector === '[data-select-loaded]' ? header : null };
    const root = { dataset: { source: '/instances', countsSource: '/counts', activitySource: '/activity', detailTemplate: '/detail/__instance_id__' },
        querySelector(selector) { if (selector === '#instancesGrid') return grid; if (!fields.has(selector)) fields.set(selector, field()); return fields.get(selector); },
        querySelectorAll() { return []; } };
    const document = { documentElement: {}, querySelector: () => root, createElement: () => field() };
    const agGrid = { themeQuartz: { withParams: () => ({}) }, createGrid(element, config) { options = config; return api; } };
    const window = { agGrid, addEventListener() {}, KrackendInstanceInspector: { create: () => ({ open() {}, close() {}, update() {} }) } };
    class MutationObserver { observe() {} disconnect() {} }
    const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.Runtime.WebUI/wwwroot/js/orchestrator-runtime-ag-grid.js'), 'utf8');
    vm.runInNewContext(source, { window, document, agGrid, MutationObserver, URL, AbortController, Date, setTimeout, clearTimeout,
        navigator: {}, location: { href: 'https://localhost/runtime/instances' }, getComputedStyle: () => ({ getPropertyValue: () => '' }),
        fetch: async () => { fetches++; return { ok: true, json: async () => ({}) }; } });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(options.rowModelType, 'infinite');
    assert.notEqual(options.pagination, true);
    const selection = new options.columnDefs[0].headerComponent();
    selection.init({ api }); header = selection.getGui();
    assert.equal(header['aria-label'], 'Select loaded rows');
    const before = fetches;
    header.checked = true; header.change();
    assert.equal(fields.get('[data-selection-count]').textContent, '2');
    assert.equal(fetches, before);
    nodes.push({ data: { id: 'c', sagaId: 'c' } });
    options.onModelUpdated({ api });
    assert.equal(header.indeterminate, true);
    assert.equal(fields.get('[data-selection-count]').textContent, '2');
    header.checked = true; header.change();
    assert.equal(fields.get('[data-selection-count]').textContent, '3');
    assert.equal(fetches, before);
});
