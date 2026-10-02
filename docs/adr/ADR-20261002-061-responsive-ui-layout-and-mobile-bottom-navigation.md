<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-061: Responsive UI Layout, Touch Ergonomics & Mobile Bottom Navigation

> **Date / Timestamp**: 2026-10-02T17:05:00+05:30  
> **Status**: `ACCEPTED`  
> **Driver / Agent / Deciders**: Product Owner & Antigravity AI Assistant  
> **Change Type**: `[PRESENTATION]` &bull; `[ARCHITECTURE]` &bull; `[UX_ERGONOMICS]`  
> **Affected Subsystems**: `WebGateway (wwwroot/index.html, wwwroot/styles.css, wwwroot/js/main.js, partials/*)`  
> **Associated Issue & PR**: Issue #40 (Layer 6 of 9, Milestone Alpha Release 01)  
> **Governing Skills**: [`diet-dost-responsive-web-mobile-readiness`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md), [`diet-dost-clean-architecture`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-clean-architecture/SKILL.md)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_responsive_web_and_tablet.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_responsive_web_and_tablet.md)  

---

## 1. Executive Summary & Change Rationale

* **Objective**: Transform the desktop-first Web presentation into an adaptive, touch-first responsive application across smartphones (`<640px`), tablets (`640px-1023px`), and desktops (`>=1024px`) before the live Azure Container Apps deployment (PR 9 / Issue #31).
* **Thumb-Zone Bottom Navigation**: Introduced a fixed bottom navigation bar on mobile viewports (`<640px`) with 4 primary touch destinations (`Overview`, `Log Meal [FAB]`, `Diary`, `Clinical`), providing one-handed ergonomic reach while keeping the header slim and uncluttered.
* **Bottom Sheet Modals for Phones**: Replaced centered desktop popups on screens `<640px` with native-feeling slide-up bottom sheets with rounded top corners, swipe handles, and safe touch areas.
* **Touch Target Standard (WCAG 2.2 / Apple HIG >= 44x44px)**: Guaranteed minimum `44px x 44px` clickable dimensions on all buttons, chips, tabs, and navigation items.

---

## 2. Context & Problem Statement

Prior to this deliverable, Diet-Dost had minimal, ad-hoc media queries, resulting in horizontal scrolling (`scrollWidth` ~680px on 375px screens) caused by wide desktop header button strips, unwrapped calorie summary boxes, and rigid companion cards. In customer validation, >70% of evaluators and dietitians test showcase links on mobile devices. Shipping desktop-only layouts to Azure Container Apps would have created an unacceptable first impression.

---

## 3. Decision Drivers & Architecture Invariants

1. **Zero Horizontal Layout Shift & Overflow**: `document.documentElement.scrollWidth === window.innerWidth` across all viewports.
2. **Zero-Throwaway Code**: CSS Grid tokens, bottom nav markup, and bottom sheet dialogs must be 100% reusable inside PWA mode and native webviews.
3. **No Breaking Changes to Backend APIs**: All responsive viewports consume the exact same single-roundtrip Web BFF composite endpoint (`GET /api/web/v1/dashboard`).

---

## 4. Considered Options

* **Option 1: Hamburger Menu Drawer**: Standard mobile hamburger drawer in header.  
  *Rejected*: Hides primary actions behind 2-3 clicks and requires two-handed thumb reach to top corners.
* **Option 2: Responsive CSS Grid + Fixed Bottom Navigation Bar (Chosen)**:  
  *Approved*: Places primary actions directly in the natural thumb zone on mobile, while automatically collapsing on tablets and desktops in favor of top navigation.

---

## 5. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 1 Layer 9 (Azure Deployment Gate / Issue #31)**: The container image deployed to Azure Container Apps bundles the responsive frontend, ensuring `https://app.dietdost.com` looks and behaves like a native app on mobile phones from minute one.
* **Phase 2 (Mobile MVP / Issues #43–#46)**: Mobile UI mockups and client-side webviews inherit this exact CSS token design system without re-engineering layouts.

---

## 6. Verification Results

* **Multi-Tier E2E CFT**: 100% pass rate across all 5 demo user tiers via `tests/validate_e2e_tiers.ps1`.
* **Automated Unit & Eval Tests**: 151 passed, 0 failed, 0 warnings.
* **Browser Verification**: Tested on iPhone SE (375px), iPhone 14 (390px), and iPad (768px). Confirmed zero horizontal overflow, bottom navigation bar visibility on mobile, and smooth bottom sheet slide-up animation.
