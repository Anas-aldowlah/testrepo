import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'checkout-draft-harness.html')).href;
const repositoryRoot = resolve(currentDirectory, '..', '..');
const scenarios = ['valid', 'fresh', 'expired', 'mismatch', 'malformed', 'legacy', 'server', 'save', 'multiple-tabs', 'logout', 'confirmation-immediate', 'confirmation-historical-then-immediate', 'confirmation-reload', 'confirmation-stale-newer', 'confirmation-parallel', 'storage-disabled'];
const browserPath = [
    process.env.CHROME_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
].filter(Boolean).find(existsSync);

if (!browserPath) throw new Error('Chrome or Edge was not found. Set CHROME_PATH to run the browser harness.');

const controllerSource = readFileSync(resolve(repositoryRoot, 'Controllers', 'OrdersController.cs'), 'utf8');
const confirmationViewSource = readFileSync(resolve(repositoryRoot, 'Views', 'Orders', 'Confirmation.cshtml'), 'utf8');
const confirmationScriptSource = readFileSync(resolve(repositoryRoot, 'wwwroot', 'js', 'orders', 'confirmation.js'), 'utf8');
const storageSource = readFileSync(resolve(repositoryRoot, 'wwwroot', 'js', 'checkout-draft-storage.js'), 'utf8');
const createOrderPosition = controllerSource.indexOf('CreateOrderAsync(');
const markerWritePosition = controllerSource.indexOf('TempData[CheckoutSuccessMarkerKey(order.Id)]');
const markerPeekPosition = controllerSource.indexOf('TempData.Peek(markerKey)');
const markerRemovePosition = controllerSource.indexOf('TempData.Remove(markerKey)');
const sourceContracts = [
    [createOrderPosition >= 0 && markerWritePosition > createOrderPosition, 'successful Checkout writes its marker only after order creation'],
    [controllerSource.includes('CheckoutSuccessMarkerKey(order.Id)') && controllerSource.includes('$"{order.Id}:{checkoutDraftId:D}"'), 'success marker correlates OrderId and DraftId'],
    [controllerSource.includes('if (order == null || order.Userid != userId) return NotFound();') && controllerSource.indexOf('ConsumeCheckoutSuccessMarker(id)') > controllerSource.indexOf('order.Userid != userId'), 'Confirmation verifies order ownership before marker consumption'],
    [markerPeekPosition >= 0 && markerRemovePosition > markerPeekPosition && controllerSource.includes('successfulOrderId != orderId'), 'mismatched Confirmation inspects non-destructively and cannot consume the marker'],
    [controllerSource.includes('ViewData["CheckoutDraftIdToClear"] = ConsumeCheckoutSuccessMarker(id);'), 'Confirmation exposes only the verified DraftId'],
    [confirmationViewSource.includes('data-yq-clear-checkout-draft-id="@checkoutDraftIdToClear"'), 'Razor safely carries the verified DraftId'],
    [confirmationScriptSource.includes('window.YaqutCheckoutDraft.remove(draftId);'), 'Confirmation requests removal of its exact DraftId'],
    [storageSource.includes('record.draftId !== draftId') && storageSource.includes('function remove(draftId)'), 'storage removal requires the currently stored DraftId to match']
];
for (const [passed, description] of sourceContracts) {
    if (!passed) throw new Error(`Checkout success contract failed: ${description}`);
}
console.log(`PASS Checkout success source contracts: ${sourceContracts.length}`);

const profileDirectory = mkdtempSync(join(tmpdir(), 'yagot-checkout-draft-harness-'));
const browser = spawn(browserPath, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--disable-default-apps',
    '--allow-file-access-from-files', '--remote-debugging-port=0',
    `--user-data-dir=${profileDirectory}`, 'about:blank'
], { stdio: 'ignore' });

function delay(milliseconds) {
    return new Promise(resolveDelay => setTimeout(resolveDelay, milliseconds));
}

async function waitForDebugPort() {
    const portFile = join(profileDirectory, 'DevToolsActivePort');
    for (let attempt = 0; attempt < 100; attempt += 1) {
        if (existsSync(portFile)) {
            const [port] = readFileSync(portFile, 'utf8').split(/\r?\n/);
            if (port) return Number(port);
        }
        await delay(100);
    }
    throw new Error('Browser DevTools port did not become available.');
}

class DevToolsClient {
    constructor(webSocketUrl) {
        this.nextId = 1;
        this.pending = new Map();
        this.socket = new WebSocket(webSocketUrl);
    }

    async connect() {
        await new Promise((resolveConnection, rejectConnection) => {
            this.socket.addEventListener('open', resolveConnection, { once: true });
            this.socket.addEventListener('error', rejectConnection, { once: true });
        });
        this.socket.addEventListener('message', event => {
            const message = JSON.parse(event.data);
            if (!message.id || !this.pending.has(message.id)) return;
            const pending = this.pending.get(message.id);
            this.pending.delete(message.id);
            if (message.error) pending.reject(new Error(message.error.message));
            else pending.resolve(message.result);
        });
    }

    send(method, params = {}) {
        const id = this.nextId++;
        return new Promise((resolveCommand, rejectCommand) => {
            this.pending.set(id, { resolve: resolveCommand, reject: rejectCommand });
            this.socket.send(JSON.stringify({ id, method, params }));
        });
    }

    close() { this.socket.close(); }
}

async function waitForHarness(client) {
    for (let attempt = 0; attempt < 100; attempt += 1) {
        const evaluation = await client.send('Runtime.evaluate', {
            expression: `(() => { const result = document.getElementById('harnessResult'); return result?.dataset.result ? { status: result.dataset.result, text: result.textContent } : null; })()`,
            returnByValue: true
        });
        if (evaluation.result?.value) return evaluation.result.value;
        await delay(100);
    }
    throw new Error('Harness did not finish.');
}

let client;
try {
    const port = await waitForDebugPort();
    const target = await fetch(`http://127.0.0.1:${port}/json/new?${encodeURIComponent(harnessUrl)}`, { method: 'PUT' }).then(response => response.json());
    client = new DevToolsClient(target.webSocketDebuggerUrl);
    await client.connect();
    await client.send('Runtime.enable');
    await client.send('Page.enable');

    for (const scenario of scenarios) {
        await client.send('Page.navigate', { url: `${harnessUrl}?scenario=${scenario}` });
        const result = await waitForHarness(client);
        if (result.status !== 'pass') throw new Error(result.text);
        console.log(`PASS ${result.text}`);
    }
    console.log(`Checkout draft browser harness passed ${scenarios.length} scenarios.`);
} finally {
    if (client) client.close();
    browser.kill();
}
