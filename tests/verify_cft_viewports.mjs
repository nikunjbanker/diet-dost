// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and "Server Side Public License, v 1".

import { spawn } from 'child_process';
import { writeFileSync, mkdirSync } from 'fs';
import { resolve } from 'path';

const EDGE_PATH = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const PORT = 9222;
const BASE_URL = 'http://localhost:5240/';
const ARTIFACT_DIR = 'C:\\Users\\nikunj.banker\\.gemini\\antigravity-ide\\brain\\110db86e-58f0-4888-b1c7-2ce1aa3f23e2';

function sleep(ms) {
    return new Promise(res => setTimeout(res, ms));
}

async function sendCdp(ws, method, params = {}) {
    return new Promise((resolveReject, reject) => {
        const id = Math.floor(Math.random() * 1000000);
        const timeout = setTimeout(() => {
            reject(new Error(`Timeout waiting for CDP ${method}`));
        }, 10000);

        function onMessage(event) {
            try {
                const data = JSON.parse(event.data);
                if (data.id === id) {
                    clearTimeout(timeout);
                    ws.removeEventListener('message', onMessage);
                    if (data.error) {
                        reject(new Error(`CDP Error in ${method}: ${JSON.stringify(data.error)}`));
                    } else {
                        resolveReject(data.result);
                    }
                }
            } catch (err) {
                // ignore unrelated messages
            }
        }

        ws.addEventListener('message', onMessage);
        ws.send(JSON.stringify({ id, method, params }));
    });
}

