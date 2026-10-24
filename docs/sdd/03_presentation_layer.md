<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# SDD-03: Presentation Layer & Web UI Architecture

> **Specification Ref**: `docs/sdd/03_presentation_layer.md`  
> **Classification**: Software Design Document (SDD) – Presentation & Web UI  
> **Governing Design System**: [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) (Linear Obsidian Surface Ladder)  
> **Architecture Pattern**: Native ES Modules, Zero-Bundler Vanilla JS, Component Partials, Web BFF Single-Roundtrip Hydration  
> **Associated ADRs**: [`ADR-055`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261001-055-web-client-single-roundtrip-hydration.md), [`ADR-061`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md), [`ADR-065`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md), [`ADR-067`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md), [`ADR-114`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261018-114-diet-dost-brand-icon-and-loading-experience.md), [`ADR-128`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261024-128-daily-view-isolation-and-yearly-view-removal.md)  
> **Relevant CFTs**: [`cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md), [`cft_responsive_web_and_tablet.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_responsive_web_and_tablet.md)  

---

## 1. Executive Summary & Presentation Philosophy

The **Diet-Dost** presentation layer delivers a high-performance, medical-grade nutrition tracking experience built without heavy JavaScript frameworks (React, Angular, Vue). By leveraging native HTML5 semantic elements, modern CSS custom properties, and native ES Modules with dependency injection, Diet-Dost achieves sub-second initial load times, zero Cumulative Layout Shift (CLS), and complete cross-device responsiveness.

The visual design language is anchored in the **Linear Obsidian Surface Ladder**: a near-black product-focused canvas (`#010102`), dense charcoal card panels (`#0f1011`), hairline borders (`#23252a`), and disciplined clinical accents (emerald `#10b981`, turmeric `#f59e0b`, and lavender `#8b5cf6`).

---

## 2. Brand Identity & Iconography Architecture

