import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import { existsSync, mkdtempSync } from 'node:fs';
import { readdir, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
    cleanupBrowserHarness,
    DevToolsClient,
    runBrowserHarness,
    runWithCleanup
} from './browser-harness-runtime.mjs';

let assertions = 0;

async function checkRejects(operation, predicate) {
    let rejection;
    try {
        await operation();
    } catch (error) {
        rejection = error;
    }
    assert.ok(rejection, 'Expected operation to reject.');
    assert.ok(predicate(rejection), `Unexpected rejection: ${rejection.stack || rejection}`);
    assertions += 1;
}

await runWithCleanup(
    async function () { assertions += 1; },
    async function () { assertions += 1; }
);

let caughtUndefined = false;
try {
    await runWithCleanup(async function () { throw undefined; }, async function () {});
} catch (error) {
    caughtUndefined = true;
    assert.equal(error, undefined);
}
assert.equal(caughtUndefined, true);
assertions += 1;

let caughtNullCleanup = false;
try {
    await runWithCleanup(async function () {}, async function () { throw null; });
} catch (error) {
    caughtNullCleanup = true;
    assert.equal(error, null);
}
assert.equal(caughtNullCleanup, true);
assertions += 1;

const originalTestError = new Error('intentional assertion failure');
const originalConsoleError = console.error;
let reportedCleanupFailure = '';
console.error = function (...values) { reportedCleanupFailure += values.join(' '); };
try {
    await checkRejects(
        function () {
            return runWithCleanup(
                async function () { throw originalTestError; },
                async function () { throw new Error('intentional cleanup failure'); }
            );
        },
        function (error) {
            return error === originalTestError;
        }
    );
} finally {
    console.error = originalConsoleError;
}
assert.match(reportedCleanupFailure, /intentional cleanup failure/);
assertions += 1;

await checkRejects(
    function () {
        return runWithCleanup(
            async function () {},
            async function () { throw new Error('cleanup-only failure'); }
        );
    },
    function (error) { return error.message === 'cleanup-only failure'; }
);

class AlreadyClosedSocket extends EventTarget {
    constructor() {
        super();
        this.readyState = 3;
    }
}
const alreadyClosedClient = new DevToolsClient('ws://closed', AlreadyClosedSocket);
await alreadyClosedClient.close();
await alreadyClosedClient.close();
assertions += 1;

class ControlledSocket extends EventTarget {
    constructor() {
        super();
        this.readyState = 0;
        queueMicrotask(function () {
            this.readyState = 1;
            this.dispatchEvent(new Event('open'));
        }.bind(this));
    }

    send() {}

    close() {
        this.readyState = 3;
        this.dispatchEvent(new Event('close'));
    }

    fail(error) {
        const event = new Event('error');
        Object.defineProperty(event, 'error', { value: error });
        this.dispatchEvent(event);
    }
}

class NeverConnectsSocket extends EventTarget {
    constructor() {
        super();
        this.readyState = 0;
    }

    close() {
        this.readyState = 3;
        this.dispatchEvent(new Event('close'));
    }
}

const connectionTimeoutClient = new DevToolsClient('ws://connect-timeout', NeverConnectsSocket, { operationTimeoutMilliseconds: 20 });
await checkRejects(
    function () { return connectionTimeoutClient.connect(); },
    function (error) { return /connection timed out after 20ms/.test(error.message); }
);
await connectionTimeoutClient.close();

class ThrowingSendSocket extends ControlledSocket {
    send() {
        throw new Error('intentional synchronous send failure');
    }
}

const throwingSendClient = new DevToolsClient('ws://send-failure', ThrowingSendSocket, { operationTimeoutMilliseconds: 20 });
await throwingSendClient.connect();
await checkRejects(
    function () { return throwingSendClient.send('Runtime.enable'); },
    function (error) { return error.message === 'intentional synchronous send failure'; }
);
assert.equal(throwingSendClient.pending.size, 0);
assertions += 1;
await throwingSendClient.close();

const errorClient = new DevToolsClient('ws://error', ControlledSocket, { operationTimeoutMilliseconds: 50 });
await errorClient.connect();
const pendingOnError = errorClient.send('Runtime.evaluate');
errorClient.socket.fail(new Error('intentional socket error'));
await checkRejects(
    function () { return pendingOnError; },
    function (error) { return error.message === 'intentional socket error'; }
);
assert.equal(errorClient.pending.size, 0);
assertions += 1;
await errorClient.close();

const disconnectClient = new DevToolsClient('ws://disconnect', ControlledSocket, { operationTimeoutMilliseconds: 50 });
await disconnectClient.connect();
const pendingOnDisconnect = disconnectClient.send('Runtime.evaluate');
disconnectClient.socket.close();
await checkRejects(
    function () { return pendingOnDisconnect; },
    function (error) { return /closed before the command completed/.test(error.message); }
);
assert.equal(disconnectClient.pending.size, 0);
assertions += 1;
await disconnectClient.close();

