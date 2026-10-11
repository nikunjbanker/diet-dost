<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261003-065: Mobile User Sign Out Repair & Meal Review Ergonomics Modernization

* **Status**: Accepted
* **Date**: 2026-10-03
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `mobile-ergonomics`, `auth-signout`, `meal-logger`, `review-modal`, `cft-verification`, `design-tokens`

---

## 1. Context & Problem Statement

During mobile viewport testing ($\le 639\,\text{px}$) of the Diet-Dost Web PWA, two critical usability defects were identified:
1. **User Sign Out Inoperability**:
   - In `styles.css`, `.app-header` was styled with `overflow-x: hidden;` under mobile media queries. Under W3C CSS specifications, setting `overflow-x: hidden` computes `overflow-y` to `auto` or clips vertical overflow. Because `.app-header` has a fixed height, the `.user-dropdown` menu extending downwards was clipped and completely invisible to mobile users.
   - In `main.js`, the click-outside handler checked `e.target !== userMenuBtn`. Tapping child `<span>` elements inside the button (avatar, tier badge, quota pill) evaluated to true and immediately dismissed the dropdown before "Sign Out" could be tapped.
2. **Meal Logger & Review Modal Obscured & Unusable**:
   - The fixed thumb-zone bottom navigation bar (`.mobile-bottom-nav`) operated at `z-index: 1000; position: fixed; bottom: 0;` while the meal review modal overlay (`.review-modal-overlay`) operated at `z-index: 100;`. The bottom nav sat directly on top of the modal, obscuring the primary action buttons ("Cancel" and "Looks Great! Log Meal").
   - The review modal card lacked mobile column flex structure and a pinned sticky footer, causing dish titles to truncate horizontally and action buttons to push below screen bounds.
   - Food item rows (`.item-row-editable`) attempted a single-line horizontal flex layout on narrow mobile screens ($375\text{px} - 390\text{px}$), squashing food names, portion inputs, and steppers.

---

## 2. Decision Drivers

1. **Guaranteed Mobile Authentication Lifecycle**: Ensure users on any mobile viewport (375px to 430px) can easily and reliably sign out with zero clipping or touch event interference.
2. **Dual Sign Out Pathways**: Provide both the header dropdown and a dedicated session management card within the Clinical Profile Sheet (`#profile-modal`) for resilient UX.
3. **Modal Stacking Context Supremacy**: Ensure full-screen modal overlays always sit above the mobile bottom navigation and prevent background interaction.
4. **DESIGN.md Mobile Ergonomics**: Enforce touch targets $\ge 48\,\text{px}$, pinned sticky action footers, and independent scrollable bodies for multi-item meal reviews.
5. **Living End-to-End Verification**: Validate all mobile core workflows (login, logout, dashboard, text meal log, photo meal log, item edits, and ledger reflection) via an automated Customer & Functional Acceptance Test (CFT) suite.

---

## 3. Considered Options

* **Option 1: Hide mobile bottom navigation permanently on all modal pages**: Brittle; would break navigation state if modal closing failed or threw an exception.
* **Option 2 (Selected): W3C-Compliant Mobile Dropdown + Stacking Context Coordination + Pinned Modal Flex Layout**:
  - Set `.app-header { overflow: visible !important; }` on mobile and position `.user-dropdown` with `position: fixed !important; top: 52px !important; right: 8px !important; z-index: 100005 !important;`.
  - Fix event delegation in `main.js` via `!userMenuBtn?.contains(e.target)` and expose a reusable `performSignOut()` routine.
  - Add an "Account Session & Sign Out" card to `partials/profile-modal.html`.
  - Elevate modal overlays to `z-index: 100010 !important;` and coordinate with `body.modal-open .mobile-bottom-nav { display: none !important; }`.
  - Structure `.review-card` into a 94vh flex column layout with `overflow-y: auto` body and pinned sticky `.review-footer`.
  - Stack `.item-row-editable` on mobile into a 2-row layout (Row 1: item name; Row 2: portion, AI search chip, stepper buttons, and delete button).

---

## 4. Architectural & Implementation Details

### A. Mobile Header & Dropdown Coordination (`styles.css` & `main.js`)
```css
@media (max-width: 639px) {
  .app-header {
    overflow: visible !important; /* Eliminate W3C computed overflow-y clip */
  }

  .user-dropdown {
    position: fixed !important;
    top: 52px !important;
    right: 8px !important;
    left: auto !important;
    min-width: 250px !important;
    max-width: calc(100vw - 16px) !important;
    z-index: 100005 !important;
    box-shadow: 0 16px 36px rgba(0, 0, 0, 0.85), 0 0 0 1px var(--hairline);
  }
}
```

