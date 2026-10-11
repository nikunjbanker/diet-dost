<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260929-051: Product Owner MVP Market Roadmap Re-Prioritization & Phased Release Milestones

> **Date / Timestamp**: 2026-09-29T12:48:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Deciders**: Product Owner (PO) & Lead Architect Pair-Programming  
> **Change Type**: `[ROADMAP]`, `[ARCHITECTURE]`, `[GOVERNANCE]`  
> **Affected Subsystems**: WebGateway / Presentation / Persistence / Mobile / Azure Infrastructure  
> **Associated PR & Stack**: PR #21 (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md), [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale

Following direct Product Owner (PO) assessment of early market discovery, live customer demonstration requirements, and Azure deployment practicalities, this decision records the authoritative re-prioritization of [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) to **v2.1.0**.

Key PO drivers and strategic adjustments:
* **"Showcase First, Scale Second" Strategy**: The primary immediate goal is to establish **Milestone: Alpha Release 01 (Workable Web Showcase MVP on Azure)** without external cloud SQL or NoSQL database dependencies, allowing rapid, zero-cost customer showcases, clinical dietitian validations, and investor feedback over the live internet.
* **Live Azure Deployment in Alpha 01 (PR 6)**: To make the MVP demoable to external customers and stakeholders worldwide, Phase 1 includes **PR 6: Azure Container Apps MVP Deployment with persistent Azure Files SQLite SMB volume mount (`/app/data`) and custom domain (`dev.diet-dost.in`) with free Azure-managed TLS certificate auto-renewal**.
* **Zero Cloud SQL/NoSQL Expense (<$0.30/Month)**: Retains lightweight embedded local SQLite (`diettracker.db`) hosted on an Azure Files SMB share, completely eliminating cloud database provisioning delays, cloud connection firewall setups, and expensive cloud database licenses during initial customer validation.
* **End-Goal Durability**: The Clean Architecture and CQRS decoupling implemented in Phase 1 ensures that transitioning to Azure SQL Serverless and Azure Cosmos DB in Beta 01 is an isolated infrastructure swap with zero client-side breaking changes.

---

## 2. Context and Problem Statement

Diet-Dost previously had a sequential 4-phase technical roadmap that grouped cloud database provisioning and mobile development in a monolithic pipeline without formal product release milestone gates. Furthermore, cloud containerization was initially postponed to Phase 4, which would have left Alpha 01 confined to `localhost`, preventing remote customer and investor demos.

From a product management and go-to-market perspective:
1. Demonstrations to prospective customers, dietitians, and angel investors require a live, publicly accessible, HTTPS-secured web application on Azure (`https://dev.diet-dost.in`).
2. Provisioning Azure SQL Serverless, Azure Cosmos DB, Managed Identity, and multi-region replication before validating customer appetite introduces premature infrastructure overhead and unnecessary cloud bills.
3. The roadmap needed clear release milestone demarcation (Alpha 01 -> Alpha 02 -> Beta 01 -> GA 1.0) with a live Azure showcase deployed at the conclusion of Phase 1.

---

## 3. Decision Drivers

* **Driver 1: Worldwide Live Customer Showcase**: Enable immediate, frictionless demos of Diet-Dost's core AI vision, ICMR-NIN 2024 calculators, and 5 user tiers on a live Azure custom domain (`https://dev.diet-dost.in`).
* **Driver 2: Zero Cloud Database Cost for Alpha 01 (<$0.30/mo)**: Avoid Azure SQL/Cosmos DB provisioning and operational costs during early feedback cycles by using Azure Files-backed SQLite.
* **Driver 3: Architectural Independence (Clean Architecture)**: Ensure client applications and domain logic remain 100% agnostic to whether persistence is Azure Files SQLite or Azure SQL Serverless.
* **Driver 4: Native GitHub Stacked PR Protocol Compliance**: Maintain small, reviewable, bottoms-up stacked PR tracks (PR 1 through PR 6 in Phase 1) in adherence with `AGENTS.md`.

---

## 4. Considered Options

* **Option 1: Complete Enterprise Cloud SQL First**: Provision Azure SQL Serverless and Cosmos DB prior to customer showcase. (Rejected: Adds unnecessary delay, cloud configuration overhead, and premature infrastructure costs).
* **Option 2: Localhost-Only Alpha 01**: Keep Alpha 01 strictly on local workstations and defer all Azure deployment to Phase 4. (Rejected: Prevents remote customer demos, mobile testing, and external stakeholder evaluation).
* **Option 3 (Chosen)**: **Alpha 01 Live Azure MVP Deployment with Azure Files SQLite & Custom Domain TLS**: Deliver Web Clean Architecture & Web BFF (PRs 1-5), and deploy to Azure Container Apps with persistent Azure Files SQLite mount and custom domain TLS (PR 6). Sequence Cross-Platform Mobile as Alpha 02, Enterprise Cloud Persistence as Beta 01, and Production CI/CD as GA 1.0.

---

## 5. Decision Outcome

* **Chosen Option**: **Option 3: Alpha 01 Live Azure MVP Deployment with Azure Files SQLite & Custom Domain TLS**.
* **Justification**: Confirmed through user interactive alignment (`ask_question`). This provides an immediate, zero-friction, worldwide demonstration vehicle (<$0.30/mo) while strictly preserving the downstream path to cross-platform mobile, Azure SQL Serverless, and production containerization.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Live Worldwide Customer Showcase**: Alpha 01 Web Showcase MVP is live at `https://dev.diet-dost.in` with free Azure-managed TLS.
* **Pre-Seeded 5 User Tiers**: Instant login with `free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app`.
* **Zero Cloud SQL/NoSQL Bills**: SQLite runs on Azure Files SMB share with single-replica constraint (`maxReplicas: 1`), keeping total cloud spend under $0.30/month.
* **Zero Technical Debt**: Because domain models and Web BFF are built with Clean Architecture, persistence is abstracted behind `IRepository<T>` and `IUnitOfWork`.
* **Clear Milestones**: Teams and stakeholders have explicit definitions of Alpha 01, Alpha 02, Beta 01, and GA 1.0.

### Negative Consequences / Accepted Trade-Offs:
* **Single-Replica Azure Constraint**: Azure Files SQLite requires `maxReplicas: 1` to prevent database locking exceptions. (Mitigated: 1 replica with 180,000 vCPU-seconds free/month is more than sufficient for customer showcases and discovery; multi-replica horizontal scaling is formally unlocked in Beta 01 with Azure SQL Serverless).

---

## 7. Verification & Compliance Results

* **Roadmap Specification**: Updated [`docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/09_master_implementation_roadmap_and_execution_sequence.md) to v2.1.0.
* **SDD Master Index**: Updated [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md).
* **Living Documentation Registries**: Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).
* **Sign-Off Status**: `ACCEPTED & SYNCHRONIZED`
