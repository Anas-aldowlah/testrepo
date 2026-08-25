import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runBrowserHarness } from './browser-harness-runtime.mjs';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'admin-product-edit-harness.html')).href;
const widths = [320, 390, 576, 768, 992, 1200, 1440];
const states = ['edit-retail', 'edit-non-retail', 'create-retail', 'create-non-retail'];

function delay(milliseconds) {
    return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
}

await runBrowserHarness({
    profilePrefix: 'yagot-admin-product-edit-harness-',
    harnessUrl,
    async run(client) {
        for (const state of states) {
          for (const width of widths) {
            const stateUrl = new URL(harnessUrl);
            stateUrl.searchParams.set('state', state);
            await client.send('Emulation.setDeviceMetricsOverride', {
                width,
                height: 1000,
                deviceScaleFactor: 1,
                mobile: width < 768
            });
            await client.send('Page.navigate', { url: stateUrl.href });

            let result;
            for (let attempt = 0; attempt < 100; attempt += 1) {
                const evaluation = await client.send('Runtime.evaluate', {
                    expression: `(() => {
                        const output = document.getElementById('harnessResult');
                        if (!output?.dataset.result) return null;
                        const controls = [...document.querySelectorAll('input, select, button, .yq-product-form__section')];
                        const outOfBounds = controls.some((element) => {
                            const rect = element.getBoundingClientRect();
                            return rect.left < -0.5 || rect.right > window.innerWidth + 0.5;
                        });
                        const retail = document.querySelector('[data-retail-prices]');
                        const retailRow = retail?.querySelector('.yq-retail-price-row');
                        const retailControls = retailRow?.querySelector('.yq-retail-controls');
                        const retailRect = retail?.getBoundingClientRect();
                        const sectionRect = retail?.parentElement?.getBoundingClientRect();
                        const activeRect = retailRow?.querySelector('.yq-retail-active-field')?.getBoundingClientRect();
                        const deleteRect = retailRow?.querySelector('.yq-retail-remove')?.getBoundingClientRect();
                        return {
                            status: output.dataset.result,
                            text: output.textContent,
                            errors: window.adminProductEditHarness?.errors || [],
                            scrollWidth: document.documentElement.scrollWidth,
                            clientWidth: document.documentElement.clientWidth,
                            outOfBounds,
                            direction: getComputedStyle(document.documentElement).direction,
                            retailLayout: retail && !retail.hidden ? {
                                widthRatio: retailRect.width / sectionRect.width,
                                columns: getComputedStyle(retailControls).gridTemplateColumns.trim().split(/\\s+/).length,
                                addButtonInHeader: Boolean(retail.querySelector('.yq-retail-prices__head [data-add-retail-price]')),
                                activeCell: Boolean(retailRow.querySelector('.yq-retail-active-field')),
                                activeDeleteAligned: Boolean(activeRect && deleteRect && Math.abs(activeRect.bottom - deleteRect.bottom) <= 1)
                            } : null
                        };
                    })()`,
                    returnByValue: true
                });
                result = evaluation.result?.value;
                if (result) break;
                await delay(50);
            }

            const expectedRetailColumns = width >= 768 ? 4 : 2;
            const retailLayoutFailed = !state.endsWith('non-retail') && (
                !result?.retailLayout ||
                result.retailLayout.columns !== expectedRetailColumns ||
                !result.retailLayout.addButtonInHeader ||
                !result.retailLayout.activeCell ||
                (width < 576 && !result.retailLayout.activeDeleteAligned) ||
                (width >= 992 && result.retailLayout.widthRatio < 0.9)
            );
            if (!result || result.status !== 'pass' || result.errors.length || result.scrollWidth > result.clientWidth || result.outOfBounds || result.direction !== 'rtl' || retailLayoutFailed) {
                throw new Error(`${state} ${width}px Admin Product Edit harness failed: ${JSON.stringify(result)}`);
            }
            console.log(`PASS ${state} ${width}px: ${result.text}`);
          }
        }
    }
});

console.log('Admin Product Create/Edit browser harness passed for retail and non-retail states at 7 RTL viewport widths.');
