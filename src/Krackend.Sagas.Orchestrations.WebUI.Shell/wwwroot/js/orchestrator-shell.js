(() => {
    const appShell = document.getElementById('orchestratorAppShell');
    const toggleButton = document.getElementById('sidebar-toggle');
    const backdrop = document.querySelector('[data-sidebar-backdrop]');
    const mobileQuery = window.matchMedia('(max-width: 991.98px)');

    if (!appShell || !toggleButton) {
        return;
    }

    const syncSidebarForViewport = () => {
        if (mobileQuery.matches) {
            appShell.classList.add('sidebar-collapsed');
            return;
        }

        appShell.classList.remove('sidebar-collapsed');
    };

    toggleButton.addEventListener('click', () => {
        appShell.classList.toggle('sidebar-collapsed');
    });

    backdrop?.addEventListener('click', () => {
        appShell.classList.add('sidebar-collapsed');
    });

    document.querySelectorAll('.sidebar a').forEach(link => {
        link.addEventListener('click', () => {
            if (mobileQuery.matches) {
                appShell.classList.add('sidebar-collapsed');
            }
        });
    });

    mobileQuery.addEventListener('change', syncSidebarForViewport);
    syncSidebarForViewport();
})();
