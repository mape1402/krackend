(() => {
    const initializeSidebar = () => {
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
    };

    const initializeInfiniteGrid = grid => {
        if (!grid || grid.dataset.odInfiniteReady === 'true') {
            return;
        }

        const items = Array.from(grid.children)
            .filter(item => item.classList.contains('od-infinite-item'));
        if (items.length === 0) {
            return;
        }

        grid.dataset.odInfiniteReady = 'true';
        const parsePositiveInt = (value, fallback) => {
            const parsed = Number.parseInt(value || '', 10);
            return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
        };
        const batchSize = parsePositiveInt(grid.dataset.odInfiniteBatchSize, 24);
        const initialCount = parsePositiveInt(grid.dataset.odInfiniteInitialCount, batchSize);
        const label = grid.dataset.odInfiniteLabel || 'items';
        const container = grid.closest('[data-od-infinite-list]') || grid.parentElement || document;
        const status = container.querySelector('[data-od-infinite-status]');
        const count = status?.querySelector('[data-od-infinite-count]');
        const total = status?.querySelector('[data-od-infinite-total]');
        const sentinel = container.querySelector('[data-od-infinite-sentinel]');

        items.forEach((item, index) => {
            item.classList.toggle('d-none', index >= initialCount);
        });

        const visibleCount = () => items.filter(item => !item.classList.contains('d-none')).length;

        const updateStatus = () => {
            const visible = visibleCount();
            if (count) {
                count.textContent = visible.toString();
            }
            if (total) {
                total.textContent = items.length.toString();
            }

            if (status) {
                status.classList.toggle('d-none', items.length <= initialCount);
                if (visible >= items.length) {
                    status.textContent = `All ${items.length} ${label} loaded`;
                }
            }

            sentinel?.classList.toggle('d-none', visible >= items.length);
        };

        const revealNext = () => {
            items
                .filter(item => item.classList.contains('d-none'))
                .slice(0, batchSize)
                .forEach(item => item.classList.remove('d-none'));
            updateStatus();
        };

        updateStatus();

        if (!sentinel || visibleCount() >= items.length) {
            return;
        }

        if ('IntersectionObserver' in window) {
            const observer = new IntersectionObserver(entries => {
                if (entries.some(entry => entry.isIntersecting)) {
                    revealNext();
                    if (visibleCount() >= items.length) {
                        observer.disconnect();
                    }
                }
            }, { rootMargin: '240px 0px' });
            observer.observe(sentinel);
            return;
        }

        window.addEventListener('scroll', () => {
            if (visibleCount() >= items.length) {
                return;
            }

            const scrollBottom = window.scrollY + window.innerHeight;
            const documentBottom = document.documentElement.scrollHeight - 240;
            if (scrollBottom >= documentBottom) {
                revealNext();
            }
        }, { passive: true });
    };

    const initializeInfiniteGrids = () => {
        document.querySelectorAll('[data-od-infinite-grid]').forEach(initializeInfiniteGrid);
    };

    initializeSidebar();
    initializeInfiniteGrids();

    window.KrackendOrchestratorInfiniteGrids = {
        refresh: initializeInfiniteGrids
    };
})();
