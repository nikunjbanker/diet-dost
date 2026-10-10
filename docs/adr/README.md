<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Architectural Decision Records (ADRs) - Diet-Dost
> **Classification**: Authoritative Architectural Decision Record (ADR) Registry & Standards  
> **Pattern**: Subsystem / Domain-Driven Atomic Fragment Pattern (Zero Merge Conflicts & Zero Baseline System Tokens)  
> **Total Decisions**: 44 Active Records Across 5 Domain Hierarchies  
> **Machine-Readable Registry**: [`docs/adr/index.json`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/index.json)  
> **Governance**: Dual AGPLv3 / SSPL v1, DPDPA 2023, ICMR-NIN 2024 Clinical Standards  

---

## 1. Architecture Overview & Subsystem Organization

To ensure high maintainability, rapid discoverability, and zero merge conflicts, all Architectural Decision Records in **Diet-Dost** are organized into **Domain / Subsystem Subdirectories**.

### Key Principles:
1. **Zero System Prompt Token Overhead**: Stored in `docs/adr/<category>/`, records consume **0 baseline tokens** in agent system prompts and are loaded strictly on-demand via `view_file`.
2. **Domain Partitioning**: Decisions are partitioned into clean, cohesive domains (`architecture/`, `security/`, `devops/`, `presentation/`, `governance/`), capping directory sizes and keeping navigation intuitive.
3. **100% PR Merge-Conflict Immunity**: Each decision introduces a dedicated, standalone file (`ADR-YYYYMMDD-NNN-<slug>.md`). Stacked or concurrent PRs never conflict on a single log file.
4. **Automated Machine-Readable Indexing**: `docs/adr/index.json` and this document are automatically synchronized via `scripts/sync-adr-index.py`, eliminating manual Markdown table editing errors.

---

## 2. Domain Subsystems Summary

| Subsystem Domain | Description | Total Decisions | Directory |
| :--- | :--- | :--- | :--- |
| **🏛️ Architecture & Domain Logic** | Clean Architecture, CQRS, Database persistence, Options Pattern, and BFF facade specifications. | **13** | [`docs/adr/architecture/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/) |
| **🛡️ Security & Identity** | Authentication, authorization, RBAC, Key Vault secrets, OWASP LLM defense, and code scanning gates. | **12** | [`docs/adr/security/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/) |
| **🚀 DevOps & Cloud Infrastructure** | Azure Container Apps, .NET Aspire deployment, SQLite SMB persistence, and GitHub OIDC CI/CD pipelines. | **3** | [`docs/adr/devops/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/) |
| **🎨 Presentation & UI/UX** | Client hydration, responsive mobile/tablet layout, touch ergonomics, and modal orchestration. | **4** | [`docs/adr/presentation/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/) |
| **⚖️ Governance, Standards & Agentic Workflows** | Token economics, GitHub Stacked PR protocol, ADR architecture, issue-driven workflow, and licensing. | **12** | [`docs/adr/governance/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/) |

---

## 3. Active ADR Registry by Domain

### 🏛️ Architecture & Domain Logic (`docs/adr/architecture/`)
> *Clean Architecture, CQRS, Database persistence, Options Pattern, and BFF facade specifications.*

| ADR ID | Title | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-041** | Architectural Verification: Web App Plan Retention in Clean Architecture Skill | `[ARCHITECTURE_VERIFICATION]` | `ACCEPTED` | [`ADR-20260926-041-web-bff-retention.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20260926-041-web-bff-retention.md) |
| **ADR-044** | Enterprise Database Architecture & Azure Cloud Persistence | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260926-044-enterprise-database-architecture-skill.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20260926-044-enterprise-database-architecture-skill.md) |
| **ADR-053** | Web BFF Composite Endpoint & Shared Application CQRS Queries | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260930-053-web-bff-composite-endpoint-and-shared-cqrs-queries.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20260930-053-web-bff-composite-endpoint-and-shared-cqrs-queries.md) |
| **ADR-054** | Shared CQRS Query Consolidation & Native Dispatcher Integration | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261001-054-shared-cqrs-query-consolidation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261001-054-shared-cqrs-query-consolidation.md) |
| **ADR-057** | Centralize Food Estimation API & Eliminate Static Client Dictionary | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-057-centralize-food-estimation-api.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261002-057-centralize-food-estimation-api.md) |
| **ADR-058** | Deconstruct Monolithic UI Controller into Single Responsibility (SRP) ES Modules | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-058-modularize-ui-controllers.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261002-058-modularize-ui-controllers.md) |
| **ADR-059** | Phase 2 Two-Stage Responsive Web & Mobile Readiness Architecture | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-059-phase-2-responsive-and-mobile-readiness-architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261002-059-phase-2-responsive-and-mobile-readiness-architecture.md) |
| **ADR-062** | Mobile BFF Composite Contracts, ETag Caching & Offline Idempotency | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-062-mobile-bff-composite-contracts-and-caching.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261002-062-mobile-bff-composite-contracts-and-caching.md) |
| **ADR-063** | Native Feature Classification Matrix & Responsive Exit Gate Signoff | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-063-native-feature-classification-and-responsive-exit-gate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261002-063-native-feature-classification-and-responsive-exit-gate.md) |
| **ADR-072** | Centralized Application Configuration, Secret Governance & Strongly-Typed Ports | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md) |
| **ADR-073** | Strongly-Typed Options Pattern & FluentValidation Configuration Governance | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-073-options-pattern-and-fluentvalidation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261008-073-options-pattern-and-fluentvalidation.md) |
| **ADR-075** | Complete Elimination of Raw Configuration Indexers in Favor of Strongly-Typed Options Classes | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-075-complete-elimination-of-raw-configuration-indexers-for-options-classes.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261008-075-complete-elimination-of-raw-configuration-indexers-for-options-classes.md) |
| **ADR-076** | Codification of Configuration, Options Pattern & Secret Management Architecture SDD | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261009-076-configuration-architecture-sdd-and-process-boundary-specification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261009-076-configuration-architecture-sdd-and-process-boundary-specification.md) |