async function main() {
    console.log('======================================================================');
    console.log('  CFT TEST SUITE: MULTI-VIEWPORT RESPONSIVE & TOUCH-FIRST ACCEPTANCE  ');
    console.log('======================================================================');

    // 1. Launch Headless Edge with remote debugging
    console.log('[1/5] Launching headless browser on port ' + PORT + '...');
    const edgeProc = spawn(EDGE_PATH, [
        '--headless=new',
        `--remote-debugging-port=${PORT}`,
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    ]);

    edgeProc.stderr.on('data', () => {}); // silence noise

    // Wait for CDP to be available
    let versionData = null;
    for (let i = 0; i < 20; i++) {
        await sleep(500);
        try {
            const res = await fetch(`http://127.0.0.1:${PORT}/json/version`);
            if (res.ok) {
                versionData = await res.json();
                break;
            }
        } catch {}
    }

    if (!versionData) {
        edgeProc.kill();
        throw new Error('Failed to connect to browser CDP');
    }
    console.log('   Browser CDP Connected: ' + versionData.Browser);

    // 2. Open new target page
    console.log('[2/5] Navigating to ' + BASE_URL + '...');
    const newPageRes = await fetch(`http://127.0.0.1:${PORT}/json/new?${encodeURIComponent(BASE_URL)}`, { method: 'PUT' });
    const pageData = await newPageRes.json();
    const ws = new WebSocket(pageData.webSocketDebuggerUrl);

    await new Promise((res, rej) => {
        ws.onopen = res;
        ws.onerror = rej;
    });

    await sendCdp(ws, 'Page.enable');
    await sendCdp(ws, 'Runtime.enable');
    await sendCdp(ws, 'DOM.enable');

    // Wait for page to fully load and hydrate
    await sleep(1500);

    // Authenticate demo session so main UI elements are visible
    await evaluate(`(async () => {
        const idInput = document.getElementById('signin-identifier');
        const pwdInput = document.getElementById('signin-password');
        const form = document.getElementById('form-signin');
        if (idInput && pwdInput && form) {
            idInput.value = 'basic@dietdost.app';
            pwdInput.value = 'DietDost@Demo2026!';
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        }
    })()`);
    await sleep(1500);

    const results = [];

    // Helper to evaluate in page
    async function evaluate(expression) {
        const res = await sendCdp(ws, 'Runtime.evaluate', {
            expression,
            returnByValue: true,
            awaitPromise: true
        });
        return res.result?.value;
    }

    // Helper to capture screenshot
    async function captureScreenshot(filename) {
        const { data } = await sendCdp(ws, 'Page.captureScreenshot', { format: 'png' });
        const filePath = resolve(ARTIFACT_DIR, filename);
        writeFileSync(filePath, Buffer.from(data, 'base64'));
        console.log(`   [Screenshot Captured]: ${filename}`);
        return filePath;
    }

    // 3. Viewport Matrix Tests
    const viewports = [
        {
            name: 'Desktop Web (1440x900)',
            width: 1440,
            height: 900,
            mobile: false,
            screenshot: 'cft_e2e_desktop_1440x900.png',
            expectBottomNav: false,
            expectDesktopButtons: true
        },
        {
            name: 'Tablet Portrait (768x1024)',
            width: 768,
            height: 1024,
            mobile: false,
            screenshot: 'cft_e2e_tablet_768x1024.png',
            expectBottomNav: false,
            expectDesktopButtons: true
        },
        {
            name: 'Mobile Standard - iPhone 14 (390x844)',
            width: 390,
            height: 844,
            mobile: true,
            screenshot: 'cft_e2e_mobile_390x844.png',
            expectBottomNav: true,
            expectDesktopButtons: false
        },
        {
            name: 'Mobile Compact - iPhone SE (375x667)',
            width: 375,
            height: 667,
            mobile: true,
            screenshot: 'cft_e2e_mobile_375x667.png',
            expectBottomNav: true,
            expectDesktopButtons: false
        }
    ];

    console.log('[3/5] Executing Viewport Layout & Overflow Assertions...');
    for (const vp of viewports) {
        console.log(`\n--> Testing Profile: ${vp.name}`);
        await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
            width: vp.width,
            height: vp.height,
            deviceScaleFactor: 2,
            mobile: vp.mobile
        });
        await sleep(500);

        const metrics = await evaluate(`(() => {
            const scrollWidth = document.documentElement.scrollWidth;
            const innerWidth = window.innerWidth;
            const bottomNav = document.querySelector('.mobile-bottom-nav');
            const bottomNavDisplay = bottomNav ? window.getComputedStyle(bottomNav).display : 'none';
            const desktopBtn = document.querySelector('#btn-open-transparency');
            const desktopBtnDisplay = desktopBtn ? window.getComputedStyle(desktopBtn).display : 'none';
            return {
                scrollWidth,
                innerWidth,
                hasOverflow: scrollWidth > innerWidth,
                bottomNavDisplay,
                desktopBtnDisplay
            };
        })()`);

        const overflowPass = !metrics.hasOverflow;
        const bottomNavPass = vp.expectBottomNav ? (metrics.bottomNavDisplay !== 'none') : (metrics.bottomNavDisplay === 'none');
        const desktopBtnPass = vp.expectDesktopButtons ? (metrics.desktopBtnDisplay !== 'none') : (metrics.desktopBtnDisplay === 'none');

        console.log(`    [PASS/FAIL] Overflow: ${overflowPass ? 'PASS' : 'FAIL'} (scrollWidth: ${metrics.scrollWidth}px, innerWidth: ${metrics.innerWidth}px)`);
        console.log(`    [PASS/FAIL] Bottom Nav Display: ${bottomNavPass ? 'PASS' : 'FAIL'} (Actual: '${metrics.bottomNavDisplay}', Expected: '${vp.expectBottomNav ? 'flex' : 'none'}')`);
        console.log(`    [PASS/FAIL] Desktop Header Buttons: ${desktopBtnPass ? 'PASS' : 'FAIL'} (Actual: '${metrics.desktopBtnDisplay}', Expected: '${vp.expectDesktopButtons ? 'inline-flex/block' : 'none'}')`);

        await captureScreenshot(vp.screenshot);

        results.push({
            name: vp.name,
            overflowPass,
            bottomNavPass,
            desktopBtnPass,
            metrics
        });
    }

    // 4. Mobile Touch Targets & Interactive Bottom Sheet Verification
    console.log('\n[4/5] Executing Mobile Touch Target & Bottom Sheet Verification (375x667)...');
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 375,
        height: 667,
        deviceScaleFactor: 2,
        mobile: true
    });
    await sleep(500);

    const touchTargets = await evaluate(`(() => {
        const ids = ['#btn-nav-overview', '#btn-nav-log-meal', '#btn-nav-history', '#btn-nav-profile', '#btn-mode-camera', '#btn-mode-text'];
        return ids.map(id => {
            const el = document.querySelector(id);
            if (!el) return { id, exists: false, width: 0, height: 0, validTouchTarget: false };
            const rect = el.getBoundingClientRect();
            const w = Math.round(rect.width);
            const h = Math.round(rect.height);
            return {
                id,
                exists: true,
                width: w,
                height: h,
                validTouchTarget: w >= 44 && h >= 44
            };
        });
    })()`);

    console.log('    Touch Target Dimensions (Target >= 44px x 44px):');
    let allTouchTargetsValid = true;
    for (const tt of touchTargets) {
        const pass = tt.exists && tt.validTouchTarget;
        if (!pass) allTouchTargetsValid = false;
        console.log(`    - ${tt.id}: ${pass ? 'PASS' : 'FAIL'} (${tt.width}px x ${tt.height}px)`);
    }

    // Test Clinical Profile Click -> Bottom Sheet Modal Flow
    console.log('\n--> Testing Clinical Profile (#btn-nav-profile) -> Mobile Bottom Sheet Modal Flow:');
    await evaluate(`document.querySelector('#btn-nav-profile').click()`);
    await sleep(600);

    const sheetMetrics = await evaluate(`(() => {
        const modal = document.querySelector('#profile-modal');
        const card = modal ? modal.querySelector('.profile-modal-card') : null;
        if (!modal || !card) return { open: false, cardFound: false };
        const modalStyle = window.getComputedStyle(modal);
        const cardStyle = window.getComputedStyle(card);
        const rect = card.getBoundingClientRect();
        return {
            open: modalStyle.display !== 'none',
            cardFound: true,
            alignItems: modalStyle.alignItems,
            borderRadius: cardStyle.borderRadius,
            width: Math.round(rect.width),
            height: Math.round(rect.height),
            top: Math.round(rect.top),
            bottom: Math.round(window.innerHeight - rect.bottom),
            viewportHeight: window.innerHeight
        };
    })()`);

    const sheetPass = sheetMetrics.open && (sheetMetrics.alignItems === 'flex-end' || sheetMetrics.borderRadius.includes('20px'));
    console.log(`    [PASS/FAIL] Bottom Sheet Open: ${sheetMetrics.open ? 'PASS' : 'FAIL'}`);
    console.log(`    [PASS/FAIL] Bottom Sheet Styling: ${sheetPass ? 'PASS' : 'FAIL'} (alignItems: ${sheetMetrics.alignItems}, borderRadius: '${sheetMetrics.borderRadius}', dimensions: ${sheetMetrics.width}x${sheetMetrics.height}px, bottomGap: ${sheetMetrics.bottom}px)`);

    await captureScreenshot('cft_e2e_mobile_bottom_sheet_modal.png');

    // Close Modal
    await evaluate(`(() => {
        const closeBtn = document.querySelector('#btn-close-profile');
        if (closeBtn) closeBtn.click();
        else {
            const modal = document.querySelector('#profile-modal');
            if (modal) modal.style.display = 'none';
        }
    })()`);
    await sleep(400);

    // Test Diary History Navigation Scroll
    console.log('\n--> Testing Diary Navigation (#btn-nav-history) Click:');
    const scrollBefore = await evaluate(`window.scrollY`);
    await evaluate(`document.querySelector('#btn-nav-history').click()`);
    await sleep(600);
    const scrollAfter = await evaluate(`window.scrollY`);
    const historyActive = await evaluate(`document.querySelector('#btn-nav-history').classList.contains('active')`);
    console.log(`    [PASS/FAIL] Diary Tab Active State: ${historyActive ? 'PASS' : 'FAIL'}`);
    console.log(`    [INFO] Scroll Position: ${scrollBefore}px -> ${scrollAfter}px`);

    // Test Overview Navigation Scroll Top
    console.log('\n--> Testing Overview Navigation (#btn-nav-overview) Click:');
    await evaluate(`document.querySelector('#btn-nav-overview').click()`);
    await sleep(600);
    const scrollOverview = await evaluate(`window.scrollY`);
    const overviewActive = await evaluate(`document.querySelector('#btn-nav-overview').classList.contains('active')`);
    console.log(`    [PASS/FAIL] Overview Tab Active State: ${overviewActive ? 'PASS' : 'FAIL'}`);
    console.log(`    [INFO] Scroll Position Top: ${scrollOverview}px`);

    // Clean up
    console.log('\n[5/5] Cleaning up headless browser session...');
    ws.close();
    edgeProc.kill();

    // Summary Assessment
    console.log('\n======================================================================');
    console.log('                    CFT EXECUTION SUMMARY RESULTS                     ');
    console.log('======================================================================');
    const allOverflowPass = results.every(r => r.overflowPass);
    const allBottomNavPass = results.every(r => r.bottomNavPass);
    const allDesktopButtonsPass = results.every(r => r.desktopBtnPass);

    console.log(`1. Horizontal Overflow (scrollWidth <= innerWidth): ${allOverflowPass ? 'ALL PASSED (100%)' : 'FAILED'}`);
    console.log(`2. Bottom Navigation Responsive Visibility:         ${allBottomNavPass ? 'ALL PASSED (100%)' : 'FAILED'}`);
    console.log(`3. Desktop Header Button Adaptive Visibility:       ${allDesktopButtonsPass ? 'ALL PASSED (100%)' : 'FAILED'}`);
    console.log(`4. Touch Target Governance (>= 44px x 44px):        ${allTouchTargetsValid ? 'ALL PASSED (100%)' : 'FAILED'}`);
    console.log(`5. Mobile Bottom Sheet Modal Behavior:              ${sheetPass ? 'PASSED (100%)' : 'FAILED'}`);
    console.log('======================================================================\n');

    if (allOverflowPass && allBottomNavPass && allDesktopButtonsPass && allTouchTargetsValid && sheetPass) {
        console.log('>>> VERDICT: CFT ACCEPTANCE CRITERIA 100% SATISFIED ON ALL VIEWPORTS! <<<');
        process.exit(0);
    } else {
        console.error('>>> VERDICT: ONE OR MORE CFT ACCEPTANCE CRITERIA FAILED! <<<');
        process.exit(1);
    }
}

main().catch(err => {
    console.error('Fatal error in CFT execution:', err);
    process.exit(1);
});
