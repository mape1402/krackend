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
        createElement: tag => ({ tagName: tag, className: '', textContent: '' })
    };
    vm.runInNewContext(source, { window, document });
    return window.KrackendCatalogUI;
};

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
