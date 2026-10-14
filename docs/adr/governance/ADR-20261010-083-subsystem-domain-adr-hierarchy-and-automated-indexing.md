<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261010-083: Subsystem Domain ADR Hierarchy, Automated Indexing Engine & Table Deduplication

> **Date**: 2026-10-10  
> **Status**: ACCEPTED  
> **Driver / Deciders**: AI Assistant & User Pair-Programming  
> **Change Type**: `[GOVERNANCE]`, `[ARCHITECTURE]`, `[TOKEN_ECONOMICS]`  
> **Affected Subsystems**: Architecture Documentation (`docs/adr/`), Living SDDs (`docs/sdd/07_living_documentation_log.md`), Governance (`AGENTS.md`)  
> **Associated PR & Stack**: PR #53 (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md)  

---

## 1. Executive Summary & Change Rationale

* **Problem**: The root of `docs/adr/` grew to 43+ atomic markdown files, accompanied by two monolithic 43-row Markdown tables maintained manually in both `docs/adr/README.md` (20 KB) and `docs/sdd/07_living_documentation_log.md` (19.5 KB). This caused file sprawl, context-window token bloat, double-bookkeeping, and Git merge-conflict risks across stacked PRs.
* **Solution**: Adopted the **Subsystem / Domain-Driven Hierarchy** standard. Partitioned all ADRs into 5 cohesive domain subdirectories (`architecture/`, `security/`, `devops/`, `presentation/`, `governance/`). Implemented an automated indexing engine (`scripts/sync-adr-index.py` & `.ps1`) that extracts structured metadata, generates a token-efficient `docs/adr/index.json`, dynamically renders modular per-category tables into `docs/adr/README.md`, and replaces duplicate tables in `docs/sdd/07_living_documentation_log.md` with an authoritative reference pointer.

---

## 2. Context and Problem Statement

As Diet-Dost expanded through Phase 1 MVP, responsive design, containerization, security hardening, and AI defense, the volume of architectural decision records grew rapidly from ADR-040 through ADR-082.

Tracking these decisions in a single flat directory caused several structural and operational bottlenecks:
1. **Flat Directory Clutter**: Over 45 files in the root of `docs/adr/` made browsing and file inspection cumbersome for developers and AI agents.
2. **Context-Window Token Blowup**: Reading `docs/adr/README.md` burned ~6,000–8,000 tokens just to view the index table, approaching tool buffer limits.
3. **Double-Bookkeeping Duplication**: Every new ADR required manual row additions to both `docs/adr/README.md` and `docs/sdd/07_living_documentation_log.md`. Concurrent and stacked PRs editing the bottom of these tables risked merge conflicts upon rebase.
4. **Lack of Machine-Readable Index**: AI agents had no structured way to filter or query ADRs by subsystem, status, or date without scanning all files.

---

## 3. Decision Drivers

* **Domain Partitioning**: Organize decisions logically by architectural boundaries to reflect Clean Architecture, Security, DevOps, Presentation, and Governance.
* **Zero Baseline System Prompt Tokens**: ADRs must remain outside `.agents/skills/`, consuming 0 system prompt tokens.
* **100% PR Merge-Conflict Immunity**: Prevent concurrent PRs from conflicting on manual markdown tables by introducing automated generation and atomic domain placement.
* **Agentic Discoverability & Token Efficiency**: Provide a lightweight `index.json` (~4 KB) allowing agents to search all decisions in <600 tokens.
* **DRY Living Documentation**: Eliminate duplicate tables between `docs/adr/README.md` and `docs/sdd/07_living_documentation_log.md`.

---

## 4. Considered Options

* **Option 1 (Flat Directory with Manual Maintenance)**: Keep all ADRs flat in `docs/adr/` and continue manually editing markdown tables.
  - *Cons*: Severe directory clutter, token waste, and recurring merge conflicts as ADR count scales past 100.
* **Option 2 (Milestone / Phased Chronological Partitioning)**: Group ADRs into `phase-1-mvp/`, `phase-2-mobile/`, etc.
  - *Cons*: Decisions spanning multiple phases (e.g. security governance) become fragmented across time buckets; difficult to locate all security policies in one place.
* **Option 3 (Chosen: Subsystem / Domain-Driven Hierarchy + Automated Indexing)**:
  - Partition ADRs into 5 authoritative domain directories: `architecture/`, `security/`, `devops/`, `presentation/`, `governance/`.
  - Develop `scripts/sync-adr-index.py` & `sync-adr-index.ps1` to crawl all ADRs, produce `docs/adr/index.json`, and render modular tables in `docs/adr/README.md`.
  - Streamline `docs/sdd/07_living_documentation_log.md` into a lean architectural status report referencing the central ADR registry.

---

## 5. Decision Outcome

* **Chosen Option**: **Option 3: Subsystem / Domain-Driven Hierarchy + Automated Indexing Engine**
* **Domain Subsystem Partitioning**:
  1. `docs/adr/architecture/`: Clean Architecture, CQRS, Database persistence, Options Pattern, BFF facades.
  2. `docs/adr/security/`: Authentication, RBAC, Key Vault secrets, OWASP LLM defense, CodeQL/SAST gates.
  3. `docs/adr/devops/`: Azure Container Apps, .NET Aspire deployment, SQLite SMB persistence, GitHub OIDC pipelines.
  4. `docs/adr/presentation/`: Client hydration, responsive mobile/tablet layouts, touch ergonomics, modals.
  5. `docs/adr/governance/`: Token economics, Stacked PR protocols, IDD workflows, licensing standards.

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* **Immediate De-cluttering**: The root of `docs/adr/` now contains only `README.md`, `template.md`, and clean domain subdirectories.
* **Zero-Effort Indexing**: Developers and agents simply write atomic files `docs/adr/<domain>/ADR-...md` and run `pwsh -File scripts/sync-adr-index.ps1` to update `index.json` and `README.md`.
* **85% Token Reduction for Agent Queries**: Agents can read `docs/adr/index.json` (~4 KB) to instantly pinpoint decisions without loading full markdown tables.
* **100% Elimination of Double-Bookkeeping**: `docs/sdd/07_living_documentation_log.md` no longer maintains an independent duplicate table.
* **Git History Preserved**: All 43 historical files were migrated using `git mv`, preserving 100% commit history and blame.

### Negative Consequences / Accepted Trade-Offs:
* Future ADR creation requires choosing the relevant subsystem directory (`architecture/`, `security/`, `devops/`, `presentation/`, `governance/`). If in doubt, `architecture/` or `governance/` acts as the natural home.

---

## 7. Verification & Compliance Results

* **Migration Integrity**: All 43 existing ADRs (ADR-040 through ADR-082) successfully relocated into domain subdirectories via `git mv`.
* **Engine Execution**: `scripts/sync-adr-index.py` executed successfully, generating valid `docs/adr/index.json` and modular `docs/adr/README.md`.
* **Verification**: `pwsh -File scripts/sync-adr-index.ps1` completes cleanly.
* **Solution Integrity**: `dotnet build DietDost.slnx` passes with 0 warnings and 0 errors.

---

## 8. Forward Roadmap Impact & Future Phase Compatibility

* **Phase 2 Mobile & Phase 3 Cloud Compatibility**: Mobile BFF decisions will cleanly reside in `docs/adr/presentation/` or `architecture/`, and Azure SQL migrations in `docs/adr/architecture/` or `devops/`, scaling seamlessly to hundreds of ADRs without directory sprawl.
* **Automated CI Sync**: The synchronization engine can run as part of pre-commit or CI linting, preventing unindexed ADR drift.
