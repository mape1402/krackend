(function () {
    const messageQueue = [];
    let activeMessage = null;
    let busyHost = null;
    let formEventsWired = false;
    let globalErrorsWired = false;

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
        if (activeMessage || messageQueue.length === 0 || busyHost) return;

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

    const showBusy = ({ title, message }) => {
        const host = ensureHost();
        busyHost = host;
        host.innerHTML = `
            <div class="orchestrator-message-backdrop"></div>
            <section class="orchestrator-message-box orchestrator-message-box-busy"
                     role="dialog"
                     aria-modal="true"
                     aria-labelledby="orchestrator-busy-title">
                <div class="orchestrator-message-stripe" aria-hidden="true"></div>
                <div class="orchestrator-message-content">
                    <div class="orchestrator-message-header">
                        <div class="orchestrator-message-spinner" aria-hidden="true"></div>
                        <h2 id="orchestrator-busy-title" class="orchestrator-message-title">${escapeHtml(title || 'Working')}</h2>
                    </div>
                    <p class="orchestrator-message-text">${escapeHtml(message || 'Please wait while the operation finishes.')}</p>
                </div>
            </section>
        `;
        host.classList.add('orchestrator-message-modal-host-open');

        return () => {
            if (busyHost !== host) return;
            host.classList.remove('orchestrator-message-modal-host-open');
            host.innerHTML = '';
            busyHost = null;
            renderNextMessage();
        };
    };

    const showValidation = ({ title, errors }) => notify({
        title: title || 'Review the capture',
        message: normalizeErrors(errors).map(x => `- ${x}`).join('\n'),
        type: 'error',
        okText: 'Review'
    });

    const wireForms = () => {
        document.querySelectorAll('form').forEach(form => {
            prepareForm(form);
        });

        if (formEventsWired) return;
        formEventsWired = true;
        document.addEventListener('submit', handleFormSubmit);
        document.addEventListener('input', event => clearFieldValidation(event.target));
        document.addEventListener('change', event => clearFieldValidation(event.target));
    };

    const prepareForm = form => {
        if (!form || form.tagName?.toLowerCase() !== 'form') return;
        form.setAttribute('novalidate', 'novalidate');
    };

    const handleFormSubmit = async event => {
        const form = event.target;
        if (!form || form.tagName?.toLowerCase() !== 'form') return;
        prepareForm(form);

        if (form.dataset.orchestratorSubmitReady === 'true') {
            form.dataset.orchestratorSubmitReady = 'false';
            showBusyIfConfigured(form);
            return;
        }

        const errors = collectValidationErrors(form);
        if (errors.length > 0) {
            event.preventDefault();
            focusFirstInvalid(form);
            await showValidation({ errors });
            return;
        }

        if (form.dataset.confirm === 'true') {
            event.preventDefault();
            const accepted = await confirm({
                title: form.dataset.confirmTitle,
                message: form.dataset.confirmMessage,
                confirmText: form.dataset.confirmAccept,
                cancelText: form.dataset.confirmCancel,
                type: form.dataset.confirmType
            });

            if (!accepted) return;
            form.dataset.orchestratorSubmitReady = 'true';
            form.requestSubmit();
            return;
        }

        showBusyIfConfigured(form);
    };

    const showBusyIfConfigured = form => {
        if (form.dataset.orchestratorBusy !== 'true') return;
        showBusy({
            title: form.dataset.busyTitle || 'Working',
            message: form.dataset.busyMessage || 'Please wait while the operation finishes.'
        });
    };

    const collectValidationErrors = form => {
        clearFormValidation(form);
        const errors = [];
        const fields = Array.from(form.elements).filter(shouldValidateField);

        fields.forEach(field => {
            const fieldErrors = validateField(field);
            if (fieldErrors.length === 0) return;
            markInvalid(field, fieldErrors[0]);
            errors.push(...fieldErrors);
        });

        return errors;
    };

    const validateField = field => {
        const errors = [];
        const value = getFieldValue(field);
        const label = getFieldLabel(field);
        const requiredMessage = field.getAttribute('data-val-required');
        const requiredOnCreateTarget = field.getAttribute('data-required-on-create');
        const isCheckbox = (field.type || '').toLowerCase() === 'checkbox';
        const isCreate = requiredOnCreateTarget
            ? !document.getElementById(requiredOnCreateTarget)?.value
            : false;

        if ((requiredMessage || isCreate) && !value && !isCheckbox) {
            errors.push(requiredMessage || `${label} is required.`);
            return errors;
        }

        if (!value) return errors;

        const maxLength = readMaxLength(field);
        if (maxLength && value.length > maxLength) {
            errors.push(`${label} must be ${maxLength} characters or fewer.`);
        }

        if (field.dataset.orchestratorKey === 'true' && !/^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$/.test(value)) {
            errors.push(`${label} must use lowercase segments separated by dot or dash.`);
        }

        if (field.dataset.orchestratorVersion === 'true' && !/^\d+\.\d+\.\d+$/.test(value)) {
            errors.push(`${label} must use semantic version format, for example 1.0.0.`);
        }

        if (field.dataset.orchestratorUrl === 'true' && !isValidUrl(value)) {
            errors.push(`${label} must be a valid absolute URL.`);
        }

        if (field.dataset.orchestratorUlid === 'true' && !/^[0-7][0-9A-HJKMNP-TV-Z]{25}$/i.test(value)) {
            errors.push(`${label} must be a valid ULID.`);
        }

        if (field.dataset.orchestratorStatusCodes === 'true' && !isValidStatusCodeList(value)) {
            errors.push(`${label} must contain HTTP status codes separated by commas.`);
        }

        if (field.dataset.orchestratorJson === 'true' && !isValidJson(value)) {
            errors.push(`${label} must contain valid JSON.`);
        }

        const regexPattern = field.getAttribute('data-val-regex-pattern');
        if (regexPattern && !(new RegExp(regexPattern).test(value))) {
            errors.push(field.getAttribute('data-val-regex') || `${label} has an invalid format.`);
        }

        validateRange(field, value, label, errors);
        return errors;
    };

    const shouldValidateField = field => {
        const type = (field.type || '').toLowerCase();
        return field.name &&
            !field.disabled &&
            type !== 'hidden' &&
            type !== 'button' &&
            type !== 'submit' &&
            type !== 'reset';
    };

    const getFieldValue = field => {
        if ((field.type || '').toLowerCase() === 'checkbox') {
            return field.checked ? 'true' : '';
        }

        return (field.value || '').trim();
    };

    const getFieldLabel = field => {
        const explicit = field.getAttribute('data-validation-label');
        if (explicit) return explicit;

        if (field.id) {
            const label = document.querySelector(`label[for="${cssEscape(field.id)}"]`);
            if (label) return label.textContent.trim().replace(/\s+/g, ' ');
        }

        const nearbyLabel = field.closest('.mb-2, .mb-3, .col, .col-12, [class*="col-md-"], [class*="col-lg-"]')?.querySelector('label');
        if (nearbyLabel) return nearbyLabel.textContent.trim().replace(/\s+/g, ' ');

        return field.name.split('.').pop();
    };

    const markInvalid = (field, message) => {
        field.classList.add('is-invalid');
        field.setAttribute('aria-invalid', 'true');

        const validation = findValidationMessage(field);
        if (validation) {
            validation.textContent = message;
            validation.classList.remove('field-validation-valid');
            validation.classList.add('field-validation-error');
        }
    };

    const clearFormValidation = form => {
        form.querySelectorAll('.is-invalid').forEach(x => x.classList.remove('is-invalid'));
        form.querySelectorAll('[aria-invalid="true"]').forEach(x => x.removeAttribute('aria-invalid'));
        form.querySelectorAll('[data-valmsg-for]').forEach(x => {
            x.textContent = '';
            x.classList.remove('field-validation-error');
            x.classList.add('field-validation-valid');
        });
    };

    const clearFieldValidation = field => {
        if (!field?.classList) return;
        field.classList.remove('is-invalid');
        field.removeAttribute('aria-invalid');
        const validation = findValidationMessage(field);
        if (validation) {
            validation.textContent = '';
            validation.classList.remove('field-validation-error');
            validation.classList.add('field-validation-valid');
        }
    };

    const findValidationMessage = field => {
        const names = [field.name, field.getAttribute('asp-for')].filter(Boolean);
        for (const name of names) {
            const message = field.form?.querySelector(`[data-valmsg-for="${cssEscape(name)}"]`);
            if (message) return message;
        }

        return null;
    };

    const focusFirstInvalid = form => {
        const invalid = form.querySelector('.is-invalid');
        invalid?.focus({ preventScroll: false });
    };

    const readMaxLength = field => {
        const dataMax = field.getAttribute('data-val-length-max');
        if (dataMax && Number(dataMax) > 0) return Number(dataMax);
        const max = field.getAttribute('maxlength');
        return max && Number(max) > 0 ? Number(max) : 0;
    };

    const validateRange = (field, value, label, errors) => {
        const min = field.getAttribute('min') || field.getAttribute('data-val-range-min');
        const max = field.getAttribute('max') || field.getAttribute('data-val-range-max');
        if (!min && !max) return;

        const number = Number(value);
        if (Number.isNaN(number)) {
            errors.push(`${label} must be numeric.`);
            return;
        }

        if (min && number < Number(min)) {
            errors.push(`${label} must be greater than or equal to ${min}.`);
        }

        if (max && number > Number(max)) {
            errors.push(`${label} must be less than or equal to ${max}.`);
        }
    };

    const isValidUrl = value => {
        try {
            const url = new URL(value);
            return url.protocol === 'http:' || url.protocol === 'https:';
        } catch {
            return false;
        }
    };

    const isValidStatusCodeList = value => value
        .split(',')
        .map(x => Number(x.trim()))
        .every(x => Number.isInteger(x) && x >= 100 && x <= 599);

    const isValidJson = value => {
        try {
            JSON.parse(value);
            return true;
        } catch {
            return false;
        }
    };

    const normalizeErrors = errors => (errors || [])
        .map(x => (x || '').toString().trim())
        .filter(Boolean);

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

    const cssEscape = value => window.CSS?.escape ? window.CSS.escape(value) : value.replace(/"/g, '\\"');

    const wireGlobalErrors = () => {
        if (globalErrorsWired) return;
        globalErrorsWired = true;

        window.addEventListener('error', event => {
            notify({
                title: 'Unexpected UI error',
                message: event.message || 'The UI hit an unexpected error.',
                type: 'error'
            });
        });

        window.addEventListener('unhandledrejection', event => {
            notify({
                title: 'Unexpected UI error',
                message: event.reason?.message || event.reason || 'The UI hit an unexpected error.',
                type: 'error'
            });
        });
    };

    window.OrchestratorMessages = {
        notify,
        confirm,
        showBusy,
        showValidation
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => {
            wireForms();
            wireGlobalErrors();
        });
    } else {
        wireForms();
        wireGlobalErrors();
    }
})();
