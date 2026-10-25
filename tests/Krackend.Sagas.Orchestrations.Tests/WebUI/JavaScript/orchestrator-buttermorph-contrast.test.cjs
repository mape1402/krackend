const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

function setup(foreground) {
    let load;
    class Frame {}
    const document = { documentElement: {}, addEventListener(name, handler, capture) {
        assert.equal(name, 'load');
        assert.equal(capture, true);
        load = handler;
    } };
    const script = path.resolve(__dirname, '../../../../src/Krackend.Sagas.Orchestrations.ControlPlane.WebUI/wwwroot/js/orchestrator-buttermorph-contrast.js');
    vm.runInNewContext(fs.readFileSync(script, 'utf8'), {
        document, HTMLIFrameElement: Frame,
        getComputedStyle: () => ({ getPropertyValue: () => foreground })
    });
    return { load: target => load({ target }), Frame };
}

test('uses the configured host foreground for the ButterMorph frame', () => {
    const ui = setup(' #081c33 ');
    const calls = [];
    const frame = new ui.Frame();
    frame.id = 'buttermorph-host-frame';
    frame.contentDocument = { body: { style: { setProperty: (...args) => calls.push(args) } } };
    ui.load(frame);
    assert.deepEqual(calls, [['--bm-on-primary', '#081c33']]);
    frame.id = 'unrelated-frame';
    ui.load(frame);
    assert.equal(calls.length, 1);
});

test('ignores unavailable and cross-origin frames', () => {
    const ui = setup('');
    const frame = new ui.Frame();
    frame.id = 'buttermorph-host-frame';
    ui.load(frame);
    Object.defineProperty(frame, 'contentDocument', { get() { throw new Error('cross-origin'); } });
    assert.doesNotThrow(() => ui.load(frame));
    assert.doesNotThrow(() => ui.load({}));
});
