<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260929-051: Product Owner MVP Market Roadmap Re-Prioritization & Phased Release Milestones

> **Date / Timestamp**: 2026-09-29T11:58:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Deciders**: Product Owner (PO) & Lead Architect Pair-Programming  
> **Change Type**: `[ROADMAP]`, `[ARCHITECTURE]`, `[GOVERNANCE]`  
> **Affected Subsystems**: WebGateway / Presentation / Persistence / Mobile / Azure Infrastructure  
> **Associated PR & Stack**: PR #21 (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

Following direct Product Owner (PO) assessment of early market discovery and customer demonstration requirements, this decision records the authoritative re-prioritization of [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md).

Key PO drivers and strategic adjustments:
* **"Showcase First, Scale Second" Strategy**: The primary immediate goal is to establish **Milestone: Alpha Release 01 (Workable Web Showcase MVP)** without external cloud SQL or NoSQL database dependencies, allowing rapid, zero-cost customer showcases, clinical dietitian validations, and stakeholder feedback.
* **Zero-Cloud-Friction Persistence**: Retains lightweight embedded local SQLite (`diettracker.db`) with Entity Framework Core, completely eliminating cloud database provisioning delays, cloud connection firewall setups, and cloud expenses during initial customer validation.
* **End-Goal Durability**: The Clean Architecture and CQRS decoupling implemented in Phase 1 ensures that transitioning to Azure SQL Serverless and Azure Cosmos DB in Beta 01 is an isolated infrastructure swap with zero client-side breaking changes.

---

## 2. Context and Problem Statement

Diet-Dost previously had a sequential 4-phase technical roadmap that grouped cloud database provisioning and mobile development in a monolithic pipeline without formal product release milestone gates. 

From a product management and go-to-market perspective:
1. Provisioning Azure SQL Serverless, Azure Cosmos DB, Managed Identity, and Azure Container Apps before putting a workable demonstration into customers' hands introduces premature infrastructure overhead and distracts from validating core clinical and AI vision value propositions.
2. Demonstrations to prospective customers, dietitians, and angel investors require a high-fidelity, deterministic, and fast UI experience with zero setup friction.
3. The roadmap needed clear release milestone demarcation (Alpha 01 -> Alpha 02 -> Beta 01 -> GA 1.0) while ensuring that long-term enterprise goals remain fully accounted for.

---

## 3. Decision Drivers

* **Driver 1: Time-to-Customer Showcase**: Enable immediate, frictionless demos of Diet-Dost's core AI vision, ICMR-NIN 2024 calculators, and 5 user tiers.
* **Driver 2: Zero Cloud Infrastructure Cost for Alpha 01 ($0/mo)**: Avoid Azure database provisioning and operational costs during early feedback cycles.
* **Driver 3: Architectural Independence (Clean Architecture)**: Ensure client applications and domain logic remain 100% agnostic to whether persistence is embedded SQLite or Azure SQL Serverless.
* **Driver 4: Native GitHub Stacked PR Protocol Compliance**: Maintain small, reviewable, bottoms-up stacked PR tracks in adherence with `AGENTS.md`.

---

## 4. Considered Options

* **Option 1: Complete Enterprise Cloud SQL First**: Provision Azure SQL Serverless and Cosmos DB prior to customer showcase. (Rejected: Adds unnecessary delay, cloud configuration overhead, and premature infrastructure costs).
* **Option 2: Pure Ephemeral In-Memory Mock Persistence**: Eliminate SQLite entirely and use in-memory EF Core. (Rejected: Loses state between local server restarts, preventing persistent customer demo review across days).
* **Option 3 (Chosen)**: **Phased Market Milestones with Embedded Local SQLite for Alpha 01**: Focus Phase 1 on delivering a polished Web Showcase MVP (Alpha 01) using embedded local SQLite and 5 seeded demo accounts. Sequence Cross-Platform Mobile as Alpha 02, Enterprise Cloud Persistence as Beta 01, and Containerized Cloud Deployment as GA 1.0.

---

## 5. Decision Outcome

* **Chosen Option**: **Option 3: Phased Market Milestones with Embedded Local SQLite for Alpha 01**.
* **Justification**: Confirmed through user interactive alignment (`ask_question`). This provides an immediate, zero-friction demonstration vehicle while strictly preserving the downstream path to cross-platform mobile, Azure SQL Serverless, and production containerization.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Immediate Customer Showcase**: Alpha 01 Web Showcase MVP can be demoed locally or on any workstation with zero cloud prerequisites.
* **Pre-Seeded 5 User Tiers**: Instant login with `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`.
* **Zero Technical Debt**: Because domain models and Web BFF are built with Clean Architecture, persistence is abstracted behind `IRepository<T>` and `IUnitOfWork`.
* **Clear Milestones**: Teams and stakeholders have explicit definitions of Alpha 01, Alpha 02, Beta 01, and GA 1.0.

### Negative Consequences / Accepted Trade-Offs:
* **Local Single-Node State**: Embedded SQLite does not support horizontal multi-node scaling. (Mitigated: Horizontal scaling is unnecessary for single-node Alpha 01 customer showcases; multi-provider Azure SQL support is formally scheduled for Beta 01).

---

## 7. Verification & Compliance Results

* **Roadmap Specification**: Updated [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) to v2.0.0.
* **SDD Master Index**: Updated [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md).
* **Living Documentation Registries**: Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).
* **Sign-Off Status**: `ACCEPTED & SYNCHRONIZED`
