const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Krackend.Sagas.Orchestrations.ControlPlane.WebUI/wwwroot/js/orchestrator-roadmap-clipboard.js'), 'utf8');

function setup(kind) {
    const handlers = {}, sectionHandlers = {}, requests = [], notices = [];
    const section = { dataset: { roadmapSection: kind, versionId: 'version', stageId: 'stage', editable: 'true' }, addEventListener: (name, fn) => { sectionHandlers[name] = fn; } };
    const menu = { style: {}, appendChild() {}, contains() { return false; } };
    const menuButton = { addEventListener(name, fn) { handlers[`menu-${name}`] = fn; }, focus() {} };
    let created = 0, copied = '', reloaded = false;
    let clipboard = JSON.stringify({ kind: 'krackend-roadmap-element', schemaVersion: 1, elementType: kind, data: {} });
    const document = {
        querySelectorAll: () => [section], querySelector: () => ({ content: 'csrf' }),
        addEventListener(name, fn) { (handlers[name] ||= []).push(fn); },
        createElement() { return created++ === 0 ? menu : menuButton; }, body: { appendChild() {} }
    };
    const window = { location: { href: 'https://example.test/roadmap?versionId=version', reload() { reloaded = true; } },
        innerWidth: 1000, innerHeight: 800, OrchestratorMessages: { notify: value => notices.push(value) }, prompt: () => null };
    const navigator = { clipboard: { readText: async () => clipboard, writeText: async text => { copied = text; } } };
    const fetch = async (url, options) => {
        requests.push({ url, options });
        return { ok: true, json: async () => options.method === 'POST' ? { id: 'new' } : { package: clipboard } };
    };
    const sessionStorage = { getItem() { return null; }, setItem() {}, removeItem() {} };
    vm.runInNewContext(source, { document, window, navigator, fetch, sessionStorage, URL, Promise });
    const click = async action => {
        const button = { dataset: { elementType: kind, elementId: 'source' }, closest: () => section,
            classList: { contains: name => name === `js-roadmap-${action}` } };
        const event = { target: { closest: () => button }, preventDefault() {}, stopPropagation() {} };
        handlers.click[0](event);
        await new Promise(resolve => setImmediate(resolve));
    };
    return { click, requests, notices, sectionHandlers, section, handlers, menu, menuButton,
        get copied() { return copied; }, get reloaded() { return reloaded; }, set clipboard(value) { clipboard = value; } };
}

test('copy writes portable JSON and clone creates without changing clipboard', async () => {
    const copy = setup('task');
    await copy.click('copy');
    assert.equal(JSON.parse(copy.copied).elementType, 'task');
    assert.equal(copy.requests[0].url.searchParams.get('handler'), 'RoadmapElement');
    const clone = setup('stage');
    await clone.click('clone');
    assert.equal(clone.copied, '');
    assert.equal(clone.requests[1].url.searchParams.get('handler'), 'PasteRoadmapElement');
    assert.equal(JSON.parse(clone.requests[1].options.body).elementType, 'stage');
    assert.equal(clone.requests[1].options.headers.RequestVerificationToken, 'csrf');
    assert.equal(clone.reloaded, true);
});

test('paste rejects mismatched clipboard content and context menu exposes paste', async () => {
    const ui = setup('trigger');
    ui.clipboard = JSON.stringify({ kind: 'krackend-roadmap-element', schemaVersion: 1, elementType: 'task' });
    await ui.click('paste');
    assert.equal(ui.requests.length, 0);
    assert.equal(ui.notices[0].type, 'error');
    let prevented = false;
    ui.sectionHandlers.contextmenu({ target: { closest: () => null }, preventDefault() { prevented = true; }, clientX: 30, clientY: 40 });
    assert.equal(prevented, true);
    assert.equal(ui.menuButton.textContent, 'Paste trigger');
    assert.equal(ui.menu.style.display, 'block');
});
