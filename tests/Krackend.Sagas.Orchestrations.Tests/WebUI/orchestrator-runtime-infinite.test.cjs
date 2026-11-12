const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('infinite instance grid loads bounded rows without selection checkboxes', async () => {
    const fields = new Map();
    const field = () => ({ value: '', textContent: '', hidden: false, dataset: {}, style: {}, classList: { add() {}, remove() {}, toggle() {} },
        setAttribute(name, value) { this[name] = value; }, addEventListener(name, callback) { this[name] = callback; } });
    let options;
    const requests = [];
    const api = { forEachNode() {}, refreshCells() {}, setGridOption() {}, destroy() {} };
    const grid = { querySelector: () => null };
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
        fetch: async url => { requests.push(String(url)); return { ok: true, json: async () => String(url).includes('/instances')
            ? { items: [{ id: 'a', sagaId: 'a' }, { id: 'b', sagaId: 'b' }], totalCount: 2 } : {} }; } });
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(options.rowModelType, 'infinite');
    assert.notEqual(options.pagination, true);
    assert.equal(options.columnDefs.some(column => column.checkboxSelection || column.headerCheckboxSelection || column.headerComponent), false);
    const result = await new Promise((resolve, reject) => options.datasource.getRows({ startRow: 0, endRow: 25,
        successCallback: (items, total) => resolve({ items, total }), failCallback: reject }));
    assert.deepEqual(Array.from(result.items, item => item.id), ['a', 'b']);
    assert.equal(result.total, 2);
    const listRequests = requests.filter(url => url.includes('/instances'));
    assert.equal(listRequests.length, 1);
    assert.equal(new URL(listRequests[0]).searchParams.get('pageSize'), '25');
});
