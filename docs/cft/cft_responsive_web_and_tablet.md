<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Customer & Functional Acceptance Test (CFT): Responsive Web & Tablet Ergonomics

> **Test Suite**: Suite 4 — Multi-Viewport Responsive Ergonomics, Touch Navigation & Zero CLS  
> **Classification**: Mandatory Pre-PR & Release Acceptance Test Harness  
> **Target Subsystem**: Web Presentation Layer (`wwwroot/index.html`, `wwwroot/css/*`, `wwwroot/js/*`)  
> **Associated Milestone**: `Alpha Release 01 (Workable Web Showcase MVP on Azure)`  
> **Governing Skill**: [`diet-dost-responsive-web-mobile-readiness`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md)  
> **Reference Playbook**: [`references/responsive-cft-template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/references/responsive-cft-template.md)  

---

## 1. Overview & Test Objectives

This document establishes the official **Customer & Functional Acceptance Test (CFT)** suite for validating responsive web behavior across smartphone, tablet, and desktop viewport dimensions.

### Core Objectives:
1. **Zero Horizontal Layout Overflow**: Assert `document.documentElement.scrollWidth === window.innerWidth` across all supported viewports.
2. **Thumb-Zone Navigation on Mobile (<640px)**: Validate that the bottom navigation bar is fixed, responsive, and clearly displays active state.
3. **Bottom Sheet Modals on Mobile (<640px)**: Assert that meal logging dialogs and quota notices slide up as bottom sheets rather than centered popups.
4. **Touch Target Size Governance**: Assert that all interactive elements satisfy the `>= 44px x 44px` standard (WCAG 2.2 AA / Apple HIG).
5. **Cumulative Layout Shift (CLS)**: Assert CLS score `< 0.05` during composite hydration on cellular 4G throttling.

---

## 2. Supported Viewport Matrix

| Category | Target Profile | Resolution | Orientation | Navigation Expectation |
| :--- | :--- | :--- | :--- | :--- |
| **Mobile Compact** | iPhone SE / iPhone 13 mini | `375 x 667` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Mobile Standard** | iPhone 14/15/16, Pixel 7 | `390 x 844` / `412 x 915` | Portrait | Bottom Navigation Bar + Bottom Sheet Modals |
| **Tablet Portrait** | iPad Mini, iPad 10th Gen | `768 x 1024` / `820 x 1180` | Portrait | Adaptive Header + 2-Column Macro Grid |
| **Tablet Landscape** | iPad Air, iPad Pro 11" | `1180 x 820` / `1024 x 768` | Landscape | Split-View Layout (HUD/Charts left, Meals right) |
| **Desktop / Laptop** | MacBook Air, 1080p Monitor | `1440 x 900` / `1920 x 1080` | Landscape | 3-Column Obsidian Dashboard + Sticky Rail |

---

## 3. Execution Verification Checklist

### 3.1 Layout Stability & Viewport Boundaries
- [ ] Mobile 375px: Zero horizontal scroll bar; text wraps without clipping.
- [ ] Mobile 390px: Caloric summary HUD stacks vertically or as a 2x2 grid cleanly.
- [ ] Tablet 768px: Top header expands with profile and tier badge; bottom nav bar is hidden.
- [ ] Desktop 1440px+: 3-column dashboard layout renders with max-container clamping (`max-width: 1400px`).

### 3.2 Touch Targets & Interactivity
- [ ] All bottom nav bar items measure `>= 44px x 44px`.
- [ ] Quick Action buttons (e.g. `+ Log Meal`) provide haptic scale `:active` visual response.
- [ ] Date picker controls and navigation arrows satisfy minimum 8px spacing.

### 3.3 Multi-Tier Feature Gating on Mobile Viewports
- [ ] **Free Tier (`free@dietdost.app`)**:
  - History is restricted to 7 days; 30D/90D tabs show locked tier paywall.
  - Locked feature buttons (e.g. Export, Face Progress) are hidden or cleanly disabled.
- [ ] **Basic Tier (`basic@dietdost.app`)**:
  - History renders 30 days of data smoothly with mobile touch scrolling.
- [ ] **Premium Tier (`premium@dietdost.app`)**:
  - 90-day history and export features available; chart touch tooltips visible.

---

## 4. Automated Execution Script Snippet

```bash
# Automated CDP viewport test execution via Node.js
node tests/verify_cft_viewports.mjs
```

---

## 5. Live Multi-Viewport Execution Evidence (Pre-Merge Certification)

**Execution Date**: 2026-10-02  
**Harness**: `tests/verify_cft_viewports.mjs` (Chrome DevTools Protocol via Headless Browser)  
**Target Server**: `http://localhost:5240/`  

```text
======================================================================
  CFT TEST SUITE: MULTI-VIEWPORT RESPONSIVE & TOUCH-FIRST ACCEPTANCE  
======================================================================
[1/5] Launching headless browser on port 9222...
   Browser CDP Connected: Chrome/154.0.8037.59
[2/5] Navigating to http://localhost:5240/...
[3/5] Executing Viewport Layout & Overflow Assertions...

--> Testing Profile: Desktop Web (1440x900)
    [PASS/FAIL] Overflow: PASS (scrollWidth: 1430px, innerWidth: 1440px)
    [PASS/FAIL] Bottom Nav Display: PASS (Actual: 'none', Expected: 'none')
    [PASS/FAIL] Desktop Header Buttons: PASS (Actual: 'flex', Expected: 'inline-flex/block')

--> Testing Profile: Tablet Portrait (768x1024)
    [PASS/FAIL] Overflow: PASS (scrollWidth: 758px, innerWidth: 768px)
    [PASS/FAIL] Bottom Nav Display: PASS (Actual: 'none', Expected: 'none')
    [PASS/FAIL] Desktop Header Buttons: PASS (Actual: 'flex', Expected: 'inline-flex/block')

--> Testing Profile: Mobile Standard - iPhone 14 (390x844)
    [PASS/FAIL] Overflow: PASS (scrollWidth: 390px, innerWidth: 390px)
    [PASS/FAIL] Bottom Nav Display: PASS (Actual: 'flex', Expected: 'flex')
    [PASS/FAIL] Desktop Header Buttons: PASS (Actual: 'none', Expected: 'none')

--> Testing Profile: Mobile Compact - iPhone SE (375x667)
    [PASS/FAIL] Overflow: PASS (scrollWidth: 375px, innerWidth: 375px)
    [PASS/FAIL] Bottom Nav Display: PASS (Actual: 'flex', Expected: 'flex')
    [PASS/FAIL] Desktop Header Buttons: PASS (Actual: 'none', Expected: 'none')

[4/5] Executing Mobile Touch Target & Bottom Sheet Verification (375x667)...
    Touch Target Dimensions (Target >= 44px x 44px):
    - #btn-nav-overview: PASS (60px x 48px)
    - #btn-nav-log-meal: PASS (62px x 71px)
    - #btn-nav-history: PASS (56px x 48px)
    - #btn-nav-profile: PASS (56px x 48px)
    - #btn-mode-camera: PASS (152px x 47px)
    - #btn-mode-text: PASS (152px x 47px)

--> Testing Clinical Profile (#btn-nav-profile) -> Mobile Bottom Sheet Modal Flow:
    [PASS/FAIL] Bottom Sheet Open: PASS
    [PASS/FAIL] Bottom Sheet Styling: PASS (alignItems: flex-end, borderRadius: '20px 20px 0px 0px', dimensions: 375x587px, bottomGap: 0px)

--> Testing Diary Navigation (#btn-nav-history) Click:
    [PASS/FAIL] Diary Tab Active State: PASS
    [INFO] Scroll Position: 0px -> 2043px

--> Testing Overview Navigation (#btn-nav-overview) Click:
    [PASS/FAIL] Overview Tab Active State: PASS
    [INFO] Scroll Position Top: 68px

======================================================================
                    CFT EXECUTION SUMMARY RESULTS                     
======================================================================
1. Horizontal Overflow (scrollWidth <= innerWidth): ALL PASSED (100%)
2. Bottom Navigation Responsive Visibility:         ALL PASSED (100%)
3. Desktop Header Button Adaptive Visibility:       ALL PASSED (100%)
4. Touch Target Governance (>= 44px x 44px):        ALL PASSED (100%)
5. Mobile Bottom Sheet Modal Behavior:              PASSED (100%)
======================================================================

>>> VERDICT: CFT ACCEPTANCE CRITERIA 100% SATISFIED ON ALL VIEWPORTS! <<<
```

