const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function createMessages(elements = {}) {
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
        getElementById(id) { return elements[id] || null; },
        createElement(tag) {
            if (tag === 'span') return { dataset: {}, classList: { add() {}, remove() {} }, textContent: '' };
            return host;
        },
        querySelector() { return null; },
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
        submit(form) { return [...(listeners.get('submit') || [])][0]({ target: form, preventDefault() {} }); },
        closeDialog(dialog) { for (const listener of listeners.get('zoorce:dialog:close') || []) listener({ target: dialog }); },
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

test('closing a Zoorce dialog clears its previous form validation', () => {
    const ui = createMessages();
    const calls = [];
    const invalid = { classList: { remove(value) { calls.push(value); } }, removeAttribute(value) { calls.push(value); } };
    const form = { querySelectorAll(selector) { return selector === '.is-invalid' || selector === '[aria-invalid="true"]' ? [invalid] : []; } };
    ui.closeDialog({ querySelectorAll(selector) { return selector === 'form' ? [form] : []; } });
    assert.deepEqual(calls, ['is-invalid', 'aria-invalid']);
});

test('validation displays the field error and immediately focuses the invalid tab', async () => {
    const calls = [];
    const ui = createMessages({ general: { click() { calls.push('tab'); } } });
    const field = {
        name: 'Input.Name', type: 'text', value: '', dataset: {},
        getAttribute(name) { return name === 'data-val-required' ? 'Name is required.' : null; },
        setAttribute() {},
        classList: { add() {}, remove() {} },
        closest(selector) { return selector === '[role="tabpanel"]' ? { getAttribute() { return 'general'; } } : null; },
        focus() { calls.push('focus'); },
        insertAdjacentElement(position, element) { calls.push(element.textContent || 'error'); }
    };
    const form = {
        tagName: 'FORM', dataset: { orchestratorValidate: 'true' }, elements: [field],
        setAttribute() {}, querySelectorAll() { return []; },
        querySelector(selector) { return selector === '.is-invalid' ? field : null; }
    };
    field.form = form;
    await ui.submit(form);
    assert.deepEqual(calls, ['error', 'tab', 'focus']);
});

test('wizard validation returns false for missing fields and true after correction', () => {
    const ui = createMessages();
    let invalid = false;
    const field = {
        name: 'Input.Name', type: 'text', value: '', dataset: {},
        getAttribute(name) { return name === 'data-val-required' ? 'Name is required.' : null; },
        setAttribute() {}, removeAttribute() {},
        classList: { add() { invalid = true; }, remove() { invalid = false; } },
        closest() { return null; }, focus() {}, insertAdjacentElement() {}
    };
    const form = {
        elements: [field], querySelectorAll(selector) { return selector === '.is-invalid' && invalid ? [field] : []; },
        querySelector(selector) { return selector === '.is-invalid' && invalid ? field : null; }
    };
    field.form = form;
    assert.equal(ui.messages.validateForm(form), false);
    field.value = 'Example';
    assert.equal(ui.messages.validateForm(form), true);
    assert.equal(ui.messages.validateForm(null), false);
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
