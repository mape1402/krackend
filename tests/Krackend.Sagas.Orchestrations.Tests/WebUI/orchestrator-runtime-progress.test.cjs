const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function catalog() {
    const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.Runtime.WebUI/wwwroot/js/orchestrator-runtime-catalog.js'), 'utf8').replace(/\r\n/g, '\n');
    const search = { value: '', closest: () => null, parentElement: null };
    const toolbar = { append() {} };
    const grid = {
        querySelector: selector => selector === '[data-z-grid-search]' ? search : selector === '.z-toolbar' ? toolbar : null,
        querySelectorAll: () => [], addEventListener() {}, dispatchEvent() {}, getAttribute: () => null
    };
    const root = { dataset: { countsSource: '/counts' }, querySelector: selector => selector === '[data-z-data-grid]' ? grid : null, querySelectorAll: () => [] };
    const document = {
        querySelector: () => root,
        createElement: () => ({ dataset: {}, style: {}, children: [], append(...items) { this.children.push(...items); } })
    };
    const window = { ZoorceDataGrid: { dispose() {}, mount() {} }, addEventListener() {}, setTimeout() {}, clearTimeout() {}, requestAnimationFrame() {} };
    class MutationObserver { observe() {} disconnect() {} }
    class CustomEvent { constructor(type, options) { this.type = type; this.detail = options?.detail; } }
    const instrumented = source.replace('    const syncScopeButtons =', '    window.testRuntime = { toLiveRow, renderProgress };\n    const syncScopeButtons =');
    assert.notEqual(instrumented, source);
    vm.runInNewContext(instrumented, { window, document, MutationObserver, CustomEvent, URL, AbortController, Date, CSS: { escape: value => value }, location: { href: 'https://localhost/' }, fetch: async () => ({ ok: true, json: async () => ({}) }) });
    return window.testRuntime;
}

test('unknown progress is distinct from zero enabled tasks', () => {
    const { renderProgress } = catalog();
    const unknown = renderProgress({ completedTaskCount: null, totalTaskCount: null });
    const zero = renderProgress({ completedTaskCount: 0, totalTaskCount: 0 });
    assert.equal(unknown.textContent, '—');
    assert.equal(unknown.dataset.progressState, 'unknown');
    assert.equal(zero.textContent, '0/0');
    assert.equal(zero.dataset.progressState, '0/0');
});

test('an unknown event clears progress while an older event preserves it', () => {
    const { toLiveRow } = catalog();
    const existing = { id: 'saga', completedTaskCount: 3, totalTaskCount: 5 };
    const unknown = toLiveRow({ orchestrationInstanceId: 'saga', progressKnown: false }, existing);
    assert.equal(unknown.completedTaskCount, null);
    assert.equal(unknown.totalTaskCount, null);
    const legacy = toLiveRow({ orchestrationInstanceId: 'saga' }, existing);
    assert.equal(legacy.completedTaskCount, 3);
    assert.equal(legacy.totalTaskCount, 5);
});
