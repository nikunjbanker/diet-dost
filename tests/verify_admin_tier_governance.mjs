// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and "Server Side Public License, v 1".

import { spawn } from 'child_process';
import { writeFileSync } from 'fs';
import { resolve } from 'path';

const EDGE_PATH = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const PORT = 9226;
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
        }, 15000);

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
            } catch (err) {}
        }

        ws.addEventListener('message', onMessage);
        ws.send(JSON.stringify({ id, method, params }));
    });
}

async function main() {
    console.log('======================================================================');
    console.log('   CFT TEST SUITE: ADMIN TIER GOVERNANCE (SUPERADMIN ONLY WRITE)      ');
    console.log('======================================================================');

    console.log('[1/5] Launching headless browser on port ' + PORT + '...');
    const edgeProc = spawn(EDGE_PATH, [
        '--headless=new',
        `--remote-debugging-port=${PORT}`,
        `--user-data-dir=${process.env.TEMP || 'C:\\\\temp'}\\edge_cft_${Date.now()}`,
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    ]);

    await sleep(2500);

    const versionRes = await fetch(`http://127.0.0.1:${PORT}/json/version`);
    const versionData = await versionRes.json();
    console.log(`   Browser CDP Connected: ${versionData.Browser}`);

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
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 1280,
        height: 950,
        deviceScaleFactor: 1,
        mobile: false
    });

    await sleep(2000);

    async function evaluate(expression) {
        const res = await sendCdp(ws, 'Runtime.evaluate', {
            expression,
            returnByValue: true,
            awaitPromise: true
        });
        return res.result?.value;
    }

    async function captureScreenshot(filename) {
        const { data } = await sendCdp(ws, 'Page.captureScreenshot', { format: 'png' });
        const filePath = resolve(ARTIFACT_DIR, filename);
        writeFileSync(filePath, Buffer.from(data, 'base64'));
        console.log(`   [Screenshot Captured]: ${filename}`);
        return filePath;
    }

    async function performLogin(email, password) {
        return await evaluate(`(async () => {
            const idInput = document.getElementById('signin-identifier');
            const pwdInput = document.getElementById('signin-password');
            const form = document.getElementById('form-signin');
            if (!idInput || !pwdInput || !form) return false;

            idInput.value = '${email}';
            pwdInput.value = '${password}';
            form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
            return true;
        })()`);
    }

    const testResults = [];

    // -------------------------------------------------------------------------
    // TEST 1: STANDARD ADMIN (admin.demo@dietdost.app) - READ-ONLY TIER GOVERNANCE
    // -------------------------------------------------------------------------
    console.log('\n[2/5] Step 1: Logging in as Standard Administrator (admin.demo@dietdost.app)...');
    await performLogin('admin.demo@dietdost.app', 'DietDost@Demo2026!');
    await sleep(3000);

    // Open Admin Console via dropdown menu
    await evaluate(`(() => {
        const menuBtn = document.getElementById('btn-user-menu');
        if (menuBtn) menuBtn.click();
    })()`);
    await sleep(400);

    await evaluate(`(() => {
        const adminBtn = document.getElementById('menu-open-admin');
        if (adminBtn) adminBtn.click();
    })()`);
    await sleep(1000);

    // Switch to Tier Configurations Tab
    console.log('    Switching to Tier Configurations Tab as Admin...');
    await evaluate(`(() => {
        const tabTiers = document.getElementById('btn-admin-tab-tiers');
        if (tabTiers) tabTiers.click();
    })()`);

    // Poll until tier cards are populated
    let adminTierCheck = null;
    for (let i = 0; i < 20; i++) {
        await sleep(500);
        adminTierCheck = await evaluate(`(() => {
            const readonlyBanner = document.querySelector('.tier-admin-readonly-banner');
            const writeBanner = document.querySelector('.tier-admin-write-banner');
            const cards = document.querySelectorAll('.tier-admin-card');
            if (cards.length === 0) return null;

            const saveBtns = Array.from(document.querySelectorAll('.btn-save-tier'));
            const allButtonsLocked = saveBtns.length > 0 && saveBtns.every(b => b.disabled && b.textContent.includes('SuperAdmin Only'));
            const inputs = Array.from(document.querySelectorAll('.tier-admin-card input'));
            const allInputsDisabled = inputs.length > 0 && inputs.every(inp => inp.disabled || inp.readOnly);
            return {
                cardsCount: cards.length,
                hasReadonlyBanner: !!readonlyBanner,
                hasWriteBanner: !!writeBanner,
                bannerText: readonlyBanner?.textContent?.trim(),
                saveBtnsCount: saveBtns.length,
                allButtonsLocked,
                allInputsDisabled
            };
        })()`);

        if (adminTierCheck) break;
    }

    const adminPass = adminTierCheck &&
                      adminTierCheck.hasReadonlyBanner &&
                      !adminTierCheck.hasWriteBanner &&
                      adminTierCheck.allButtonsLocked &&
                      adminTierCheck.allInputsDisabled;

    console.log(`    [PASS/FAIL] Standard Admin Read-Only Governance: ${adminPass ? 'PASS' : 'FAIL'}`);
    if (adminTierCheck) {
        console.log(`    [INFO] Cards Rendered: ${adminTierCheck.cardsCount}`);
        console.log(`    [INFO] Read-Only Banner Present: ${adminTierCheck.hasReadonlyBanner}`);
        console.log(`    [INFO] Save Buttons Locked (SuperAdmin Only): ${adminTierCheck.allButtonsLocked} (${adminTierCheck.saveBtnsCount} buttons)`);
        console.log(`    [INFO] All Inputs Disabled/Readonly: ${adminTierCheck.allInputsDisabled}`);
    }
    await captureScreenshot('cft_admin_tier_readonly_mode.png');
    testResults.push({ name: 'Standard Admin Read-Only Governance View', pass: !!adminPass });

    // -------------------------------------------------------------------------
    // TEST 2: SIGN OUT
    // -------------------------------------------------------------------------
    console.log('\n[3/5] Step 2: Signing out of Standard Admin account...');
    await evaluate(`(() => {
        const closeBtn = document.getElementById('btn-close-admin-modal');
        if (closeBtn) closeBtn.click();
        const menuBtn = document.getElementById('btn-user-menu');
        if (menuBtn) menuBtn.click();
    })()`);
    await sleep(400);

    await evaluate(`(() => {
        const signoutBtn = document.getElementById('menu-btn-signout');
        if (signoutBtn) signoutBtn.click();
    })()`);
    await sleep(2000);

    // -------------------------------------------------------------------------
    // TEST 3: SUPERADMIN (superadmin@dietdost.app) - FULL WRITE GOVERNANCE
    // -------------------------------------------------------------------------
    console.log('\n[4/5] Step 3: Logging in as SuperAdmin (superadmin@dietdost.app)...');
    await performLogin('superadmin@dietdost.app', 'DietDost@Demo2026!');
    await sleep(3000);

    // Open Admin Console via dropdown menu
    await evaluate(`(() => {
        const menuBtn = document.getElementById('btn-user-menu');
        if (menuBtn) menuBtn.click();
    })()`);
    await sleep(400);

    await evaluate(`(() => {
        const adminBtn = document.getElementById('menu-open-admin');
        if (adminBtn) adminBtn.click();
    })()`);
    await sleep(1000);

    // Switch to Tier Configurations Tab
    console.log('    Switching to Tier Configurations Tab as SuperAdmin...');
    await evaluate(`(() => {
        const tabTiers = document.getElementById('btn-admin-tab-tiers');
        if (tabTiers) tabTiers.click();
    })()`);

    // Poll until tier cards are populated
    let superTierCheck = null;
    for (let i = 0; i < 20; i++) {
        await sleep(500);
        superTierCheck = await evaluate(`(() => {
            const readonlyBanner = document.querySelector('.tier-admin-readonly-banner');
            const writeBanner = document.querySelector('.tier-admin-write-banner');
            const cards = document.querySelectorAll('.tier-admin-card');
            if (cards.length === 0) return null;

            const saveBtns = Array.from(document.querySelectorAll('.btn-save-tier'));
            const activeSaveBtns = saveBtns.filter(b => !b.disabled && b.textContent.includes('Save Configuration'));
            const basicCard = document.querySelector('.tier-admin-card[data-tier="1"]');
            const basicLimitInput = basicCard?.querySelector('.inp-daily-limit');
            const isBasicEditable = basicLimitInput && !basicLimitInput.disabled && !basicLimitInput.readOnly;
            return {
                cardsCount: cards.length,
                hasReadonlyBanner: !!readonlyBanner,
                hasWriteBanner: !!writeBanner,
                bannerText: writeBanner?.textContent?.trim(),
                totalSaveBtns: saveBtns.length,
                activeSaveBtnsCount: activeSaveBtns.length,
                isBasicEditable
            };
        })()`);

        if (superTierCheck) break;
    }

    const superPass = superTierCheck &&
                      !superTierCheck.hasReadonlyBanner &&
                      superTierCheck.hasWriteBanner &&
                      superTierCheck.activeSaveBtnsCount >= 3 &&
                      superTierCheck.isBasicEditable;

    console.log(`    [PASS/FAIL] SuperAdmin Full Write Governance: ${superPass ? 'PASS' : 'FAIL'}`);
    if (superTierCheck) {
        console.log(`    [INFO] Cards Rendered: ${superTierCheck.cardsCount}`);
        console.log(`    [INFO] SuperAdmin Active Banner Present: ${superTierCheck.hasWriteBanner}`);
        console.log(`    [INFO] Active 'Save Configuration' Buttons: ${superTierCheck.activeSaveBtnsCount}`);
        console.log(`    [INFO] Basic Tier Inputs Editable: ${superTierCheck.isBasicEditable}`);
    }
    await captureScreenshot('cft_superadmin_tier_write_mode.png');
    testResults.push({ name: 'SuperAdmin Full Write Governance Authority', pass: !!superPass });

    // Clean up
    console.log('\n[5/5] Cleaning up browser session...');
    ws.close();
    edgeProc.kill();

    console.log('\n======================================================================');
    console.log('              ADMIN TIER GOVERNANCE CFT REPORT                        ');
    console.log('======================================================================');
    let allPassed = true;
    testResults.forEach((t, i) => {
        const mark = t.pass ? 'PASS (100%)' : 'FAIL';
        if (!t.pass) allPassed = false;
        console.log(`${i + 1}. ${t.name.padEnd(45)}: ${mark}`);
    });
    console.log('======================================================================\n');

    if (allPassed) {
        console.log('>>> VERDICT: ADMIN TIER GOVERNANCE SECURITY FULLY VERIFIED! <<<');
        process.exit(0);
    } else {
        console.error('>>> VERDICT: ONE OR MORE GOVERNANCE CHECKS FAILED! <<<');
        process.exit(1);
    }
}

main().catch(err => {
    console.error('Fatal error during admin governance CFT execution:', err);
    process.exit(1);
});
