const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../..', 'src/Krackend.Sagas.Orchestrations.ControlPlane.WebUI/wwwroot/js/orchestrator-buttermorph-context.js'), 'utf8');

test('loads the current context without caching and preserves the page route', async () => {
    let request;
    const window = { location: { href: 'https://example.test/design/stage?versionId=v1' } };
    vm.runInNewContext(source, { window, URL, fetch: async (url, options) => {
        request = { url, options };
        return { ok: true, json: async () => ({ context: 'updated-context' }) };
    } });
    assert.equal(await window.KrackendButterMorphContext('task:v1:t1'), 'updated-context');
    assert.equal(request.url.pathname, '/design/stage');
    assert.equal(request.url.searchParams.get('versionId'), 'v1');
    assert.equal(request.url.searchParams.get('handler'), 'ButterMorphContext');
    assert.equal(request.url.searchParams.get('context'), 'task:v1:t1');
    assert.equal(request.options.cache, 'no-store');
});

test('does not open a stale context when the lookup fails', async () => {
    let message;
    const window = { location: { href: 'https://example.test/' }, OrchestratorMessages: { notify: value => { message = value; } } };
    vm.runInNewContext(source, { window, URL, fetch: async () => ({ ok: false }) });
    assert.equal(await window.KrackendButterMorphContext('task:v1:t1'), null);
    assert.equal(message.type, 'error');
});
