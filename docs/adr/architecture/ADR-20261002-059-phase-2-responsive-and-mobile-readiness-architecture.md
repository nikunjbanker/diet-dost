<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-059: Phase 2 Two-Stage Responsive Web & Mobile Readiness Architecture

> **Date / Timestamp**: 2026-10-02T12:45:00+05:30  
> **Status**: `ACCEPTED`  
> **Driver / Agent / Deciders**: User (Product Owner & Architect) & Antigravity AI Assistant  
> **Change Type**: `[ARCHITECTURE]` &bull; `[ROADMAP]` &bull; `[GOVERNANCE]`  
> **Affected Subsystems**: `WebGateway` &bull; `Application` &bull; `Presentation` &bull; `Mobile` &bull; `Roadmap`  
> **Associated Deliverables**: Issues #40 through #46 on Project Board `Diet-Dost Roadmap`  
> **Governing Skill**: [`.agents/skills/diet-dost-responsive-web-mobile-readiness/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_responsive_web_and_tablet.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_responsive_web_and_tablet.md), [`docs/cft/cft_cross_platform_functional_parity_matrix.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_cross_platform_functional_parity_matrix.md)  

---

## 1. Context & Problem Statement

Following the completion and merge of Phase 1 (Web BFF composite endpoint, shared CQRS queries, single-roundtrip hydration, centralized food estimation, and SRP UI services), Phase 2 originally called for jumping directly into .NET MAUI cross-platform mobile app development.

However, a direct jump from desktop-first web views directly to native mobile apps introduces significant architectural risks:
1. **Contract Rework Risk**: Authoring native mobile clients before testing mobile-optimized BFF payloads and compression leads to churn in data models and endpoints.
2. **Ergonomic Inconsistency**: Web and tablet users accessing the web application on phones and iPads would suffer from a desktop-only viewport layout, missing thumb-zone navigation, and awkward popups.
3. **Blurred Architectural Boundaries**: Without a formal feature classification taxonomy, developers risk accidentally implementing web-like features natively or building device-specific hardware features in the browser where capabilities are lacking.

---

## 2. Decision & Architectural Framework

We formalize **Phase 2 (Milestone Alpha Release 02)** as a **Two-Stage Execution Model**:

```
PHASE 2: Milestone Alpha Release 02
Responsive / shared preparation (Stage 2A)
        │
        ├── Implement now (Responsive UI & Ergonomics)
        ├── Prepare contract now (Mobile BFF & Compact Payloads)
        └── Document for native (Feature Taxonomy & Parity Suite)
                         │
                         ▼ [Phase 2 Exit Gate]
NATIVE MOBILE (Stage 2B)
Implement device-specific capability (Camera, Keystore, Offline SQLite)
```

### Stage 2A: Responsive / Shared Preparation (Execute First)
- **Implement Now**: Responsive CSS Grid/Flexbox layouts across mobile (<640px), tablet (640px-1023px), and desktop (>=1024px); fixed bottom navigation bar for mobile thumb-zone; slide-up bottom sheet modals; touch targets `>= 44px x 44px`.
- **Prepare Contract Now**: Mobile BFF facade (`GET /api/mobile/v1/dashboard/composite`) reusing Phase 1 shared CQRS query handlers; compact camelCase serialization; Brotli/Gzip compression; HTTP `ETag` and `If-None-Match` caching returning `304 Not Modified`; offline mutation sync metadata (`clientMutationId`, `clientTimestampUtc`).
- **Document for Native**: Formal classification matrix separating Web PWA capabilities from native device hardware requirements; living responsive CFT suite; Phase 2 exit gate verification.

### Stage 2B: Native Mobile Implementation (Execute Next)
- Cross-platform native app shell (.NET MAUI / Android & iOS) with `ISecureTokenStore` backed by `AndroidKeyStore` / iOS Keychain.
- Native hardware camera with SkiaSharp 1080p client-side compression (<400 KB) connecting to `/api/mobile/v1/meals/capture`.
- Offline-first SQLite local ledger cache (`diet_dost_local.db`) with pending mutation queue and 1-tap meal review screen.
- Multi-Platform CFT Parity Verification across Web, Android, and iOS against the live Azure backend.

---

## 3. Dedicated Companion Skill & Reference Playbooks

We establish the authoritative companion skill [`.agents/skills/diet-dost-responsive-web-mobile-readiness/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md) with 7 reference playbooks:
1. `references/responsive-ui-playbook.md`
2. `references/mobile-tablet-ux-playbook.md`
3. `references/api-mobile-readiness-playbook.md`
4. `references/native-feature-classification.md`
5. `references/responsive-cft-template.md`
6. `references/cross-platform-parity-template.md`
7. `references/phase-2-exit-gate.md`

---

## 4. GitHub Issues & Project Board Registration

Seven atomic deliverables have been created and registered on GitHub Project Board #1 (`Diet-Dost Roadmap`) under Milestone `Alpha Release 02: Cross-Platform Mobile MVP`:
- **Issue #40 (Layer 1)**: `feat(presentation): implement Phase 2 Layer 1 - responsive UI layout and touch-first navigation for mobile and tablet viewports`
- **Issue #41 (Layer 2)**: `feat(api): implement Phase 2 Layer 2 - Mobile BFF contracts, compact payloads, and caching headers for mobile readiness`
- **Issue #42 (Layer 3)**: `docs(mobile): implement Phase 2 Layer 3 - native feature classification matrix and responsive CFT acceptance suite`
- **Issue #43 (Layer 4)**: `feat(mobile): implement Phase 2 Layer 4 - cross-platform native app shell with hardware keystore security`
- **Issue #44 (Layer 5)**: `feat(mobile): implement Phase 2 Layer 5 - native camera integration with SkiaSharp 1080p client-side compression`
- **Issue #45 (Layer 6)**: `feat(mobile): implement Phase 2 Layer 6 - offline-first SQLite local ledger cache and sync engine`
- **Issue #46 (Layer 7)**: `test(mobile): implement Phase 2 Layer 7 - multi-platform CFT parity verification across Web, Android, and iOS`

All issues are queued in `.agents/state/issue_workflow_state.json` awaiting Layer 6 Azure deployment completion of Phase 1 (Issue #31).

---

## 5. Forward Roadmap Impact & Future Phase Compatibility

* **Zero Disposable Code Guarantee**: All responsive CSS Grid tokens, bottom nav bar, and sheet modal styles created in Stage 2A are 100% reusable inside PWA mode and native webviews.
* **Shared Mobile BFF Contract**: `MobileDashboardCompositeDto` and compression rules established in PR 8 serve as the immutable contract for .NET MAUI in PR 10 and future client platforms.
* **Offline SQLite Prepared for Phase 3**: The local SQLite database schema implemented in PR 12 includes sync metadata (`SyncStatus`, `LastModifiedUtc`, `ClientMutationId`), making it ready for Phase 3's bidirectional Azure SQL Serverless sync pipeline.
