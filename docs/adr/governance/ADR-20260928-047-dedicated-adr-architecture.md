<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260928-047: Dedicated ADR Directory & Single Unified Architecture Decision Record System

> **Date**: 2026-09-28  
> **Status**: ACCEPTED  
> **Driver / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[ARCHITECTURE]`, `[GOVERNANCE]`, `[TOKEN_ECONOMICS]`  
> **Affected Subsystems**: Architecture Documentation (`docs/adr/`), Living SDDs (`docs/sdd/`), Governance (`AGENTS.md`)  
> **Associated PR & Stack**: PR #22 (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md)  

---

## 1. Context and Problem Statement

Architectural decisions in Diet-Dost were previously tracked under `docs/sdd/logs/` (and historical logs in `docs/sdd/archive/`) alongside the core Software Design Documents (SDDs 00-09).

While this preserved the fragment pattern and token efficiency, the project lacked a standard, dedicated `docs/adr/` directory expected in enterprise software engineering. Additionally, introducing an ADR directory risked data duplication or token inflation if decision records were duplicated between `docs/sdd/logs/` and `docs/adr/`.

The problem was: How to establish a dedicated, industry-standard `docs/adr/` architecture that guarantees:
1. **Zero Duplication**: Exactly one authoritative location and format for all architectural decisions.
2. **Zero Baseline Token Overhead**: No increase in agent system prompt tokens.
3. **100% Git Merge-Conflict Immunity**: Preservation of the atomic fragment pattern for concurrent and stacked PR workflows.

---

## 2. Decision Drivers

* **Zero Duplication Mandate**: Eliminate duplicate copies of decision documents across the codebase.
* **Token Economics Standard**: System prompt token consumption must remain 0 baseline tokens (never placing decision documents inside `.agents/skills/`).
* **PR Merge-Conflict Immunity**: Avoid monolithic files that cause Git merge conflicts during rebase across stacked feature branches.
* **Developer Ergonomics**: Provide an industry-standard `docs/adr/` structure with a standard MADR template.

---

## 3. Considered Options

* **Option 1 (Dual System - Reference Registry)**: Retain `docs/sdd/logs/` and create `docs/adr/README.md` as an external index pointing to them without moving files.
  - *Cons*: Two different directories involved in architectural records; confusion over where new decisions belong.
* **Option 2 (Single Unified ADR Architecture in `docs/adr/`)**:
  - Migrate all active atomic fragments from `docs/sdd/logs/` to `docs/adr/ADR-YYYYMMDD-NNN-<slug>.md`.
  - Relocate historical archive to `docs/adr/archive/`.
  - Retire `docs/sdd/logs/` completely to ensure zero duplicated files or data.
  - Update all pointers (`07_living_documentation_log.md`, `00_sdd_index.md`, `AGENTS.md`) to point to `docs/adr/`.
  - Provide a standardized MADR template in `docs/adr/template.md`.

---

## 4. Decision Outcome

* **Chosen Option**: **Option 2 (Single Unified ADR Architecture in `docs/adr/`)**
* **Justification**:
  - Option 2 provides a clean, single source of truth.
  - Zero duplicated files exist; `docs/sdd/logs/` is retired.
  - Preserves 100% data integrity and traceability for all existing records (ADR-040 through ADR-046, and archive LOG-001 through LOG-042).
  - Guarantees 0 baseline tokens because `docs/adr/` lives in `docs/`, loaded strictly on-demand.
  - Preserves atomic fragment naming (`ADR-YYYYMMDD-NNN-<slug>.md`) guaranteeing PR merge-conflict immunity.

---

## 5. Consequences & Trade-Offs

### Positive Consequences:
* **Single Authoritative Destination**: All architectural decisions reside in `docs/adr/`.
* **Zero Duplication**: No duplicate files or redundant logging mechanisms.
* **Zero Token Inflation**: 0 baseline tokens injected into agent system prompts.
* **Standardized Format**: New ADRs follow the unified MADR template with clear lifecycle statuses (`ACCEPTED`, `PROPOSED`, `SUPERSEDED`, `DEPRECATED`).
* **Deterministic Tool Operations**: Small individual files prevent tool buffer truncations (>46 KB).

### Negative Consequences / Accepted Trade-Offs:
* Existing external links referencing `docs/sdd/logs/` must be updated to `docs/adr/`. (Fully remediated across repository documentation and skills).

---

## 6. Verification & Compliance

* **Automated Builds & Tests**:
  - `dotnet build --configuration Release`: 0 warnings, 0 errors.
  - `dotnet test --configuration Release`: 129 passed, 0 failed.
* **File Integrity**:
  - 7 active ADRs cleanly relocated to `docs/adr/`.
  - Historical archive (244 KB) relocated to `docs/adr/archive/` with 100% checksum match.
  - `docs/sdd/logs/` completely deleted with 0 orphan files.
* **Living Documentation Synchronization**:
  - Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md).
  - Cross-linked from [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).
