import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runBrowserHarness } from './browser-harness-runtime.mjs';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'form-action-state-harness.html')).href;

function delay(milliseconds) {
    return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

await runBrowserHarness({
    profilePrefix: 'yagot-form-action-state-harness-',
    harnessUrl,
    async run(client) {
        for (const width of [390, 1200]) {
            await client.send('Emulation.setDeviceMetricsOverride', {
                width,
                height: 1200,
                deviceScaleFactor: 1,
                mobile: width < 768
            });
            await client.send('Page.navigate', { url: harnessUrl });

            for (let attempt = 0; attempt < 100; attempt += 1) {
                const ready = await client.send('Runtime.evaluate', {
                    expression: "document.readyState === 'complete'",
                    returnByValue: true
                });
                if (ready.result?.value) break;
                await delay(25);
            }

            const evaluation = await client.send('Runtime.evaluate', {
                expression: `(async () => {
                    const event = (type) => new Event(type, { bubbles: true });
                    const create = document.querySelector('[data-test-category-create]');
                    const createName = create.elements.Name;
                    const createSubmit = create.querySelector('[data-category-action-submit]');
                    const createInitialDisabled = createSubmit.disabled;
                    createName.value = 'عطور جديدة';
                    createName.dispatchEvent(event('input'));
                    const createValidEnabled = !createSubmit.disabled;
                    createName.value = '   ';
                    createName.dispatchEvent(event('input'));
                    const createInvalidDisabled = createSubmit.disabled;

                    const edit = document.querySelector('[data-test-category-edit]');
                    const editName = edit.elements.Name;
                    const editSubmit = edit.querySelector('[data-category-action-submit]');
                    const editInitialDisabled = editSubmit.disabled;
                    editName.value = 'عطور فاخرة';
                    editName.dispatchEvent(event('input'));
                    const editChangedEnabled = !editSubmit.disabled;
                    editName.value = 'عطور';
                    editName.dispatchEvent(event('input'));
                    const editRevertedDisabled = editSubmit.disabled;

                    const settings = document.querySelector('[data-test-settings]');
                    const settingsSubmit = settings.querySelector('[data-yq-settings-submit]');
                    const email = settings.elements.ContactEmail;
                    const settingsInitialDisabled = settingsSubmit.disabled;
                    email.value = 'new@example.com';
                    email.dispatchEvent(event('input'));
                    const settingsChangedEnabled = !settingsSubmit.disabled;
                    email.value = 'info@example.com';
                    email.dispatchEvent(event('input'));
                    const settingsRevertedDisabled = settingsSubmit.disabled;
                    email.value = 'invalid';
                    email.dispatchEvent(event('input'));
                    const settingsInvalidDisabled = settingsSubmit.disabled;
                    email.value = 'info@example.com';
                    email.dispatchEvent(event('input'));
                    const paymentList = settings.querySelector('[data-yq-payment-methods]');
                    const blankRow = document.createElement('section');
                    blankRow.dataset.yqPaymentMethodRow = '';
                    blankRow.innerHTML = '<input type="hidden" name="AdditionalPaymentMethods[1].Id" value=""><input type="hidden" name="AdditionalPaymentMethods[1].Delete" value="false" data-yq-payment-delete><input name="AdditionalPaymentMethods[1].Name" value=""><input name="AdditionalPaymentMethods[1].AccountHolderName" value=""><input name="AdditionalPaymentMethods[1].AccountNumber" value="">';
                    paymentList.appendChild(blankRow);
                    await new Promise((resolveWait) => setTimeout(resolveWait, 0));
                    const blankPaymentIgnored = settingsSubmit.disabled;
                    const blankName = blankRow.querySelector('input[name$=".Name"]');
                    blankName.value = 'بطاقة';
                    blankName.dispatchEvent(event('input'));
                    const namedPaymentEnabled = !settingsSubmit.disabled;
                    blankRow.remove();
                    await new Promise((resolveWait) => setTimeout(resolveWait, 0));
                    const removedPaymentDisabled = settingsSubmit.disabled;

                    const role = document.querySelector('[data-test-role]');
                    const roleSelect = role.querySelector('[data-role-select]');
                    const roleSubmit = role.querySelector('[data-role-submit]');
                    const roleInitialDisabled = roleSubmit.disabled;
                    roleSelect.value = 'Admin';
                    roleSelect.dispatchEvent(event('change'));
                    const roleChangedEnabled = !roleSubmit.disabled;
                    roleSelect.value = 'Customer';
                    roleSelect.dispatchEvent(event('change'));
                    const roleRevertedDisabled = roleSubmit.disabled;

                    const checkout = document.querySelector('[data-test-checkout]');
                    const checkoutButtons = [document.getElementById('confirmOrderBottom'), document.getElementById('yqPlaceOrderBtn')];
                    const checkoutInitialDisabled = checkoutButtons.every((button) => button.disabled);
                    checkout.elements.Governorate.value = 'عدن';
                    checkout.elements.City.value = 'كريتر';
                    checkout.elements.District.value = 'محمد';
                    checkout.elements.Street.value = '771234567';
                    checkout.elements.Street.dispatchEvent(event('input'));
                    const checkoutValidEnabled = checkoutButtons.every((button) => !button.disabled);
                    checkout.elements.Street.value = '123';
                    checkout.elements.Street.dispatchEvent(event('input'));
                    const checkoutInvalidDisabled = checkoutButtons.every((button) => button.disabled);

                    const buttons = [...document.querySelectorAll('button[type="submit"]')];
                    const badDisabledStyles = buttons.filter((button) => button.disabled).map((button) => {
                        const style = getComputedStyle(button);
                        return { id: button.id, className: button.className, cursor: style.cursor, opacity: style.opacity };
                    }).filter((item) => item.cursor !== 'not-allowed' || Number(item.opacity) >= 1);
                    const inBounds = buttons.every((button) => {
                        const rect = button.getBoundingClientRect();
                        return rect.left >= -0.5 && rect.right <= innerWidth + 0.5;
                    });
                    return {
                        createInitialDisabled, createValidEnabled, createInvalidDisabled,
                        editInitialDisabled, editChangedEnabled, editRevertedDisabled,
                        settingsInitialDisabled, settingsChangedEnabled, settingsRevertedDisabled,
                        settingsInvalidDisabled, blankPaymentIgnored, namedPaymentEnabled, removedPaymentDisabled,
                        roleInitialDisabled, roleChangedEnabled, roleRevertedDisabled,
                        checkoutInitialDisabled, checkoutValidEnabled, checkoutInvalidDisabled,
                        inBounds,
                        noOverflow: document.documentElement.scrollWidth <= document.documentElement.clientWidth,
                        disabledStyles: badDisabledStyles.length === 0,
                        badDisabledStyles
                    };
                })()`,
                awaitPromise: true,
                returnByValue: true
            });
            const result = evaluation.result?.value;
            if (!result || Object.entries(result).some(([key, value]) => key !== 'badDisabledStyles' && value !== true)) {
                throw new Error(`${width}px form action state harness failed: ${JSON.stringify(result)}`);
            }
            console.log(`PASS ${width}px: Category, Settings, User Role, and Checkout action states.`);
        }
    }
});

console.log('Form action state browser harness passed at 390px and 1200px.');
