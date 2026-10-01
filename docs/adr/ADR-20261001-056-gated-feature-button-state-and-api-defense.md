<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261001-056: Gated Premium Feature Button State & Underlying API Defense

> **Date / Timestamp**: 2026-10-01T20:15:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[DEFECT_FIX]`, `[SECURITY]`, `[PRESENTATION]`  
> **Affected Subsystems**: WebGateway | Application (`Features/ProgressPhotos/`) | Presentation (`wwwroot/js/ui/`) | E2E Tests  
> **Associated PR & Stack**: Issue #36 (Branch: `fix/issue-36-disable-or-hide-gated-premium-buttons`, Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md), [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

* Fixes defect [Issue #36](https://github.com/nikunjbanker/diet-dost/issues/36): Buttons for unavailable premium features were previously active or misleading in non-premium tiers (Free and Basic), and underlying endpoints were not uniformly gated with explicit HTTP `403 Forbidden` responses.
* Enforces defense-in-depth:
  1. **Presentation Layer**: For users without entitlements, buttons for premium features (e.g. `🧍 Full Body & Detail View`, `btn-export-excel`) are disabled or hidden in the UI (`style.display = 'none'`, `opacity: 0.5`, `cursor: not-allowed`), and any attempted direct modal opening intercepts the flow and triggers the Quota/Upgrade Modal.
  2. **Application / Backend Layer**: `GetProgressPhotosQuery` and `ProgressPhotosController` strictly inject `ITierConfigurationService` and enforce tier validation, returning `403 Forbidden` (`FeatureTierUpgradeRequired`) if called directly by Free or Basic users.
* Updates `tests/validate_e2e_tiers.ps1` to assert that `GET /api/progress-photos` returns HTTP `403 Forbidden` for Free/Basic and HTTP `200 OK` for Premium, Admin, and SuperAdmin tiers.

---

## 2. Context and Problem Statement

During user acceptance review, two UI/UX and security discrepancies were identified:
1. In the Web UI, buttons for premium features were either clickable or visible to Free/Basic tier users, creating user confusion and misleading expectations.
2. If non-premium users triggered direct API requests to `GET /api/progress-photos`, the endpoint previously bypassed tier verification (unlike `GET /api/progress-photos/comparison` which was already gated), creating an inconsistent security posture between comparison queries and photo list queries.

Per the solution engineering rules and user mandate:
- Buttons for unavailable premium features must be disabled or hidden in the UI.
- All underlying endpoints must strictly return HTTP `403 Forbidden` with a descriptive error code (`FeatureTierUpgradeRequired`).

---

## 3. Decision Drivers

* **Zero-Assumption Security & OWASP Top 10 API Security**: Never rely on client-side button hiding alone. Both UI state and API endpoints must independently enforce authorization boundaries.
* **Streamlined User Experience**: Clearly indicate locked capabilities with disabled states or paywall upgrade prompts instead of dead clicks or cryptic error dialogs.
* **Single Source of Truth for Tier Entitlements**: Leverage `ITierConfigurationService` consistently across Native CQRS handlers.

---

## 4. Considered Options

* **Option 1: Client-Side Only Hiding**: Hide the buttons in JavaScript and leave `GET /api/progress-photos` unguarded. (Rejected: Violates OWASP API Security Broken Object Level Authorization / Broken Function Level Authorization standards).
* **Option 2 (Chosen): Defense-in-Depth UI Gating & Strict 403 API Defense**:
  - Web UI: Hide `#btn-open-body-modal` and comparison modal tabs when `isCompareAllowed === false`; disable `#btn-export-excel` when `isExportAllowed === false`.
  - Backend: Inject `ITierConfigurationService` into `GetProgressPhotosQuery` and return `403 Forbidden` (`FeatureTierUpgradeRequired`) when `!tierConfig.IsCompareAllowed(request.Tier)`.

---

## 5. Decision Outcome

* **Backend Native CQRS**: Updated `GetProgressPhotosQuery` with `ITierConfigurationService` dependency and user tier evaluation.
* **Thin Controller**: `ProgressPhotosController.GetPhotos` extracts `UserTier` from user claims and forwards it to `GetProgressPhotosQuery`.
* **Presentation**: `progress-modal.js` and `analytics-chart.js` dynamically reflect tier flags (`isCompareAllowed`, `isExportAllowed`).
* **Live CFT Verification**: Multi-tier acceptance suite verifies all 5 tiers pass with 100% compliance.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 (Mobile MVP)**: `MobileBffController` and MAUI/Android clients will reuse the exact same `GetProgressPhotosQuery` handler, guaranteeing that mobile clients inherit the identical 403 authorization defense without rewriting clinical or tier gating code.
* **Phase 3 (Enterprise Persistence)**: Provider independence is preserved; tier verification occurs in the Application layer before reaching database queries.
