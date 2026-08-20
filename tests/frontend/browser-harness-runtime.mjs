import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync } from 'node:fs';
import { rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, dirname, join, resolve } from 'node:path';

const DEFAULT_CLEANUP_STEP_TIMEOUT_MS = 5000;
const DEFAULT_DEVTOOLS_OPERATION_TIMEOUT_MS = 10000;

function delay(milliseconds) {
    return new Promise(function (resolveDelay) { setTimeout(resolveDelay, milliseconds); });
}

function withTimeout(operation, milliseconds, label) {
    let timeout;
    const timeoutPromise = new Promise((resolveTimeout, rejectTimeout) => {
        timeout = setTimeout(function () {
            rejectTimeout(new Error(`${label} timed out after ${milliseconds}ms.`));
        }, milliseconds);
    });

    return Promise.race([operation, timeoutPromise]).finally(function () { clearTimeout(timeout); });
}

function describeThrownValue(value) {
    return value instanceof Error ? value.message : String(value);
}

async function waitForProcessExit(childProcess) {
    if (childProcess.exitCode !== null || childProcess.signalCode !== null) return;
    await new Promise((resolveExit, rejectExit) => {
        const onExit = function () {
            childProcess.removeListener('error', onError);
            resolveExit();
        };
        const onError = function (error) {
            childProcess.removeListener('exit', onExit);
            rejectExit(error);
        };
        childProcess.once('exit', onExit);
        childProcess.once('error', onError);
    });
}

function hasProcessExited(childProcess) {
    return !childProcess || !childProcess.pid || childProcess.exitCode !== null || childProcess.signalCode !== null;
}

async function runProcess(command, args, timeoutMilliseconds, label) {
    const childProcess = spawn(command, args, { stdio: 'ignore' });
    try {
        await withTimeout(waitForProcessExit(childProcess), timeoutMilliseconds, label);
    } catch (error) {
        if (!hasProcessExited(childProcess)) {
            childProcess.kill();
            childProcess.unref();
        }
        throw error;
    }
    return childProcess.exitCode;
}

function signalProcessGroup(processId, signal) {
    try {
        process.kill(-processId, signal);
    } catch (error) {
        if (error.code !== 'ESRCH') throw error;
    }
}

function isProcessGroupRunning(processId) {
    try {
        process.kill(-processId, 0);
        return true;
    } catch (error) {
        if (error.code === 'ESRCH') return false;
        throw error;
    }
}

async function waitForProcessGroupExit(processId) {
    while (isProcessGroupRunning(processId)) await delay(50);
}

async function terminateBrowser(browser, timeoutMilliseconds) {
    if (process.platform === 'win32') {
        if (hasProcessExited(browser)) return;
        const exitCode = await runProcess(
            'taskkill.exe',
            ['/PID', String(browser.pid), '/T', '/F'],
            timeoutMilliseconds,
            'Browser process-tree termination'
        );
        if (exitCode !== 0 && !hasProcessExited(browser)) {
            throw new Error(`Browser process-tree termination failed with exit code ${exitCode}.`);
        }
        await withTimeout(waitForProcessExit(browser), timeoutMilliseconds, 'Browser process exit');
        return;
    }

    if (!browser?.pid) return;
    signalProcessGroup(browser.pid, 'SIGTERM');
    try {
        await withTimeout(waitForProcessGroupExit(browser.pid), timeoutMilliseconds, 'Browser process-group exit');
    } catch (error) {
        if (isProcessGroupRunning(browser.pid)) {
            signalProcessGroup(browser.pid, 'SIGKILL');
            await withTimeout(waitForProcessGroupExit(browser.pid), timeoutMilliseconds, 'Forced browser process-group exit');
        }
    }
    await withTimeout(waitForProcessExit(browser), timeoutMilliseconds, 'Browser process exit');
}

function validateProfilePrefix(profilePrefix) {
    if (!/^yagot-[a-z0-9-]+-harness-$/.test(profilePrefix)) {
        throw new Error(`Invalid browser profile prefix: ${profilePrefix}`);
    }
}

function validateProfileDirectory(profileDirectory, profilePrefix) {
    const resolvedProfile = resolve(profileDirectory);
    const profileName = basename(resolvedProfile);
    const suffix = profileName.slice(profilePrefix.length);
    validateProfilePrefix(profilePrefix);
    if (dirname(resolvedProfile) !== resolve(tmpdir())
        || !profileName.startsWith(profilePrefix)
        || !/^[A-Za-z0-9]{6}$/.test(suffix)) {
        throw new Error(`Refusing to remove unexpected browser profile path: ${resolvedProfile}`);
    }
    return resolvedProfile;
}

