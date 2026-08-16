import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const currentDirectory = dirname(fileURLToPath(import.meta.url));
const harnessUrl = pathToFileURL(resolve(currentDirectory, 'home-carousel-harness.html')).href;
const widths = [320, 390, 576, 768, 992, 1200, 1440];
const chromeCandidates = [
    process.env.CHROME_PATH,
    'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
].filter(Boolean);
const browserPath = chromeCandidates.find(existsSync);

if (!browserPath) throw new Error('Chrome or Edge was not found. Set CHROME_PATH to run the browser harness.');

const profileDirectory = mkdtempSync(join(tmpdir(), 'yagot-home-carousel-harness-'));
const browser = spawn(browserPath, [
    '--headless=new',
    '--disable-gpu',
    '--no-first-run',
    '--disable-default-apps',
    '--allow-file-access-from-files',
    '--remote-debugging-port=0',
    `--user-data-dir=${profileDirectory}`,
    'about:blank'
], { stdio: 'ignore' });

function delay(milliseconds) {
    return new Promise(function (resolveDelay) { setTimeout(resolveDelay, milliseconds); });
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
        const id = this.nextId;
        this.nextId += 1;
        return new Promise((resolveCommand, rejectCommand) => {
            this.pending.set(id, { resolve: resolveCommand, reject: rejectCommand });
            this.socket.send(JSON.stringify({ id, method, params }));
        });
    }

    close() {
        this.socket.close();
    }
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

async function runAtWidth(client, width, reducedMotion) {
    await client.send('Emulation.setDeviceMetricsOverride', {
        width,
        height: 900,
        deviceScaleFactor: 1,
        mobile: width < 768
    });
    await client.send('Page.navigate', { url: `${harnessUrl}?reduced=${reducedMotion ? '1' : '0'}` });
    const result = await waitForHarness(client);
    if (result.status !== 'pass' || result.width !== width || result.scrollWidth > width) {
        throw new Error(`${width}px${reducedMotion ? ' reduced-motion' : ''} failed: ${JSON.stringify(result)}`);
    }
    console.log(`PASS ${width}px${reducedMotion ? ' reduced-motion' : ''}: ${result.text}; scrollWidth=${result.scrollWidth}`);
}

let client;
try {
    const port = await waitForDebugPort();
    const target = await fetch(`http://127.0.0.1:${port}/json/new?${encodeURIComponent(harnessUrl)}`, { method: 'PUT' }).then(function (response) {
        if (!response.ok) throw new Error(`Unable to create browser target: ${response.status}`);
        return response.json();
    });

    client = new DevToolsClient(target.webSocketDebuggerUrl);
    await client.connect();
    await client.send('Runtime.enable');
    await client.send('Page.enable');

    for (const width of widths) await runAtWidth(client, width, false);
    await runAtWidth(client, 390, true);

    console.log(`Home carousel browser harness passed at ${widths.length} exact CSS widths plus reduced motion.`);
} finally {
    if (client) client.close();
    browser.kill();
}
