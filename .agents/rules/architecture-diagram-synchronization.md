<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Architecture Diagram Synchronization & Solution Alignment Rules

This rule defines mandatory engineering and agentic workflows for maintaining, synchronizing, and validating architecture diagrams across the **Diet-Dost** repository.

> [!IMPORTANT]
> **Zero Documentation Drift Mandate**: Whenever modifying or introducing new architectural components, persistence models, networking boundaries, CQRS handlers, controllers, or test suites, the canonical Mermaid diagrams in `docs/architecture/diagrams/*.mermaid` and embedded documentation in `README.md` and `docs/sdd/*.md` **MUST** be updated and synchronized immediately.

---

## 1. Authoritative Diagram Hierarchy

The repository maintains canonical single-source-of-truth Mermaid diagrams under [`docs/architecture/diagrams/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/):

1. **[`solution_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/solution_architecture.mermaid)**:
   - Master Solution Architecture across all 8 layers:
     - Layer 0: CI/CD & 8-Job Pre-Deployment Security Gate
     - Layer 1: Presentation Layer (Linear Obsidian PWA, DPDPA 2023 Auth Gate, Dynamic Badges)
     - Layer 2: Security & Boundary Defense (OWASP ASVS, PromptShield, Debug Isolation)
     - Layer 3: Presentation Gateway & BFF (Thin Controllers, Web BFF Composite, Mobile BFF Facade)
     - Layer 4: Application Layer (Native Zero-Dependency CQRS Pipeline, Port Abstractions)
     - Layer 5: Domain Core Layer (Aggregates, Clinical ICMR-NIN 2024 & WHO Safeties)
     - Layer 6: Infrastructure & Zero-Trust Cloud (NSG, ACA MicroVM, SMB 3.1.1, Key Vault)
     - Layer 7: DevOps & Observability (Aspire AppHost, OTel Spans, 242 Tests, Domain ADRs)
2. **[`security_boundary.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/security_boundary.mermaid)**:
   - Security Perimeter, Zero-Trust Subnet Isolation (NSG HTTPS 443), SMB 3.1.1 AES-GCM Wire Encryption, PromptShield, and Key Vault Audit Diagnostics.
3. **[`azure_zero_trust_infra_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/azure_zero_trust_infra_architecture.mermaid)**:
   - Azure VNet (10.0.0.0/16), Delegated Subnet (`snet-aca-infra`), NSG (`nsg-dietdost-dev`), Checkov CKV_AZURE_9 and CKV_AZURE_160 compliance, ACA Ingress, SMB 3.1.1 mount, Key Vault diagnostics, and threat metric alerts.
4. **[`devops_observability.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/devops_observability.mermaid)**:
   - CI/CD 3-stage flow, OTel pipeline, Aspire Dashboard, and 242 automated test harnesses.
5. **[`frontend_modular_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/frontend_modular_architecture.mermaid)**:
   - Native HTML5 partials loader, Vanilla ES Modules, ServiceContainer IoC, and EventBus.
6. **[`functional_meal_flow.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/functional_meal_flow.mermaid)**:
   - Photo ingestion, PromptShield validation, Microsoft Agent Framework cascade, confidence gating (>=70%), and portion review.

---

## 2. ADR-086 Mermaid Syntax Invariants

Every `.mermaid` file in this repository **MUST** conform to these syntax rules:
1. **Line 1 Root Directive**: Line 1 **MUST** begin directly with the Mermaid diagram declaration (e.g. `graph TB`, `graph TD`, `graph LR`, `sequenceDiagram`, `flowchart TD`). No blank lines, HTML comments, or markdown formatting are permitted on Line 1.
2. **Line 2 License Block**: The dual AGPLv3 / SSPL v1 license header must be embedded on lines 2–8 using Mermaid comment syntax (`%%`).
3. **Balanced Brackets**: All node labels with special characters (brackets, parentheses, commas) must use double quotes (e.g., `id["Label (Details)"]`).
4. **No HTML Tags in Labels**: Use `<br/>` for line breaks inside double-quoted node labels. Do not use raw HTML block elements (`<div>`, `<span>`, `<p>`).

---

## 3. Automated Synchronization & Validation Runbook

The repository provides automated tooling to validate and synchronize diagrams with 0 manual copy-pasting:

### 3.1 Auto-Synchronize Embedded Documentation
When code changes affect architecture, update the canonical diagram in `docs/architecture/diagrams/<name>.mermaid`, then run:
```powershell
pwsh -File scripts/sync-architecture-diagrams.ps1 -Sync
```
*What this does*: Automatically updates the embedded Mermaid diagrams in `README.md` and `docs/sdd/02_solution_architecture.md`, guaranteeing identical diagram definitions across the codebase.

### 3.2 Verify Alignment (CI Gate & Pre-Commit)
Before committing, verify that diagrams are syntactically valid and aligned with solution reality:
```powershell
pwsh -File scripts/sync-architecture-diagrams.ps1 -Verify
```
*Verification Checks*:
- Validates Line 1 root directive and bracket balancing across all `.mermaid` files.
- Asserts presence of required solution components (Zero-Trust NSG, SMB 3.1.1 encryption, Key Vault audit streaming, Web BFF composite hydration, Mobile BFF readiness, 242 automated tests).
- Detects drift between canonical files and embedded markdown diagrams.

---

## 4. Agentic Workflow Mandate for AI Agents

> [!CAUTION]
> **Strict Agent Enforcement**: Any AI agent modifying code in `src/`, `infra/`, or `tests/` MUST perform the following closed-loop actions before creating a pull request:
> 1. **Assess Architectural Impact**: If adding/renaming a controller, service, port, adapter, Bicep resource, or database model, determine which canonical diagram(s) must reflect the change.
> 2. **Update Canonical Diagram**: Surgically edit `docs/architecture/diagrams/*.mermaid` using `replace_file_content`. Preserve Line-1 directives.
> 3. **Execute Auto-Sync**: Run `pwsh -File scripts/sync-architecture-diagrams.ps1 -Sync` to propagate changes into `README.md` and SDDs.
> 4. **Run Security & Defense Validation**: Run `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode Staged` (Vector 6 enforces diagram synchronization).
> 5. **Record ADR**: If the change represents an architectural milestone, document an ADR in `docs/adr/<domain>/` and run `pwsh -File scripts/sync-adr-index.ps1`.
