<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261031-142: Zero-PII Auth Gate Direct Entry, Error Preservation, and Unauthorized Loop Prevention

- **Status**: Accepted
- **Date**: 2026-10-31
- **Domain**: Presentation / UX / Security
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: [#117](https://github.com/nikunjbanker/diet-dost/issues/117)

---

## 1. Context & Problem Statement
During testing of the Zero-PII Phrase and JSON key authentication flow on `https://dev.diet-dost.in`, two UX issues degraded user experience:
1. **Unwanted Registration Modal on Returning Login**:
   When logging in successfully with a 12-word phrase or JSON file, `qr-auth-controller.js` triggered the registration recovery modal showing new mnemonic words and a "Download JSON" button instead of directly transitioning into the dashboard.
2. **Error Masking and Modal Tab Reset**:
   When entering an invalid or mistyped mnemonic phrase on the `🔑 Phrase / Key` tab, `api-client.js` caught the HTTP 401 response and dispatched a global `auth:unauthorized` event. This event was caught by `main.js`, which immediately called `authGate.show('signin')`, resetting the modal tab to standard password login and destroying the user's input before they could read the error message. Furthermore, ASP.NET Core `ProblemDetails.Detail` messages were not displayed.

---

## 2. Root Cause Analysis
1. **Modal Event Misalignment**:
   `handleRecoverSubmit` called `this.showRecoverySuccessModal(res)` which was intended solely for initial anonymous onboarding, confusing users who were simply returning to their existing account.
2. **Global Auth Interceptor Overreach**:
   `api-client.js` handled any 401 response by dispatching `window.dispatchEvent(new CustomEvent('auth:unauthorized'))`. Because `/api/auth/recover` is an authentication endpoint, expected authentication failures (e.g. invalid phrase or unknown key) triggered this global event, kicking the user out of the Phrase/Key tab and discarding error feedback.

---

## 3. Decision & Implementation

### 3.1 Direct Dashboard Transition on Zero-PII Login
- Updated `handleRecoverSubmit` in `qr-auth-controller.js`:
  - When returning via Phrase / JSON key login, calls `this.onAuthSuccess(res.user)` and immediately dismisses the Auth Gate modal.
  - Updates navigation UI and composite user hydration without displaying redundant download prompts.

### 3.2 Selective `auth:unauthorized` Dispatch in `api-client.js`
- Modified `ApiClient.request()` to check `!url.includes('/api/auth/')` before dispatching `auth:unauthorized`.
- Preserved detailed server error messages by prioritizing `data?.detail || data?.error || data?.message`.
- Invalid phrase/key errors now stay on the active tab and display inline in red without resetting tabs.

### 3.3 Cache Busting & Asset Synchronization
- Bumped asset cache-busting queries to `v=1.9.3` across `index.html`, `main.js`, `api.js`, `auth-gate.js`, and `qr-auth-controller.js`.

---

## 4. Verification Evidence
- **Browser & Unit Tests**: Verified clean error reporting and tab retention on invalid credentials.
- **CFT Execution**: Multi-tier acceptance tests run successfully with zero regression across authentication workflows.
- **Static Code Analysis**: 0 ESLint warnings, 0 syntax issues.
