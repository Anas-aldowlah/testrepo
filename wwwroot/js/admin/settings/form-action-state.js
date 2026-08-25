(function () {
    'use strict';

    const form = document.querySelector('[data-yq-settings-form]');
    const submit = form?.querySelector('[data-yq-settings-submit]');
    const paymentList = form?.querySelector('[data-yq-payment-methods]');
    if (!form || !submit) return;

    function normalize(input) {
        if (input.type === 'checkbox') return input.checked;
        const value = input.value.trim();
        if (input.type === 'number' && value !== '') {
            const number = Number(value);
            return Number.isFinite(number) ? number : value;
        }
        return value;
    }

    function settingsState() {
        const fields = [...form.elements]
            .filter((input) => input.name && !input.disabled && input.type !== 'hidden'
                && input.type !== 'submit' && input.type !== 'reset' && input.type !== 'button'
                && !input.closest('[data-yq-payment-method-row]'))
            .map((input) => [input.name, normalize(input)]);

        const additionalMethods = [...form.querySelectorAll('[data-yq-payment-method-row]')]
            .map((row) => {
                const id = row.querySelector('input[name$=".Id"]')?.value || '';
                const deleted = row.querySelector('[data-yq-payment-delete]')?.value === 'true';
                const name = row.querySelector('input[name$=".Name"]')?.value.trim() || '';
                if (!id && !name) return null;
                if (deleted) return { id, deleted: true };
                return {
                    id,
                    name,
                    accountHolderName: row.querySelector('input[name$=".AccountHolderName"]')?.value.trim() || '',
                    accountNumber: row.querySelector('input[name$=".AccountNumber"]')?.value.trim() || ''
                };
            })
            .filter(Boolean);

        return JSON.stringify({ fields, additionalMethods });
    }

    const initialState = settingsState();
    const sync = function () {
        const validator = window.jQuery?.(form).data('validator');
        const unobtrusiveValid = !validator || typeof validator.checkForm !== 'function' || validator.checkForm();
        submit.disabled = !(settingsState() !== initialState && form.checkValidity() && unobtrusiveValid);
    };

    form.addEventListener('input', sync);
    form.addEventListener('change', sync);
    form.addEventListener('reset', () => window.setTimeout(sync, 0));
    if (paymentList) {
        new MutationObserver(sync).observe(paymentList, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['hidden']
        });
    }
    sync();
})();
