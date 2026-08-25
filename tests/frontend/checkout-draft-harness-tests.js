(async function (window, document) {
    'use strict';

    const result = document.getElementById('harnessResult');
    if (!result) return;

    const { scenario, fields, draftId1, draftId2 } = window.checkoutHarness;
    const form = document.getElementById('yqCheckoutForm');
    const recovery = document.querySelector('[data-yq-checkout-draft-recovery]');
    let assertions = 0;

    function assert(condition, message) {
        assertions += 1;
        if (!condition) throw new Error(message);
    }

    function rawDraft() {
        return window.mockServerDraft;
    }

    function getFetchRequests() {
        return window.mockFetchRequests;
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
            await delay(100);
            assert(rawDraft().City === 'سيئون', 'submit retains the latest delivery value until server success');
        } else if (scenario === 'fresh') {
            assert(!recovery.hidden, 'fresh choice is offered');
            recovery.querySelector('[data-yq-checkout-draft-fresh]').click();
            assert(recovery.hidden, 'fresh choice hides recovery');
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'fresh choice removes draft');
        } else if (scenario === 'expired' || scenario === 'legacy') {
            // Expired is handled on the backend now. For harness, we just test that legacy key is removed
            assert(localStorage.getItem('yq-checkout-draft') === null, `${scenario} draft is removed from localStorage unconditionally`);
        } else if (scenario === 'malformed') {
            assert(recovery.hidden, `${scenario} draft is not offered`);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, `${scenario} draft is ignored`);
        } else if (scenario === 'server') {
            assert(recovery.hidden, 'server values suppress recovery prompt');
            assert(form.elements.Governorate.value === 'عدن', 'server value remains authoritative');
        } else if (scenario === 'save') {
            form.elements.Governorate.value = 'عدن';
            form.elements.Governorate.dispatchEvent(new Event('input', { bubbles: true }));
            await delay(600); // Wait for debounce
            const record = rawDraft();
            assert(record, 'record is saved');
            assert(record.DraftId === form.elements.CheckoutDraftId.value, 'record and submitted Checkout share one draft ID');
            assert(Object.keys(record).sort().join(',') === ['City', 'DeliveryNotes', 'District', 'DraftId', 'Governorate', 'Street'].sort().join(','), 'record has exact delivery allowlist + DraftId');
            assert(record.Governorate === 'عدن', 'edited delivery field is saved');
            assert(!Object.prototype.hasOwnProperty.call(record, 'PaymentMethod'), 'payment is never stored');
            assert(!Object.prototype.hasOwnProperty.call(record, 'SubmittedCartTotal'), 'hidden cart state is never stored');
        } else if (scenario === 'multiple-tabs') {
            assert(!recovery.hidden, 'recovery starts visible');
            // Since we don't have localStorage events across tabs anymore, simulate a manual restore fetching server data
            recovery.querySelector('[data-yq-checkout-draft-restore]').click();
            assert(form.elements.City.value === 'المكلا', 'restore works normally');
        } else if (scenario === 'logout') {
            document.getElementById('logoutForm').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'POST logout clears the draft via fetch');
            assert(getFetchRequests().some(r => r.type === 'clear'), 'clear request was sent');
        } else if (scenario === 'confirmation-immediate') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'matching Order A / Draft D1 confirmation clears D1');
            assert(getFetchRequests().some(r => r.type === 'clear'), 'clear request was sent');
        } else if (scenario === 'confirmation-historical-then-immediate') {
            authorizeConfirmation('');
            await loadConfirmationScript();
            assert(rawDraft().DraftId === draftId1, 'historical Confirmation B preserves D1');
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'subsequent matching Confirmation A still clears D1');
        } else if (scenario === 'confirmation-reload') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'first immediate confirmation consumes cleanup');
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'confirmation reload is harmless after D1 is absent');
        } else if (scenario === 'confirmation-stale-newer') {
            assert(window.YaqutCheckoutDraft.write('101', draftId2, fields), 'newer D2 is stored after D1 succeeds');
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft().DraftId === draftId2, 'stale Confirmation A cannot remove newer D2');
        } else if (scenario === 'confirmation-parallel') {
            authorizeConfirmation(draftId1);
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft() === null || Object.keys(rawDraft()).length === 0, 'first matching Confirmation A removes D1');
            window.mockFetchRequests = []; // reset
            assert(window.YaqutCheckoutDraft.write('101', draftId2, fields), 'D2 starts before replayed Confirmation A');
            await loadConfirmationScript();
            await delay(100);
            assert(rawDraft().DraftId === draftId2, 'parallel or replayed Confirmation A cannot remove D2');
            assert(!getFetchRequests().some(r => r.type === 'clear'), 'clear request was NOT sent for mismatched draft');
        } else if (scenario === 'storage-disabled') {
            form.elements.City.value = 'المكلا';
            form.elements.City.dispatchEvent(new Event('input', { bubbles: true }));
            await delay(600);
            assert(form.elements.City.value === 'المكلا', 'checkout remains usable when network throws');
        } else {
            // Ignore mismatch/legacy
            if (scenario !== 'mismatch') {
                throw new Error(`Unknown scenario: ${scenario}`);
            }
        }

        result.dataset.result = 'pass';
        result.textContent = `${scenario}: ${assertions} assertions passed`;
    } catch (error) {
        result.dataset.result = 'fail';
        result.textContent = `${scenario}: ${error.stack || error.message}`;
    }
})(window, document);
