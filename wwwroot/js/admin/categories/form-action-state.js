(function () {
    'use strict';

    function normalizedValue(input) {
        if (input.type === 'checkbox') return input.checked;
        if (input.type === 'file') {
            return [...input.files].map((file) => [file.name, file.size, file.type, file.lastModified]);
        }

        const value = input.value.trim();
        if (input.type === 'number' && value !== '') {
            const number = Number(value);
            return Number.isFinite(number) ? number : value;
        }
        return value;
    }

    function formState(form) {
        return JSON.stringify([...form.elements]
            .filter((input) => input.name && !input.disabled && input.type !== 'hidden'
                && input.type !== 'submit' && input.type !== 'reset' && input.type !== 'button')
            .map((input) => [input.name, normalizedValue(input)]));
    }

    document.querySelectorAll('[data-category-action-state]').forEach((form) => {
        const submit = form.querySelector('[data-category-action-submit]');
        if (!submit) return;

        const mode = form.dataset.categoryActionState;
        const initialState = formState(form);
        const sync = function () {
            const name = form.elements.namedItem('Name');
            const requiredNameValid = typeof name?.value === 'string' && name.value.trim().length > 0;
            const validator = window.jQuery?.(form).data('validator');
            const unobtrusiveValid = !validator || typeof validator.checkForm !== 'function' || validator.checkForm();
            const valid = requiredNameValid && form.checkValidity() && unobtrusiveValid;
            const hasChanges = mode === 'create' || formState(form) !== initialState;
            submit.disabled = !(hasChanges && valid);
        };

        form.addEventListener('input', sync);
        form.addEventListener('change', sync);
        form.addEventListener('reset', () => window.setTimeout(sync, 0));
        sync();
    });
})();
