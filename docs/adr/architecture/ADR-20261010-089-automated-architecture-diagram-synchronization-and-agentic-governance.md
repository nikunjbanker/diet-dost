<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261010-089: Automated Architecture Diagram Synchronization, Semantic Solution Alignment, and Agentic Governance

- **Status**: Accepted
- **Date**: 2026-10-10
- **Domain**: Architecture & Solution Governance
- **Author**: Diet-Dost Core Architectural Pair
- **Decision Makers**: `@nikunjbanker` (Sole Authority)

## 1. Context and Problem Statement
As Diet-Dost rapidly scales across Clean Architecture CQRS slices, Azure cloud infrastructure (Container Apps, SMB 3.1.1 mounts, Zero-Trust NSGs), Web/Mobile BFFs, and multi-tier verification suites, a critical maintenance challenge emerged:
1. **Multi-File Documentation Drift**: Master diagrams exist both as canonical source files in `docs/architecture/diagrams/*.mermaid` and embedded blocks inside `README.md` and `docs/sdd/*.md`. When solution architecture evolves, embedded diagrams easily fall out of sync, displaying obsolete topologies or missing new layers.
2. **Mermaid Syntax Fragility (ADR-086)**: Mermaid parser crashes occur when blank lines or comments precede the root directive on line 1, or when bracket balancing errors are introduced.
3. **Semantic Drift Risk**: Code changes (e.g. adding NSGs, new controllers, or test count updates) occur without corresponding diagram updates, degrading architectural transparency for new contributors and AI agents.
4. **Agentic Knowledge Loss**: Without an automated synchronizer and explicit agentic instructions, AI coding agents fail to update diagrams during feature implementations.

## 2. Decision and Implementation

### 2.1 Canonical Source of Truth Pattern
All architectural diagrams are strictly authored in canonical source files under [`docs/architecture/diagrams/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/):
- `solution_architecture.mermaid`: Master 8-Layer Solution Architecture (CI/CD, Presentation, Security, Gateway/BFF, Application CQRS, Domain Core, Infrastructure/Cloud, Orchestration/Observability).
- `security_boundary.mermaid`: Defense-in-depth security perimeter, NSG, SMB 3.1.1 encryption, and Key Vault diagnostics.
- `azure_zero_trust_infra_architecture.mermaid`: VNet, delegated subnet, NSG stateful rules (Checkov CKV_AZURE_9 & CKV_AZURE_160), ACA ingress, and persistence mounts.
- `devops_observability.mermaid`: CI/CD 3-stage flow, OTel pipeline, Aspire Dashboard, and 242 tests.
- `frontend_modular_architecture.mermaid`: HTML5 partials loader, Vanilla ES Modules, ServiceContainer IoC, and EventBus.
- `functional_meal_flow.mermaid`: Meal ingestion, PromptShield validation, Microsoft Agent cascade, and confidence gating.

### 2.2 Automated Architecture Diagram Synchronizer (`scripts/sync-architecture-diagrams.ps1` / `.py`)
Implemented a dedicated synchronization and validation engine:
- **Mermaid Syntax & ADR-086 Linter**: Enforces Line-1 root directive (`graph TB`, `sequenceDiagram`, etc.) and bracket balancing.
- **Semantic Alignment Checker**: Verifies that canonical diagrams accurately represent core solution components (Zero-Trust NSG, SMB 3.1.1 AES-GCM encryption, Key Vault audit diagnostics, Web/Mobile BFF, 242 automated tests).
- **Automated Document Synchronization (`-Sync` / `--sync`)**: Surgically updates embedded Mermaid blocks in `README.md` and `docs/sdd/02_solution_architecture.md` from canonical files.
- **Verification Mode (`-Verify` / `--verify`)**: Dry-run check for CI/CD and pre-commit hooks, exiting 0 on full alignment and 1 on drift.

### 2.3 Pre-Commit & CI Defense Integration (Vector 6)
Wired diagram verification directly into `scripts/verify-ai-security-defense.ps1` as **Vector 6: Living Architecture Diagram Synchronization & Solution Alignment**. Any diagram drift or syntax failure immediately halts the Git pre-commit hook and blocks the 8-job GitHub Actions security gate (`security-scan.yml`).

### 2.4 Agentic Governance & Rule Codification
- Created [`.agents/rules/architecture-diagram-synchronization.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/rules/architecture-diagram-synchronization.md).
- Updated [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md) with **Rule 21: Mandatory Architecture Diagram Auto-Sync & Living Alignment Rule**.
- Updated [`CLAUDE.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/CLAUDE.md) with CLI commands and architecture invariants.

## 3. Consequences and Benefits
- **Zero Documentation Drift**: Documentation diagrams and canonical `.mermaid` files are guaranteed to remain 100% identical.
- **Automated Developer & Agent Ergonomics**: Running `pwsh -File scripts/sync-architecture-diagrams.ps1 -Sync` performs full synchronization in < 1 second.
- **Fail-Safe CI Protection**: Any pull request that introduces architectural drift or broken Mermaid syntax is blocked before merge.

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile Modernization**: When the MAUI / React Native client and Mobile BFF endpoints are expanded, `sync-architecture-diagrams.py` will assert that Mobile BFF components are registered across diagrams.
- **Phase 3 Enterprise Cloud DB**: Transitioning from SQLite to Azure SQL Serverless will be automatically tracked and verified across all diagrams.
