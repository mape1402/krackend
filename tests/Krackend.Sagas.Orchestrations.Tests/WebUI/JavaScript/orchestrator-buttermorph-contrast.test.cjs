const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const source = fs.readFileSync(path.resolve(__dirname, '../../../../src/Krackend.Sagas.Orchestrations.ControlPlane.WebUI/wwwroot/js/orchestrator-buttermorph-contrast.js'), 'utf8');

test('applies the host contrast to the ButterMorph frame and updates it on theme changes', () => {
    const handlers = {}, values = {}, styles = [];
    const colors = { '--krackend-on-primary': '#ffffff', '--krackend-on-primary-hover': '#ffffff' };
    const frameDocument = {
        head: { appendChild: style => styles.push(style) },
        body: { style: { setProperty: (key, value) => { values[key] = value; } } },
        getElementById: id => styles.find(style => style.id === id),
        createElement: () => ({})
    };
    class HTMLIFrameElement {}
    const frame = Object.assign(new HTMLIFrameElement(), { id: 'buttermorph-host-frame', contentDocument: frameDocument });
    const document = { documentElement: {}, addEventListener: (name, callback) => { handlers[name] = callback; }, getElementById: () => frame };
    vm.runInNewContext(source, { document, HTMLIFrameElement, getComputedStyle: () => ({ getPropertyValue: key => colors[key] || '' }) });

    handlers.load({ target: frame });
    assert.equal(styles.length, 1);
    assert.equal(values['--bm-on-primary'], '#ffffff');
    assert.match(styles[0].textContent, /\.bm-topbar h1/);
    assert.match(styles[0].textContent, /\.bm-dock-titlebar/);
    assert.match(styles[0].textContent, /\.bm-view-button/);
    assert.match(styles[0].textContent, /\.bm-array-mapping/);

    colors['--krackend-on-primary'] = '#000000';
    colors['--krackend-on-primary-hover'] = '#111111';
    handlers['zoorce:theme-change']();
    assert.equal(styles.length, 1);
    assert.equal(values['--bm-on-primary'], '#000000');
    assert.equal(values['--bm-on-primary-hover'], '#111111');

    handlers.load({ target: Object.assign(new HTMLIFrameElement(), { id: 'unrelated-frame' }) });
    assert.equal(styles.length, 1);
});

test('ignores unavailable and cross-origin frames', () => {
    const handlers = {};
    class HTMLIFrameElement {}
    const document = { documentElement: {}, addEventListener: (name, callback) => { handlers[name] = callback; }, getElementById: () => null };
    vm.runInNewContext(source, { document, HTMLIFrameElement, getComputedStyle: () => ({ getPropertyValue: () => '' }) });
    const frame = Object.assign(new HTMLIFrameElement(), { id: 'buttermorph-host-frame' });
    assert.doesNotThrow(() => handlers.load({ target: frame }));
    Object.defineProperty(frame, 'contentDocument', { get() { throw new Error('cross-origin'); } });
    assert.doesNotThrow(() => handlers.load({ target: frame }));
    assert.doesNotThrow(() => handlers.load({ target: {} }));
    assert.doesNotThrow(() => handlers['zoorce:theme-change']());
});
