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
> **Associated ADRs**: [`ADR-055`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261001-055-web-client-single-roundtrip-hydration.md), [`ADR-061`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md), [`ADR-065`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md), [`ADR-067`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md), [`ADR-114`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261018-114-diet-dost-brand-icon-and-loading-experience.md)  
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

---

## 5. Verification & Quality Gates

* **Zero Console Errors**: All script dependencies, favicons, and assets resolve with HTTP 200.
* **Responsive Breakpoints**: Seamless scaling across Mobile (<640px), Tablet (640px–1024px), Desktop (1024px–1440px), and Ultra-wide (>1440px).
* **Automated Acceptance**: Validated through `docs/cft/cft_web_bff_and_clean_architecture.md` and `tests/validate_e2e_tiers.ps1`.
