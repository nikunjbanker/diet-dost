<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-127: Mobile Edit Review Modal Save Button Isolation, Asynchronous AI Debounce Flush Synchronization, and Modal Layering Immunity

> **Date / Timestamp**: 2026-10-24T19:00:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[UI/UX]`, `[RELIABILITY]`  
> **Affected Subsystems**: WebGateway (`wwwroot/js/ui/review-modal.js`, `wwwroot/js/ui/modal-stack-manager.js`, `wwwroot/styles.css`, `wwwroot/partials/review-modal.html`, `wwwroot/index.html`), Tests (`Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`)  
> **Associated PR & Branch**: `fix/edit-meal-save-after-ai-search`  
> **Relevant SDDs & Rules**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)  

---

## 1. Executive Summary & Rationale

This architectural decision resolves a critical user defect encountered in the meal log edit view on mobile touch devices. Specifically, after editing a food item (name or portion), an inline AI estimation search (`⏳ 🤖 AI Searching...`) begins; once complete, tapping the "💾 Save Changes ✨" (`#btn-confirm-meal`) button appeared unresponsive or failed to save changes.

Investigation identified three interrelated root causes across touch layout, lifecycle timing, and DOM event orchestration:
1. **Asynchronous Debounce / In-Flight AI Search Race Condition**:
   In `review-modal.js`, editing a food item scheduled a 300ms debounced call to `estimateFoodItem()`. If a user tapped "Save Changes" before the debounce elapsed or while the backend Gemini query was in-flight, `handleConfirmMeal()` fired immediately with stale item states, followed by the debounced callback's `finally` block triggering `renderItems()` and `recalculateTotals()` on an unmounted or closing modal, discarding user edits or resetting button states.
2. **Missing Touch Ergonomics and Stacking Context Interception**:
   On mobile devices (<640px), `.floating-maker-chip` (`z-index: 990; pointer-events: auto;`) and `.mobile-bottom-nav` were not guaranteed to be suppressed when nested or stacked modals were opened. Furthermore, `modal-stack-manager.js` only applied `body.modal-open` if `this._stack.length === 1`. In dirty or stacked states (`length > 1`), `body.modal-open` was omitted, allowing fixed bottom elements to intercept taps over the sticky `.review-footer`.
3. **Payload Identity & Form Element Defenses**:
   `handleConfirmMeal()` used `this._state.userId` rather than `this._state.currentMeal.userId || this._state.userId`, causing potential 403 Forbidden rejections during multi-user or demo sessions. Additionally, buttons lacked explicit `type="button"`, causing potential synthetic form bubbling.

---

## 2. Technical Architecture & Modifications

### 2.1 Asynchronous Debounce Flush & Active Promise Synchronization (`review-modal.js`)
- Introduced `this._activeAiPromises = new Set();` and `this._isSaving = false;` to track in-flight AI searches and eliminate duplicate submission attempts.
- Upgraded `_debounce(key, fn, delay = 300)` to register `{ timer, fn }` in `this._debounceTimers`, ensuring callbacks can be executed immediately when saving.
- Added `flushPendingAiSearches()`:
  ```javascript
  async flushPendingAiSearches() {
    const pendingFns = [];
    for (const [key, entry] of this._debounceTimers.entries()) {
      clearTimeout(entry.timer);
      try {
        const p = entry.fn();
        if (p && typeof p.then === 'function') pendingFns.push(p);
      } catch (err) {
        console.warn(`Error invoking pending debounce '${key}':`, err);
      }
    }
    this._debounceTimers.clear();
    for (const p of pendingFns) this._activeAiPromises.add(p);
    if (this._activeAiPromises.size > 0) {
      await Promise.all(Array.from(this._activeAiPromises));
    }
  }
  ```
- In `handleConfirmMeal()`:
  - Immediately read values from `.item-name-input` and `.item-portion-input` directly into state to eliminate input blur race conditions.
  - Set button text to `'Recalculating with AI...'` and `await this.flushPendingAiSearches()` before generating the payload.
  - Set button text to `'Saving Changes...'` and send payload with fallback `userId: this._state.currentMeal.userId || this._state.userId || 'user-default'`.
- In `close()`: iterated over `this._debounceTimers.values()` to clean up pending timers cleanly without unused variable bindings, maintaining 100% ESLint SAST compliance.

### 2.2 Modal Layering & Tap Isolation Guarantee (`styles.css` & `modal-stack-manager.js`)
- In `modal-stack-manager.js`:
  ```javascript
  if (this._stack.length > 0) {
    document.body.classList.add('modal-open');
  }
  ```
- In `styles.css`:
  ```css
  body.modal-open .mobile-bottom-nav,
  body.modal-open .floating-maker-chip {
    display: none !important;
    pointer-events: none !important;
  }

  @media (max-width: 639px) {
    .review-footer {
      z-index: 100020 !important;
      pointer-events: auto !important;
    }
    .review-footer .btn {
      pointer-events: auto !important;
      cursor: pointer !important;
      touch-action: manipulation !important;
      -webkit-tap-highlight-color: transparent !important;
    }
  }
  ```

### 2.3 HTML Semantics & Cache Invalidation
- Added `type="button"` to `#btn-confirm-meal` and `#btn-cancel-review` in `review-modal.html`.
- Bumped cache-busting version strings in `index.html`: `styles.css?v=1.9.0`, `modal-stack-manager.js?v=1.7.0`, `main.js?v=1.7.0`.

---

## 3. Verification & Compliance Evidence

1. **Contract Tests**:
   - Added `ReviewModal_MobileSaveButton_HasTouchErgonomics_AndIsIsolatedFromFloatingElements` to `tests/Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`.
   - All tests passing with 0 warnings.
2. **End-to-End Tier Verification**:
   - Multi-tier acceptance suite (`validate_e2e_tiers.ps1`) passes 100% across Free, Basic, Premium, Admin, and SuperAdmin tiers.
3. **DevSecOps Gate**:
   - Verified clean pass on `scripts/verify-ai-security-defense.ps1 -Mode All`.
