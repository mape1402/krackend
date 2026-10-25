const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const root = path.resolve(__dirname, '../../../..');
const cases = [
    ['control plane', 'src/Krackend.Sagas.Orchestrations.ControlPlane.WebUI/Areas/OrchestratorDistribution/Pages/RuntimeNodes/Index.cshtml', 'runtimeNodeSetupWizard'],
    ['runtime', 'src/Krackend.Sagas.Orchestrations.Runtime.WebUI/Areas/OrchestratorRuntime/Pages/DesignNodes/Index.cshtml', 'designNodeSetupWizard']
];

function setupWizard(file, wizardId, saved) {
    const source = fs.readFileSync(path.join(root, file), 'utf8');
    const start = source.indexOf('const showSetupStep = step => {');
    const end = source.indexOf('const moveSetup = direction => {', start);
    assert.ok(start >= 0 && end > start, 'wizard step handler exists');
    const steps = ['basic', 'policy', 'runtimeCredentials', 'designCredentials', 'verify'];
    const items = steps.map(id => ({ dataset: { zStepId: id } }));
    const panels = steps.map(id => ({ dataset: { setupStep: id }, classList: { toggle() {} } }));
    const back = {};
    const next = {};
    let snapshot;
    const context = {
        setupSequence: steps.slice(0, 4), setupStep: 'basic', currentNode: saved ? { dataset: { id: 'node' } } : null,
        document: {
            querySelectorAll(selector) { return selector.includes('data-z-wizard-step') ? items : panels; },
            getElementById(id) { return id === 'setupBackButton' ? back : next; }
        },
        window: { ZoorceWizards: { get(id) { assert.equal(id, wizardId); return { applySnapshot(value) { snapshot = value; } }; } } }
    };
    vm.runInNewContext(`${source.slice(start, end)}; this.showStep = showSetupStep;`, context);
    return { show: context.showStep, get snapshot() { return snapshot; }, back, next };
}

for (const [name, file, wizardId] of cases) {
    test(`${name}: unsaved nodes cannot navigate beyond basic data`, () => {
        const wizard = setupWizard(file, wizardId, false);
        wizard.show('basic');
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'basic').state, 'Active');
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'policy').isNavigable, false);
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'verify').state, 'Disabled');
        assert.equal(wizard.back.disabled, true);
    });

    test(`${name}: saved nodes show the active step and final action`, () => {
        const wizard = setupWizard(file, wizardId, true);
        wizard.show('designCredentials');
        assert.equal(wizard.snapshot.currentStepId, 'designCredentials');
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'basic').state, 'Completed');
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'designCredentials').state, 'Active');
        assert.equal(wizard.snapshot.steps.find(step => step.id === 'policy').isNavigable, true);
        assert.equal(wizard.next.textContent, 'Finish');
        assert.equal(wizard.back.disabled, false);
    });

    test(`${name}: unavailable steps fall back to basic data`, () => {
        const wizard = setupWizard(file, wizardId, true);
        wizard.show('verify');
        assert.equal(wizard.snapshot.currentStepId, 'basic');
        assert.equal(wizard.next.textContent, 'Next');
    });
}