export class DevToolsClient {
    constructor(webSocketUrl, WebSocketConstructor = WebSocket, options = {}) {
        this.nextId = 1;
        this.pending = new Map();
        this.operationTimeoutMilliseconds = options.operationTimeoutMilliseconds || DEFAULT_DEVTOOLS_OPERATION_TIMEOUT_MS;
        this.socket = new WebSocketConstructor(webSocketUrl);
        this.handleMessage = this.handleMessage.bind(this);
        this.handleDisconnect = this.handleDisconnect.bind(this);
        this.handleSocketError = this.handleSocketError.bind(this);
    }

    async connect() {
        await new Promise((resolveConnection, rejectConnection) => {
            let timeout;
            const finish = function (complete, value) {
                clearTimeout(timeout);
                this.socket.removeEventListener('open', onOpen);
                this.socket.removeEventListener('error', onError);
                complete(value);
            }.bind(this);
            const onOpen = function () {
                finish(resolveConnection);
            }.bind(this);
            const onError = function (event) {
                finish(rejectConnection, event.error || new Error('DevTools WebSocket connection failed.'));
            }.bind(this);
            this.socket.addEventListener('open', onOpen, { once: true });
            this.socket.addEventListener('error', onError, { once: true });
            timeout = setTimeout(function () {
                finish(rejectConnection, new Error(`DevTools WebSocket connection timed out after ${this.operationTimeoutMilliseconds}ms.`));
            }.bind(this), this.operationTimeoutMilliseconds);
        });
        this.socket.addEventListener('message', this.handleMessage);
        this.socket.addEventListener('error', this.handleSocketError);
        this.socket.addEventListener('close', this.handleDisconnect, { once: true });
    }

    handleMessage(event) {
        let message;
        try {
            message = JSON.parse(event.data);
        } catch (error) {
            this.rejectPending(error);
            return;
        }
        if (!message.id || !this.pending.has(message.id)) return;
        const pending = this.pending.get(message.id);
        this.pending.delete(message.id);
        if (message.error) pending.reject(new Error(message.error.message));
        else pending.resolve(message.result);
    }

    handleDisconnect() {
        this.socket.removeEventListener('message', this.handleMessage);
        this.socket.removeEventListener('error', this.handleSocketError);
        this.rejectPending(new Error('DevTools WebSocket closed before the command completed.'));
    }

    handleSocketError(event) {
        this.rejectPending(event.error || new Error('DevTools WebSocket operation failed.'));
    }

    rejectPending(error) {
        for (const pending of this.pending.values()) pending.reject(error);
        this.pending.clear();
    }

    send(method, params = {}) {
        if (this.socket.readyState !== 1) {
            return Promise.reject(new Error(`Cannot send ${method}; DevTools WebSocket is not open.`));
        }
        const id = this.nextId;
        this.nextId += 1;
        return new Promise((resolveCommand, rejectCommand) => {
            const timeout = setTimeout(function () {
                this.pending.delete(id);
                rejectCommand(new Error(`${method} timed out after ${this.operationTimeoutMilliseconds}ms.`));
            }.bind(this), this.operationTimeoutMilliseconds);
            const settle = function (complete, value) {
                clearTimeout(timeout);
                complete(value);
            };
            this.pending.set(id, {
                resolve: function (result) { settle(resolveCommand, result); },
                reject: function (error) { settle(rejectCommand, error); }
            });
            try {
                this.socket.send(JSON.stringify({ id, method, params }));
            } catch (error) {
                const pending = this.pending.get(id);
                this.pending.delete(id);
                pending.reject(error);
            }
        });
    }

    async close() {
        if (this.socket.readyState === 3) {
            this.handleDisconnect();
            return;
        }

        await new Promise((resolveClose, rejectClose) => {
            const onClose = function () {
                this.socket.removeEventListener('error', onError);
                resolveClose();
            }.bind(this);
            const onError = function (event) {
                this.socket.removeEventListener('close', onClose);
                rejectClose(event.error || new Error('DevTools WebSocket close failed.'));
            }.bind(this);
            this.socket.addEventListener('close', onClose, { once: true });
            this.socket.addEventListener('error', onError, { once: true });
            if (this.socket.readyState === 0 || this.socket.readyState === 1) this.socket.close();
        });
    }
}

async function waitForDebugPort(profileDirectory, resources) {
    const portFile = join(profileDirectory, 'DevToolsActivePort');
    for (let attempt = 0; attempt < 100; attempt += 1) {
        if (resources.browserError) {
            throw new Error(`Browser failed to start: ${describeThrownValue(resources.browserError)}`, { cause: resources.browserError });
        }
        if (hasProcessExited(resources.browser)) {
            throw new Error(`Browser exited before the DevTools port became available (exit=${resources.browser.exitCode}, signal=${resources.browser.signalCode}).`);
        }
        if (existsSync(portFile)) {
            const [port] = readFileSync(portFile, 'utf8').split(/\r?\n/);
            if (port) return Number(port);
        }
        await delay(100);
    }
    throw new Error('Browser DevTools port did not become available.');
}

