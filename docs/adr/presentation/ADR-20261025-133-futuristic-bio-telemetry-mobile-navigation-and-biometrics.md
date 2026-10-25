<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261025-133: Futuristic Bio-Telemetry Mobile Bottom Navigation Dock and Biometric Face & Body Composition Visualizations

> **Date / Timestamp**: 2026-10-25T19:15:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: UI/UX & Clinical Dietetics Domain Lead (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[MOBILE_UX]`, `[DESIGN_SYSTEM]`, `[BIOMETRIC_VISUALIZATION]`  
> **Affected Subsystems**: WebGateway | Presentation (`wwwroot/partials/bottom-nav.html`, `wwwroot/index.html`, `wwwroot/styles.css`, `wwwroot/js/main.js`, `wwwroot/uploads/progress/*.svg`, `wwwroot/assets/placeholder-progress.svg`, `wwwroot/partials/face-progress-card.html`, `wwwroot/partials/progress-modal.html`)  
> **Associated PR & Stack**: Branch `feature/futuristic-mobile-navigation-and-biometrics` (Base: `origin/main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md), [`docs/cft/cft_mobile_mvp_cross_platform.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_mobile_mvp_cross_platform.md)  

---

## 1. Executive Summary & Change Rationale

* **Problem Solved**: Replaces primitive emoji buttons (`📊` Overview, `➕` Log Meal, `📅` Diary, `⚙️` Clinical) and outdated cartoon face/stick-figure progress assets with a cohesive, futuristic bio-telemetry visual design system matching the repository's Linear Obsidian surface ladder.
* **Futuristic Mobile Navigation Dock**:
  - **Pulse** (`#btn-nav-overview`): Metabolic EKG waveform with baseline telemetry glow (`#5e6ad2`) and live activity pulse dot (`#10b981`).
  - **AI Snap** (`#btn-nav-log-meal`): Elevated Neural Aperture Capsule with 4-point AI sparkle star, optical lens iris, and subtle `@keyframes aiFabPulse` radial aura.
  - **Timeline** (`#btn-nav-history`): Circadian chrononutrition clock dial with 3 meal checkpoint nodes (Breakfast, Lunch, Dinner) representing nutritional meal timing.
  - **Bio-Profile** (`#btn-nav-profile`): Precision DNA double-helix integrated within a clinical shield (`#06b6d4`), signifying deep biomarker intelligence.
* **Biometric Face Slimming & DEXA 3D Body Composition Scans**:
  - **Face Slimming Telemetry** (`face_baseline.svg`, `face_current.svg`): Replaced cartoon faces with high-tech cyan vector wireframe contours, anatomical landmark scan reticles, jawline angle telemetry (132° $\rightarrow$ 124°), and hydration/water-retention status badges.
  - **DEXA 3D Body Composition** (`body_baseline.svg`, `body_current.svg`): Replaced mannequin outlines with medical-grade holographic anatomical silhouettes showing visceral fat density heatmaps, waist caliper tape (38.0" $\rightarrow$ 35.5"), and lean mass preservation indicators.
  - **Biometric Scanning Reticle Placeholder** (`placeholder-progress.svg`): Replaced static emoji avatar with an illuminated HUD biometric alignment reticle.
* **Tactile Haptic Feedback**: Integrated subtle mobile haptic vibrations (`navigator.vibrate(10)`) on navigation tab interactions.
* **100% Contract & Backward Compatibility**: Maintained all existing DOM IDs, event bus subscriptions, and routing selectors, guaranteeing zero regression across mobile and desktop test harnesses.

---

## 2. Context and Problem Statement

1. **Aesthetic Incongruity**: The mobile bottom navigation previously utilized standard OS emojis (`📊`, `➕`, `📅`, `⚙️`). Emojis render inconsistently across Android, iOS, and desktop browsers, clashing with the high-end Linear.app Obsidian dark surface aesthetic.
2. **De-motivating Progress Placeholders**: Transformation photos and baseline placeholders featured cartoon drawings and stick mannequins, diminishing the clinical credibility of the AI-powered health platform.
3. **Ergonomic Elevation**: The primary action button (Meal Logging) lacked prominent visual hierarchy and depth required for thumb-zone micro-interactions.

---

## 3. Decision Drivers & Design Principles

* **DESIGN.md Compliance**: Canvas `#08090a` / `#010102`, Linear Lavender accent `#5e6ad2`, cyan bio-telemetry `#06b6d4`, and emerald metabolic confirmation `#10b981`.
* **Glassmorphic Obsidian Dock**: Bottom navigation styled with `backdrop-filter: blur(24px)`, specular top hairline border `rgba(255, 255, 255, 0.08)`, and floating capsule pill for active tabs.
* **Touch Target Governance**: All navigation touch targets exceed 48x48px (actual: 58x50px to 81x50px, AI Snap FAB: 62x76px).
* **Zero Layout Shift (CLS = 0)**: Precise inline SVG dimensions (`viewBox="0 0 24 24"`) eliminate dynamic layout recalculations.

---

## 4. Verification and Validation Evidence

* **Multi-Tier E2E CFT Suite (`validate_e2e_tiers.ps1`)**: 100% PASS across Free, Basic, Premium, Admin, and SuperAdmin tiers.
* **Mobile Core Flows CFT (`verify_mobile_cft_core_flows.mjs`)**: 8/8 (100%) test cases passed including authenticated hydration, modal stacking, and meal confirmation.
* **Design System Audit (`audit_design_system_compliance.mjs`)**: 100% COMPLIANT across all surface ladders and viewports.
* **Responsive Viewport Acceptance (`verify_cft_viewports.mjs`)**: 100% PASS on Desktop (1440x900), Tablet (768x1024), iPhone 14 (390x844), and iPhone SE (375x667).
* **Solution Test Suite (`dotnet test`)**: 327/327 tests passed (0 failures, 0 warnings).
