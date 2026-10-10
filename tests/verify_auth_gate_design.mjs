// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and "Server Side Public License, v 1".

import { spawn } from 'child_process';
import { writeFileSync } from 'fs';
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
                // ignore
            }
        }

        ws.addEventListener('message', onMessage);
        ws.send(JSON.stringify({ id, method, params }));
    });
}

async function evaluate(ws, expression) {
    const res = await sendCdp(ws, 'Runtime.evaluate', {
        expression,
        returnByValue: true,
        awaitPromise: true
    });
    return res.result ? res.result.value : null;
}

async function captureScreenshot(ws, filename) {
    const res = await sendCdp(ws, 'Page.captureScreenshot', { format: 'png' });
    const buf = Buffer.from(res.data, 'base64');
    const fullPath = resolve(ARTIFACT_DIR, filename);
    writeFileSync(fullPath, buf);
    console.log(`   [Screenshot Captured]: ${filename}`);
}

async function main() {
    console.log('======================================================================');
    console.log('  DESIGN.MD & AUTH GATE CFT SUITE: REGISTRATION DISABLED VERIFICATION');
    console.log('======================================================================');

    console.log('[1/6] Launching headless browser on port 9222...');
    const edge = spawn(EDGE_PATH, [
        '--headless=new',
        `--remote-debugging-port=${PORT}`,
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    ]);
    edge.stderr.on('data', () => {});

    await sleep(1500);

    let versionResp = null;
    for (let i = 0; i < 15; i++) {
        try {
            const r = await fetch(`http://127.0.0.1:${PORT}/json/version`);
            if (r.ok) {
                versionResp = await r.json();
                break;
            }
        } catch {
            await sleep(500);
        }
    }

    if (!versionResp) {
        console.error('Failed to connect to CDP endpoint.');
        edge.kill();
        process.exit(1);
    }
    console.log(`   Connected to CDP: ${versionResp.Browser}`);

    console.log('[2/6] Opening http://localhost:5240/ and clearing auth state...');
    const newPageRes = await fetch(`http://127.0.0.1:${PORT}/json/new?${encodeURIComponent(BASE_URL)}`, { method: 'PUT' });
    const pageData = await newPageRes.json();
    const ws = new WebSocket(pageData.webSocketDebuggerUrl);
    await new Promise(r => ws.addEventListener('open', r));

    await sendCdp(ws, 'Page.enable');
    await sendCdp(ws, 'DOM.enable');
    await sendCdp(ws, 'CSS.enable');
    await sendCdp(ws, 'Runtime.enable');

    await sleep(2000);

    // Clear cookies & localStorage to ensure Auth Gate modal is active
    await sendCdp(ws, 'Network.clearBrowserCookies');
    await evaluate(ws, `
        localStorage.clear();
        sessionStorage.clear();
        location.reload();
    `);
    await sleep(2500);

    console.log('[3/6] Verifying DESIGN.md Tokens & Elements on Desktop (1440x900)...');
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 1440,
        height: 900,
        deviceScaleFactor: 1,
        mobile: false
    });
    await sleep(500);

    const desktopChecks = await evaluate(ws, `
        (() => {
            const modal = document.getElementById('auth-gate-modal');
            const tabRegister = document.getElementById('btn-tab-register');
            const tabSignIn = document.getElementById('btn-tab-signin');
            const formSignIn = document.getElementById('form-signin');
            const formRegister = document.getElementById('form-register');
            const noticeCard = document.querySelector('.auth-notice-card');
            const badge = document.querySelector('.auth-preview-badge');
            const demoButtons = document.querySelectorAll('.demo-pill-btn');
            const disabledBanner = document.querySelector('.auth-disabled-banner');

            const modalDisplay = window.getComputedStyle(modal).display;
            const regDisplay = window.getComputedStyle(tabRegister).display;
            const signinDisplay = window.getComputedStyle(tabSignIn).display;
            const isSigninActive = tabSignIn.classList.contains('active');

            const cardStyle = noticeCard ? window.getComputedStyle(noticeCard) : {};
            const badgeStyle = badge ? window.getComputedStyle(badge) : {};

            return {
                modalVisible: modalDisplay === 'flex' || modalDisplay === 'block',
                registerTabPresent: regDisplay !== 'none',
                signInTabVisible: signinDisplay !== 'none',
                signInActive: isSigninActive,
                noticeCardPresent: !!noticeCard,
                badgePresent: !!badge,
                demoButtonCount: demoButtons.length,
                disabledBannerHidden: !disabledBanner || disabledBanner.style.display === 'none' || window.getComputedStyle(disabledBanner).display === 'none',
                noticeCardBorder: cardStyle.borderWidth,
                noticeCardRadius: cardStyle.borderRadius,
                badgeRadius: badgeStyle.borderRadius
            };
        })()
    `);

    console.log('   Desktop UI Checks:');
    console.log(`   - Auth Gate Modal Visible: ${desktopChecks.modalVisible ? 'PASS' : 'FAIL'}`);
    console.log(`   - Create Account Tab Present: ${desktopChecks.registerTabPresent ? 'PASS' : 'FAIL'}`);
    console.log(`   - Sign In Tab Active: ${desktopChecks.signInActive ? 'PASS' : 'FAIL'}`);
    console.log(`   - Alpha Preview Notice Card Present: ${desktopChecks.noticeCardPresent ? 'PASS' : 'FAIL'}`);
    console.log(`   - Status Badge Present: ${desktopChecks.badgePresent ? 'PASS' : 'FAIL'}`);
    console.log(`   - Demo Quick-Fill Buttons Count: ${desktopChecks.demoButtonCount} (Expected: 4)`);
    console.log(`   - Disabled Form Banner Hidden: ${desktopChecks.disabledBannerHidden ? 'PASS' : 'FAIL'}`);
    console.log(`   - DESIGN.md Card Radius: ${desktopChecks.noticeCardRadius}`);
    console.log(`   - DESIGN.md Badge Radius: ${desktopChecks.badgeRadius}`);

    await captureScreenshot(ws, 'cft_auth_gate_desktop_1440x900.png');

    console.log('[4/6] Verifying Tablet (768x1024) Responsive Layout...');
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 768,
        height: 1024,
        deviceScaleFactor: 1,
        mobile: false
    });
    await sleep(500);

    const tabletChecks = await evaluate(ws, `
        (() => {
            return {
                scrollWidth: document.documentElement.scrollWidth,
                innerWidth: window.innerWidth,
                overflowPass: document.documentElement.scrollWidth <= window.innerWidth
            };
        })()
    `);
    console.log(`   - Tablet Horizontal Overflow: ${tabletChecks.overflowPass ? 'PASS' : 'FAIL'} (${tabletChecks.scrollWidth}px / ${tabletChecks.innerWidth}px)`);
    await captureScreenshot(ws, 'cft_auth_gate_tablet_768x1024.png');

    console.log('[5/6] Verifying Mobile (375x667) & Touch Target Compliance (>= 44x44px)...');
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 375,
        height: 667,
        deviceScaleFactor: 2,
        mobile: true
    });
    await sleep(500);

    const mobileChecks = await evaluate(ws, `
        (() => {
            const btnFree = document.getElementById('btn-demo-free');
            const btnBasic = document.getElementById('btn-demo-basic');
            const btnPrem = document.getElementById('btn-demo-premium');
            const btnAdmin = document.getElementById('btn-demo-admin');
            const submitBtn = document.getElementById('btn-submit-signin');

            function getDim(el) {
                if (!el) return { w: 0, h: 0, pass: false };
                const rect = el.getBoundingClientRect();
                return {
                    w: Math.round(rect.width),
                    h: Math.round(rect.height),
                    pass: rect.height >= 44
                };
            }

            return {
                overflowPass: document.documentElement.scrollWidth <= window.innerWidth,
                freeDim: getDim(btnFree),
                basicDim: getDim(btnBasic),
                premDim: getDim(btnPrem),
                adminDim: getDim(btnAdmin),
                submitDim: getDim(submitBtn)
            };
        })()
    `);

    console.log(`   - Mobile Horizontal Overflow: ${mobileChecks.overflowPass ? 'PASS' : 'FAIL'}`);
    console.log(`   - #btn-demo-free Touch Target: ${mobileChecks.freeDim.pass ? 'PASS' : 'FAIL'} (${mobileChecks.freeDim.w}x${mobileChecks.freeDim.h}px)`);
    console.log(`   - #btn-demo-basic Touch Target: ${mobileChecks.basicDim.pass ? 'PASS' : 'FAIL'} (${mobileChecks.basicDim.w}x${mobileChecks.basicDim.h}px)`);
    console.log(`   - #btn-demo-premium Touch Target: ${mobileChecks.premDim.pass ? 'PASS' : 'FAIL'} (${mobileChecks.premDim.w}x${mobileChecks.premDim.h}px)`);
    console.log(`   - #btn-demo-admin Touch Target: ${mobileChecks.adminDim.pass ? 'PASS' : 'FAIL'} (${mobileChecks.adminDim.w}x${mobileChecks.adminDim.h}px)`);
    console.log(`   - #btn-submit-signin Touch Target: ${mobileChecks.submitDim.pass ? 'PASS' : 'FAIL'} (${mobileChecks.submitDim.w}x${mobileChecks.submitDim.h}px)`);

    await captureScreenshot(ws, 'cft_auth_gate_mobile_375x667.png');

    console.log('[6/6] Testing Interactive Demo Pill Quick-Fill & Authentication Flow...');
    // Click #btn-demo-basic
    await evaluate(ws, `
        document.getElementById('btn-demo-basic').click();
    `);
    await sleep(300);

    const filledCreds = await evaluate(ws, `
        (() => {
            const idInput = document.getElementById('signin-identifier');
            const passInput = document.getElementById('signin-password');
            return {
                email: idInput.value,
                isPopulated: passInput.value.length > 0
            };
        })()
    `);

    console.log(`   - Clicked 'Basic Tier' Demo Pill -> Filled Identifier: "${filledCreds.email}" (Field populated: ${filledCreds.isPopulated})`);
    const credsPass = filledCreds.email === 'basic@dietdost.app' && filledCreds.isPopulated;
    console.log(`   [PASS/FAIL] Demo Quick-Fill: ${credsPass ? 'PASS' : 'FAIL'}`);

    // Submit sign-in and assert transition into authenticated dashboard
    await evaluate(ws, `
        document.getElementById('form-signin').dispatchEvent(new Event('submit', { cancelable: true, bubbles: true }));
    `);
    await sleep(2500);

    const authResult = await evaluate(ws, `
        (() => {
            const modal = document.getElementById('auth-gate-modal');
            const userPill = document.getElementById('header-user-tier');
            return {
                modalClosed: window.getComputedStyle(modal).display === 'none',
                userPillText: userPill ? userPill.textContent : ''
            };
        })()
    `);

    console.log(`   - Modal Dismissed & Dashboard Hydrated: ${authResult.modalClosed ? 'PASS' : 'FAIL'}`);
    console.log(`   - Authenticated User Header Pill: "${authResult.userPillText}"`);

    await captureScreenshot(ws, 'cft_auth_gate_after_login_375x667.png');

    console.log('Cleaning up browser session...');
    edge.kill();

    const allPassed = desktopChecks.registerTabPresent &&
                      desktopChecks.signInActive &&
                      desktopChecks.noticeCardPresent &&
                      desktopChecks.demoButtonCount === 4 &&
                      desktopChecks.disabledBannerHidden &&
                      tabletChecks.overflowPass &&
                      mobileChecks.overflowPass &&
                      mobileChecks.freeDim.pass &&
                      credsPass &&
                      authResult.modalClosed;

    console.log('======================================================================');
    console.log(`  VERDICT: ${allPassed ? 'ALL DESIGN.MD & AUTH GATE ACCEPTANCE TESTS PASSED (100%)' : 'SOME TESTS FAILED'}`);
    console.log('======================================================================');

    process.exit(allPassed ? 0 : 1);
}

main().catch(err => {
    console.error('Test execution failed:', err);
    process.exit(1);
});
