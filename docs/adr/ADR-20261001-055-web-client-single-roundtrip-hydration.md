<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261001-055: Web Client Single-Roundtrip Hydration & Zero CLS Orchestration

> **Date / Timestamp**: 2026-10-01T15:45:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[ARCHITECTURE]`, `[PRESENTATION]`, `[PERFORMANCE]`  
> **Affected Subsystems**: WebGateway | Presentation (`src/Nutrition.WebGateway/wwwroot/js/`) | Evaluation Tests  
> **Associated PR & Stack**: Issue #28 (Branch: `feature/issue-28-feat-web-client-single-roundtrip-hydration`, Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

* Delivers **Phase 1, Layer 3 (PR 3)** of the Web Clean Architecture & Web BFF modernization roadmap.
* Refactors the frontend client architecture (`src/Nutrition.WebGateway/wwwroot/js/`) to hydrate the entire web application in **1 single HTTP roundtrip** (`GET /api/web/v1/dashboard?period=7D`).
* Eliminates 5–6 fragmented, chatty API startup requests (`/api/analytics/daily`, `/api/analytics/projections`, `/api/meals/history`, `/api/progressphotos/comparison`, and `/api/meals/quota`).
* Introduces a modern API client facade (`services/api.js` and `api-client.js`) declaring `getWebDashboard(period = '7D')`.
* Adds synchronous in-memory hydration across `daily-hud.js`, `analytics-chart.js`, `quota-modal.js`, and `progress-modal.js`, achieving **zero Cumulative Layout Shift (CLS)** and instant dashboard rendering.
* Connects the dynamic AI Quota badge in `header.html` displaying the exact daily remaining detection quota per tier (e.g. `1 scan remaining today` for Free tier).

---

## 2. Context and Problem Statement

Following the completion of Phase 1 Layer 1 (Issue #26: Web BFF composite endpoint) and Layer 2 (Issue #27: Shared CQRS queries), the backend composite endpoint was operational, but the web presentation layer was still performing fragmented individual fetch requests on startup:
1. `dailyHud.refresh()` fetched `/api/analytics/daily`.
2. `analyticsChart.refresh()` fetched `/api/analytics/projections` and `/api/meals/history`.
3. `progressModal.refresh()` attempted to fetch `/api/progressphotos/comparison` regardless of user tier.
4. Quota and user entitlements were fetched asynchronously or inferred, causing layout shifts and UI popping.

Per Customer & Functional Acceptance Test (CFT) Suite 1 in [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md), the client must perform exactly 1 single composite request on initial load with zero redundant requests.

---

## 3. Decision Drivers

* **Zero Cumulative Layout Shift (CLS) & Premium Aesthetics**: Instant DOM hydration from a unified composite payload eliminates component pop-in, layout stutter, and secondary loading spinners.
* **Single Network Roundtrip**: Replaces chatty startup traffic with 1 unified `GET /api/web/v1/dashboard` call dispatched concurrently by the backend in <50ms.
* **Forward-Roadmap Reusability (ADR-052 & SDD 09 §1.4)**: The client hydration pattern established here models the startup contract for the upcoming Phase 2 cross-platform mobile client (`MobileBffController`).
* **Clean Architecture & Separation of Concerns (SoC)**: Controllers remain thin view orchestrators; UI controllers (`DailyHudController`, `AnalyticsChartController`, `QuotaModalController`) accept preloaded payloads through synchronous `hydrate(...)` methods.

---

## 4. Considered Options

* **Option 1: Parallel Individual Fetches in `main.js`**: Keep individual REST endpoints and wrap them in client-side `Promise.all`. (Rejected: Suffers from connection limits, redundant HTTP handshakes, EF Core concurrency overhead, and inconsistent state transitions).
* **Option 2 (Chosen): Unified Single-Roundtrip Hydration via Web BFF**: Refactor `api-client.js` and `api.js` to expose `getWebDashboard()`, update `main.js` to hydrate all components synchronously from `WebDashboardCompositeDto`, and deprecate individual startup calls.

---

## 5. Decision Outcome

* **Chosen Option**: Option 2.
* **Implementation Artifacts**:
  1. `src/Nutrition.WebGateway/wwwroot/js/services/api.js`: Created dedicated module facade exporting `ApiClient`, `apiClient`, and `getWebDashboard(period)`.
  2. `src/Nutrition.WebGateway/wwwroot/js/services/api-client.js`: Implemented `getWebDashboard(period = '7D')`.
  3. `src/Nutrition.WebGateway/wwwroot/js/services/analytics-service.js` & `meals-service.js`: Added deprecation documentation for startup hydration.
  4. `src/Nutrition.WebGateway/wwwroot/js/ui/daily-hud.js`: Added `hydrate(ledger)` and updated `refresh(preloadedLedger)` to hydrate with 0 network calls.
  5. `src/Nutrition.WebGateway/wwwroot/js/ui/analytics-chart.js`: Added `renderProjections(data, period)`, `hydrate(projections, recentMeals, period)`, and updated `switchPeriod` to leverage single-roundtrip updates.
  6. `src/Nutrition.WebGateway/wwwroot/js/ui/quota-modal.js`: Added `hydrate(stats)` for preloaded telemetry hydration.
  7. `src/Nutrition.WebGateway/wwwroot/js/ui/progress-modal.js`: Updated `refresh(preloadedFlags)` to gate photo comparison locally without redundant 403 network trips for Free and Basic users.
  8. `src/Nutrition.WebGateway/wwwroot/js/main.js`: Bootstraps via `getWebDashboard`, orchestrates `hydrateFromDashboard`, and binds `ai-quota-badge` dynamically.
  9. `src/Nutrition.WebGateway/wwwroot/partials/header.html`: Added `#ai-quota-badge` inside the header user menu button.
  10. `tests/Nutrition.EvalHarness.Tests/WebBffCompositeTests.cs`: Added `WebClientHydrationAssets_VerifyIntegrityAndZeroClsContracts` and sub-50ms execution verification.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Cross-Platform Mobile Client**: The client hydration lifecycle directly mirrors the `MobileClientStartupService` in .NET MAUI / Expo, enabling 1-to-1 parity when hydrating the mobile touch HUD from `/api/mobile/v1/dashboard`.
* **Zero Disposable Code**: The modular methods (`hydrate`, `renderProjections`) preserve existing interactive capabilities (e.g. chart bar click date filtering, view toggles) without code waste.

---

## 7. Verification & Acceptance Checklist

* [x] **CFT Test Suite 1 Verification**: Single composite request to `GET /api/web/v1/dashboard?period=7D` hydrates user, HUD, chart, meals, quota, and feature flags.
* [x] **Zero CLS**: Synchronous hydration from preloaded payload eliminates layout jumping.
* [x] **AI Quota Badge Display**: Header badge displays remaining detections (e.g. `1 scan remaining today` on Free tier).
* [x] **All Tests Passing**: 148 test scenarios (36 Domain + 112 EvalHarness) passing with 0 warnings.
