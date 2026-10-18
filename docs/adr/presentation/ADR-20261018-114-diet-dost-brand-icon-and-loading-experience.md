<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-114: Diet-Dost Brand Iconography, Favicon Suite, and Zero-CLS Splash Screen

> **Date / Timestamp**: 2026-10-18T23:55:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: UI/UX & Clinical Dietetics Domain Lead (Antigravity Agent) & Human Lead Architect  
> **Change Type**: `[PRESENTATION]`, `[BRAND_IDENTITY]`, `[PWA]`, `[UX_ERGONOMICS]`  
> **Affected Subsystems**: WebGateway | Presentation (`wwwroot/assets/`, `wwwroot/partials/`, `wwwroot/index.html`, `wwwroot/styles.css`, `wwwroot/js/main.js`, `manifest.json`)  
> **Associated PR & Stack**: Stack #2 (Branch: `feature/brand-icon-and-loading-screen`, Base: `feature/agentic-ai-standards-alignment`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Change Rationale

* Establishes the authoritative brand identity and visual iconography for Diet-Dost: **"The Clinical DD Pulse & Living Thali"** (Concept 1).
* Replaces the default browser globe/web icon with a comprehensive, crisp, multi-resolution favicon and web app icon suite:
  - Vector Master SVG: [`src/Nutrition.WebGateway/wwwroot/assets/diet-dost-icon.svg`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/assets/diet-dost-icon.svg) (512x512 viewBox)
  - Root Favicons: [`src/Nutrition.WebGateway/wwwroot/favicon.ico`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/favicon.ico) (multi-resolution 16x16, 32x32, 48x48) and [`src/Nutrition.WebGateway/wwwroot/favicon.svg`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/favicon.svg)
  - PWA / Mobile Icons: [`icon-192.png`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/assets/icon-192.png) (192x192), [`icon-512.png`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/assets/icon-512.png) (512x512) with maskable support in `manifest.json`.
  - Browser Pinning: [`favicon-32x32.png`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/assets/favicon-32x32.png) and [`favicon-16x16.png`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/assets/favicon-16x16.png).
