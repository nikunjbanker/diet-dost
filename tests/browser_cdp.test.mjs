/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';
import test from 'node:test';
import { PassThrough } from 'node:stream';
import { launchBrowserWithCdp } from './helpers/browser-cdp.mjs';

function createBrowserProcess() {
    const child = new EventEmitter();
    child.stderr = new PassThrough();
    child.killed = false;
    child.kill = () => {
        child.killed = true;
        return true;
    };
    return child;
}

test('browser CDP startup retries until the endpoint is ready', async () => {
    const browserProcess = createBrowserProcess();
    let now = 0;
    let attempts = 0;
    let launchArguments;
    const result = await launchBrowserWithCdp({
        browserPath: '/usr/bin/google-chrome',
        port: 9225,
        userDataDirectory: '/tmp/edge_cft_test',
        startupTimeoutMs: 2_000,
        spawnProcess: (_path, args) => {
            launchArguments = args;
            return browserProcess;
        },
        fetchImpl: async () => {
            attempts++;
            if (attempts < 3) return { ok: false, status: 503 };
            return {
                ok: true,
                json: async () => ({ webSocketDebuggerUrl: 'ws://127.0.0.1/devtools/browser/test' })
            };
        },
        delay: async ms => { now += ms; },
        now: () => now
    });

    assert.equal(result.browserProcess, browserProcess);
    assert.equal(result.versionData.webSocketDebuggerUrl, 'ws://127.0.0.1/devtools/browser/test');
    assert.equal(attempts, 3);
    assert.ok(launchArguments.includes('--remote-debugging-address=127.0.0.1'));
    assert.ok(launchArguments.includes('--user-data-dir=/tmp/edge_cft_test'));
    assert.equal(browserProcess.killed, false);
});

test('browser CDP startup timeout reports endpoint and captured browser diagnostics', async () => {
    const browserProcess = createBrowserProcess();
    let now = 0;
    await assert.rejects(
        launchBrowserWithCdp({
            browserPath: '/usr/bin/google-chrome',
            port: 9227,
            userDataDirectory: '/tmp/edge_cft_test',
            startupTimeoutMs: 1_000,
            spawnProcess: () => {
                Promise.resolve().then(() => browserProcess.stderr.write('Chrome failed to initialize its DevTools listener'));
                return browserProcess;
            },
            fetchImpl: async () => ({ ok: false, status: 503 }),
            delay: async ms => { now += ms; },
            now: () => now
        }),
        error => {
            assert.match(error.message, /127\.0\.0\.1:9227\/json\/version/);
            assert.match(error.message, /HTTP 503/);
            assert.match(error.message, /Chrome failed to initialize its DevTools listener/);
            return true;
        }
    );

    assert.equal(browserProcess.killed, true);
});

test('browser process exit fails promptly with its exit status', async () => {
    const browserProcess = createBrowserProcess();
    let attempts = 0;
    await assert.rejects(
        launchBrowserWithCdp({
            browserPath: '/usr/bin/google-chrome',
            port: 9228,
            userDataDirectory: '/tmp/edge_cft_test',
            spawnProcess: () => {
                Promise.resolve().then(() => browserProcess.emit('exit', 127, null));
                return browserProcess;
            },
            fetchImpl: async () => {
                attempts++;
                return { ok: false, status: 503 };
            },
            delay: async () => {}
        }),
        /Browser exited with code 127/
    );
    assert.equal(attempts, 1);
});
