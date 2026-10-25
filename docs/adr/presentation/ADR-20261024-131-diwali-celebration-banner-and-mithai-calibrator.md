<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-131: Interactive Diwali (Deepavali) Celebration Banner, Panch-Deep Diya Lighting, Smart Mithai Calibrator, and Gujarati New Year (Bestu Varas) Integration

> **Date / Timestamp**: 2026-10-24T23:58:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: UI/UX & Clinical Dietetics Domain Lead (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[FESTIVAL_CELEBRATION]`, `[INTERACTIVE_UX]`, `[CULTURAL_HERITAGE]`  
> **Affected Subsystems**: WebGateway | Presentation (`wwwroot/partials/diwali-celebration-banner.html`, `wwwroot/index.html`, `wwwroot/styles.css`, `wwwroot/js/main.js`, `tests/Nutrition.EvalHarness.Tests/DiwaliBannerContractTests.cs`)  
> **Associated PR & Stack**: Stack #3 (Branch: `feature/diwali-celebration-banner`, Base: `origin/main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Change Rationale

* Seamlessly transitions the top banner experience from the concluded Dussehra launch milestone to a culturally vibrant, clinical-first, and highly innovative **Diwali (Deepavali) Festival Celebration Banner & Smart Mithai Calibrator** ([`src/Nutrition.WebGateway/wwwroot/partials/diwali-celebration-banner.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/diwali-celebration-banner.html)).
* Introduces the **Panch-Deep 5-Day Diya Lighting System**:
  - 5 interactive lamps representing Dhanteras, Naraka Chaturdashi (Kali Chaudas), Deepavali Lakshmi Puja, Bestu Varas (Gujarati New Year / Nutan Varsh), and Bhai Dooj.
  - Interactive tap-to-light state machine with realistic `@keyframes diyaFlicker` CSS flame animation, clinical wisdom toasts, golden particle celebrations, and `localStorage` persistence under `diet_dost_diwali_diyas_lit`.
  - Igniting all 5 lamps unlocks the grand **Arogya Jyoti Activated** celebration with festive multi-burst confetti.
* Introduces the **Smart Diwali Mithai & Farsan Calibrator** (Slide 1):
  - Interactive chips for traditional delicacies: *Kaju Katli* (~55 kcal), *Besan Laddoo* (~180 kcal), *Crispy Chakli* (~150 kcal), and *Ghughra / Mathiya* (~140 kcal).
  - Dynamic portion guidance showing compensatory activity (e.g. 15-minute celebratory post-puja family walk) and direct shortcuts for AI camera scanning or sweet intake logging.
* Introduces the **Gujarati New Year (Bestu Varas / Saal Mubarak) Slide** (Slide 2):
  - Celebrates Vikram Samvat Nutan Varsh with authentic Gujarati festive warmth ("સાલ મુબારક"), Annakut mindful eating guidance, and a golden New Year Health Pledge button.
* Re-affirms the **Founder's Diwali Dedication** (Slide 3):
  - Warm festive greetings from creator **Nikunj Banker**, dedicated to health, vitality, and happy diet food.
* Retains the **Collapsed Sleek Ticker Bar** mode with `localStorage` persistence (`diet_dost_diwali_minimized`) and instant expand toggle.

---

## 2. Context and Problem Statement

1. **Dussehra Milestone Expiry**: The Vijayadashami launch milestone concluded, and having a Dussehra-themed banner displayed during the festive Diwali season created temporal disorientation.
2. **Festive Calorie Anxiety**: Diwali is celebrated with sweet delicacies and savory farsan. Users often experience guilt or anxiety over festive foods, leading to restrictive crash diets or abandonment of healthy routines.
3. **Desire for Greater Interactivity**: A static or basic carousel does not engage users. Introducing interactive lamp lighting and real-time delicacy calibration provides delight, cultural pride, and practical portion intelligence.

---

## 3. Decision Drivers

* **DESIGN.md Compliance**: Adheres to the Obsidian canvas `#0b0d13` to `#171410`, 1px hairline amber borders `rgba(245, 158, 11, 0.35)`, and warm golden gradients (`#fbbf24`, `#f59e0b`, `#ea580c`).
* **Zero Layout Shift (CLS = 0)**: Fixed height viewports and CSS transforms prevent jumping or shifts during slide transitions and Diya lighting.
* **Positive, Guilt-Free Framing**: Empowers users to enjoy traditional sweets mindfully through compensatory balance rather than restrictive deprivation.
* **Gujarati Cultural Warmth**: Honors creator Nikunj Banker's cultural heritage with authentic Bestu Varas / Saal Mubarak celebrations.

---

## 4. Implementation Details

1. **HTML Partial**: Created [`src/Nutrition.WebGateway/wwwroot/partials/diwali-celebration-banner.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/partials/diwali-celebration-banner.html) and deleted legacy `partials/dussehra-launch-banner.html`.
2. **Dashboard Inclusion**: Updated `data-include="partials/diwali-celebration-banner.html"` in [`src/Nutrition.WebGateway/wwwroot/index.html`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/index.html).
3. **Controller Logic**: Implemented `setupDiwaliBanner()` in [`src/Nutrition.WebGateway/wwwroot/js/main.js`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/js/main.js) with 5-day diya lighting state machine, mithai selector, and sankalp pledge handler.
4. **CSS Token Extensions**: Appended rules for `.diwali-banner-card`, `@keyframes diyaFlicker`, `.diwali-diya-btn`, and `.mithai-chip` to [`src/Nutrition.WebGateway/wwwroot/styles.css`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/wwwroot/styles.css).
5. **Contract Test Suite**: Authored [`tests/Nutrition.EvalHarness.Tests/DiwaliBannerContractTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/DiwaliBannerContractTests.cs) verifying DOM landmarks, accessibility ARIA roles, and obsolete partial removal.

---

## 5. Consequences & Verification

* **Positive Consequences**:
  - Immersive, joyful festive experience connecting Indian cultural celebration with medical-grade ICMR-NIN 2024 guidance.
  - Interactive Diya lighting creates memorable gamified delight.
  - Smart Mithai Calibrator provides actionable, guilt-free portion tips.
  - 100% test pass rate across all 298 tests in solution.
* **Verification**:
  - `DiwaliBannerContractTests`: 4 passed (100%).
  - Full test suite: 298 passed across all assemblies with 0 errors and 0 warnings.
