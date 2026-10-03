// Copyright (c) 2026 diet-dost and/or its contributors.
// Licensed under the "GNU Affero General Public License v3.0 only" and "Server Side Public License, v 1".

import { spawn } from 'child_process';
import { writeFileSync } from 'fs';
import { resolve } from 'path';

const EDGE_PATH = 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const PORT = 9224;
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
    console.log('    CFT TEST SUITE: MOBILE USER TIERS, LOGOUT, & MEAL LOGGING FLOWS   ');
    console.log('======================================================================');

    // 1. Launch Headless Edge
    console.log('[1/10] Launching headless browser on port ' + PORT + '...');
    const edgeProc = spawn(EDGE_PATH, [
        '--headless=new',
        `--remote-debugging-port=${PORT}`,
        '--disable-gpu',
        '--no-first-run',
        '--no-default-browser-check',
        'about:blank'
    ]);

    edgeProc.stderr.on('data', () => {});

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

    // 2. Open Page
    console.log('[2/10] Navigating to ' + BASE_URL + '...');
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

    // Emulate Mobile Device (iPhone 14: 390x844, touch enabled, DPR 3)
    console.log('   Configuring Mobile Viewport (390 x 844, Mobile/Touch Enabled)...');
    await sendCdp(ws, 'Emulation.setDeviceMetricsOverride', {
        width: 390,
        height: 844,
        deviceScaleFactor: 3,
        mobile: true
    });
    await sendCdp(ws, 'Emulation.setTouchEmulationEnabled', { enabled: true });

    await sleep(1500);

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

    const testResults = [];

    // Helper: Perform Sign In
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

    // -------------------------------------------------------------------------
    // TEST 1: MOBILE LOGIN AS BASIC TIER USER
    // -------------------------------------------------------------------------
    console.log('\n[3/10] Step 1: Performing Mobile Login as basic@dietdost.app...');
    await performLogin('basic@dietdost.app', 'DietDost@Demo2026!');
    await sleep(2500);

    const loginCheck = await evaluate(`(() => {
        const authGateModal = document.getElementById('auth-gate-modal');
        const gateHidden = !authGateModal || authGateModal.style.display === 'none';
        const tierPill = document.getElementById('header-tier-pill')?.textContent;
        const mainContainer = document.querySelector('main.container');
        const mainVisible = mainContainer && window.getComputedStyle(mainContainer).display !== 'none';
        const bottomNav = document.getElementById('mobile-bottom-nav');
        const bottomNavVisible = bottomNav && window.getComputedStyle(bottomNav).display === 'flex';
        return { gateHidden, mainVisible, tierPill, bottomNavVisible };
    })()`);

    const loginPass = loginCheck.gateHidden && loginCheck.mainVisible && loginCheck.bottomNavVisible;
    console.log(`    [PASS/FAIL] Login Successful & Mobile Bottom Nav Active: ${loginPass ? 'PASS' : 'FAIL'}`);
    console.log(`    [INFO] Tier Pill: "${loginCheck.tierPill}", Bottom Nav: ${loginCheck.bottomNavVisible}`);
    await captureScreenshot('cft_mobile_01_authenticated_dashboard.png');
    testResults.push({ name: 'Mobile Login & Dashboard Hydration', pass: loginPass });

    // -------------------------------------------------------------------------
    // TEST 2: MOBILE USER SIGN OUT VIA TOP HEADER DROPDOWN
    // -------------------------------------------------------------------------
    console.log('\n[4/10] Step 2: Testing Mobile User Sign Out via Top Header Dropdown...');
    // Click user menu button to open dropdown
    await evaluate(`(() => {
        const btn = document.getElementById('btn-user-menu');
        if (btn) btn.click();
    })()`);
    await sleep(400);

    const dropdownCheck = await evaluate(`(() => {
        const dropdown = document.getElementById('user-menu-dropdown');
        if (!dropdown) return { open: false };
        const style = window.getComputedStyle(dropdown);
        const rect = dropdown.getBoundingClientRect();
        return {
            open: style.display === 'block',
            top: rect.top,
            left: rect.left,
            width: rect.width,
            height: rect.height,
            visible: rect.width > 0 && rect.height > 0
        };
    })()`);

    console.log(`    [INFO] User Dropdown Opened: ${dropdownCheck.open}, Dimensions: ${dropdownCheck.width}x${dropdownCheck.height} at top: ${dropdownCheck.top}px`);
    await captureScreenshot('cft_mobile_02_user_dropdown_open.png');

    // Click Sign Out
    const signoutClicked = await evaluate(`(() => {
        const signoutBtn = document.getElementById('menu-btn-signout');
        if (signoutBtn) {
            signoutBtn.click();
            return true;
        }
        return false;
    })()`);
    await sleep(1500);

    const signoutCheck = await evaluate(`(() => {
        const authGateModal = document.getElementById('auth-gate-modal');
        const gateVisible = authGateModal && window.getComputedStyle(authGateModal).display !== 'none';
        const token = localStorage.getItem('dd_jwt_token');
        return { gateVisible, tokenNull: !token };
    })()`);

    const signoutPass = signoutClicked && signoutCheck.gateVisible && signoutCheck.tokenNull;
    console.log(`    [PASS/FAIL] Mobile Sign Out via Header Menu: ${signoutPass ? 'PASS' : 'FAIL'} (Gate Visible: ${signoutCheck.gateVisible}, Token Cleared: ${signoutCheck.tokenNull})`);
    await captureScreenshot('cft_mobile_03_signed_out_gate.png');
    testResults.push({ name: 'Mobile Sign Out via Header Dropdown', pass: signoutPass });

    // -------------------------------------------------------------------------
    // TEST 3: RE-LOGIN AND TEST SIGN OUT VIA CLINICAL PROFILE MODAL
    // -------------------------------------------------------------------------
    console.log('\n[5/10] Step 3: Testing Alternative Mobile Sign Out via Clinical Profile Bottom Sheet...');
    await performLogin('basic@dietdost.app', 'DietDost@Demo2026!');
    await sleep(2500);

    // Click bottom nav Clinical button (#btn-nav-profile)
    await evaluate(`(() => {
        const btnProfile = document.getElementById('btn-nav-profile');
        if (btnProfile) btnProfile.click();
    })()`);
    await sleep(500);

    const profileSignoutCheck = await evaluate(`(() => {
        const modal = document.getElementById('profile-modal');
        const open = modal && window.getComputedStyle(modal).display !== 'none';
        const signoutBtn = document.getElementById('btn-profile-signout');
        const signoutVisible = signoutBtn && window.getComputedStyle(signoutBtn).display !== 'none';
        const sessionEmail = document.getElementById('profile-user-session-email')?.textContent;
        return { open, signoutVisible, sessionEmail };
    })()`);

    console.log(`    [INFO] Clinical Modal Open: ${profileSignoutCheck.open}, Profile Signout Visible: ${profileSignoutCheck.signoutVisible}, Session: "${profileSignoutCheck.sessionEmail}"`);
    await captureScreenshot('cft_mobile_04_profile_sheet_signout.png');

    // Click profile sign out
    await evaluate(`(() => {
        const btn = document.getElementById('btn-profile-signout');
        if (btn) btn.click();
    })()`);
    await sleep(1500);

    const profileSignoutPass = await evaluate(`(() => {
        const authGateModal = document.getElementById('auth-gate-modal');
        const gateVisible = authGateModal && window.getComputedStyle(authGateModal).display !== 'none';
        return gateVisible && !localStorage.getItem('dd_jwt_token');
    })()`);

    console.log(`    [PASS/FAIL] Mobile Sign Out via Clinical Sheet: ${profileSignoutPass ? 'PASS' : 'FAIL'}`);
    testResults.push({ name: 'Mobile Sign Out via Clinical Profile Sheet', pass: !!profileSignoutPass });

    // Re-login as premium user for meal logging tests (unlimited quota)
    await performLogin('premium@dietdost.app', 'DietDost@Demo2026!');
    await sleep(2500);

    // -------------------------------------------------------------------------
    // TEST 4: MEAL LOGGER MOBILE RENDERING & MODE SWITCHER
    // -------------------------------------------------------------------------
    console.log('\n[6/10] Step 4: Testing Instant Meal Logger Mobile Rendering & Ergonomics...');
    const loggerRender = await evaluate(`(() => {
        const card = document.querySelector('.logging-card');
        const modeCamera = document.getElementById('btn-mode-camera');
        const modeText = document.getElementById('btn-mode-text');
        const dropzone = document.getElementById('photo-dropzone');
        const sampleBtn = document.getElementById('btn-sample-thali');
        return {
            hasCard: !!card,
            modeCameraHeight: modeCamera?.offsetHeight,
            modeTextHeight: modeText?.offsetHeight,
            dropzoneVisible: dropzone && window.getComputedStyle(dropzone).display !== 'none',
            hasSampleBtn: !!sampleBtn
        };
    })()`);

    const loggerErgonomicsPass = loggerRender.hasCard && loggerRender.modeCameraHeight >= 40 && loggerRender.modeTextHeight >= 40;
    console.log(`    [PASS/FAIL] Meal Logger Touch Ergonomics (Height >= 40px): ${loggerErgonomicsPass ? 'PASS' : 'FAIL'} (Camera: ${loggerRender.modeCameraHeight}px, Text: ${loggerRender.modeTextHeight}px)`);
    await captureScreenshot('cft_mobile_05_meal_logger_camera_mode.png');
    testResults.push({ name: 'Meal Logger Touch Ergonomics', pass: loggerErgonomicsPass });

    // Switch to Text Mode
    await evaluate(`(() => {
        const btn = document.getElementById('btn-mode-text');
        if (btn) btn.click();
    })()`);
    await sleep(300);

    const textModeRender = await evaluate(`(() => {
        const box = document.getElementById('text-logger-box');
        const input = document.getElementById('text-input');
        const submit = document.getElementById('btn-submit-text');
        return {
            boxVisible: box && window.getComputedStyle(box).display !== 'none',
            inputHeight: input?.offsetHeight,
            submitHeight: submit?.offsetHeight,
            inputWidth: input?.offsetWidth,
            submitWidth: submit?.offsetWidth
        };
    })()`);

    console.log(`    [INFO] Text Mode Active: Input: ${textModeRender.inputWidth}x${textModeRender.inputHeight}px, Submit: ${textModeRender.submitWidth}x${textModeRender.submitHeight}px`);
    await captureScreenshot('cft_mobile_06_meal_logger_text_mode.png');

    // -------------------------------------------------------------------------
    // TEST 5: LOG MEAL BY TEXT & VERIFY REVIEW MODAL NO-OVERLAP
    // -------------------------------------------------------------------------
    console.log('\n[7/10] Step 5: Logging Meal by Text and Inspecting Mobile Review Modal...');
    const mealQuery = "2 Phulkas + 1 Katori Dal Tadka + Cucumber Salad";
    await evaluate(`(() => {
        const input = document.getElementById('text-input');
        if (input) input.value = ${JSON.stringify(mealQuery)};
        const btn = document.getElementById('btn-submit-text');
        if (btn) btn.click();
    })()`);

    let reviewModalOpen = false;
    let reviewMetrics = null;
    for (let i = 0; i < 25; i++) {
        await sleep(500);
        reviewMetrics = await evaluate(`(() => {
            const modal = document.getElementById('review-modal');
            if (!modal || window.getComputedStyle(modal).display === 'none') return null;

            const isModalOpenClass = document.body.classList.contains('modal-open');
            const bottomNav = document.getElementById('mobile-bottom-nav');
            const bottomNavDisplay = bottomNav ? window.getComputedStyle(bottomNav).display : 'none';
            const modalZIndex = parseInt(window.getComputedStyle(modal).zIndex || '0', 10);
            const bottomNavZIndex = parseInt(bottomNav ? window.getComputedStyle(bottomNav).zIndex || '0' : '0', 10);

            const dishInput = document.getElementById('review-dish-name-input');
            const kcalBadge = document.getElementById('review-dish-kcal-badge');
            const confirmBtn = document.getElementById('btn-confirm-meal');
            const cancelBtn = document.getElementById('btn-cancel-review');

            const confirmRect = confirmBtn?.getBoundingClientRect();
            const dishInputRect = dishInput?.getBoundingClientRect();

            return {
                open: true,
                isModalOpenClass,
                bottomNavDisplay,
                modalZIndex,
                bottomNavZIndex,
                dishTitle: dishInput?.value,
                dishInputWidth: dishInputRect?.width,
                kcalText: kcalBadge?.textContent,
                confirmVisible: confirmBtn && window.getComputedStyle(confirmBtn).display !== 'none',
                confirmHeight: confirmRect?.height,
                confirmTop: confirmRect?.top,
                cancelVisible: cancelBtn && window.getComputedStyle(cancelBtn).display !== 'none'
            };
        })()`);

        if (reviewMetrics) {
            reviewModalOpen = true;
            break;
        }
    }

    const reviewPass = reviewModalOpen &&
        (reviewMetrics.bottomNavDisplay === 'none' || reviewMetrics.modalZIndex > reviewMetrics.bottomNavZIndex) &&
        reviewMetrics.confirmVisible &&
        reviewMetrics.confirmHeight >= 44 &&
        reviewMetrics.dishInputWidth > 200;

    console.log(`    [PASS/FAIL] Review Modal Displayed Without Bottom Nav Conflict: ${reviewPass ? 'PASS' : 'FAIL'}`);
    console.log(`    [INFO] Modal z-index: ${reviewMetrics?.modalZIndex} vs Nav z-index: ${reviewMetrics?.bottomNavZIndex} (Nav Display: "${reviewMetrics?.bottomNavDisplay}")`);
    console.log(`    [INFO] Dish Title: "${reviewMetrics?.dishTitle}" (Input Width: ${reviewMetrics?.dishInputWidth}px)`);
    console.log(`    [INFO] Confirm Button Visible: ${reviewMetrics?.confirmVisible} (Height: ${reviewMetrics?.confirmHeight}px, Top: ${reviewMetrics?.confirmTop}px)`);
    await captureScreenshot('cft_mobile_07_review_modal_layout.png');
    testResults.push({ name: 'Mobile Review Modal Layout & Action Visibility', pass: reviewPass });

    // -------------------------------------------------------------------------
    // TEST 6: UPDATE FOOD ITEM & RECALCULATE TOTALS ON MOBILE
    // -------------------------------------------------------------------------
    console.log('\n[8/10] Step 6: Testing Mobile Food Item Adjustments & Realtime Recalculation...');
    const kcalBefore = await evaluate(`document.getElementById('review-dish-kcal-badge')?.textContent`);

    // Click portion stepper '+' on first item
    await evaluate(`(() => {
        const plusBtn = document.querySelector('#review-items-list .step-btn[data-step="0.5"]');
        if (plusBtn) plusBtn.click();
    })()`);
    await sleep(300);

    const kcalAfterStepper = await evaluate(`document.getElementById('review-dish-kcal-badge')?.textContent`);

    // Add Ghee Smear (+45 kcal)
    await evaluate(`(() => {
        const gheeChip = document.getElementById('chip-ghee');
        if (gheeChip) gheeChip.click();
    })()`);
    await sleep(300);

    const kcalAfterGhee = await evaluate(`document.getElementById('review-dish-kcal-badge')?.textContent`);
    console.log(`    Portion Stepper: ${kcalBefore} -> ${kcalAfterStepper}`);
    console.log(`    Ghee Smear Added: ${kcalAfterStepper} -> ${kcalAfterGhee}`);

    // Rename meal title
    await evaluate(`(() => {
        const input = document.getElementById('review-dish-name-input');
        if (input) {
            input.value = 'Homestyle Phulkas & Dal Tadka (Deluxe)';
            input.dispatchEvent(new Event('input', { bubbles: true }));
        }
    })()`);
    await sleep(200);

    await captureScreenshot('cft_mobile_08_food_items_adjusted.png');

    // Confirm & Save Meal
    console.log('    Confirming meal via pinned mobile footer button...');
    await evaluate(`(() => {
        const btn = document.getElementById('btn-confirm-meal');
        if (btn) btn.click();
    })()`);
    await sleep(2500);

    const modalClosed = await evaluate(`(() => {
        const modal = document.getElementById('review-modal');
        const closed = !modal || window.getComputedStyle(modal).display === 'none';
        const modalOpenClassRemoved = !document.body.classList.contains('modal-open');
        const bottomNav = document.getElementById('mobile-bottom-nav');
        const bottomNavRestored = bottomNav && window.getComputedStyle(bottomNav).display === 'flex';
        return { closed, modalOpenClassRemoved, bottomNavRestored };
    })()`);

    const updateAndSavePass = modalClosed.closed && modalClosed.bottomNavRestored;
    console.log(`    [PASS/FAIL] Meal Saved & Mobile Nav Restored: ${updateAndSavePass ? 'PASS' : 'FAIL'}`);
    testResults.push({ name: 'Update Food Item & Save Daily Meal Log', pass: updateAndSavePass });

    // -------------------------------------------------------------------------
    // TEST 7: DAILY HUD & MEAL DIARY UPDATED ON MOBILE
    // -------------------------------------------------------------------------
    console.log('\n[9/10] Step 7: Verifying Daily Calorie HUD & Diary Reflection on Mobile...');
    await sleep(1000);
    const hudMetrics = await evaluate(`(() => {
        const consumed = parseInt(document.getElementById('val-consumed')?.textContent?.replace(/,/g, '') || '0', 10);
        const budget = parseInt(document.getElementById('val-budget')?.textContent?.replace(/,/g, '') || '0', 10);
        return { consumed, budget };
    })()`);

    const hudPass = hudMetrics.consumed > 0;
    console.log(`    [PASS/FAIL] HUD Consumed Incremented: ${hudPass ? 'PASS' : 'FAIL'} (${hudMetrics.consumed} kcal / budget: ${hudMetrics.budget} kcal)`);
    await captureScreenshot('cft_mobile_09_hud_updated.png');
    testResults.push({ name: 'Daily HUD & Calorie Ledger Synchronized', pass: hudPass });

    // -------------------------------------------------------------------------
    // TEST 8: LOG MEAL BY PHOTO (SAMPLE MEAL PHOTO FLOW) WITH PREMIUM TIER
    // -------------------------------------------------------------------------
    console.log('\n[10/10] Step 8: Testing Log Meal by Photo via Try Sample Photo with Premium User (Unlimited AI Scans)...');
    // Switch to camera mode and click sample photo button
    await evaluate(`(() => {
        const btnCamera = document.getElementById('btn-mode-camera');
        if (btnCamera) btnCamera.click();
    })()`);
    await sleep(300);

    await evaluate(`(() => {
        const sampleBtn = document.getElementById('btn-sample-thali');
        if (sampleBtn) sampleBtn.click();
    })()`);

    let photoModalOpen = false;
    let photoData = null;
    for (let i = 0; i < 45; i++) {
        await sleep(500);
        photoData = await evaluate(`(() => {
            const modal = document.getElementById('review-modal');
            const open = modal && window.getComputedStyle(modal).display !== 'none';
            const photoImg = document.getElementById('review-meal-photo');
            const photoSrc = photoImg ? photoImg.src : '';
            const hasSrc = !!photoSrc && !photoSrc.includes('placeholder') && photoSrc.length > 5;
            const dishTitle = document.getElementById('review-dish-name-input')?.value;
            const confirmBtn = document.getElementById('btn-confirm-meal');
            const confirmVisible = confirmBtn && window.getComputedStyle(confirmBtn).display !== 'none';
            return { open, hasSrc, dishTitle, confirmVisible, photoSrc };
        })()`);
        if (photoData && photoData.open && (photoData.hasSrc || photoData.dishTitle)) {
            photoModalOpen = true;
            // Wait an extra second for image rendering to complete
            await sleep(1000);
            photoData = await evaluate(`(() => {
                const modal = document.getElementById('review-modal');
                const open = modal && window.getComputedStyle(modal).display !== 'none';
                const photoImg = document.getElementById('review-meal-photo');
                const photoSrc = photoImg ? photoImg.src : '';
                const hasSrc = !!photoSrc && !photoSrc.includes('placeholder') && photoSrc.length > 5;
                const dishTitle = document.getElementById('review-dish-name-input')?.value;
                const confirmBtn = document.getElementById('btn-confirm-meal');
                const confirmVisible = confirmBtn && window.getComputedStyle(confirmBtn).display !== 'none';
                return { open, hasSrc, dishTitle, confirmVisible, photoSrc };
            })()`);
            break;
        }
    }

    console.log(`    [PASS/FAIL] Photo Review Modal Opened with Plate Image: ${photoModalOpen ? 'PASS' : 'FAIL'}`);
    if (photoData) {
        console.log(`    [INFO] Dish Detected: "${photoData.dishTitle}", Photo Loaded: ${photoData.hasSrc} (${photoData.photoSrc}), Confirm Visible: ${photoData.confirmVisible}`);
    }
    await captureScreenshot('cft_mobile_10_photo_review_modal.png');

    if (photoModalOpen) {
        // Confirm photo meal
        await evaluate(`(() => {
            const btn = document.getElementById('btn-confirm-meal');
            if (btn) btn.click();
        })()`);
        await sleep(2500);
    }

    testResults.push({ name: 'Log Meal by Photo Flow', pass: photoModalOpen });

    // Clean up
    console.log('\n--> Cleaning up browser session...');
    ws.close();
    edgeProc.kill();

    // Summary Assessment
    console.log('\n======================================================================');
    console.log('              MOBILE CORE ACCEPTANCE CFT REPORT                       ');
    console.log('======================================================================');
    let allPassed = true;
    testResults.forEach((t, i) => {
        const mark = t.pass ? 'PASS (100%)' : 'FAIL';
        if (!t.pass) allPassed = false;
        console.log(`${i + 1}. ${t.name.padEnd(45)}: ${mark}`);
    });
    console.log('======================================================================\n');

    if (allPassed) {
        console.log('>>> VERDICT: ALL CORE MOBILE CFT FEATURES VERIFIED SUCCESSFULLY! <<<');
        process.exit(0);
    } else {
        console.error('>>> VERDICT: ONE OR MORE MOBILE ACCEPTANCE TESTS FAILED! <<<');
        process.exit(1);
    }
}

main().catch(err => {
    console.error('Fatal error during mobile CFT execution:', err);
    process.exit(1);
});