Diet-Dost's visual brand identity is codified in **"The Clinical DD Pulse & Living Thali"** (ADR-114):

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                 DIET-DOST BRAND ICON ARCHITECTURE                      │
  │                                                                        │
  │   [ Master Artwork: wwwroot/assets/icon-512.png (512x512) ]            │
  │   [ Vector Encapsulation: wwwroot/assets/diet-dost-icon.svg ]          │
  │                                                                        │
  │    ├── Root Favicon: wwwroot/favicon.ico (Multi-res 16/32/48/64/128/256 PNG-in-ICO) │
  │    ├── Root SVG Favicon: wwwroot/favicon.svg (Infinite DPI, 100% PNG) │
  │    ├── Browser Pins: wwwroot/assets/favicon-16x16.png & 32x32.png     │
  │    ├── PWA Mobile: wwwroot/assets/icon-192.png (Android home screen)   │
  │    ├── PWA Launcher: wwwroot/assets/icon-512.png (Desktop install)     │
  │    └── PWA Manifest: wwwroot/manifest.json (#010102 dark canvas)       │
  └────────────────────────────────────────────────────────────────────────┘
```

### Detailed Concept Breakdown

| Concept | Visual Metaphor | Emotional Connection & Domain Meaning | Best Suited For |
|---|---|---|---|
| **Concept: The Clinical DD Pulse & Living Thali (Recommended)** | Bold interlocking DD monogram with an inner living green leaf, set against a warm golden turmeric thali halo and an AI vitality rhythm pulse. | Unites clinical authority with cultural food pride. The double "D" anchors the name Diet Dost, while the pulse highlights AI vitality and the leaf honors fresh Indian produce. | Master app icon, browser favicon, and high-tech health dashboard |

### Visual Metaphor Elements:
1. **Interlocking DD Monogram**: Modern geometric typography representing **Diet** (clinical nutrition intake) and **Dost** (empathetic Indian companion).
2. **Living Emerald Leaf**: Botanical leaf with delicate inner veins resting on the crown of the first "D", symbolizing fiber-rich living nutrition and metabolic vitality.
3. **Golden Turmeric Thali Halo**: Concentric circular track in golden turmeric (`#f59e0b`), symbolizing the traditional Indian thali with 3 balance studs representing Protein, Carbohydrate, and Fat equilibrium.
4. **Lavender Vitality Cardiogram Wave**: Electrocardiogram rhythm in Linear lavender (`#8b5cf6`), signifying AI multimodal perception and cardiovascular health.

---

## 3. Application Splash Screen & Zero-CLS Loading Lifecycle

To prevent Flash of Unhydrated Elements (FOUC) and eliminate layout jumping while HTML partials and initial Web BFF payloads load, an inline loading screen is embedded into `index.html`:

```
   [Browser Requests /]
            │
            ▼
   ┌────────────────────────────────────────────────────────┐
   │ Inline Splash Screen Renders Instantly (#app-loading)  │
   │ - Deep Obsidian #010102 canvas & hairline borders      │
   │ - Pulsing 512x512 Brand Image (icon-512.png)           │
   │ - Rotating orbital loader ring (conic gradient symbol) │
   │ - Dual concentric biological heartbeat ripple rings    │
   │ - Micro-spinner indicator & Linear shimmer track       │
   └────────────────────────────────────────────────────────┘
            │
            ▼
   ┌────────────────────────────────────────────────────────┐
   │ main.js: loadPartials() asynchronously loads HTML     │
   │ - header.html, hero-hud.html, meal-logger.html, etc.   │
   └────────────────────────────────────────────────────────┘
            │
            ▼
   ┌────────────────────────────────────────────────────────┐
   │ Web BFF Composite Hydration: apiClient.getWebDashboard │
   │ - Synchronously populates HUD, charts, quota badge     │
   └────────────────────────────────────────────────────────┘
            │
            ▼
   ┌────────────────────────────────────────────────────────┐
   │ main.js: dismissSplashScreen() (min 750ms duration)    │
   │ - Adds .splash-fade-out class (500ms cubic-bezier)     │
   │ - Smoothly scales and dissolves overlay from DOM       │
   └────────────────────────────────────────────────────────┘
```

---

## 4. UI Component Architecture & ES Modules

The frontend follows SOLID principles with explicit Dependency Injection via `src/Nutrition.WebGateway/wwwroot/js/core/di-container.js`:

1. **Header Component (`partials/header.html`)**:
   - Houses the brand badge (`<img src="assets/diet-dost-icon.svg">`), application title, clinical subtitle, calculation transparency launcher, and user profile/quota dropdown.
2. **Hero Calorie Balance & Macro HUD (`partials/hero-hud.html`)**:
   - Managed by `DailyHudController` (`js/ui/daily-hud.js`). Displays daily calorie target, deficit gauge, and P/C/F progress rings with zero client-side calculation.
3. **AI Meal Vision & Smart Logger (`partials/meal-logger.html`)**:
   - Managed by `MealLoggerController` (`js/ui/meal-logger.js`) and `ReviewModalController` (`js/ui/review-modal.js`). Handles image upload, AI detection preview, portion stepping, and quick-add chips.
4. **Historical Analytics (`partials/analytics-card.html`)**:
   - Managed by `AnalyticsChartController` (`js/ui/analytics-chart.js`). Renders interactive projection curves, meal history logs, and XLSX export.
5. **Obsidian Auth Gate (`partials/auth-gate.html`)**:
   - Managed by `AuthGateController` (`js/ui/auth-gate.js`). Provides sign-in, account registration, OTP verification, and quick-access demo account pills for testing all 5 user tiers.
6. **Authoritative Shared Luxury Footer (`partials/footer.html`)**:
   - Standardized Obsidian global footer across all pages (`index.html`, `terms.html`, `clinical-health-consent.html`).
   - Features the Creator Showcase Hero Card: **"Created with ❤️ by Nikunj Banker — Dedicated to health, vitality, and happy diet food."**
   - Incorporates official copyright: **`© 2026 Diet-Dost (@diet-dost). All rights reserved.`** and interactive GitHub repository link.
   - Houses the **Alpha Version Warning Strip**: **`⚠️ Alpha Version Notice: Diet-Dost is currently in active Alpha preview (v1.5.0)...`** ensuring full user awareness and clinical transparency.
7. **Always-Visible Floating Maker Chip (`.floating-maker-chip`)**:
   - Persistent glassmorphic Obsidian dock (`z-index: 990;`) keeping creator branding visible at all times across all viewports.
   - Desktop placement: Docked at `bottom: 24px; right: 24px;`.
   - Mobile placement: Elevated at `bottom: calc(64px + env(safe-area-inset-bottom, 0px) + 12px); right: 12px;` to guarantee 0 touch collisions with `.mobile-bottom-nav` or the meal logging FAB.
   - Features animated `@keyframes heartBeat` pulse, brand icon, Alpha version chip, and direct GitHub repo pill.
8. **Dussehra (Vijayadashami) Festival Launch Dynamic Carousel (`partials/dussehra-launch-banner.html`)**:
   - Dynamic 4-concept launch banner carousel with auto-rotation (7.5s), concept tabs, indicator dots, and collapse/expand ticker:
     - *Concept 1 (Metabolic Victory)*: Vanquish the 10 Metabolic Foes with AI Precision.
     - *Concept 2 (Auspicious Beginning)*: Shubh Aarambh — Gift Yourself the Gold of True Health.
     - *Concept 3 (Mindful Celebration)*: Celebrate Festive Delicacies with Pure Joy & Mindful Balance.
     - *Concept 4 (Founder's Dedication)*: Dussehra Dedication — Created with ❤️ by Nikunj Banker.
   - Interactive *Shubh Sankalp* health pledge button triggers celebratory confetti burst (`ConfettiService`) and empowering launch toast.
9. **Mobile Ergonomics & Single Fluid Vertical Scroll Architecture (`styles.css`, `partials/review-modal.html`, `partials/auth-gate.html`)**:
   - **Auth Gate Mobile Ergonomics**: Decouples `.auth-gate-overlay` from desktop bottom-sheet positioning on screens `<640px` (`align-items: flex-start !important;`), eliminating the 250px dead top void and framing `.auth-gate-card` as an immersive, full-height mobile sheet with `env(safe-area-inset-top)` and `env(safe-area-inset-bottom)` support.
   - **Form-Group Input Standardization**: Enforces `.form-group` (`display: flex; flex-direction: column; gap: 0.35rem; width: 100%;`) and `.form-label-row` (`display: flex; justify-content: space-between; align-items: center;`), guaranteeing that `<label>` elements sit consistently directly above `<input>` fields without awkward side-by-side distortions.
   - **Dissolved Split Columns (`display: contents`)**: In mobile viewports (`<640px`), `.review-media-column` and `.review-details-column` are dissolved using `display: contents !important;`, eliminating dual nested inner scrollboxes and unifying all modal content into **one single fluid vertical scroll** on `.review-body-layout`. Scoped specifically to `#review-modal` to avoid CSS conflicts with bottom-sheet modals.
   - **Mobile Bottom-Sheet Modal Preservation**: Preserves `#profile-modal.review-modal-overlay` with bottom-sheet alignment (`align-items: flex-end !important;`) and `.profile-modal-card` with top rounded corners (`border-radius: 20px 20px 0 0 !important;`), ensuring compliance with responsive CFT viewports.
   - **Mobile Visual Reading Hierarchy**: Reorders modal sections via CSS `order`: Media preview (1) -> Meal Timing (2) -> **Detected Food Items List (`dish-items-list`) (3)** -> Quick Add Subzi Chips (4) -> Cooking Fat Chips (5) -> Search Bar (6) -> Dietitian Note (7) -> AI Feedback (8) -> Continuous Training Banner (9) -> Matrix Warnings (10).
   - **Sticky Mobile Action Bar**: `.review-footer` is pinned persistently to the bottom of the viewport with backdrop blur and safe-area padding for effortless one-thumb meal logging confirmation.

10. **LIFO Modal Stacking & Lifecycle Governance (`modal-stack-manager.js`, `styles.css`)**:
    - **Centralized Stacking Context**: Implemented `ModalStackManager` to assign calculated z-indices (`100010 + depth * 20`) dynamically, ensuring that child dialogs (Admin Create/Edit User, Review Photo Lightbox, Delete Confirmations) strictly render on top of their parent modals.
    - **Scroll-Lock Retention**: Manages `body.modal-open` such that background scroll-lock is acquired on the first opened modal and revoked ONLY when the entire stack is empty. Dismissing a child dialog never prematurely unfreezes background scrolling.
    - **Save & Cancel Lifecycle**: Standardized `ProgressModalController` to invoke `this.close()` on both Save and Cancel button actions instead of switching tabs. Eliminated redundant inline `onclick` handlers on navigation tabs.
    - **Accessible Mobile Dismissal**: Integrated explicit header close button (`#btn-review-header-close`) in `review-modal.html`, ensuring thumb-friendly dismissal from anywhere in the modal hierarchy.

11. **Uncut Aspect-Ratio Photo Architecture & Universal Lightbox Inspection (`styles.css`, `progress-modal.html`, `face-progress-card.html`, `review-modal.js`, `progress-modal.js`)**:
    - **Zero-Cropping Standard**: Standardized `.progress-img`, `.detail-img`, `.timeline-thumb`, and `#progress-img-preview` with `object-fit: contain; object-position: center; max-width: 100%; max-height: 100%; width: auto; height: auto;`, eliminating legacy `object-fit: cover` and fixed percentage stretching where selfie faces or heads were cut off.
    - **External Metadata Chip Row (`.photo-meta-sub`)**: Moved date and weight badges outside `.photo-frame` and `.detail-photo-frame` into dedicated sub-rows beneath the photos. Eliminates internal dark gradient overlays (`.photo-meta-overlay`) that previously sat directly over the user's chin, jawline, mouth, and neck, ensuring 100% unobstructed visibility of all facial slimming progress.
    - **Letterboxed Centering**: Configured containers (`.photo-frame`, `.detail-photo-frame`, `.timeline-thumb-frame`) with obsidian-dark backdrop (`#080a0f`) and flex centering (`display: flex; align-items: center; justify-content: center;`), displaying 100% of any uploaded portrait (9:16, 3:4, 1:1, or landscape).
    - **Universal High-Resolution Lightbox**: Exposes `window.openPhotoLightbox(photoUrl, title)` across the entire presentation layer. Users can tap any progress or comparison photo to inspect full resolution without cuts in the modal stack.
    - **Mobile Cache Invalidation & Static Asset Headers**: Bumped `index.html` asset query strings (`styles.css?v=1.9.0`, `modal-stack-manager.js?v=1.7.0`, `main.js?v=1.7.0`) and configured `OnPrepareResponse` in `WebApplicationExtensions.cs` to issue `Cache-Control: no-cache, must-revalidate` for HTML, CSS, and JS, guaranteeing mobile browsers immediately receive fresh layout rules without manual cache clearing.

12. **Mobile Edit Review Modal Save Button Isolation & AI Debounce Flush Synchronization (`review-modal.js`, `modal-stack-manager.js`, `styles.css`)**:
    - **Asynchronous AI Debounce Synchronization**: Upgraded `_debounce` to register active tasks and introduced `flushPendingAiSearches()`. When tapping "Save Changes" (`#btn-confirm-meal`), any pending 300ms debounce timers and in-flight Gemini estimation queries are automatically flushed and awaited before creating the update payload.
    - **Interactive Submission Feedback**: Added `this._isSaving = true;` guard to eliminate double submissions. When in-flight searches exist, button text transitions immediately to `'Recalculating with AI...'` and then `'Saving Changes...'`.
    - **Direct DOM Input Synchronization**: Before generating the PUT payload, `handleConfirmMeal()` reads the current values of `.item-name-input` and `.item-portion-input` directly from the DOM, ensuring modifications are never lost if a user taps Save without triggering an input `blur` event.
    - **Floating Element Touch Suppression**: Added `body.modal-open .floating-maker-chip { display: none !important; pointer-events: none !important; }` to eliminate touch collisions between the docked creator pill and the mobile `.review-footer`.
    - **Sticky Action Bar Stacking Context**: Elevated `.review-footer` on screens `<640px` to `z-index: 100020 !important;` with `pointer-events: auto !important;`, `touch-action: manipulation;`, and `-webkit-tap-highlight-color: transparent;`.
    - **Multi-Modal Scroll-Lock Integrity**: Updated `ModalStackManager` to ensure `document.body.classList.add('modal-open')` is applied whenever `this._stack.length > 0`, preventing bottom nav leaks during stacked modal workflows.
    - **ESLint & SAST Verification**: Standardized debounce timer cleanup iterations using `_debounceTimers.values()`, guaranteeing zero unused variable lint warnings across CI pipelines.

13. **Analytics Timeline Period Standardization & Daily Isolation (`analytics-card.html`, `analytics-chart.js`, `MealDiaryView.js`)**:
    - **Daily View Isolation (`1D`)**: Daily timeline view strictly filters meal history and category trends to today's logged meals (`userLocalDate == today`), displaying subtitle *"Showing Today's meal history"*.
    - **Yearly View Retirement (`365D`)**: Retired the 365D period tab from client UI and backend aggregation to eliminate massive data consolidation overhead, standardizing the supported timeline intervals to `1D` (Daily), `7D` (Weekly), `30D` (Monthly), and `90D` (Quarterly).
    - **Dedicated Contract Tests**: Isolated timeline assertions into `tests/Nutrition.EvalHarness.Tests/AnalyticsTimelineContractTests.cs`, preserving test segregation boundaries.

---

## 5. Verification & Quality Gates

* **Zero Console Errors**: All script dependencies, favicons, and assets resolve with HTTP 200.
* **Responsive Breakpoints**: Seamless scaling across Mobile (<640px), Tablet (640px–1024px), Desktop (1024px–1440px), and Ultra-wide (>1440px).
* **Automated Acceptance**: Validated through `docs/cft/cft_web_bff_and_clean_architecture.md`, `tests/validate_e2e_tiers.ps1`, `tests/Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`, and `tests/Nutrition.EvalHarness.Tests/AnalyticsTimelineContractTests.cs`.


