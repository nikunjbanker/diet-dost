<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-058: Deconstruct Monolithic UI Controller into Single Responsibility (SRP) ES Modules

> **Date / Timestamp**: 2026-10-02T12:20:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[ARCHITECTURE]`, `[CLEAN_CODE]`, `[PRESENTATION]`, `[REFACTOR]`  
> **Affected Subsystems**: Presentation (`wwwroot/js/ui/analytics-chart.js`, `wwwroot/js/ui/ChartRenderer.js`, `wwwroot/js/ui/MealDiaryView.js`, `wwwroot/js/services/ExcelExportService.js`, `wwwroot/js/main.js`) | Eval Tests  
> **Associated PR & Stack**: Issue #30 (Branch: `feature/issue-30-modularize-analytics-chart`, Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

* Delivers **Phase 1, Layer 5 (PR 5)** of the Web Clean Architecture & Web BFF modernization roadmap per [Issue #30](https://github.com/nikunjbanker/diet-dost/issues/30).
* Deconstructs the monolithic 894-line `analytics-chart.js` into focused, Single Responsibility Principle (SRP) ES modules:
  1. `ChartRenderer.js`: Pure SVG/CSS coordinate math, bar rendering, tooltips, timeline state management (`1D`, `7D`, `30D`, `90D`, `365D`), and bar selection click handling.
  2. `MealDiaryView.js`: Responsible strictly for rendering meal diary cards (Obsidian Dark cards) and high-density responsive data grid, view switching (`cards` vs `grid`), empty state handling, and Obsidian Dark delete confirmation dialog.
  3. `ExcelExportService.js`: Pure data serialization handling CSV/XLSX RFC 4180 export with UTF-8 BOM, tier entitlement checks, and paywall notifications.
* Retains `AnalyticsChartController` as a slim coordinating facade (< 250 lines) preserving 100% backward compatibility for DI registration, EventBus subscriptions, and Web BFF composite hydration.

---

## 2. Context and Problem Statement

Previously, `analytics-chart.js` was an 894-line monolithic controller violating the Single Responsibility Principle:
1. **Tight Coupling**: Mixed SVG math, DOM card templates, HTML table construction, delete dialog event listeners, and CSV file serialization in a single file.
2. **Maintenance Overhead**: Modifying chart aesthetics or timeline scales risked breaking meal diary filtering or file export logic.
3. **Reusability Blockers**: Neither chart rendering nor file export could be reused or tested in isolation for PWA offline caching or future hybrid mobile shells.

---

## 3. Decision Drivers

* **Single Responsibility Principle (SRP)**: Each ES module must have one and only one reason to change.
* **Preserve 100% Visual and Behavioral Fidelity**: Zero visual regression on the Hero Caloric HUD, SVG chart rendering, Card/Grid layouts, and Obsidian Dark delete dialog.
* **Tier Gating Integrity**: Maintain defense-in-depth where Excel export is unlocked for Premium/SuperAdmin and blocked with paywall notifications for Free/Basic tiers.
* **Forward-Roadmap Reusability (ADR-052 & SDD 09 §1.4)**: Standalone `ChartRenderer` and `ExcelExportService` can be cleanly packaged or adapted for Phase 2 cross-platform mobile hybrid views and PWA shells.

---

## 4. Considered Options

* **Option 1: Complete Rewrite & Renaming**: Discard `analytics-chart.js` and register each module directly in `main.js` with individual event bus bindings. (Rejected: Would break existing facade callers such as `window.switchAnalyticsPeriod` and composite hydration in `main.js`).
* **Option 2 (Chosen): Facade Decomposition**: Extract `ChartRenderer`, `MealDiaryView`, and `ExcelExportService` as standalone ES modules, and refactor `AnalyticsChartController` into a clean coordinating facade.

---

## 5. Decision Outcome

* **Created `ChartRenderer.js`** (`src/Nutrition.WebGateway/wwwroot/js/ui/ChartRenderer.js`).
* **Created `MealDiaryView.js`** (`src/Nutrition.WebGateway/wwwroot/js/ui/MealDiaryView.js`).
* **Created `ExcelExportService.js`** (`src/Nutrition.WebGateway/wwwroot/js/services/ExcelExportService.js`).
* **Refactored `analytics-chart.js`** (`src/Nutrition.WebGateway/wwwroot/js/ui/analytics-chart.js`): Reduced from 894 lines to 244 lines of clean orchestration.
* **Registered `ExcelExportService` in DI** (`src/Nutrition.WebGateway/wwwroot/js/main.js`).
* **Verified Test Suite 3 in CFT** ([`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)).
* **Validation**: 151/151 tests passed, 0 build warnings/errors on .NET 11, 100% pass across all 5 demo user tiers on live WebGateway port 5240.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Mobile**: `ExcelExportService` logic provides the template for Mobile CSV/PDF export in .NET MAUI / hybrid shell without needing Web DOM bindings.
* **PWA Offline Mode**: Modular `ChartRenderer` and `MealDiaryView` can be cached independently via service worker with minimal footprint.
