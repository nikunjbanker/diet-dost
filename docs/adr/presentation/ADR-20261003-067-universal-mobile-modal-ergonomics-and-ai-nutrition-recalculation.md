<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-067: Universal Mobile Modal Ergonomics, Layering Isolation, and Dynamic AI Nutrition Recalculation

* **Status**: Accepted
* **Date**: 2026-10-03
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `mobile-ergonomics`, `modal-open`, `z-index`, `ai-recalculation`, `cft-verification`, `design-system`

---

## 1. Context & Problem Statement

During mobile viewport verification and acceptance testing of the Diet-Dost Web PWA, four critical ergonomic and functional challenges were identified across mobile dialogs and meal logging flows:

1. **Mobile Bottom Navigation Stacking Interference**:
   On mobile viewports (< 640px), the fixed bottom navigation bar (`.mobile-bottom-nav`) rendered at `z-index: 1000` with `position: fixed; bottom: 0;`. When dialogs or modals opened (such as Clinical Profile, Admin Console, Calculation Transparency, Quota Upgrade, Delete Confirmation, and Auth Gate), the bottom navigation overlay remained visible and intercepted touch events, obscuring primary action buttons, pinned footers, or confirmation controls.
2. **Modal Card Scroll Distortion**:
   Applying `overflow-y: auto !important` indiscriminately to modal containers collapsed the flexbox column architecture, causing pinned header bars (titles and close targets) and bottom action footers to scroll off-screen on compact displays like iPhone SE.
3. **Static AI Food Quantities & Macro Stagnation**:
   When users adjusted food item names or stepped portions in the Review Modal after initial AI photo or text detection, macros and calorie totals were statically clamped or lacked intelligent re-estimation against clinical databases (IFCT 2017 / ICMR-NIN 2024).
4. **Registration Control State Coupling**:
   The registration submission button and mode tabs had hardcoded disabled styles that did not dynamically reflect changes to `Auth:AllowRegistration` configuration in runtime environments.

---

## 2. Decision Drivers

1. **Authoritative UI/UX Design System Standard (DESIGN.md)**:
   All dialogs on touch and mobile devices must adhere to the 4-step surface ladder, provide >= 44x44px touch targets, and avoid visual collision with persistent chrome.
2. **Universal Modal Lifecycle Contract**:
   Every modal controller must uniformly synchronize body state via `body.modal-open` to cleanly eliminate navigation collision without ad-hoc per-screen hacks.
3. **Responsive Bottom Sheet Pattern**:
   Mobile dialogs must behave as polished bottom sheets with pinned headers (`flex-shrink: 0`), independently scrollable bodies (`flex: 1; overflow-y: auto; -webkit-overflow-scrolling: touch;`), and sticky action footers (`position: sticky; bottom: 0;`).
4. **Zero-Assumption Nutrition Re-estimation**:
   Adjusting food items or portion quantities must trigger debounced, non-destructive AI recalibration using `/api/meals/estimate` while maintaining active input focus and displaying inline status badges.

---

## 3. Considered Options

* **Option 1: Isolated z-Index Increments**: Increase each modal's z-index arbitrarily to 999999.  
  *Critique*: Fragile. Does not solve background scrolling, rubber-banding, or virtual keyboard displacement on mobile browsers.
* **Option 2: Ad-Hoc DOM Element Hiding**: Have each individual view manually hide `.mobile-bottom-nav` on trigger.  
  *Critique*: Error-prone. Causes architectural drift if any modal fails to restore the nav upon cancellation, ESC keypress, or backdrop click.
* **Option 3 (Selected): Universal `body.modal-open` Lifecycle & Unified Mobile Bottom Sheet Architecture**:
  - Enforce `document.body.classList.toggle('modal-open')` across all modal controllers (`AuthGate`, `ReviewModal`, `ProfileModal`, `AdminModal`, `TransparencyModal`, `QuotaModal`, `ProgressModal`, and `MealDiaryView`).
  - Configure CSS rule `body.modal-open .mobile-bottom-nav { display: none !important; }` and elevate modal overlays to `z-index: 100010 !important;`.
  - Structure all mobile dialog cards as strict flexbox columns with pinned headers, scrollable panes, and sticky footers.
  - Implement debounced `updateItemQuantityAi(idx, newQty)` in `ReviewModalController` to recalibrate macros dynamically.

