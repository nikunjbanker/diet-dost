<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-063: Native Feature Classification Matrix & Responsive Exit Gate Signoff

> **Date / Timestamp**: 2026-10-02T23:55:00+05:30  
> **Status**: `ACCEPTED`  
> **Driver / Deciders**: Pair-Programming with Product Owner & Lead Architect  
> **Change Type**: `[ARCHITECTURE]`  
> **Affected Subsystems**: Web Presentation Layer, Cross-Platform Mobile Blueprint, CFT Acceptance Suites (`docs/cft/`), Architecture Specifications (`docs/sdd/`)  
> **Associated Issue & PR**: Issue #42 (Phase 1 Layer 8 of 9)  
> **Governing Standards**: Clean Architecture, WCAG 2.2 AA / Apple HIG, Zero-Throwaway Engineering, ADR-052 (Reusability & Forward-Roadmap Compatibility)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_responsive_web_and_tablet.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_responsive_web_and_tablet.md)  

---

## 1. Executive Summary & Change Rationale

* **Authoritative 3-Tier Native Feature Classification Matrix**: Formally established the architectural boundary between Browser PWA and Native Mobile device hardware capabilities. Categorized all Diet-Dost capabilities into:
  1. *Tier 1 (Responsive Web PWA - Stage 2A)*: Responsive CSS Grid/Flexbox layouts, thumb-zone bottom navigation, bottom sheet modals, HTML5 file uploads, Web BFF single-roundtrip hydration, client-side Excel export.
  2. *Tier 2 (Native Mobile Hardware - Stage 2B)*: Hardware camera with viewfinder/flash, client-side SkiaSharp 1080p compression (<400 KB), Hardware Keystore/Keychain security (`ISecureTokenStore`), biometric authentication, offline SQLite sync (`ILocalLedgerSyncEngine`), and native APNs/FCM push notifications.
  3. *Tier 3 (Future Native Sensors - Phase 4 GA)*: BLE smart scale integration, Apple HealthKit / Google Health Connect sync, and CGM NFC ingestion.
* **Living Responsive CFT Acceptance Suite (`docs/cft/cft_responsive_web_and_tablet.md`)**: Codified the automated verification harness across Desktop (`1440x900`), Tablet Portrait (`768x1024`), Mobile Standard (`390x844`), and Mobile Compact (`375x667`). Recorded 100% pass across zero horizontal layout overflow, touch target size governance (`>= 44px x 44px`), and bottom sheet modal interaction.
* **Responsive Exit Gate Formal Certification**: Validated that Gate 1 (Responsive Presentation), Gate 2 (Mobile BFF Contracts & Compression), and Gate 3 (Native Feature Classification & Documentation) are 100% satisfied, formally unblocking Phase 1 Layer 9 (Azure Container Apps live deployment).

---

## 2. Context & Problem Statement

Prior to Layer 8, responsive layout styling (Layer 6) and Mobile BFF contracts (Layer 7) were implemented, but the solution lacked an authoritative boundary dividing what belongs in the web client versus what requires native mobile device hardware. Without strict classification:
1. **Scope Creep & Architectural Drift**: Teams risk attempting native-only capabilities (such as background camera viewfinder control or hardware keystore encryption) inside browser JavaScript, or duplicating clinical calculation math on mobile.
2. **Missing Pre-Deployment Exit Gate**: No formalized checklist existed to certify that responsive web presentation, mobile contracts, and documentation were release-ready prior to deploying to Azure Container Apps in Layer 9.

---

## 3. Decision Drivers

* **Zero Scope Creep**: Clearly demarcate what belongs in the browser sandbox versus native OS runtimes.
* **Zero Duplicate Domain Math**: Reiterate the ironclad rule that both Web and Mobile clients consume identical backend CQRS endpoints for clinical calculations (ICMR-NIN 2024 / Mifflin-St Jeor).
* **Automated Acceptance Verification**: Every visual and touch requirement must be verified through automated headless browser CDP testing, not manual guesswork.
* **Forward-Roadmap Compatibility (ADR-052 & SDD 09 §1.4)**: The classification matrix and responsive CFT suite must serve as an invariant baseline for subsequent roadmap stages.

---

## 4. Considered Options

* **Option 1: Informal Team Guidelines**:
  - *Pros*: Zero documentation overhead.
  - *Cons*: High risk of architectural drift, accidental mobile math duplication, and unverified responsive edge cases.
* **Option 2: Living Documented Taxonomy & Formal Exit Gate Signoff (Chosen)**:
  - *Pros*: Codified in repository (`docs/cft/cft_responsive_web_and_tablet.md`, `docs/sdd/08_*.md`); backed by automated CDP testing; provides verifiable acceptance evidence; prevents scope creep before Phase 1 Layer 9.
  - *Cons*: None.

---

## 5. Decision Outcome

1. Adopted the **3-Tier Feature Classification Matrix** separating Web PWA, Native Mobile (.NET MAUI), and Future Sensors.
2. Formally signed off on the **Responsive Exit Gate**, confirming all Gate 1, 2, and 3 criteria are met.
3. Fully greenlit **Phase 1 Layer 9 (Issue #31)**: Docker multi-stage packaging, Azure Container Apps deployment with Azure Files SMB SQLite mount, and custom domain TLS binding.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 (Mobile App Implementation - Stage 2B)**:
  - The classification matrix serves as the exact functional specification for native .NET MAUI development. Developers will implement `ISecureTokenStore`, `IImageCompressionService` (SkiaSharp), and `ILocalLedgerSyncEngine` (SQLite) against the approved mobile contracts.
* **Phase 1 Layer 9 (Azure Container Apps Live Showcase)**:
  - Validated responsive web client ensures flawless presentation for reviewers across smartphones, tablets, and desktop displays on the live URL `https://app.dietdost.com`.
* **Zero Technical Debt**:
  - Eliminates disposable prototypes; all responsive styles, test harnesses, and architecture blueprints directly transfer into future production releases.