### B. Modal vs Bottom Navigation Stacking Governance
```css
/* Ensure modals sit above mobile bottom navigation bar */
.modal-overlay,
.review-modal-overlay {
  z-index: 100010 !important;
}

/* Hide mobile bottom navigation bar while any modal is open */
body.modal-open .mobile-bottom-nav {
  display: none !important;
}
```

### C. Review Modal Mobile Flex Column & Pinned Footer
```css
@media (max-width: 639px) {
  .review-card {
    height: 94vh !important;
    max-height: 94vh !important;
    display: flex !important;
    flex-direction: column !important;
    overflow: hidden !important;
  }

  .review-body-layout {
    flex: 1 1 auto !important;
    overflow-y: auto !important;
  }

  .review-footer {
    flex-shrink: 0 !important;
    position: sticky !important;
    bottom: 0 !important;
    background: var(--surface-1) !important;
    padding: 12px 14px !important;
    border-top: 1px solid var(--hairline) !important;
    z-index: 10 !important;
  }

  .review-footer button {
    min-height: 48px !important;
    height: 48px !important;
  }
}
```

---

## 5. Verification & Acceptance Evidence

### A. Automated Mobile Core Flows Suite (`tests/verify_mobile_cft_core_flows.mjs`)
Executed against live `Nutrition.WebGateway` on port 5240 under an iPhone 14 mobile viewport ($390 \times 844$, touch enabled):

| Test Scenario | Result | Evidence / Metric |
| :--- | :--- | :--- |
| **1. Mobile Login & Dashboard Hydration** | **PASS (100%)** | Tier pill: `⭐ Basic`, Bottom Nav: active |
| **2. Mobile Sign Out via Header Menu** | **PASS (100%)** | Dropdown: $253 \times 159\,\text{px}$, Gate visible, JWT cleared |
| **3. Mobile Sign Out via Clinical Profile** | **PASS (100%)** | Profile modal open, `#btn-profile-signout` visible and functional |
| **4. Meal Logger Touch Ergonomics** | **PASS (100%)** | Camera/Text mode switcher buttons: $\ge 50\,\text{px}$ touch height |
| **5. Mobile Review Modal Layout & Action Visibility** | **PASS (100%)** | Modal z-index: `100010`, Nav display: `none`, Confirm button: $53\,\text{px}$ height at $830\,\text{px}$ top |
| **6. Update Food Item & Save Daily Meal Log** | **PASS (100%)** | Portion stepper: $+85\,\text{kcal}$, Ghee smear: $+45\,\text{kcal}$, Meal confirmed, Nav restored |
| **7. Daily HUD & Ledger Synchronized** | **PASS (100%)** | Consumed: $950\,\text{kcal}$ / budget: $1586\,\text{kcal}$, toast displayed above nav |
| **8. Log Meal by Photo Flow (Sample Plate)** | **PASS (100%)** | Plate photo loaded, AI vision dish recognized, Confirm action functional |

### B. Visual Quality Inspection
- `cft_mobile_02_user_dropdown_open.png`: Verified dropdown appears crisp, fully floating below the top header with zero clipping.
- `cft_mobile_07_review_modal_layout.png`: Verified review card occupies 94vh with full-width dish input, scrollable nutrition info, and pinned footer.
- `cft_mobile_08_food_items_adjusted.png`: Verified portion steppers adjust values and macros in real-time.
- `cft_mobile_09_hud_updated.png`: Verified consumed calories, remaining budget, and macro progress bars update smoothly.
- `cft_mobile_10_photo_review_modal.png`: Verified food plate image inspection and confirm button visibility.

### C. Solution Build & Unit Test Verification
- `dotnet build src/Nutrition.WebGateway/Nutrition.WebGateway.csproj -c Release`: 0 warnings, 0 errors.
- `dotnet test --configuration Release`: 159 / 159 tests passed across `Nutrition.Domain.Tests` and `Nutrition.EvalHarness.Tests`.
- `pwsh -File tests/validate_e2e_tiers.ps1`: 100% Pass across all 5 demo user tiers.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Native Mobile Modernization**: The pinned footer and independently scrolling meal body layout directly translates to native Jetpack Compose (`Scaffold` bottomBar) and SwiftUI (`safeAreaInset(edge: .bottom)`) paradigms.
* **Mobile BFF Consistency**: Both text and photo meal logging use standard payload schemas compatible with future `/api/mobile/v1/*` endpoints.
* **Design System Uniformity**: All modal surfaces and touch target dimensions strictly comply with [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md).
