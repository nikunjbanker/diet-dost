<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-125: Mobile Dashboard Unobscured Face Photo Presentation, External Metadata Chip Architecture, and Mobile Static Asset Cache Invalidation

> **Date / Timestamp**: 2026-10-24T15:35:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[UI/UX]`, `[PERFORMANCE]`  
> **Affected Subsystems**: WebGateway (`wwwroot/partials/face-progress-card.html`, `wwwroot/partials/progress-modal.html`, `wwwroot/styles.css`, `wwwroot/index.html`, `Extensions/WebApplicationExtensions.cs`), Tests (`Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`)  
> **Associated PR & Branch**: `fix/mobile-dashboard-photo-uncut`  
> **Relevant SDDs & Rules**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)  

---

## 1. Executive Summary & Rationale

This architectural decision resolves the user-reported defect where self/face transformation photos appeared cropped on the mobile dashboard view (`#face-progress-card`), while correctly displaying in full inside the "New Check-In" modal preview.

Investigation revealed three distinct root causes:
1. **Metadata Overlay Occlusion**: In `face-progress-card.html` and `progress-modal.html`, the `.photo-meta-overlay` element used `position: absolute; bottom: 0; left: 0; right: 0;` with a heavy dark gradient (`background: linear-gradient(to top, rgba(0, 0, 0, 0.85) 0%, rgba(0, 0, 0, 0.4) 60%, transparent 100%);`) and text badges (`Baseline (20 Oct)`, `83.0 kg`). In a standard portrait selfie, this overlay sat directly across the user's mouth, chin, beard, and jawline, blotting out the primary visual indicators of facial weight loss.
2. **Intrinsic Sizing Rule Differences**: `.progress-img` in `styles.css` was set to `width: 100%; height: 100%;` rather than `max-width: 100%; max-height: 100%; width: auto; height: auto; object-fit: contain !important;`, which prevented proper intrinsic aspect-ratio sizing on mobile WebKit/Blink browsers within the flex container.
3. **Aggressive Mobile Browser Asset Caching**: Mobile Chrome and Safari cached `styles.css?v=1.7.0` because previous PRs did not bump the query-string version in `index.html` and `WebApplicationExtensions.cs` did not set explicit `no-cache, must-revalidate` response headers on static assets.

---

## 2. Technical Architecture & Modifications

### 2.1 External Metadata Sub-Row Architecture (`.photo-meta-sub`)
- Extracted the date and weight metadata chips outside `.photo-frame` and `.detail-photo-frame` into a dedicated `.photo-meta-sub` component situated immediately below the photo frame.
- `.photo-frame` now contains *only* the photo, guaranteeing zero pixels of facial or anatomical obstruction.
- Styled `.photo-meta-sub` following the Linear.app / Obsidian surface ladder:
  ```css
  .photo-meta-sub {
    width: 100%;
    max-width: 280px;
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 6px 10px;
    background: rgba(18, 22, 28, 0.85);
    border: 1px solid var(--border-subtle);
    border-radius: var(--radius-sm);
    font-size: 0.75rem;
    color: var(--text-secondary);
    box-sizing: border-box;
  }
  ```

### 2.2 Sizing & Aspect-Ratio Containment
- Standardized `.progress-img` in `styles.css` to match `.detail-img`:
  ```css
  .progress-img {
    max-width: 100%;
    max-height: 100%;
    width: auto;
    height: auto;
    object-fit: contain !important;
    object-position: center !important;
    display: block;
    margin: 0 auto;
    user-select: none;
    transition: transform 0.2s ease;
  }
  ```

### 2.3 Mobile Asset Invalidation & Cache-Control Headers
- Bumped query strings in `index.html`: `styles.css?v=1.8.0`, `modal-stack-manager.js?v=1.6.0`, `main.js?v=1.6.0`.
- Configured `OnPrepareResponse` in `src/Nutrition.WebGateway/Extensions/WebApplicationExtensions.cs`:
  ```csharp
  app.UseStaticFiles(new StaticFileOptions
  {
      OnPrepareResponse = ctx =>
      {
          var path = ctx.Context.Request.Path.Value ?? string.Empty;
          if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
              path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
              path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
          {
              ctx.Context.Response.Headers.CacheControl = "no-cache, must-revalidate";
          }
      }
  });
  ```

---

## 3. Verification & Compliance Evidence

1. **TDD Contract Test Verification**:
   - Added `MobileDashboardFaceCard_MovesMetadataOutsidePhotoFrame_ToPreventOcclusionAndCropping` in `tests/Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`.
   - Verified initial RED failure: `String: "<!-- Face Transformation & Progress Check"··· Not found: "photo-meta-sub"`.
   - Verified GREEN pass after implementation: 8 of 8 contract tests passing (277 total solution tests passing, 0 warnings).
2. **Security & AI Defense Gate**:
   - Validated against all 7 DevSecOps defense vectors (`verify-ai-security-defense.ps1`).
