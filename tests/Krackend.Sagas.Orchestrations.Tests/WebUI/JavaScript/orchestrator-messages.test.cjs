const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function createMessages() {
    const listeners = new Map();
    const buttons = new Map();
    const host = {
        classList: { add() {}, remove() {} },
        setAttribute() {},
        set innerHTML(value) {
            this.content = value;
            buttons.clear();
        },
        querySelector(selector) {
            if (selector.includes('cancel') && !this.content.includes('orchestrator-message-cancel')) return null;
            if (!buttons.has(selector)) buttons.set(selector, {
                focus() {},
                addEventListener(name, callback) { this[name] = callback; }
            });
            return buttons.get(selector);
        }
    };
    const document = {
        readyState: 'complete',
        body: { appendChild() {} },
        getElementById() { return null; },
        createElement() { return host; },
        querySelectorAll() { return []; },
        addEventListener(name, listener) {
            if (!listeners.has(name)) listeners.set(name, new Set());
            listeners.get(name).add(listener);
        },
        removeEventListener(name, listener) { listeners.get(name)?.delete(listener); }
    };
    const window = { addEventListener() {} };
    const script = path.resolve(__dirname, '../../../../src/Krackend.Sagas.Orchestrations.WebUI.Shell/wwwroot/js/orchestrator-messages.js');
    vm.runInNewContext(fs.readFileSync(script, 'utf8'), { document, window });
    return {
        messages: window.OrchestratorMessages,
        key(key) { for (const listener of [...(listeners.get('keydown') || [])]) listener({ key, preventDefault() {} }); },
        accept() { host.querySelector('.orchestrator-message-accept').click(); },
        keyListeners() { return listeners.get('keydown')?.size || 0; }
    };
}

test('Escape still closes a confirmation after other keys', async () => {
    const ui = createMessages();
    const result = ui.messages.confirm({ title: 'Confirm', message: 'Continue?' });
    ui.key('Tab');
    ui.key('Escape');
    assert.equal(await result, false);
    assert.equal(ui.keyListeners(), 0);
});

test('closing a message removes its listener before the next message', async () => {
    const ui = createMessages();
    const first = ui.messages.notify({ title: 'First' });
    ui.accept();
    assert.equal(await first, true);
    assert.equal(ui.keyListeners(), 0);
    const second = ui.messages.confirm({ title: 'Second' });
    assert.equal(ui.keyListeners(), 1);
    ui.key('Escape');
    assert.equal(await second, false);
    assert.equal(ui.keyListeners(), 0);
});