const commandTimeoutClient = new DevToolsClient('ws://timeout', ControlledSocket, { operationTimeoutMilliseconds: 20 });
await commandTimeoutClient.connect();
await checkRejects(
    function () { return commandTimeoutClient.send('Page.enable'); },
    function (error) { return /Page.enable timed out after 20ms/.test(error.message); }
);
assert.equal(commandTimeoutClient.pending.size, 0);
assertions += 1;
await commandTimeoutClient.close();

const timeoutPrefix = 'yagot-runtime-timeout-harness-';
const timeoutProfile = mkdtempSync(join(tmpdir(), timeoutPrefix));
await checkRejects(
    function () {
        return cleanupBrowserHarness({
            client: { close: function () { return new Promise(function () {}); } },
            browser: { exitCode: 0, signalCode: null },
            profileDirectory: timeoutProfile,
            profilePrefix: timeoutPrefix
        }, { timeoutMilliseconds: 20 });
    },
    function (error) { return /DevTools WebSocket close timed out/.test(error.message); }
);
assert.equal(existsSync(timeoutProfile), false);
assertions += 1;

const falsyCleanupPrefix = 'yagot-falsy-cleanup-harness-';
const falsyCleanupProfile = mkdtempSync(join(tmpdir(), falsyCleanupPrefix));
await checkRejects(
    function () {
        return cleanupBrowserHarness({
            client: { close: async function () { throw undefined; } },
            browser: { exitCode: 0, signalCode: null },
            profileDirectory: falsyCleanupProfile,
            profilePrefix: falsyCleanupPrefix
        });
    },
    function (error) { return /DevTools cleanup failed: undefined/.test(error.message); }
);
assert.equal(existsSync(falsyCleanupProfile), false);
assertions += 1;

const unsafeProfile = mkdtempSync(join(tmpdir(), 'yagot-unexpected-profile-test-'));
try {
    await checkRejects(
        function () {
            return cleanupBrowserHarness({
                client: null,
                browser: { exitCode: 0, signalCode: null },
                profileDirectory: unsafeProfile,
                profilePrefix: 'yagot-expected-profile-harness-'
            });
        },
        function (error) { return /Refusing to remove unexpected browser profile path/.test(error.message); }
    );
} finally {
    await rm(unsafeProfile, { recursive: true, force: true });
}

class NeverExitsProcess extends EventEmitter {
    constructor() {
        super();
        this.exitCode = null;
        this.signalCode = null;
        this.pid = 2147483647;
    }

    kill() {}
}
const runningPrefix = 'yagot-runtime-running-harness-';
const runningProfile = mkdtempSync(join(tmpdir(), runningPrefix));
try {
    await checkRejects(
        function () {
            return cleanupBrowserHarness({
                client: null,
                browser: new NeverExitsProcess(),
                profileDirectory: runningProfile,
                profilePrefix: runningPrefix
            }, { timeoutMilliseconds: 20 });
        },
        function (error) {
            const messages = [error.message].concat(error.errors?.map(function (item) { return item.message; }) || []);
            return messages.some(function (message) { return /browser process is still running/.test(message); });
        }
    );
    assert.equal(existsSync(runningProfile), true);
    assertions += 1;
} finally {
    await rm(runningProfile, { recursive: true, force: true });
}

const startupRoot = mkdtempSync(join(tmpdir(), 'yagot-invalid-browser-test-'));
const invalidBrowserPath = join(startupRoot, 'invalid-browser.exe');
const startupPrefix = 'yagot-startup-failure-harness-';
const previousChromePath = process.env.CHROME_PATH;
try {
    await writeFile(invalidBrowserPath, 'not an executable');
    process.env.CHROME_PATH = invalidBrowserPath;
    await checkRejects(
        function () {
            return runBrowserHarness({
                profilePrefix: startupPrefix,
                harnessUrl: 'about:blank',
                run: async function () {}
            });
        },
        function (error) { return /Browser failed to start|spawn/.test(error?.message || ''); }
    );
    const retainedStartupProfiles = (await readdir(tmpdir())).filter(function (name) { return name.startsWith(startupPrefix); });
    assert.deepEqual(retainedStartupProfiles, []);
    assertions += 1;
} finally {
    if (previousChromePath === undefined) delete process.env.CHROME_PATH;
    else process.env.CHROME_PATH = previousChromePath;
    await rm(startupRoot, { recursive: true, force: true });
}

await checkRejects(
    function () {
        return runBrowserHarness({
            profilePrefix: 'unsafe-prefix-',
            harnessUrl: 'about:blank',
            run: async function () {}
        });
    },
    function (error) { return /Invalid browser profile prefix/.test(error.message); }
);

console.log(`Browser harness runtime tests passed: ${assertions} assertions.`);
