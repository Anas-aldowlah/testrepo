(function () {
    'use strict';

    document.querySelectorAll('[data-role-change-form]').forEach(function (form) {
        const select = form.querySelector('[data-role-select]');
        const submit = form.querySelector('[data-role-submit]');
        if (!select || !submit) return;

        const currentRole = form.dataset.currentRole || '';
        const syncSubmitState = function () {
            submit.disabled = select.value === currentRole;
        };

        select.addEventListener('change', syncSubmitState);
        syncSubmitState();
    });
})();
