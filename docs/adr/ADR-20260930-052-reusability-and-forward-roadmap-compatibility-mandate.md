<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260930-052: Zero-Throwaway Engineering, Cross-Phase Reusability & Forward-Roadmap Compatibility Mandate

> **Date / Timestamp**: 2026-09-30T14:10:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Deciders**: Product Owner (PO) & Lead Architect Pair-Programming  
> **Change Type**: `[GOVERNANCE]`, `[ARCHITECTURE]`, `[ROADMAP]`  
> **Affected Subsystems**: All Subsystems (Presentation / WebGateway / Application / Domain / Infrastructure / Mobile / CFTs / Docs)  
> **Associated PR & Stack**: `docs/reusable-forward-roadmap-mandate` (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md), [`docs/cft/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md)  

---

## 1. Executive Summary & Change Rationale

This decision establishes the mandatory **Zero-Throwaway Engineering, Cross-Phase Reusability & Forward-Roadmap Compatibility Mandate** across the entire Diet-Dost engineering lifecycle.

Following Product Owner (PO) strategic alignment:
* **Zero Disposable Code Policy**: MVP (Phase 1 / Alpha 01) implementations must NEVER be treated as disposable prototypes or isolated demo hacks. All domain entities, CQRS handlers, DTO schemas, UI modules, customer acceptance tests (CFTs), and architectural decisions (ADRs) must be engineered **in concert with future roadmap milestones in consideration** (Phase 2 Mobile Alpha 02, Phase 3 Enterprise Cloud DB Beta 01, Phase 4 Automated CI/CD GA 1.0).
* **Multi-Platform Reuse**: Backend CQRS query and command handlers created for the Web BFF in Phase 1 must be directly consumable by the Mobile BFF in Phase 2 with 0 duplicated clinical code.
* **Non-Breaking Cloud Persistence Evolution**: Persistence abstractions (`INutritionContext`) and EF Core entity mappings created for Azure Files SMB SQLite in Phase 1 must be strictly decoupled from SQLite dialect idiosyncrasies, enabling seamless multi-provider injection for Azure SQL Serverless in Phase 3 with zero client breaking changes.
* **Living CFT Test Harness Inheritance**: Customer & Functional Acceptance Tests in `docs/cft/` are durable regression test suites inherited and cross-validated across all client platforms and persistence providers.
* **Mandatory ADR Forward-Roadmap Impact Analysis**: Every future ADR must formally document how its design supports upcoming milestones without painting the architecture into a corner.

---

## 2. Context and Problem Statement

In rapid MVP product development, teams frequently fall into the trap of writing "throwaway demo code" or "quick hacks" to get a showcase working, intending to rewrite it later. In complex domains like Diet-Dost—which combines clinical dietetics (ICMR-NIN 2024 / WHO), multimodal AI vision, dual web/mobile frontends, multi-tier paywalls, and cloud scaling—throwaway code creates severe hazards:
1. **Clinical Drift & Logic Duplication**: Re-implementing nutritional math across Web, Mobile, and Cloud DB causes conflicting calorie budgets and health recommendation discrepancies.
2. **Expensive Re-Architecting**: If the MVP persistence layer or Web BFF is built with tight local coupling, transitioning to Mobile in Phase 2 or Azure SQL in Phase 3 requires rewriting clients or recreating databases from scratch.
3. **Transient Testing**: If functional acceptance tests (CFTs) are written as one-off manual notes for the MVP, the team loses regression protection when subsequent phases are introduced.

A unified governance rule was required to bind all contributors and AI agents to forward-compatible, reusable engineering across every deliverable.

---

## 3. Decision Drivers

* **Driver 1: Zero Duplicate Domain Code**: 100% of ICMR-NIN 2024 clinical calculations, TDEE, and food estimation logic must reside exclusively in `Nutrition.Domain` and `Nutrition.Application`, reused identically across all frontends.
* **Driver 2: Smooth Cross-Platform Progression**: Assets built in Phase 1 (Web MVP) must directly empower and accelerate Phase 2 (Mobile MVP).
* **Driver 3: Seamless Persistence Migration**: Decouple EF Core entities so transitioning from SQLite to Azure SQL Serverless (Phase 3) is a pure infrastructure swap with zero client changes.
* **Driver 4: Living CFT Test Integrity**: Maintain test suites as reusable, permanent verification assets that guard against regressions across all platforms.
* **Driver 5: Token Economics & Agentic Governance**: Clearly codify these rules in SDD 09, SDD 08, and `AGENTS.md` so AI agents automatically adhere to forward-roadmap compatibility.

---

## 4. Considered Options

* **Option 1: Ad-hoc MVP Development (Status Quo)**: Allow MVP to be built with any fast approach, postponing reusability refactoring to Phase 2 or Phase 3. (Rejected: Results in technical debt, throwaway code, and clinical drift).
* **Option 2: Over-Engineered Enterprise Cloud from Day 1**: Force full Azure SQL, multi-region clustering, and mobile sync into Phase 1. (Rejected: Violates the "Showcase First, Scale Second" PO mandate and introduces premature cloud costs).
* **Option 3 (Chosen)**: **Zero-Throwaway Engineering & Forward-Roadmap Reusability Mandate**: Deliver the lightweight, low-cost Web MVP deployed on Azure in Phase 1, but strictly mandate that all code, CQRS queries, DTOs, persistence abstractions, CFTs, and ADRs are engineered in concert with future roadmap milestones in consideration.

---

## 5. Decision Outcome & Binding Rules

### Chosen Option: Option 3 — Zero-Throwaway Engineering & Forward-Roadmap Reusability Mandate

The following four binding pillars are codified into [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) and [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md):

1. **Code & Functional Reusability**:
   - `Nutrition.Application` CQRS query handlers (`GetDailyLedgerQueryHandler`, `GetHistoricalAnalyticsQueryHandler`, `GetMealHistoryQueryHandler`, `GetAiQuotaQueryHandler`) created for Web BFF in Phase 1 MUST be directly consumed by Mobile BFF in Phase 2.
   - Centralized food estimation (`POST /api/meals/estimate`) created in Phase 1 MUST be consumed by Mobile Camera Review modal in Phase 2.
   - EF Core entity models in Phase 1 MUST preserve 100% schema parity for Phase 3 Azure SQL Serverless.
   - Presentation ES modules MUST adhere to Single Responsibility Principle (SRP) for potential reuse in PWA or hybrid mobile shells.
   - Azure Container Apps deployment templates in PR 6 MUST parameterize database providers (`DATABASE_PROVIDER=Sqlite`) to allow declarative swapping to `DATABASE_PROVIDER=AzureSql` in PR 12.
2. **CFT Reusability & Living Acceptance Test Inheritance**:
   - Every acceptance test document in `docs/cft/` is a permanent regression suite.
   - Phase 1 Web CFT (`cft_web_bff_and_clean_architecture.md`) sets the functional baseline that Phase 2 Mobile CFT (`cft_mobile_mvp_cross_platform.md`) and Master Parity Matrix (`cft_cross_platform_functional_parity_matrix.md`) inherit and re-verify.
3. **ADR Reusability & Forward Architectural Precedent**:
   - Every atomic ADR fragment MUST include Section 6: `Forward Roadmap Impact & Future Phase Compatibility`.
4. **Cross-Phase Synchronization Verification**:
   - PR reviews and milestone completion criteria require verifying compatibility with upcoming roadmap phases.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

* **Impact on Phase 1 (Alpha 01: Web Clean Architecture & Azure MVP)**:
  Guarantees that all PRs (PR 1 through PR 6) produce hardened, reusable backend handlers, decoupled persistence abstractions, and parameterized deployment assets rather than quick hacks.
* **Impact on Phase 2 (Alpha 02: Cross-Platform Mobile MVP)**:
  Mobile development accelerates dramatically because `MobileBffController` simply dispatches the existing, pre-tested CQRS query handlers from Phase 1, and the mobile camera review modal reuses the centralized food estimation API.
* **Impact on Phase 3 (Beta 01: Enterprise Cloud Persistence)**:
  Swapping persistence providers from SQLite to Azure SQL Serverless with Managed Identity requires zero code changes in Domain, Application, or client applications.
* **Impact on Phase 4 (GA 1.0: Production Scaling & CI/CD)**:
  The GitHub Actions CI/CD pipeline runs the complete, reusable CFT regression matrix across Web and Mobile with zero test re-authoring overhead.

---

## 7. Consequences & Trade-Offs

### Positive Consequences:
* **Zero Wasted Engineering Effort**: Every line of code written in MVP serves as the durable foundation for subsequent phases.
* **Clinical Invariant Integrity**: Zero risk of formula drift between Web, Mobile, and Cloud DB.
* **Drastically Faster Phase 2 & 3 Delivery**: Reusing Phase 1 backend handlers cuts mobile backend development effort by ~60%.
* **Seamless Cloud Scaling**: Eliminates breaking changes during database provider transition.

### Accepted Trade-Offs:
* **Upfront Rigor in Phase 1**: Requires contributors and AI agents to design interfaces, DTOs, and persistence mappings with future phases in mind, rather than taking expedient shortcuts. (Mitigated by playbooks in `.agents/skills/diet-dost-clean-architecture/`).

---

## 8. Verification & Compliance Results
* **Synchronized Documents**:
  - [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) (v2.2.0)
  - [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md) (Tenet 6)
  - [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md) (Standard 15)
  - [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) & [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md) (ADR-052 registered)
