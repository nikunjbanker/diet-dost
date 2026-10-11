<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-057: Centralize Food Estimation API & Eliminate Static Client Dictionary

> **Date / Timestamp**: 2026-10-02T11:45:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[ARCHITECTURE]`, `[CLEAN_CODE]`, `[PRESENTATION]`, `[PERFORMANCE]`  
> **Affected Subsystems**: WebGateway | Application (`Features/Meals/Queries/EstimateFoodItem/`) | Presentation (`wwwroot/js/ui/review-modal.js`) | Eval Tests  
> **Associated PR & Stack**: Issue #29 (Branch: `feature/issue-29-centralize-food-estimation`, Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

* Delivers **Phase 1, Layer 4 (PR 4)** of the Web Clean Architecture & Web BFF modernization roadmap per [Issue #29](https://github.com/nikunjbanker/diet-dost/issues/29).
* Completely excises the hardcoded, 1,008-line duplicate static food dictionary (`src/Nutrition.WebGateway/wwwroot/js/services/nutrition-estimator.js`), reducing client bundle overhead by ~31.5 KB.
* Wires the meal review modal (`src/Nutrition.WebGateway/wwwroot/js/ui/review-modal.js`) to dispatch debounced (300ms) calls to the centralized backend endpoint `POST /api/meals/estimate` (and `POST /api/meals/estimate-item`).
* Leverages the authoritative backend C# clinical calculation engine ([`Nutrition.Domain.Clinical.IndianFoodEstimator`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Domain/Clinical/IndianFoodEstimator.cs) and `EstimateFoodItemQueryHandler`), enforcing the Zero-Assumption ICMR-NIN 2024 and IFCT 2017 rules with 0 duplicated client-side math.

---

## 2. Context and Problem Statement

Previously, the Web frontend maintained a separate 1,008-line JavaScript file containing a hardcoded food dictionary (`INDIAN_FOOD_DICTIONARY`) and client-side scaling mathematics (`estimateIndianFoodNutrition`, `scaleNutritionByPortion`, `generateDietitianAdvice`).

This anti-pattern caused:
1. **Domain Code Duplication**: Clinical nutrition formulas were maintained in both C# (`Nutrition.Domain.Clinical`) and JavaScript (`nutrition-estimator.js`).
2. **Calculation Drift Risk**: Updating nutritional algorithms or ICMR-NIN 2024 reference values in the backend did not reflect in client estimation without manually modifying the client dictionary.
3. **Bundle Bloat**: Loaded over 31 KB of static text into the client browser before user interaction.

---

## 3. Decision Drivers

* **Zero-Duplicate Domain Code Guarantee**: Clinical food estimation must be centralized in the backend Application and Domain layers.
* **Single Source of Truth (SSOT)**: Ensure ICMR-NIN 2024 and IFCT 2017 reference standards are evaluated identically for Web, Mobile, and API consumers.
* **Responsive UX with 300ms Debounce**: User typing and portion adjustments trigger debounced backend requests without freezing the UI or overwhelming the server.
* **Forward-Roadmap Reusability (ADR-052 & SDD 09 §1.4)**: The exact same `POST /api/meals/estimate` endpoint is immediately ready for Phase 2 cross-platform mobile clients (`MobileBffController`).

---

## 4. Considered Options

* **Option 1: Keep Client Dictionary as Offline Cache**: Retain `nutrition-estimator.js` as a local fallback for offline web use. (Rejected: Web MVP is online-connected via Web BFF; mobile offline caching will be managed cleanly via SQLite in Phase 2).
* **Option 2 (Chosen): Full Centralization via `POST /api/meals/estimate`**: Delete `nutrition-estimator.js`, route all dish name and portion adjustment inquiries to `MealsService.estimateFoodItem`, and enforce 300ms client debouncing.

---

## 5. Decision Outcome

* **Client Cleanup**: Deleted `src/Nutrition.WebGateway/wwwroot/js/services/nutrition-estimator.js` (-1,008 lines of code).
* **Backend Endpoint Alias**: Added `[HttpPost("estimate")]` alongside `[HttpPost("estimate-item")]` in `MealsController.cs`.
* **API Facade**: Exposed `estimateFoodItem` in `services/api.js` and `services/meals-service.js`.
* **Presentation**: Updated `review-modal.js` with `_debounce(key, fn, 300)` for portion and name editing.
* **Automated Tests**: Added 3 new unit tests in `Nutrition.EvalHarness.Tests/SharedCqrsQueryTests.cs` validating `EstimateFoodItemQuery`.
* **Verification**: All 151 unit/eval tests pass; live 5-tier CFT passed 100%; browser subagent verified 0 script errors and 0 console warnings.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 (Mobile MVP)**: When mobile users type custom dish names or adjust portions in .NET MAUI / Android / iOS, the mobile client will call `POST /api/meals/estimate`, ensuring 100% calculation and dietitian advice consistency with zero duplicated code.
* **Phase 3 (Enterprise Persistence)**: No database migration required; queries operate purely in memory against clinical models and cached AI profiles.
