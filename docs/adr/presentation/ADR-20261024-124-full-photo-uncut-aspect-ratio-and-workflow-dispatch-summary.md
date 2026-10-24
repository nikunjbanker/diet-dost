<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-124: Full Photo Uncut Aspect-Ratio Display, High-Resolution Lightbox Inspection, and GitHub Actions Dispatch Options Audit Summary

> **Date / Timestamp**: 2026-10-24T14:35:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[PRESENTATION]`, `[UI/UX]`, `[DEVOPS]`  
> **Affected Subsystems**: WebGateway (`wwwroot/styles.css`, `wwwroot/partials/progress-modal.html`, `wwwroot/partials/face-progress-card.html`, `wwwroot/js/ui/review-modal.js`, `wwwroot/js/ui/progress-modal.js`), Workflows (`.github/workflows/azure-app-deploy.yml`, `.github/workflows/azure-infra-deploy.yml`), Tests (`Nutrition.EvalHarness.Tests/ModalStackingAndLifecycleContractTests.cs`)  
> **Associated PR & Branch**: `feature/workflow-dispatch-options-summary`  
> **Relevant SDDs & Rules**: [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Rationale

This architectural decision addresses two key production usability and governance requirements:
1. **Self / Progress Photo Uncut Aspect-Ratio Presentation**:
   - Resolves user-reported issue where self/progress portraits had cropped faces or chopped heads due to legacy `object-fit: cover` styling.
   - Replaces `object-fit: cover` with `object-fit: contain`, flex alignment centering (`display: flex; align-items: center; justify-content: center;`), and obsidian-dark letterboxing (`#080a0f`) across all self-photo frames (`.progress-img`, `.detail-img`, `.timeline-thumb`, and `#progress-img-preview`).
   - Parallels the existing, proven presentation model used by food plate inspection (`.review-meal-img`), guaranteeing 100% photo visibility regardless of whether the camera captured in 9:16 portrait, 3:4 portrait, 1:1 square, or landscape orientation.
   - Introduces universal high-resolution photo lightbox inspection: exposes `window.openPhotoLightbox` in `review-modal.js` and wires interactive click handlers across dashboard face frames, detail modal frames, and timeline thumbnails so users can tap any photo to view it full-screen without cuts.
2. **GitHub Actions Workflow Dispatch Options Audit Summary**:
   - Updates `enableCustomDomain` in `azure-app-deploy.yml` to default to `true` to ensure production deployments automatically bind `dev.diet-dost.in` unless explicitly disabled.
   - Introduces a dedicated `workflow-dispatch-summary` job in both `azure-app-deploy.yml` and `azure-infra-deploy.yml` that writes a markdown audit table to `$GITHUB_STEP_SUMMARY`, capturing selected deployment flags, environment targets, image tags, and custom domain options for full auditability and issue diagnosis.

---

## 2. Context & Root Cause Analysis

1. **Cropped Face / Progress Photos**:
   - Previously, `.detail-img` and `.timeline-thumb` were styled with `object-fit: cover` inside containers with fixed aspect ratios (`4/5` or `3/4`).
   - Because modern smartphones shoot portraits in 9:16 or 3:4 aspect ratios, `object-fit: cover` forcefully scaled the image to fill the 4/5 container, shearing off the top (forehead, hair, and face) or bottom (chin and neck).
   - In contrast, food plate photos (`.review-meal-img` and `.review-lightbox-img`) utilized `object-fit: contain` with radial dark letterbox backgrounds, displaying 100% of the meal plate without cuts.
2. **Custom Domain Workflow Execution Transparency**:
   - During manual deployment dispatch, the `enableCustomDomain` option defaulted to `false`. Running without checking this box led to `dev.diet-dost.in` returning "site not reachable" because step 11b was skipped.
   - Furthermore, GitHub Actions does not natively summarize dispatch input values in the run summary tab, making post-incident investigation slower.

---

## 3. Decision & Technical Architecture

### 3.1 Uncut Photo Styling Architecture

In `styles.css`:
- `.progress-img`, `.detail-img`, and `.timeline-thumb`:
  - `object-fit: contain`
  - `object-position: center`
  - `max-width: 100%`, `max-height: 100%`
  - `width: auto`, `height: auto`
  - `margin: 0 auto`, `display: block`
- `.photo-frame`, `.detail-photo-frame`, `.timeline-thumb-frame`:
  - `background: #080a0f` (obsidian-dark backdrop)
  - `display: flex; align-items: center; justify-content: center;`
  - `cursor: pointer`
  - Micro-animation hover scale (`transform: scale(1.02)`)

### 3.2 Universal Lightbox Inspection Architecture

- Expose global `window.openPhotoLightbox(photoUrl, title)` and `window.closePhotoLightbox()` in `review-modal.js`.
- In `progress-modal.js`:
  - Bind click events on `imgBaselineFace`, `imgCurrentFace`, `detailBaselineFaceImg`, `detailCurrentFaceImg`, `detailBaselineBodyImg`, and `detailCurrentBodyImg` frames.
  - Wire timeline card click handlers to open photo in full-screen modal stack lightbox.
  - Added accessibility inspection titles (`title="Click to inspect photo in high-resolution lightbox"`) to all photo frames.

### 3.3 CI/CD Dispatch Options Audit Summary

- `.github/workflows/azure-app-deploy.yml`:
  - `enableCustomDomain` default updated from `false` to `true`.
  - Added `workflow-dispatch-summary` job rendering an audit table of inputs:
    - Target Environment
    - Deploy Core App
    - Enable Custom Domain Binding
    - Seed Demo Accounts
    - Container Image Tag
  - Updated Step 12 to include domain URL and seed account verification.
- `.github/workflows/azure-infra-deploy.yml`:
  - Added `workflow-dispatch-summary` job rendering infrastructure parameters table.

---

## 4. Verification & Validation Evidence

1. **Automated Contract Tests**:
   - Added `ProgressAndSelfPhotos_StylesAndAspectRatios_ShowFullUncutPhotosWithoutCropping` in `ModalStackingAndLifecycleContractTests.cs`.
   - Confirmed test fails on legacy `object-fit: cover` and passes when `object-fit: contain` is present.
   - Solution test suite: 276 tests passed (0 failures, 0 warnings).
2. **DevSecOps Security Defense Gate**:
   - `scripts/verify-ai-security-defense.ps1 -Mode All`: all 7 vectors green.
