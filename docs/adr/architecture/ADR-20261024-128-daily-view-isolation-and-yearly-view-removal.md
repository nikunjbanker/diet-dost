<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-128: Web BFF Daily View Isolation to Today's Records, Retirement of Yearly (365D) Analytics View, and Contract Test Segregation

> **Date / Timestamp**: 2026-10-24T20:25:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[ARCHITECTURE]`, `[PRESENTATION]`, `[CQRS]`  
> **Affected Subsystems**: WebGateway (`Controllers/Web/WebBffController.cs`, `wwwroot/partials/analytics-card.html`, `wwwroot/js/ui/analytics-chart.js`, `wwwroot/js/ui/MealDiaryView.js`, `wwwroot/js/ui/ChartRenderer.js`, `wwwroot/js/services/analytics-service.js`, `wwwroot/index.html`), Application (`Services/ClinicalDietitianService.cs`, `Features/Analytics/Queries/GetHistoricalAnalytics/GetHistoricalAnalyticsQuery.cs`, `Features/Analytics/Queries/ExportAnalytics/ExportAnalyticsDataQuery.cs`), Tests (`Nutrition.EvalHarness.Tests`)  
> **Associated PR & Branch**: `fix/daily-view-isolation-today-only` (Tracking Issue #93)  
> **Relevant SDDs & Rules**: [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md), [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Rationale

This architectural decision resolves a critical temporal scoping defect in the Web BFF composite and client diary, while retiring the overly broad Yearly (`365D`) view to optimize client-server payload consolidation:
1. **Daily View Isolation to Today's Meals Only (`1D`)**:
   - Resolved the root cause where `WebBffController.cs` sanitized `period` using a switch expression lacking `"1D"`, coercing daily requests into default `"7D"`.
   - Explicitly mapped `"1D" => "1D"` in `WebBffController.cs`, propagating the daily timeframe to both `GetProjectionsQuery` and `GetMealHistoryQuery`.
   - Updated `ClinicalDietitianService.GetMealHistoryAsync`: when `period == "1D"`, `startDate = today`, querying `userLocalDate >= today && userLocalDate <= today`. Daily view now exclusively returns meals logged on the user's current calendar day.
   - Updated `GetHistoricalAnalyticsQueryHandler`: for `1D`, category trends (Breakfast, Lunch, Snack, Dinner) aggregate meals strictly from `LoggedAtUtc >= todayStartUtc && LoggedAtUtc <= todayEndUtc`.
2. **Yearly (365D) Consolidation View Retirement**:
   - Retired the `365D` Yearly tab across the client and backend. Consolidating 365 days of micro-nutrient and meal logs into mobile/web charts produced excessive payloads and visual clutter without clinical utility for daily dietary tracking.
   - Removed `<button class="tab-btn" data-period="365D">Yearly (1Y)</button>` from `analytics-card.html`.
   - Removed `365D` entries from `analytics-chart.js` and `MealDiaryView.js`, leaving clean `1D` (Today), `7D` (7-Day), `30D` (30-Day), and `90D` (Quarterly) timeline options.
   - Removed obsolete 365D multi-month aggregation branches from `ClinicalDietitianService.cs` and `GetHistoricalAnalyticsQuery.cs`.
3. **Contract Test Segregation & Single Responsibility Principle (SRP)**:
   - Extracted timeline assertions out of `ModalStackingAndLifecycleContractTests.cs` into a dedicated test suite: `AnalyticsTimelineContractTests.cs`.
   - Prevents domain test coupling and guarantees `ModalStackingAndLifecycleContractTests.cs` remains purely focused on modal lifecycle, z-index hierarchy, and event flushing.
   - Added CQRS query tests in `SharedCqrsQueryTests.cs` and composite integration tests in `WebBffCompositeTests.cs` asserting `1D` returns only today's meals.

---

## 2. Context & Root Cause Analysis

1. **Daily Coerced into 7-Day in Web BFF**:
   - In `WebBffController.cs`:
     ```csharp
     var sanitizedPeriod = period switch
     {
         "30D" => "30D",
         "90D" => "90D",
         _ => "7D"
     };
     ```
     Because `"1D"` was missing, any client request for `period=1D` was mapped to `"7D"`, causing identical data to be returned for both Daily and Weekly views.
2. **Yearly Data Consolidation Overhead**:
   - The `365D` timeframe required querying a full year of meal logs, leading to high database query costs and unreadable timeline graphs on mobile viewports.
   - Quarterly (`90D`) is the standard clinical horizon for HbA1c and metabolic trend monitoring (ICMR-NIN 2024 / WHO Asian-Indian guidelines).
3. **Test Segregation Drift**:
   - A newly introduced fact checking analytics tabs had been placed in `ModalStackingAndLifecycleContractTests.cs`. This was flagged as violating test cohesion and was relocated to `AnalyticsTimelineContractTests.cs`.

---

## 3. Decision & Technical Architecture

### A. Web BFF Endpoint Sanitization (`WebBffController.cs`)
```csharp
var sanitizedPeriod = period switch
{
    "1D" => "1D",
    "30D" => "30D",
    "90D" => "90D",
    _ => "7D"
};
```

### B. Daily Meal Scoping in Domain Service (`ClinicalDietitianService.cs`)
```csharp
var today = DateOnly.FromDateTime(DateTime.UtcNow);
DateOnly startDate = period.ToUpperInvariant() switch
{
    "1D" => today,
    "30D" => today.AddDays(-30),
    "90D" => today.AddDays(-90),
    _ => today.AddDays(-7)
};
```

### C. Client Presentation Synchronization
1. `analytics-card.html`: Tabs are `1D` (Daily), `7D` (Weekly), `30D` (Monthly), `90D` (Quarterly).
2. `analytics-chart.js`: `'1D': 'Daily (Today)'`.
3. `MealDiaryView.js`: `'1D': "Today's"`.
4. Cache-busted `index.html`: `main.js?v=1.8.0`.

---

## 4. Verification & Evidence

1. **EvalHarness & Domain Unit Tests**:
   - `AnalyticsTimelineContractTests.cs`: Verified tabs and JS mappings.
   - `SharedCqrsQueryTests.cs`: Verified `1D` returns only meals from today (and excludes yesterday's meals).
   - `WebBffCompositeTests.cs`: Verified `GetDashboardComposite` with `period=1D` returns today's meals only and reports `period = "1D"`.
   - Result: 284 passed (36 Domain, 248 EvalHarness), 0 failed, 0 warnings.
2. **Multi-Tier Live CFT Suite (`tests/validate_e2e_tiers.ps1`)**:
   - 100% pass across all 5 user tiers (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`).
3. **Interactive Browser Subagent Verification**:
   - Clicked Daily tab on `http://localhost:5240`, confirmed subtitle displays `"Showing Today's meal history"`, confirmed absence of Yearly button, and captured screenshot.
