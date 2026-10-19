<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261019-121: Mobile Ergonomics Single Vertical Scroll, Consistent Form-Group Input Stacks, and Polly Database Resilience

> **Date / Timestamp**: 2026-10-19T17:45:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: UI/UX & Frontend Lead (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[UX_ERGONOMICS]`, `[RESILIENCE]`, `[POLLY]`  
> **Affected Subsystems**: WebGateway (`wwwroot/styles.css`, `wwwroot/partials/auth-gate.html`, `wwwroot/partials/review-modal.html`, `wwwroot/index.html`, `Extensions/DatabaseInitializationExtensions.cs`)  
> **Associated PR & Branch**: `feature/mobile-ergonomics-and-polly-resilience`  
> **Relevant SDDs & Rules**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Rationale

This decision solves critical mobile viewport (`<640px`, Android `412x915`) usability defects and replaces imperative database retry loops with industry-standard Polly resilience:
1. **Login Screen Mobile Ergonomics**:
   - Eliminated the massive ~250px dead black void above the login card by decoupling `.auth-gate-overlay` from center/bottom-sheet modal alignments and styling `.auth-gate-card` as an immersive top-aligned mobile sheet with `env(safe-area-inset-top)` padding.
   - Standardized form inputs across auth and settings: established base `.form-group` (`display: flex; flex-direction: column; gap: 0.35rem; width: 100%;`) and `.form-label-row` (`display: flex; justify-content: space-between; align-items: center;`) ensuring labels sit consistently directly above full-width inputs without awkward side-by-side stretching.
2. **Meal Log Review Modal Single Fluid Vertical Scroll**:
   - Eliminated desktop 50/50 horizontal split-screen behavior and nested competing scrollboxes (`overflow-y: auto`) on mobile.
   - Dissolved desktop column wrappers (`.review-media-column`, `.review-details-column`) using `display: contents !important;` on mobile, enabling all child components to participate directly in a single fluid vertical document scroll on `.review-body-layout`.
   - Defined strict mobile visual reading priority via CSS `order`: Photo Media (1) -> Meal Timing (2) -> **Detected Food Items List (`dish-items-list`) (3, front & center)** -> Quick Add Subzi Chips (4) -> Cooking Fat Chips (5) -> Search Bar (6) -> Dietitian Note (7) -> AI Feedback (8) -> Continuous Training Banner (9) -> Matrix Warnings (10).
   - Fixed sticky `.review-footer` action bar at the viewport bottom with safe-area bottom padding for one-tap confirmation.
3. **Polly v8 Resilience Pipeline for SQLite Lock 5**:
   - Replaced imperative `for (var attempt = 1; attempt <= maxRetries; attempt++)` retry loop in `DatabaseInitializationExtensions.cs` with declarative **Polly v8** resilience pipeline (`ResiliencePipelineBuilder`).
   - Configured exponential backoff with jitter filtering specifically on `Microsoft.Data.Sqlite.SqliteException` where `SqliteErrorCode == 5` (`SQLITE_BUSY` / `database is locked`), logging structured warnings across Azure Files SMB mounts.

---

## 2. Context & Problem Statement

1. **Login Viewport Dead Space**: On mobile devices (`<640px`), the Auth Gate overlay centered the login modal in a desktop modal wrapper, pushing the sign-in form down into a tiny card surrounded by 250px of black space on top, requiring excessive vertical scrolling to reach demo account quick-login pills.
2. **Inconsistent Form Label/Input Alignment**: Password and email input rows lacked standardized `.form-group` containment, causing labels and inputs to render with inconsistent widths or side-by-side alignment.
3. **Meal Log Double-Column & Nested Scrollbox Trap**: On mobile, the review modal retained desktop horizontal column styling. Each column (`.review-media-column` and `.review-details-column`) maintained its own `overflow-y: auto` property. This created 3 competing scrollbars in a single mobile screen, trapping the detected food items list off-screen below a massive photo placeholder and preventing natural thumb scrolling.
4. **Imperative Database Retry Code**: `DatabaseInitializationExtensions.cs` used a manual `for` loop with `Math.Pow(2, attempt)` for SQLite lock contention rather than leveraging the project's standard Polly resilience framework.

---

## 3. Decision Drivers & Architecture Principles

* **Design System Authority (`DESIGN.md`)**: Enforce Obsidian surface ladders, `#090a0d` backgrounds, hairline borders (`rgba(255, 255, 255, 0.08)`), and thumb-friendly 44px minimum touch targets.
* **Single Vertical Flow Invariant**: On mobile screens, pages and full-screen modals must offer exactly **one** continuous vertical scroll gesture. Nested inner scrollboxes are strictly prohibited.
* **Declarative Resilience**: Cloud database lock recovery should rely on robust, tested resilience primitives (Polly v8) with structured logging and exponential backoff.

---

## 4. Implemented Changes

### 4.1 CSS Architecture (`src/Nutrition.WebGateway/wwwroot/styles.css`)
```css
/* Standard Form Group */
.form-group {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
    width: 100%;
}
.form-label-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    width: 100%;
}

/* Mobile Auth Gate Overlay & Card (<640px) */
@media (max-width: 640px) {
    .auth-gate-overlay {
        align-items: flex-start !important;
        padding: 0 !important;
    }
    .auth-gate-card {
        width: 100% !important;
        max-width: 100% !important;
        min-height: 100vh;
        border-radius: 0 !important;
        padding: calc(env(safe-area-inset-top, 16px) + 1.25rem) 1.25rem calc(env(safe-area-inset-bottom, 16px) + 2rem) 1.25rem !important;
    }
}

/* Mobile Review Modal Single Fluid Scroll (<640px) */
@media (max-width: 640px) {
    .review-body-layout {
        display: flex !important;
        flex-direction: column !important;
        overflow-y: auto !important;
        -webkit-overflow-scrolling: touch;
        gap: 1rem !important;
        padding: 1rem 1rem calc(env(safe-area-inset-bottom, 16px) + 5rem) 1rem !important;
    }
    .review-media-column,
    .review-details-column {
        display: contents !important; /* Dissolves column wrappers so children participate in single scroll */
    }
    .review-media-column > *,
    .review-details-column > * {
        overflow: visible !important;
        max-height: none !important;
    }
    /* Mobile Visual Priority */
    .review-media-wrap { order: 1; }
    .review-timing-wrap { order: 2; }
    .dish-items-list { order: 3 !important; }
    .review-quick-add-wrap { order: 4; }
    .review-cooking-fat-wrap { order: 5; }
    .review-search-bar { order: 6; }
    .review-dietitian-note { order: 7; }
    .review-feedback-wrap { order: 8; }
    .review-training-banner { order: 9; }
    .matrix-warnings-wrap { order: 10; }
}
```

### 4.2 Polly v8 Resilience Pipeline (`src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs`)
```csharp
var resiliencePipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        ShouldHandle = new PredicateBuilder().Handle<Microsoft.Data.Sqlite.SqliteException>(ex => ex.SqliteErrorCode == 5),
        BackoffType = DelayBackoffType.Exponential,
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(2),
        MaxDelay = TimeSpan.FromSeconds(30),
        OnRetry = args =>
        {
            var delaySeconds = args.RetryDelay.TotalSeconds;
            logger.LogWarning(args.Outcome.Exception, 
                "SQLite database is temporarily locked (attempt {Attempt}/5). Waiting {DelaySeconds:F0}s for lock release on SMB share...", 
                args.AttemptNumber + 1, delaySeconds);
            return ValueTask.CompletedTask;
        }
    })
    .Build();

await resiliencePipeline.ExecuteAsync(async _ =>
{
    await EnsureDatabaseCreatedAndMigratedAsync(db);
});
```

---

## 5. Verification & Compliance Evidence

1. **Compilation & Unit/Eval Tests**: `dotnet build` passed with 0 warnings. `dotnet test` passed 261/261 tests (36 Domain, 225 EvalHarness).
2. **Live E2E Multi-Tier CFT Suite**: `pwsh -File tests/validate_e2e_tiers.ps1` passed 100% across all 5 demo user tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`).
3. **Mobile Browser Ergonomics Subagent**:
   - Screen: `login_screen_auth_gate_mobile_1792411038495.png` (zero top void, form-group label above input).
   - Screen: `flow1_review_modal_mobile_top_1792411673725.png` (AI Food Photo flow: single fluid scroll, items list immediately visible).
   - Screen: `flow2_review_modal_mobile_top_1792411717616.png` (Audio/Text flow: items list top & center, single scroll).
