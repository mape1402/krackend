const { test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function inspector() {
    const elements = new Map(), requests = [];
    const field = () => ({ textContent: '', innerHTML: '', hidden: false, style: {}, addEventListener(type, fn) { this[type] = fn; } });
    const panel = { inert: true, attrs: {}, setAttribute(k, v) { this.attrs[k] = v; }, querySelector(s) { if (!elements.has(s)) elements.set(s, field()); return elements.get(s); } };
    const workspace = { classList: { add() {}, remove() {} } };
    const root = { dataset: { stagesSource: '/instances?handler=InspectorStages', detailTemplate: '/detail/__instance_id__' }, querySelector: () => panel };
    const window = {};
    const source = fs.readFileSync(path.join(__dirname, '../../../src/Krackend.Sagas.Orchestrations.Runtime.WebUI/wwwroot/js/orchestrator-runtime-inspector.js'), 'utf8');
    vm.runInNewContext(source, { window, URL, Date, AbortController, location: { href: 'https://localhost/runtime/history' },
        fetch: (url, options) => new Promise(resolve => requests.push({ url, signal: options.signal, resolve })) });
    return { panel, elements, requests, instance: window.KrackendInstanceInspector.create(root, workspace) };
}

const row = id => ({ id, status: 'Running', orchestrationDefinitionKey: id, retryCount: 0, startedOnUtc: '2026-10-01T00:00:00Z' });
const result = name => ({ ok: true, json: async () => ({ items: [{ stageKey: name, status: 'Completed' }], hasMore: false }) });

test('switching rows aborts the old request and never renders its late stages', async () => {
    const h = inspector();
    h.instance.open(row('first')); h.instance.open(row('second'));
    assert.equal(h.requests.length, 2); assert.equal(h.requests[0].signal.aborted, true);
    assert.equal(h.requests[1].url.searchParams.get('instanceId'), 'second');
    h.requests[1].resolve(result('second-stage')); await new Promise(resolve => setImmediate(resolve));
    h.requests[0].resolve(result('old-stage')); await new Promise(resolve => setImmediate(resolve));
    const content = h.elements.get('[data-inspector-stages]').innerHTML;
    assert.match(content, /second-stage/); assert.doesNotMatch(content, /old-stage/);
    h.instance.open(row('second')); assert.equal(h.requests.length, 2);
    h.instance.close(); assert.equal(h.panel.attrs['aria-hidden'], 'true'); assert.equal(h.panel.inert, true);
});

test('closing the inspector cancels its pending stage request', async () => {
    const h = inspector(); h.instance.open(row('first'));
    h.instance.close(); assert.equal(h.requests[0].signal.aborted, true);
    h.requests[0].resolve(result('late')); await new Promise(resolve => setImmediate(resolve));
    assert.doesNotMatch(h.elements.get('[data-inspector-stages]').innerHTML, /late/);
});
