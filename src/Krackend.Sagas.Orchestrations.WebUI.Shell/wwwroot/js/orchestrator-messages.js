(function () {
    const messageQueue = [];
    let activeMessage = null;

    const ensureHost = () => {
        let host = document.getElementById('orchestrator-message-modal-host');
        if (host) return host;

        host = document.createElement('div');
        host.id = 'orchestrator-message-modal-host';
        host.className = 'orchestrator-message-modal-host';
        host.setAttribute('aria-live', 'assertive');
        document.body.appendChild(host);
        return host;
    };

    const showMessage = options => new Promise(resolve => {
        messageQueue.push({ options, resolve });
        renderNextMessage();
    });

    const renderNextMessage = () => {
        if (activeMessage || messageQueue.length === 0) return;

        activeMessage = messageQueue.shift();
        const { options, resolve } = activeMessage;
        const type = options.type || 'info';
        const isConfirm = options.mode === 'confirm';
        const host = ensureHost();

        host.innerHTML = `
            <div class="orchestrator-message-backdrop"></div>
            <section class="orchestrator-message-box orchestrator-message-box-${escapeAttribute(type)}"
                     role="dialog"
                     aria-modal="true"
                     aria-labelledby="orchestrator-message-title">
                <div class="orchestrator-message-stripe" aria-hidden="true"></div>
                <div class="orchestrator-message-content">
                    <div class="orchestrator-message-header">
                        <div class="orchestrator-message-icon" aria-hidden="true">${getIcon(type)}</div>
                        <h2 id="orchestrator-message-title" class="orchestrator-message-title">${escapeHtml(options.title || 'Message')}</h2>
                    </div>
                    <p class="orchestrator-message-text">${escapeHtml(options.message || '')}</p>
                    <div class="orchestrator-message-actions">
                        ${isConfirm ? `<button type="button" class="btn btn-outline-secondary orchestrator-message-cancel">${escapeHtml(options.cancelText || 'Cancel')}</button>` : ''}
                        <button type="button" class="btn btn-primary orchestrator-message-accept">${escapeHtml(options.confirmText || options.okText || 'OK')}</button>
                    </div>
                </div>
            </section>
        `;

        host.classList.add('orchestrator-message-modal-host-open');

        const acceptButton = host.querySelector('.orchestrator-message-accept');
        const cancelButton = host.querySelector('.orchestrator-message-cancel');
        const close = value => {
            host.classList.remove('orchestrator-message-modal-host-open');
            host.innerHTML = '';
            activeMessage = null;
            resolve(value);
            renderNextMessage();
        };

        acceptButton.focus();
        acceptButton.addEventListener('click', () => close(true), { once: true });
        cancelButton?.addEventListener('click', () => close(false), { once: true });

        const onKeyDown = event => {
            if (!activeMessage) return;
            if (event.key === 'Escape') {
                event.preventDefault();
                document.removeEventListener('keydown', onKeyDown);
                close(!isConfirm);
            }
        };
        document.addEventListener('keydown', onKeyDown, { once: true });
    };

    const notify = ({ title, message, type, okText }) => showMessage({
        mode: 'notify',
        title,
        message,
        type,
        okText
    });

    const confirm = ({ title, message, confirmText, cancelText, type }) => showMessage({
        mode: 'confirm',
        title,
        message,
        confirmText,
        cancelText,
        type
    });

    const wireConfirmForms = () => {
        document.querySelectorAll('form[data-confirm="true"]').forEach(form => {
            if (form.dataset.confirmWired === 'true') return;
            form.dataset.confirmWired = 'true';

            form.addEventListener('submit', async event => {
                if (form.dataset.confirmed === 'true') {
                    form.dataset.confirmed = 'false';
                    return;
                }

                event.preventDefault();
                const accepted = await confirm({
                    title: form.dataset.confirmTitle,
                    message: form.dataset.confirmMessage,
                    confirmText: form.dataset.confirmAccept,
                    cancelText: form.dataset.confirmCancel,
                    type: form.dataset.confirmType
                });

                if (!accepted) return;
                form.dataset.confirmed = 'true';
                form.requestSubmit();
            });
        });
    };

    const getIcon = type => {
        switch ((type || '').toLowerCase()) {
            case 'success':
                return '&#10003;';
            case 'error':
                return '!';
            case 'warning':
                return '!';
            default:
                return 'i';
        }
    };

    const escapeHtml = value => (value || '').toString()
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const escapeAttribute = value => escapeHtml(value).replace(/\s+/g, '-').toLowerCase();

    window.OrchestratorMessages = { notify, confirm };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wireConfirmForms);
    } else {
        wireConfirmForms();
    }
})();