---

## 4. Architectural & Implementation Details

### A. Universal `body.modal-open` Synchronization
All modal controllers synchronize opening and closing with `document.body`:
```javascript
// Example in ProfileModalController
open() {
  if (this.elements.modal) {
    this.elements.modal.style.display = 'flex';
    document.body.classList.add('modal-open');
  }
}
close() {
  if (this.elements.modal) {
    this.elements.modal.style.display = 'none';
    document.body.classList.remove('modal-open');
  }
}
```

### B. Unified Mobile Bottom Sheet CSS (`styles.css`)
```css
@media (max-width: 639px) {
  body.modal-open .mobile-bottom-nav {
    display: none !important;
  }

  .modal-overlay,
  .review-modal-overlay,
  .delete-modal-overlay,
  .auth-gate-overlay {
    z-index: 100010 !important;
  }

  .review-card,
  .modal-card {
    display: flex !important;
    flex-direction: column !important;
    max-height: 90vh !important;
    overflow: hidden !important;
    border-radius: var(--radius-xl) var(--radius-xl) 0 0 !important;
  }

  .modal-header,
  .review-header {
    flex-shrink: 0 !important;
  }

  .modal-body,
  .review-body-layout {
    flex: 1 1 auto !important;
    overflow-y: auto !important;
    -webkit-overflow-scrolling: touch;
  }

  .modal-footer,
  .review-actions-footer {
    position: sticky !important;
    bottom: 0 !important;
    flex-shrink: 0 !important;
    z-index: 20 !important;
    background: var(--surface-modal) !important;
  }
}
```

### C. Live AI Food & Quantity Re-estimation (`review-modal.js`)
```javascript
async updateItemQuantityAi(idx, newQty) {
  const item = this._state.currentMeal.identifiedItems[idx];
  item.quantity = newQty;
  const scaledPortion = `${newQty} ${unitName}`;
  const estimate = await this._meals.estimateFoodItem(item.name, scaledPortion);
  if (estimate) {
    item.calories = estimate.calories;
    item.proteinGrams = estimate.proteinGrams;
    item.carbsGrams = estimate.carbsGrams;
    item.fatGrams = estimate.fatGrams;
    item.isAiEstimated = true;
    this.recalculateTotals();
  }
}
```

---

## 5. Verification & Acceptance Evidence

1. **Core Mobile Acceptance CFT Suite (`tests/verify_mobile_cft_core_flows.mjs`)**:
   - Step 1: Mobile Login as `basic@dietdost.app`: **PASS**
   - Step 2: Mobile Sign Out via Header Dropdown: **PASS**
   - Step 3: Mobile Sign Out via Clinical Profile Bottom Sheet: **PASS**
   - Step 4: Meal Logger Touch Ergonomics (>= 44px): **PASS**
   - Step 5: Mobile Review Modal Layout & Zero Bottom Nav Collision: **PASS**
   - Step 6: Food Item Stepper & Ghee Smear Dynamic Recalculation: **PASS**
   - Step 7: Daily HUD Consumed & Calorie Ledger Synchronization: **PASS**
   - Step 8: Log Meal by Photo Flow with Live Vision AI: **PASS**
   - **Verdict: 8 / 8 Tests Passed (100%)**.

2. **Design System Token & Ergonomics Audit (`tests/audit_design_system_compliance.mjs`)**:
   - Desktop-XL (1440x900), Tablet-Portrait (768x1024), Mobile-Standard (390x844), and Mobile-Compact (375x667): **100% COMPLIANT**.

3. **Multi-Tier End-to-End Suite (`tests/validate_e2e_tiers.ps1`)**:
   - Validated Free, Basic, Premium, Admin, and SuperAdmin tiers: **100% PASS**.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

- **Phase 2 Native Mobile Shell (Android / iOS)**: The `body.modal-open` protocol and bottom-sheet flexbox model provide identical layout invariants when running in native web views or hybrid shells.
- **Phase 3 Offline-First Sync**: Dynamic food and portion estimation functions consume standardized CQRS query interfaces (`/api/meals/estimate`), ensuring identical behavior when synced locally via SQLite cache.
