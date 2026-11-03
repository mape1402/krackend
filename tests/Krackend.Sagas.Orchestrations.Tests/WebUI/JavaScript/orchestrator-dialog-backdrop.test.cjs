const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

test('capture dialogs disable backdrop closing and retain explicit close', () => {
    const rootPath = path.resolve(__dirname, '../../../..');
    const roots = ['ControlPlane', 'Runtime'].map(part => path.join(rootPath, `src/Krackend.Sagas.Orchestrations.${part}.WebUI`));
    let count = 0;
    function visit(directory) {
        for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
            if (entry.isDirectory() && !['bin', 'obj'].includes(entry.name)) visit(path.join(directory, entry.name));
            if (!entry.isFile() || !entry.name.endsWith('.cshtml')) continue;
            const file = path.join(directory, entry.name);
            const text = fs.readFileSync(file, 'utf8');
            for (const match of text.matchAll(/new (?:Zoorce\.Web\.UI\.Foundation\.Components\.)?ZoorceDialogOptions\s*\{/g)) {
                count++;
                assert.match(text.slice(match.index + match[0].length), /^\s*CloseOnBackdrop\s*=\s*false\s*,/, file);
            }
        }
    }
    roots.forEach(visit);
    assert.ok(count > 0);

    const packages = process.env.NUGET_PACKAGES || path.join(process.env.USERPROFILE || process.env.HOME, '.nuget', 'packages');
    const source = fs.readFileSync(path.join(packages, 'zoorce.web.ui.foundation/1.2.1/staticwebassets/js/zoorce-foundation-controls.js'), 'utf8');
    const handlers = {};
    const closeButton = { addEventListener(name, handler) { handlers[`close-${name}`] = handler; } };
    const dialog = { focus() {}, querySelectorAll() { return []; } };
    const root = {
        id: 'capture', hidden: true, dataset: { zDialogBackdrop: 'false' },
        querySelector() { return dialog; },
        querySelectorAll(selector) { return selector === '[data-z-dialog-close]' ? [closeButton] : []; },
        addEventListener(name, handler) { handlers[name] = handler; }, dispatchEvent() {}
    };
    const document = { activeElement: null, addEventListener() {}, querySelectorAll: selector => selector === '[data-z-component-dialog]' ? [root] : [] };
    vm.runInNewContext(source, { document, HTMLElement: class {}, CustomEvent: class {} });
    root.open();
    handlers.click({ target: root });
    assert.equal(root.hidden, false);
    handlers['close-click']();
    assert.equal(root.hidden, true);
});
