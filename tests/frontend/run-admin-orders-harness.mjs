import { dirname, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { runBrowserHarness } from './browser-harness-runtime.mjs';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'admin-orders-harness.html')).href;
const widths = [320, 390, 576, 768, 992, 1200, 1440];
function delay(milliseconds) {
    return new Promise(function (resolveDelay) { setTimeout(resolveDelay, milliseconds); });
}

async function waitForHarness(client) {
    for (let attempt = 0; attempt < 100; attempt += 1) {
        const evaluation = await client.send('Runtime.evaluate', {
            expression: `(() => {
                const result = document.getElementById('harnessResult');
                return result && result.dataset.result ? {
                    status: result.dataset.result,
                    text: result.textContent,
                    width: window.innerWidth,
                    scrollWidth: document.documentElement.scrollWidth
                } : null;
            })()`,
            returnByValue: true
        });
        if (evaluation.result?.value) return evaluation.result.value;
        await delay(100);
    }
    throw new Error('Harness did not finish.');
}

await runBrowserHarness({
    profilePrefix: 'yagot-orders-harness-',
    harnessUrl,
    run: async function (client) {
        for (const width of widths) {
            await client.send('Emulation.setDeviceMetricsOverride', {
                width,
                height: 900,
                deviceScaleFactor: 1,
                mobile: width < 768
            });
            await client.send('Page.navigate', { url: harnessUrl });
            const result = await waitForHarness(client);
            if (result.status !== 'pass' || result.width !== width || result.scrollWidth > width) {
                throw new Error(`${width}px failed: ${JSON.stringify(result)}`);
            }
            console.log(`PASS ${width}px: ${result.text}; scrollWidth=${result.scrollWidth}`);
        }
    }
});

console.log(`Admin Orders browser harness passed at ${widths.length} exact CSS widths.`);
