import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runBrowserHarness } from './browser-harness-runtime.mjs';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'admin-feedback-harness.html')).href;

function delay(milliseconds) {
    return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

await runBrowserHarness({
    profilePrefix: 'yagot-admin-feedback-harness-',
    harnessUrl,
    async run(client) {
        for (const currentRole of ['Customer', 'Admin']) {
            for (const width of [390, 1200]) {
                const stateUrl = new URL(harnessUrl);
                stateUrl.searchParams.set('current', currentRole);
                await client.send('Emulation.setDeviceMetricsOverride', {
                    width,
                    height: 900,
                    deviceScaleFactor: 1,
                    mobile: width < 768
                });
                await client.send('Page.navigate', { url: stateUrl.href });

                let ready = false;
                for (let attempt = 0; attempt < 100; attempt += 1) {
                    const evaluation = await client.send('Runtime.evaluate', {
                        expression: "document.readyState === 'complete' && Boolean(window.adminFeedbackHarness)",
                        returnByValue: true
                    });
                    ready = evaluation.result?.value === true;
                    if (ready) break;
                    await delay(25);
                }
                if (!ready) throw new Error(`Harness did not load for ${currentRole} at ${width}px.`);

                const alternateRole = currentRole === 'Customer' ? 'Admin' : 'Customer';
                const evaluation = await client.send('Runtime.evaluate', {
                    expression: `(() => {
                        const form = document.querySelector('[data-role-change-form]');
                        const select = form.querySelector('[data-role-select]');
                        const button = form.querySelector('[data-role-submit]');
                        const alert = document.querySelector('[data-test-alert]');
                        const initialDisabled = button.disabled;
                        const disabledStyle = getComputedStyle(button);
                        button.click();
                        const disabledSubmitCount = window.adminFeedbackHarness.submitCount;
                        select.value = ${JSON.stringify(alternateRole)};
                        select.dispatchEvent(new Event('change', { bubbles: true }));
                        const enabledAfterChange = !button.disabled;
                        const enabledCursor = getComputedStyle(button).cursor;
                        button.click();
                        const enabledSubmitCount = window.adminFeedbackHarness.submitCount;
                        select.value = ${JSON.stringify(currentRole)};
                        select.dispatchEvent(new Event('change', { bubbles: true }));
                        const disabledAfterRestore = button.disabled;
                        const alertRect = alert.getBoundingClientRect();
                        const buttonRect = button.getBoundingClientRect();
                        const alertStyle = getComputedStyle(alert);
                        return {
                            initialDisabled,
                            disabledSubmitCount,
                            enabledAfterChange,
                            enabledSubmitCount,
                            disabledAfterRestore,
                            disabledCursor: disabledStyle.cursor,
                            disabledOpacity: Number(disabledStyle.opacity),
                            enabledCursor,
                            alertBackground: alertStyle.backgroundColor,
                            alertColor: alertStyle.color,
                            direction: alertStyle.direction,
                            scrollWidth: document.documentElement.scrollWidth,
                            clientWidth: document.documentElement.clientWidth,
                            alertInBounds: alertRect.left >= -0.5 && alertRect.right <= innerWidth + 0.5,
                            buttonInBounds: buttonRect.left >= -0.5 && buttonRect.right <= innerWidth + 0.5
                        };
                    })()`,
                    returnByValue: true
                });
                const result = evaluation.result?.value;
                if (!result?.initialDisabled
                    || result.disabledSubmitCount !== 0
                    || !result.enabledAfterChange
                    || result.enabledSubmitCount !== 1
                    || !result.disabledAfterRestore
                    || result.disabledCursor !== 'not-allowed'
                    || result.disabledOpacity >= 1
                    || result.enabledCursor !== 'pointer'
                    || result.direction !== 'rtl'
                    || result.scrollWidth > result.clientWidth
                    || !result.alertInBounds
                    || !result.buttonInBounds
                    || result.alertBackground === 'rgba(0, 0, 0, 0)') {
                    throw new Error(`${currentRole} ${width}px Admin feedback harness failed: ${JSON.stringify(result)}`);
                }
                console.log(`PASS ${currentRole} ${width}px: role state, click behavior, RTL alert, and bounds.`);
            }
        }
    }
});

console.log('Admin feedback browser harness passed at 390px and 1200px.');