* Implements a luxury, zero-Cumulative-Layout-Shift (zero-CLS) brand splash loading screen in [`index.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/index.html) that displays during HTML partial fetching and initial Web BFF composite hydration.
* Elevates UI components across the application ([`header.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/header.html), [`auth-gate.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/auth-gate.html), [`terms.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/terms.html), and [`clinical-health-consent.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/clinical-health-consent.html)) by embedding the SVG brand mark in place of plain monospace text.

---

## 2. Context and Problem Statement

Diet-Dost delivers clinical-grade South Asian nutrition tracking adhering to ICMR-NIN 2024 guidelines and WHO Asian-specific BMI cutoffs. Prior to this decision:
1. **Generic Browser Appearance**: The application lacked custom favicon metadata and icon assets, rendering the browser default globe or blank document icon in modern browser tabs.
2. **Missing Brand Connection**: Header and authentication modals used raw text badges (`DD` inside a generic purple rounded box), lacking emotional resonance with users embarking on weight-loss or metabolic health journeys.
3. **Flash of Unhydrated Elements (FOUC)**: On initial load, modular HTML partials fetched asynchronously over the network, causing micro-layout shifts prior to the Web BFF composite payload arriving and controllers mounting.
4. **PWA Incompleteness**: `manifest.json` referenced placeholder asset dimensions without matching high-fidelity maskable icons.

---

## 3. Decision Drivers

* **Emotional Connection & Habit Formation**: A diet and health tracker requires immediate visual warmth, discipline, and scientific credibility to encourage daily adherence.
* **Cultural Nutrition Metaphor (South Asian Context)**: The visual identity must celebrate Indian culinary wisdom (the circular *Thali* of macro balance and Ayurvedic *Haldi* turmeric warmth) harmonized with modern botanical vitality (organic green leaf) and clinical AI precision (lavender cardiogram vitality wave).
* **Linear Obsidian Aesthetic Alignment**: Adheres strictly to [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) — Obsidian `#010102` deep canvas, `#0f1011` charcoal card surfaces, hairline borders (`#23252a`), and emerald `#10b981` / lavender `#8b5cf6` accents.
* **Performance & Zero CLS**: Instant rendering of inline loading markup with smooth CSS fade-out transition upon controller readiness.

---

## 4. Considered Options

* **Option 1: Generic Fruit / Dumbbell / Scale Icon**: Common clip-art apple or dumbbell.  
  *Rejected*: Generic, lacks South Asian clinical identity, communicates gym bro-science rather than medical-grade clinical dietetics.
* **Option 2: Photo-Based High-Density Raster Logo**: Photorealistic render of Indian food.  
  *Rejected*: Completely illegible at 16x16 / 32x32 browser tab scale, high bandwidth overhead, non-scalable.
* **Option 3 (Chosen): The Clinical DD Pulse & Living Thali Vector Iconography**: Precision geometric SVG with concentric thali halo, interlocking dual "DD" typography, emerald botanical leaf with veins, and lavender vitality pulse. Scalable from 16px to 512px with zero fidelity loss.

---

## 5. Visual Metaphor Breakdown

```
  ┌─────────────────────────────────────────────────────────┐
  │                   DIET-DOST ICON METAPHOR               │
  │                                                         │
  │   [ Outer Squircle: Obsidian #030712 + Hairline #10b981 ] │
  │                                                         │
  │     ( Golden Haldi Thali Halo: 360° Macro Balance )      │
  │           ╭─────────────────────────────────╮           │
  │           │      🌿 Organic Emerald Leaf     │           │
  │           │   (Nourishment & Living Vitality)│           │
  │           │                                 │           │
  │           │   🔤 Interlocking "DD" Monogram  │           │
  │           │     (Diet + Dost Companionship)  │           │
  │           │                                 │           │
  │           │   ⚡ Lavender Cardiogram Wave    │           │
  │           │  (AI Multimodal Perception)     │           │
  │           ╰─────────────────────────────────╯           │
  │                                                         │
  └─────────────────────────────────────────────────────────┘
```

1. **The Interlocking "DD" Monogram**: Two sculpted modern letterforms in emerald gradient (`#10b981` to `#34d399`), symbolizing **Diet** (scientific nutritional intake) and **Dost** (your trusted, empathetic companion).
2. **The Living Emerald Leaf**: Integrated into the upper crown of the first "D", featuring delicate inner veins symbolizing biological vitality and fiber-rich living nutrition.
3. **The Golden Turmeric (Haldi) Thali Halo**: Concentric circular track in golden turmeric (`#f59e0b` to `#fbbf24`), echoing the traditional South Asian meal thali with 3 balance studs representing Proteins, Carbs, and Fats.
4. **The Lavender Vitality Pulse**: A refined cardiogram wave running through the base of the letters in signature Linear lavender (`#8b5cf6` to `#a78bfa`), representing AI perception, metabolic rhythm, and heart health.

---

## 6. Implementation Architecture

### 6.1 Multi-Resolution Asset Matrix

| Asset Path | Format | Resolution | Target Usage |
|---|---|---|---|
| `assets/diet-dost-icon.svg` | SVG | Scalable Vector | Master icon, HTML badges, Splash screen, SVG Favicon |
| `favicon.svg` | SVG | Scalable Vector | Root modern browser tab icon (infinite DPI) |
| `favicon.ico` | ICO | 16x16, 32x32, 48x48 | Legacy browser tab icon fallback |
| `assets/favicon-16x16.png` | PNG | 16x16 px | Low-DPI browser tab icon |
| `assets/favicon-32x32.png` | PNG | 32x32 px | High-DPI browser tab & pinned bookmark |
| `assets/icon-192.png` | PNG | 192x192 px | Android home screen & PWA mobile install |
| `assets/icon-512.png` | PNG | 512x512 px | PWA splash launcher & desktop app install |

### 6.2 Splash Screen Lifecycle & Zero CLS

1. **Inline HTML Execution**: The splash container (`#app-loading-screen`) is declared in `index.html` above all modular partials, styled via `styles.css` with `position: fixed; inset: 0; z-index: 99999; background: #010102`.
2. **Breathing Animation**: The icon gently breathes (`@keyframes splashBreathe`) with an ambient radial emerald/amber glow.
3. **Single-Roundtrip Hydration**: `initApp()` in `main.js` loads modular partials and initiates `apiClient.getWebDashboard('7D')`.
4. **Deterministic Dismissal**: In the `finally` block of `initApp()`, `dismissSplashScreen()` adds `.splash-fade-out` (450ms cubic-bezier transition) and surgically removes the node from the DOM.

---

## 7. Verification & Compliance Evidence

1. **Pre-Commit AI Security Gate (Vector 7)**: Passes 7/7 vectors including living documentation, link integrity, and presentation change-detection rules.
2. **Cross-Browser Favicon Verification**: Verified SVG favicon precedence with automatic ICO fallback.
3. **End-to-End Tier Suite (`tests/validate_e2e_tiers.ps1`)**: 100% pass across all 5 demo user tiers with zero network regressions.
4. **Browser Console & Performance**: 0 console errors, 0 failed asset requests (404s), and zero layout shifts.
