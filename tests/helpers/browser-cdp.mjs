import { spawn } from 'node:child_process';

const STARTUP_TIMEOUT_MS = 30_000;
const POLL_INTERVAL_MS = 500;
const MAX_STDERR_LENGTH = 4_000;

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

export async function launchBrowserWithCdp({
    browserPath,
    port,
    userDataDirectory,
    startupTimeoutMs = STARTUP_TIMEOUT_MS,
    spawnProcess = spawn,
    fetchImpl = fetch,
    delay = sleep,
    now = Date.now
}) {
    const browserProcess = spawnProcess(browserPath, [
        '--headless=new',
        '--no-sandbox',
        '--disable-dev-shm-usage',
        '--remote-debugging-address=127.0.0.1',
        `--remote-debugging-port=${port}`,
        `--user-data-dir=${userDataDirectory}`,
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    ], { stdio: ['ignore', 'ignore', 'pipe'] });

    let stderr = '';
    let processError = null;
    let exitDetails = null;
    browserProcess.stderr?.on('data', chunk => {
        stderr = (stderr + chunk.toString()).slice(-MAX_STDERR_LENGTH);
    });
    browserProcess.once('error', error => {
        processError = error;
    });
    browserProcess.once('exit', (code, signal) => {
        exitDetails = { code, signal };
    });

    const endpoint = `http://127.0.0.1:${port}/json/version`;
    const deadline = now() + startupTimeoutMs;
    let lastConnectionError = 'no CDP response received';

    while (now() < deadline) {
        if (processError) {
            break;
        }
        if (exitDetails) {
            break;
        }

        try {
            const response = await fetchImpl(endpoint);
            if (response.ok) {
                const versionData = await response.json();
                if (versionData?.webSocketDebuggerUrl) {
                    return { browserProcess, versionData };
                }
                lastConnectionError = 'CDP returned an invalid version response';
            } else {
                lastConnectionError = `CDP endpoint returned HTTP ${response.status}`;
            }
        } catch (error) {
            lastConnectionError = error instanceof Error ? error.message : String(error);
        }

        await delay(POLL_INTERVAL_MS);
    }

    browserProcess.kill();
    const processStatus = processError
        ? ` Browser spawn failed: ${processError.message}.`
        : exitDetails
            ? ` Browser exited with code ${exitDetails.code ?? 'unknown'}${exitDetails.signal ? ` (${exitDetails.signal})` : ''}.`
            : '';
    const stderrDetails = stderr.trim() ? ` Browser stderr: ${stderr.trim()}` : '';
    throw new Error(
        `Could not connect to browser CDP at ${endpoint} within ${startupTimeoutMs}ms: ${lastConnectionError}.${processStatus}${stderrDetails}`
    );
}