### 🛡️ Security & Identity (`docs/adr/security/`)
> *Authentication, authorization, RBAC, Key Vault secrets, OWASP LLM defense, and code scanning gates.*

| ADR ID | Title | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-050** | CODEOWNER Gating & Approval Policy for Autonomous Issue-Driven Development | `[SECURITY]` | `ACCEPTED` | [`ADR-20260928-050-codeowner-approval-issue-workflow.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20260928-050-codeowner-approval-issue-workflow.md) |
| **ADR-056** | Gated Premium Feature Button State & Underlying API Defense | `[DEFECT_FIX]` | `ACCEPTED` | [`ADR-20261001-056-gated-feature-button-state-and-api-defense.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261001-056-gated-feature-button-state-and-api-defense.md) |
| **ADR-064** | Disable Public User Sign-Up and Codify DESIGN.md UI/UX Authority | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-064-disable-public-signup-and-codify-design-system-authority.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261003-064-disable-public-signup-and-codify-design-system-authority.md) |
| **ADR-066** | SuperAdmin-Only Tier Configuration Governance & Authorization | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-066-superadmin-tier-governance-authorization.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261003-066-superadmin-tier-governance-authorization.md) |
| **ADR-069** | Released Environment Privileged Demo User Prohibition & Showcase End-User Tier Isolation | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-069-released-environment-privileged-demo-user-prohibition.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261003-069-released-environment-privileged-demo-user-prohibition.md) |
| **ADR-071** | Azure Key Vault Secret Governance and .NET Aspire Integration in Deployed Environments | `[SECURITY]` | `ACCEPTED` | [`ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md) |
| **ADR-077** | Secret-Only Governance for SuperAdmin Email and Mobile Verification | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-077-secret-only-governance-for-superadmin-email-and-mobile-verification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-077-secret-only-governance-for-superadmin-email-and-mobile-verification.md) |
| **ADR-078** | Sole-Authority Governance for Secrets and Environment Variables Restricted to @nikunjbanker | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-078-sole-authority-governance-for-secrets-and-environment-variables.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-078-sole-authority-governance-for-secrets-and-environment-variables.md) |
| **ADR-079** | Remediation of CodeQL Code Scanning Security Alerts | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-079-remediation-of-codeql-code-scanning-security-alerts.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-079-remediation-of-codeql-code-scanning-security-alerts.md) |
| **ADR-080** | ESLint Code Scanning Workflow and SARIF Integration | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-080-eslint-code-scanning-workflow-and-sarif-integration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-080-eslint-code-scanning-workflow-and-sarif-integration.md) |
| **ADR-081** | Git Code Scanning, PR Validation, and Pre-Deployment Security Gates | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-081-git-code-scanning-and-pre-deployment-security-validation-pipeline.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-081-git-code-scanning-and-pre-deployment-security-validation-pipeline.md) |
| **ADR-082** | AI-Based Development Security Defense, Content Safety Shield & Pre-Commit Gating | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-082-ai-development-defense-content-safety-and-pre-commit-gating.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/ADR-20261010-082-ai-development-defense-content-safety-and-pre-commit-gating.md) |

### 🚀 DevOps & Cloud Infrastructure (`docs/adr/devops/`)
> *Azure Container Apps, .NET Aspire deployment, SQLite SMB persistence, and GitHub OIDC CI/CD pipelines.*

| ADR ID | Title | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-068** | Azure Container Apps Deployment with Persistent SQLite SMB Volume Mount, Private VNet Service Endpoints, and Showcase Demo Governance | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md) |
| **ADR-070** | .NET Aspire-Native Cloud Deployment Pipeline and Two-Stage Infrastructure vs. Application Workflow | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md) |
| **ADR-074** | GitHub Environment Variables & Passwordless Azure OIDC Pipeline Authentication | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md) |

### 🎨 Presentation & UI/UX (`docs/adr/presentation/`)
> *Client hydration, responsive mobile/tablet layout, touch ergonomics, and modal orchestration.*

| ADR ID | Title | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-055** | Web Client Single-Roundtrip Hydration & Zero CLS Orchestration | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261001-055-web-client-single-roundtrip-hydration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261001-055-web-client-single-roundtrip-hydration.md) |
| **ADR-061** | Responsive UI Layout, Touch Ergonomics & Mobile Bottom Navigation | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md) |
| **ADR-065** | Mobile User Sign Out Repair & Meal Review Ergonomics Modernization | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md) |
| **ADR-067** | Universal Mobile Modal Ergonomics, Layering Isolation, and Dynamic AI Nutrition Recalculation | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md) |

### ⚖️ Governance, Standards & Agentic Workflows (`docs/adr/governance/`)
> *Token economics, GitHub Stacked PR protocol, ADR architecture, issue-driven workflow, and licensing.*

| ADR ID | Title | Change Type | Status | File Path |
| :--- | :--- | :--- | :--- | :--- |
| **ADR-040** | Token Economics & Agentic Memory Rule: SDD vs. Skill Separation Standard | `[TOKEN_ECONOMICS]` | `ACCEPTED` | [`ADR-20260926-040-token-economics-standard.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260926-040-token-economics-standard.md) |
| **ADR-042** | Agentic Memory Persistence: Web App Plan Codification Standard | `[MEMORY_PERSISTENCE]` | `ACCEPTED` | [`ADR-20260926-042-web-app-plan-codification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260926-042-web-app-plan-codification.md) |
| **ADR-043** | Transition to Distributed Fragment Pattern for Living Documentation & ADRs | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260926-043-living-doc-fragment-pattern.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260926-043-living-doc-fragment-pattern.md) |
| **ADR-045** | Master Implementation Roadmap & Skill-by-Skill Execution Sequencing | `[ROADMAP]` | `ACCEPTED` | [`ADR-20260926-045-master-implementation-roadmap.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260926-045-master-implementation-roadmap.md) |
| **ADR-046** | License Governance, Dual AGPLv3/SSPL v1 Compliance & Header Enforcement | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260926-046-license-governance-and-header-enforcement.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260926-046-license-governance-and-header-enforcement.md) |
| **ADR-047** | Dedicated ADR Directory & Single Unified Architecture Decision Record System | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260928-047-dedicated-adr-architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260928-047-dedicated-adr-architecture.md) |
| **ADR-048** | Autonomous Issue-Driven Development (IDD) Skill & Resumable Local State Engine | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260928-048-issue-driven-agentic-workflow-skill.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260928-048-issue-driven-agentic-workflow-skill.md) |
| **ADR-049** | Native GitHub Stacked PR Protocol, gh-stack Automation & Bidirectional Navigation | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260928-049-native-github-stacked-pr-protocol.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260928-049-native-github-stacked-pr-protocol.md) |
| **ADR-051** | Product Owner MVP Market Roadmap Re-Prioritization & Phased Release Milestones | `[ROADMAP]` | `ACCEPTED` | [`ADR-20260929-051-mvp-market-roadmap-reprioritization.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260929-051-mvp-market-roadmap-reprioritization.md) |
| **ADR-052** | Zero-Throwaway Engineering, Cross-Phase Reusability & Forward-Roadmap Compatibility Mandate | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md) |
| **ADR-060** | Re-sequence Responsive Web & Mobile BFF Ahead of Azure Deployment | `[ROADMAP]` | `ACCEPTED` | [`ADR-20261002-060-resequence-responsive-web-before-azure-deployment.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20261002-060-resequence-responsive-web-before-azure-deployment.md) |
| **ADR-083** | Subsystem Domain ADR Hierarchy, Automated Indexing Engine & Table Deduplication | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20261010-083-subsystem-domain-adr-hierarchy-and-automated-indexing.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/ADR-20261010-083-subsystem-domain-adr-hierarchy-and-automated-indexing.md) |

---

## 4. Historical Decision Archive

Historical decision entries from the initial project inception through the mobile strategy (`LOG-001` through `LOG-042`) are preserved with complete data integrity in the historical archive:

* **Historical Archive**: [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md)

---

## 5. How to Author & Register a New ADR

1. Identify the target subsystem domain folder (`architecture/`, `security/`, `devops/`, `presentation/`, or `governance/`).
2. Copy the template from [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).
3. Create your atomic file: `docs/adr/<domain>/ADR-<YYYYMMDD>-<NNN>-<slug>.md`.
4. Run the automated indexer to refresh `index.json` and `README.md`:
   ```bash
   pwsh -File scripts/sync-adr-index.ps1
   # Or cross-platform Python:
   python3 scripts/sync-adr-index.py
   ```

