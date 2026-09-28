<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# SDD 07: Living Documentation Index & Architectural Decision Records (ADRs)
> **Specification Version**: `v2.1.0 (Unified ADR Architecture)`  
> **Pattern**: Distributed Atomic ADR Fragment Pattern (Zero Merge Conflicts & Zero Token Waste)  
> **Authoritative ADR Registry**: [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md)  
> **Mandate**: Zero Documentation Drift Mandate & Token Economics Standard  

---

## 1. Unified Living Architecture Overview

To achieve **zero Git merge conflicts** in multi-branch/stacked PR workflows and **conserve context window tokens** for agentic AI development, all architectural decisions, technology evolutions, and living changes are unified into the dedicated **`docs/adr/`** directory.

1. **Dedicated ADR Repository & Registry**:
   - The central index, lifecycle rules, and templates reside in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).
2. **Atomic ADR Fragments (`docs/adr/`)**:
   - For every new feature, architectural milestone, or major change, agents and contributors create a **single, standalone file** inside [`docs/adr/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/):
     `docs/adr/ADR-<YYYYMMDD>-<NUMBER>-<slug>.md`
   - **Zero Merge Conflicts**: Because each PR creates an independent file, concurrent and stacked PRs can be merged and rebased without conflicting on a monolithic log.
   - **Zero String-Replace Failures**: Agents use `write_to_file` to create atomic records in a single tool call, avoiding fragile line-offset math and chunk-matching retries.
   - **Token Economy**: Stored in `docs/adr/` (outside `.agents/skills/`), consuming **0 baseline prompt tokens**. Agents read and write only the small, relevant record (~350 tokens) rather than an entire monolithic history.
3. **Historical Decision Archive**:
   - Historical entries (`LOG-001` through `LOG-042`) covering early inception through mobile strategy are preserved with complete data integrity in [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md).

---

## 2. Standardized ADR Fragment Template

Every new decision record in [`docs/adr/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/) follows the MADR-aligned atomic template specified in [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).

---

## 3. Active ADR Registry Summary

The active register is canonically maintained in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md):

| ADR ID | Title & Focus Subsystem | Status | Canonical Path |
| :--- | :--- | :--- | :--- |
| **ADR-040** | Token Economics & SDD vs Skill Separation Standard | `ACCEPTED` | [`ADR-20260926-040-token-economics-standard.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-040-token-economics-standard.md) |
| **ADR-041** | Architectural Verification: Web App Plan Retention in Clean Architecture Skill | `ACCEPTED` | [`ADR-20260926-041-web-bff-retention.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-041-web-bff-retention.md) |
| **ADR-042** | Agentic Memory Persistence: Web App Plan Codification Standard | `ACCEPTED` | [`ADR-20260926-042-web-app-plan-codification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-042-web-app-plan-codification.md) |
| **ADR-043** | Transition to Distributed Fragment Pattern for Living SDD | `ACCEPTED` | [`ADR-20260926-043-living-doc-fragment-pattern.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-043-living-doc-fragment-pattern.md) |
| **ADR-044** | Enterprise Database Architecture & Azure Cloud Persistence | `ACCEPTED` | [`ADR-20260926-044-enterprise-database-architecture-skill.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-044-enterprise-database-architecture-skill.md) |
| **ADR-045** | Master Implementation Roadmap & Skill-by-Skill Execution Sequencing | `ACCEPTED` | [`ADR-20260926-045-master-implementation-roadmap.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-045-master-implementation-roadmap.md) |
| **ADR-046** | License Governance, Dual AGPLv3/SSPL v1 Compliance & Header Enforcement | `ACCEPTED` | [`ADR-20260926-046-license-governance-and-header-enforcement.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260926-046-license-governance-and-header-enforcement.md) |
| **ADR-047** | Dedicated ADR Directory & Single Unified ADR Architecture | `ACCEPTED` | [`ADR-20260928-047-dedicated-adr-architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260928-047-dedicated-adr-architecture.md) |

---

## 4. Archive Index

| Archive Ledger | Coverage | Entries | Path |
| :--- | :--- | :--- | :--- |
| **September 2026 Archive** | Initial bootstrap through mobile strategy | LOG-001 to LOG-042 | [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md) |
