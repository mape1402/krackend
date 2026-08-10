(() => {
    const appShell = document.getElementById('orchestratorAppShell');
    const toggleButton = document.getElementById('sidebar-toggle');

    if (!appShell || !toggleButton) {
        return;
    }

    toggleButton.addEventListener('click', () => {
        appShell.classList.toggle('sidebar-collapsed');
    });
})();
