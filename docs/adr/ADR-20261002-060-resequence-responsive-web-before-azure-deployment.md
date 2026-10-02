<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261002-060: Re-sequence Responsive Web & Mobile BFF Ahead of Azure Deployment

> **Date / Timestamp**: 2026-10-02T13:05:00+05:30  
> **Status**: `ACCEPTED`  
> **Driver / Agent / Deciders**: User (Product Owner & Architect) & Antigravity AI Assistant  
> **Change Type**: `[ROADMAP]` &bull; `[ARCHITECTURE]` &bull; `[GOVERNANCE]`  
> **Affected Subsystems**: `Presentation` &bull; `WebGateway` &bull; `DevOps` &bull; `Mobile`  
> **Associated Deliverables**: Issues #40, #41, #42, #31 (Milestone Alpha Release 01)  
> **Governing Skills**: [`diet-dost-responsive-web-mobile-readiness`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-responsive-web-mobile-readiness/SKILL.md), [`diet-dost-azure-deployment`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/cft/cft_responsive_web_and_tablet.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_responsive_web_and_tablet.md)  

---

## 1. Context & Problem Statement

In the previous roadmap sequence (v2.3.0), Issue #31 (Azure Container Apps MVP Deployment) was scheduled immediately after Issue #30 (Modularize UI services). Responsive UI layouts (#40) and Mobile BFF contracts (#41) were scheduled as Phase 2 Stage 2A *after* the initial cloud deployment.

### Critical Assessment:
1. **The First-Impression Demo Flaw**: In consumer health tech, early users, angel investors, and dietitians primarily test shared showcase URLs (`https://app.dietdost.com`) on smartphones via links clicked in WhatsApp, LinkedIn, or Email. If deployed before responsive layouts, mobile visitors would encounter a desktop-optimized UI lacking bottom navigation and thumb-zone ergonomics.
2. **Cloud Container Churn**: Deploying to Azure Container Apps first, then implementing responsive CSS and the Mobile BFF facade immediately afterwards, forces building a second container image revision and updating Azure Container Apps revisions within days of initial launch.

---

## 2. Decision Outcome: Option A Approved

We approve **Option A**: Re-sequencing Responsive Web Preparation (#40, #41, #42) into **Milestone Alpha Release 01** *ahead* of the Azure Container Apps deployment (#31).

```
UPDATED EXECUTION SEQUENCE:
Phase 1: Milestone Alpha Release 01 (Workable Web Showcase MVP on Azure)
  Layer 1-5: Clean Architecture Core, Web BFF, Single-Roundtrip Hydration, SRP Modules [COMPLETED]
  Layer 6 (#40): Responsive UI Layout & Mobile Thumb Navigation (Phone, Tablet, Desktop)
  Layer 7 (#41): Mobile BFF Contracts, Compact Payloads & Caching Headers (/api/mobile/v1/*)
  Layer 8 (#42): Native Feature Classification & Responsive CFT Suite
  Layer 9 (#31): Azure Container Apps MVP Deployment (All-Device Ready Showcase Gate on Azure)
                         │
                         ▼
Phase 2: Milestone Alpha Release 02 (Cross-Platform Mobile MVP)
  Layer 1 (#43): Cross-Platform Native App Shell (.NET MAUI) with Hardware Keystore
  Layer 2 (#44): Native Camera & SkiaSharp 1080p Compression (<400 KB)
  Layer 3 (#45): Offline-First SQLite Local Ledger Cache & Sync Engine
  Layer 4 (#46): Multi-Platform CFT Parity Verification (Web vs Android vs iOS)
```

---

## 3. Positive Consequences & Architectural Impact

1. **All-Device Flawless First Impression**: The very first public release on `https://app.dietdost.com` is 100% responsive, touch-ergonomic, and tablet-optimized.
2. **Single Cloud Build**: The initial production container image (`diet-dost-webgateway:1.0.0`) bundles the fully responsive frontend, centralized food estimation, and the live Mobile BFF facade in one cohesive deployment.
3. **Mobile Ready from Day 1**: When .NET MAUI development begins in Phase 2 Layer 1 (#43), the cloud Mobile BFF endpoint (`https://app.dietdost.com/api/mobile/v1/dashboard/composite`) is already active, eliminating backend delays.

---

## 4. Reusability & Forward-Roadmap Compatibility

* **Zero Disposable Code**: Responsive CSS Grid/Flexbox layouts and bottom sheet modals are engineered for PWA mode and native webviews.
* **Shared Mobile BFF Contract**: `MobileDashboardCompositeDto` dispatches the identical Clean Architecture CQRS query handlers in `Nutrition.Application`.
