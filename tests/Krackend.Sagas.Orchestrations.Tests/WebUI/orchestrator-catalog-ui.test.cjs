const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.WebUI.Shell/wwwroot/js/orchestrator-catalog-ui.js'), 'utf8');
const setup = () => {
    const window = {};
    const document = {
        readyState: 'loading',
        addEventListener() {},
        createElement: tag => ({ tagName: tag, className: '', textContent: '', attributes: {}, setAttribute(name, value) { this.attributes[name] = value; } })
    };
    vm.runInNewContext(source, { window, document });
    return window.KrackendCatalogUI;
};

test('card icons use the requested icon and are hidden from assistive technology', () => {
    const icon = setup().cardIcon('diagram-3');
    assert.equal(icon.tagName, 'i');
    assert.equal(icon.className, 'bi bi-diagram-3 od-card-icon');
    assert.equal(icon.attributes['aria-hidden'], 'true');
});

test('success states receive a success chip', () => {
    const { status } = setup();
    for (const value of ['Active', 'Completed', 'Deployed', 'Approved']) {
        assert.match(status(value).className, /od-list-status--success/);
    }
});

test('pending states and failed states have distinct semantic colors', () => {
    const { status } = setup();
    assert.match(status('In Review').className, /od-list-status--warning/);
    for (const value of ['Pending deployment', 'Pending deprecation', 'Pending archive']) {
        assert.match(status(value).className, /od-list-status--warning/);
    }
    assert.match(status('DeadLettered').className, /od-list-status--danger/);
    assert.match(status('Rejected').className, /od-list-status--danger/);
});

test('unknown states are neutral and their text is not interpreted as markup', () => {
    const { status } = setup();
    const value = '<img src=x onerror=alert(1)>';
    const badge = status(value);
    assert.equal(badge.className, 'od-list-status');
    assert.equal(badge.textContent, value);
    assert.equal(status(null).textContent, '');
});

test('visible grid height follows the available window space', () => {
    let height;
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => { height = [key, value]; } } };
    const grid = { getClientRects: () => [1], querySelector: () => viewport };
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [grid] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 800, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.deepEqual(height, ['--z-grid-viewport-height', '520px']);
});

test('grid height has a minimum and hidden grids are not resized', () => {
    const heights = [];
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => heights.push(value) } };
    const visible = { getClientRects: () => [1], querySelector: () => viewport };
    const hidden = { getClientRects: () => [], querySelector: () => { throw new Error('hidden grid was measured'); } };
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [visible, hidden] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 250, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.deepEqual(heights, ['220px']);
});
