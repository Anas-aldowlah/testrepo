import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'products-catalog-harness.html')).href;
const browserPath = [
    process.env.CHROME_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
].filter(Boolean).find(existsSync);
if (!browserPath) throw new Error('Chrome or Edge was not found.');

const profileDirectory = mkdtempSync(join(tmpdir(), 'yagot-products-catalog-'));
const browser = spawn(browserPath, [
    '--headless=new', '--disable-gpu', '--no-first-run', '--disable-default-apps',
    '--allow-file-access-from-files', '--remote-debugging-port=0',
    `--user-data-dir=${profileDirectory}`, 'about:blank'
], { stdio: 'ignore' });

function delay(milliseconds) {
    return new Promise((resolveDelay) => setTimeout(resolveDelay, milliseconds));
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
        this.socket.addEventListener('message', (event) => {
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

let client;
try {
    const port = await waitForDebugPort();
    const target = await fetch(`http://127.0.0.1:${port}/json/new?${encodeURIComponent(harnessUrl)}`, { method: 'PUT' }).then((response) => response.json());
    client = new DevToolsClient(target.webSocketDebuggerUrl);
    await client.connect();
    await client.send('Runtime.enable');
    await client.send('Page.enable'); await client.send('Runtime.enable'); client.socket.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.method === 'Runtime.consoleAPICalled') { console.log('BROWSER CONSOLE:', m.params.args.map(a => a.value || a.description).join(' ')); } if (m.method === 'Runtime.exceptionThrown') { console.log('BROWSER ERROR:', m.params.exceptionDetails); } }); await client.send('Runtime.enable'); client.socket.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.method === 'Runtime.consoleAPICalled') { console.log('BROWSER CONSOLE:', m.params.args.map(a => a.value || a.description).join(' ')); } if (m.method === 'Runtime.exceptionThrown') { console.log('BROWSER ERROR:', m.params.exceptionDetails); } }); await client.send('Runtime.enable'); client.socket.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.method === 'Runtime.consoleAPICalled') { console.log('BROWSER CONSOLE:', m.params.args.map(a => a.value || a.description).join(' ')); } if (m.method === 'Runtime.exceptionThrown') { console.log('BROWSER ERROR:', m.params.exceptionDetails); } }); await client.send('Runtime.enable'); client.socket.addEventListener('message', e => { const m = JSON.parse(e.data); if (m.method === 'Runtime.consoleAPICalled') { console.log('BROWSER CONSOLE:', m.params.args.map(a => a.value || a.description).join(' ')); } if (m.method === 'Runtime.exceptionThrown') { console.log('BROWSER ERROR:', m.params.exceptionDetails); } });

    for (const width of [320, 390, 576, 768, 992, 1200, 1440]) {
        await client.send('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: width < 768 });
        await client.send('Page.navigate', { url: harnessUrl });
        let result;
        for (let attempt = 0; attempt < 100; attempt += 1) {
            const evaluation = await client.send('Runtime.evaluate', {
                expression: `(() => { const el = document.getElementById('harnessResult'); return el?.dataset.result ? { status: el.dataset.result, text: el.textContent, errors: window.catalogHarness?.errors || [], scrollWidth: document.documentElement.scrollWidth, width: window.innerWidth } : null; })()`,
                returnByValue: true
            });
            result = evaluation.result?.value;
            if (result) break;
            await delay(50);
        }
        if (!result || result.status !== 'pass' || result.errors.length || result.scrollWidth > width) {
            throw new Error(`${width}px catalog harness failed: ${JSON.stringify(result)}`);
        }
        console.log(`PASS ${width}px: ${result.text}`);
    }
    console.log('Products catalog browser harness passed at 7 RTL viewport widths.');
} finally {
    if (client) client.close();
    browser.kill();
}
