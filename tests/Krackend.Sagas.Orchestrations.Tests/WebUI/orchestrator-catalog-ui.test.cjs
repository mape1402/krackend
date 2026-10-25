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

test('grid footer moves beside the toolbar and the viewport uses the available height', () => {
    let height;
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => { height = [key, value]; } } };
    const footer = { getBoundingClientRect: () => ({ height: 44 }) };
    const toolbar = { append: item => { item.parentElement = toolbar; } };
    const grid = { getClientRects: () => [1], querySelector: selector => ({ '[data-z-grid-viewport]': viewport, '.z-grid-footer': footer, '.z-toolbar': toolbar })[selector] };
    footer.parentElement = grid;
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [grid] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 800, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.equal(footer.parentElement, toolbar);
    assert.deepEqual(height, ['--z-grid-viewport-height', '588px']);
});

test('grid retains room for a footer when no toolbar is available', () => {
    let height;
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => { height = value; } } };
    const footer = { getBoundingClientRect: () => ({ height: 44 }) };
    const grid = { getClientRects: () => [1], querySelector: selector => ({ '[data-z-grid-viewport]': viewport, '.z-grid-footer': footer })[selector] };
    footer.parentElement = grid;
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [grid] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 800, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.equal(height, '544px');
});

test('grid height has a minimum and hidden grids are not resized', () => {
    const heights = [];
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => heights.push(value) } };
    const visible = { getClientRects: () => [1], querySelector: selector => selector === '[data-z-grid-viewport]' ? viewport : null };
    const hidden = { getClientRects: () => [], querySelector: selector => { if (selector === '[data-z-grid-viewport]') throw new Error('hidden grid was measured'); return null; } };
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [visible, hidden] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 250, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.deepEqual(heights, ['220px']);
});

test('grid without a footer uses the remaining window height', () => {
    let height;
    const viewport = { getBoundingClientRect: () => ({ top: 200 }), style: { setProperty: (key, value) => { height = value; } } };
    const grid = { getClientRects: () => [1], querySelector: selector => selector === '[data-z-grid-viewport]' ? viewport : null };
    const root = { querySelectorAll: selector => selector === '[data-z-data-grid]' ? [grid] : [] };
    const document = { readyState: 'complete', querySelectorAll: () => [root], createElement: () => ({}) };
    const window = { innerHeight: 800, addEventListener() {} };
    class MutationObserver { observe() {} }
    vm.runInNewContext(source, { window, document, MutationObserver, requestAnimationFrame: fn => { fn(); return 1; }, cancelAnimationFrame() {} });
    assert.equal(height, '588px');
});
