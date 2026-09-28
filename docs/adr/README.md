<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Architectural Decision Records (ADRs) - Diet-Dost
> **Classification**: Authoritative Architectural Decision Record (ADR) Registry & Standards  
> **Pattern**: Distributed Atomic Fragment Pattern (100% PR Merge-Conflict Immunity & 0 Baseline System Tokens)  
> **Governance**: Dual AGPLv3 / SSPL v1, DPDPA 2023, ICMR-NIN 2024 Clinical Standards  

---

## 1. Overview & Strategy

This directory serves as the **single authoritative repository** for all Architectural Decision Records (ADRs) within the **Diet-Dost** solution.

To prevent documentation sprawl and eliminate duplicate data, all architectural decisions, technology evaluations, and living architectural modifications are standardized here using the **Atomic Fragment Pattern**.

### Why Atomic ADR Fragments?
1. **Zero System Prompt Token Overhead**: Stored in `docs/adr/`, records consume **0 baseline tokens** in agent system prompts. They are loaded strictly on-demand via `view_file` only when relevant to a specific task.
2. **100% Git Merge-Conflict Immunity**: Each architectural change or PR introduces a dedicated, standalone file (`ADR-YYYYMMDD-NNN-<slug>.md`). Multiple stacked or concurrent PRs can be merged or rebased without touching a single monolithic changelog.
3. **Single Source of Truth**: There are no duplicate documents or mirrored logs. `docs/adr/` is the sole destination for architectural decision records.

---

## 2. ADR Lifecycle & Statuses

Each ADR documents a distinct architectural decision and maintains one of the following statuses:

| Status | Meaning |
| :--- | :--- |
| `PROPOSED` | Under active review or prototyping; awaiting confirmation. |
| `ACCEPTED` | Approved, active, and binding across the codebase. |
| `SUPERSEDED` | Replaced by a subsequent ADR (must link to replacing ADR). |
| `DEPRECATED` | No longer applicable due to decommissioned components. |

---

## 3. Active ADR Registry

| ADR ID | Title & Focus Subsystem | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-040** | Token Economics & SDD vs Skill Separation Standard | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260926-040-token-economics-standard.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-040-token-economics-standard.md) |
| **ADR-041** | Architectural Verification: Web App Plan Retention in Clean Architecture Skill | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260926-041-web-bff-retention.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-041-web-bff-retention.md) |
| **ADR-042** | Agentic Memory Persistence: Web App Plan Codification Standard | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260926-042-web-app-plan-codification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-042-web-app-plan-codification.md) |
| **ADR-043** | Transition to Distributed Fragment Pattern for Living SDD | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260926-043-living-doc-fragment-pattern.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-043-living-doc-fragment-pattern.md) |
| **ADR-044** | Enterprise Database Architecture & Azure Cloud Persistence | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260926-044-enterprise-database-architecture-skill.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-044-enterprise-database-architecture-skill.md) |
| **ADR-045** | Master Implementation Roadmap & Skill-by-Skill Execution Sequencing | `[ROADMAP]` | `ACCEPTED` | [`ADR-20260926-045-master-implementation-roadmap.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-045-master-implementation-roadmap.md) |
| **ADR-046** | License Governance, Dual AGPLv3/SSPL v1 Compliance & Solution-Wide Header Enforcement | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260926-046-license-governance-and-header-enforcement.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-046-license-governance-and-header-enforcement.md) |
| **ADR-047** | Dedicated ADR Directory & Single Unified ADR Architecture | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260928-047-dedicated-adr-architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260928-047-dedicated-adr-architecture.md) |

---

## 4. Historical Decision Archive

Historical decision entries from the initial project inception through the mobile strategy (`LOG-001` through `LOG-042`) are preserved with complete data integrity in the historical archive:

* **Historical Archive**: [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md)

---

## 5. How to Propose or Author a New ADR

1. Copy the standard template from [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).
2. Create a new atomic file named:
   `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md`
3. Document the context, options considered, final decision rationale, and verification results.
4. Register the new ADR in this [`README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) table under Section 3.
