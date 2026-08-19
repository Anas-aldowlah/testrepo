(async function (window, document) {
    'use strict';

    const result = document.getElementById('harnessResult');
    const { scenario, fields, draftId1, draftId2 } = window.checkoutHarness;
    const key = 'yq-checkout-draft';
    const form = document.getElementById('yqCheckoutForm');
    const recovery = document.querySelector('[data-yq-checkout-draft-recovery]');
    let assertions = 0;

    function assert(condition, message) {
        assertions += 1;
        if (!condition) throw new Error(message);
    }

    function rawDraft() {
        const raw = localStorage.getItem(key);
        return raw ? JSON.parse(raw) : null;
    }

    function delay(milliseconds) {
        return new Promise(resolve => window.setTimeout(resolve, milliseconds));
    }

    function loadConfirmationScript() {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = `../../wwwroot/js/orders/confirmation.js?run=${Date.now()}-${Math.random()}`;
            script.onload = resolve;
            script.onerror = reject;
            document.body.appendChild(script);
        });
    }

    function authorizeConfirmation(draftId) {
        document.querySelector('[data-yq-confirmation-page]')
            .setAttribute('data-yq-clear-checkout-draft-id', draftId || '');
    }

    try {
        await delay(0);

        if (scenario === 'valid') {
            assert(!recovery.hidden, 'valid draft offers recovery');
            assert(form.elements.Governorate.value === '', 'draft is not silently restored');
            assert(form.elements.CheckoutDraftId.value === draftId1, 'restored draft ID is submitted with Checkout');
            recovery.querySelector('[data-yq-checkout-draft-restore]').click();
            Object.keys(fields).forEach(name => assert(form.elements[name].value === fields[name], `${name} restored`));
            assert(form.elements.PaymentMethod.value === 'server-payment', 'server payment selection is untouched');
            form.elements.City.value = 'سيئون';
            form.elements.City.dispatchEvent(new Event('input', { bubbles: true }));
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            assert(rawDraft().fields.City === 'سيئون', 'submit retains the latest delivery value until server success');
        } else if (scenario === 'fresh') {
            assert(!recovery.hidden, 'fresh choice is offered');
            recovery.querySelector('[data-yq-checkout-draft-fresh]').click();
            assert(recovery.hidden, 'fresh choice hides recovery');
            assert(localStorage.getItem(key) === null, 'fresh choice removes draft');
        } else if (scenario === 'expired' || scenario === 'mismatch' || scenario === 'malformed' || scenario === 'legacy') {
            assert(recovery.hidden, `${scenario} draft is not offered`);
            assert(localStorage.getItem(key) === null, `${scenario} draft is removed`);
        } else if (scenario === 'server') {
            assert(recovery.hidden, 'server values suppress recovery prompt');
            assert(form.elements.Governorate.value === 'عدن', 'server value remains authoritative');
        } else if (scenario === 'save') {
            form.elements.Governorate.value = 'عدن';
            form.elements.Governorate.dispatchEvent(new Event('input', { bubbles: true }));
            await delay(550);
            const record = rawDraft();
            assert(record.version === 2 && record.userId === '101', 'record is versioned and user-bound');
            assert(record.draftId === form.elements.CheckoutDraftId.value, 'record and submitted Checkout share one draft ID');
            assert(record.expiresAt - record.savedAt === 2 * 60 * 60 * 1000, 'record has exact two-hour TTL');
            assert(Object.keys(record.fields).sort().join(',') === ['City', 'DeliveryNotes', 'District', 'Governorate', 'Street'].sort().join(','), 'record has exact delivery allowlist');
            assert(record.fields.Governorate === 'عدن', 'edited delivery field is saved');
            assert(!Object.prototype.hasOwnProperty.call(record.fields, 'PaymentMethod'), 'payment is never stored');
            assert(!Object.prototype.hasOwnProperty.call(record.fields, 'SubmittedCartTotal'), 'hidden cart state is never stored');
        } else if (scenario === 'multiple-tabs') {
            assert(!recovery.hidden, 'recovery starts visible');
            const latest = rawDraft();
            latest.fields.City = 'سيئون';
            localStorage.setItem(key, JSON.stringify(latest));
            recovery.querySelector('[data-yq-checkout-draft-restore]').click();
            assert(form.elements.City.value === 'سيئون', 'restore re-reads the latest cross-tab record');
            localStorage.removeItem(key);
            window.dispatchEvent(new StorageEvent('storage', { key, oldValue: JSON.stringify(latest), newValue: null }));
            assert(recovery.hidden, 'cross-tab removal hides stale recovery');
        } else if (scenario === 'logout') {
            document.getElementById('logoutForm').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            assert(localStorage.getItem(key) === null, 'POST logout clears the draft');
        } else if (scenario === 'confirmation-immediate') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            assert(localStorage.getItem(key) === null, 'matching Order A / Draft D1 confirmation clears D1');
        } else if (scenario === 'confirmation-historical-then-immediate') {
            authorizeConfirmation('');
            await loadConfirmationScript();
            assert(rawDraft().draftId === draftId1, 'historical Confirmation B preserves D1');
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            assert(localStorage.getItem(key) === null, 'subsequent matching Confirmation A still clears D1');
        } else if (scenario === 'confirmation-reload') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            assert(localStorage.getItem(key) === null, 'first immediate confirmation consumes cleanup');
            await loadConfirmationScript();
            assert(localStorage.getItem(key) === null, 'confirmation reload is harmless after D1 is absent');
        } else if (scenario === 'confirmation-stale-newer') {
            assert(window.YaqutCheckoutDraft.write('101', draftId2, fields), 'newer D2 is stored after D1 succeeds');
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            assert(rawDraft().draftId === draftId2, 'stale Confirmation A cannot remove newer D2');
        } else if (scenario === 'confirmation-parallel') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            assert(localStorage.getItem(key) === null, 'first matching Confirmation A removes D1');
            assert(window.YaqutCheckoutDraft.write('101', draftId2, fields), 'D2 starts before replayed Confirmation A');
            await loadConfirmationScript();
            assert(rawDraft().draftId === draftId2, 'parallel or replayed Confirmation A cannot remove D2');
        } else if (scenario === 'storage-disabled') {
            form.elements.City.value = 'المكلا';
            form.elements.City.dispatchEvent(new Event('input', { bubbles: true }));
            await delay(550);
            assert(form.elements.City.value === 'المكلا', 'checkout remains usable when storage throws');
        } else {
            throw new Error(`Unknown scenario: ${scenario}`);
        }

        result.dataset.result = 'pass';
        result.textContent = `${scenario}: ${assertions} assertions passed`;
    } catch (error) {
        result.dataset.result = 'fail';
        result.textContent = `${scenario}: ${error.stack || error.message}`;
    }
})(window, document);