export async function cleanupBrowserHarness(resources, options = {}) {
    const timeoutMilliseconds = options.timeoutMilliseconds || DEFAULT_CLEANUP_STEP_TIMEOUT_MS;
    const failures = [];
    const capture = async function (label, operation) {
        try {
            await operation();
        } catch (error) {
            failures.push(new Error(`${label}: ${describeThrownValue(error)}`, { cause: error }));
        }
    };

    if (resources.client) {
        await capture('DevTools cleanup failed', function () {
            return withTimeout(resources.client.close(), timeoutMilliseconds, 'DevTools WebSocket close');
        });
    }
    await capture('Browser cleanup failed', function () {
        return terminateBrowser(resources.browser, timeoutMilliseconds);
    });
    if (hasProcessExited(resources.browser)) {
        if (resources.browserErrorHandler) resources.browser.removeListener('error', resources.browserErrorHandler);
        await capture('Temporary profile cleanup failed', async function () {
            const profileDirectory = validateProfileDirectory(resources.profileDirectory, resources.profilePrefix);
            await withTimeout(
                rm(profileDirectory, { recursive: true, force: true, maxRetries: 3, retryDelay: 100 }),
                timeoutMilliseconds,
                'Temporary profile removal'
            );
        });
    } else {
        failures.push(new Error('Temporary profile cleanup skipped because the browser process is still running.'));
    }

    if (failures.length === 1) throw failures[0];
    if (failures.length > 1) throw new AggregateError(failures, 'Multiple browser harness cleanup operations failed.');
}

export async function runWithCleanup(run, cleanup) {
    let testFailed = false;
    let testError;
    try {
        await run();
    } catch (error) {
        testFailed = true;
        testError = error;
    }

    let cleanupFailed = false;
    let cleanupError;
    try {
        await cleanup();
    } catch (error) {
        cleanupFailed = true;
        cleanupError = error;
    }

    if (testFailed) {
        if (cleanupFailed) console.error('Browser harness cleanup also failed:', cleanupError);
        throw testError;
    }
    if (cleanupFailed) throw cleanupError;
}

export async function runBrowserHarness(options) {
    validateProfilePrefix(options.profilePrefix);
    const chromeCandidates = [
        process.env.CHROME_PATH,
        'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
        'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
        'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe'
    ].filter(Boolean);
    const browserPath = chromeCandidates.find(existsSync);
    if (!browserPath) throw new Error('Chrome or Edge was not found. Set CHROME_PATH to run the browser harness.');

    const profileDirectory = mkdtempSync(join(tmpdir(), options.profilePrefix));
    const resources = {
        browser: null,
        client: null,
        browserError: null,
        browserErrorHandler: null,
        profileDirectory,
        profilePrefix: options.profilePrefix
    };

    await runWithCleanup(async function () {
        resources.browser = spawn(browserPath, [
            '--headless=new',
            '--disable-gpu',
            '--disable-background-mode',
            '--no-first-run',
            '--disable-default-apps',
            '--allow-file-access-from-files',
            '--remote-debugging-port=0',
            `--user-data-dir=${profileDirectory}`,
            'about:blank'
        ], { stdio: 'ignore', detached: process.platform !== 'win32' });
        resources.browserErrorHandler = function (error) { resources.browserError ||= error; };
        resources.browser.on('error', resources.browserErrorHandler);
        const port = await waitForDebugPort(profileDirectory, resources);
        const target = await fetch(`http://127.0.0.1:${port}/json/new?${encodeURIComponent(options.harnessUrl)}`, {
            method: 'PUT',
            signal: AbortSignal.timeout(options.operationTimeoutMilliseconds || DEFAULT_DEVTOOLS_OPERATION_TIMEOUT_MS)
        }).then(function (response) {
            if (!response.ok) throw new Error(`Unable to create browser target: ${response.status}`);
            return response.json();
        });
        resources.client = new DevToolsClient(target.webSocketDebuggerUrl, WebSocket, {
            operationTimeoutMilliseconds: options.operationTimeoutMilliseconds
        });
        await resources.client.connect();
        await resources.client.send('Runtime.enable');
        await resources.client.send('Page.enable');
        await options.run(resources.client);
        if (resources.browserError) throw resources.browserError;
    }, function () {
        return cleanupBrowserHarness(resources, { timeoutMilliseconds: options.cleanupTimeoutMilliseconds });
    });
}
