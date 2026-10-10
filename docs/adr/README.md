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
| **ADR-048** | Autonomous Issue-Driven Development (IDD) Skill & Resumable Local State Engine | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260928-048-issue-driven-agentic-workflow-skill.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260928-048-issue-driven-agentic-workflow-skill.md) |
| **ADR-049** | Native GitHub Stacked PR Protocol, gh-stack Automation & Bidirectional Navigation | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260928-049-native-github-stacked-pr-protocol.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260928-049-native-github-stacked-pr-protocol.md) |
| **ADR-050** | CODEOWNER Gating & Approval Policy for Autonomous Issue-Driven Development | `[SECURITY]` | `ACCEPTED` | [`ADR-20260928-050-codeowner-approval-issue-workflow.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260928-050-codeowner-approval-issue-workflow.md) |
| **ADR-051** | Product Owner MVP Market Roadmap Re-Prioritization & Phased Release Milestones | `[ROADMAP]` | `ACCEPTED` | [`ADR-20260929-051-mvp-market-roadmap-reprioritization.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260929-051-mvp-market-roadmap-reprioritization.md) |
| **ADR-052** | Zero-Throwaway Engineering, Cross-Phase Reusability & Forward-Roadmap Compatibility Mandate | `[GOVERNANCE]` | `ACCEPTED` | [`ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md) |
| **ADR-053** | Web BFF Composite Endpoint & Shared Application CQRS Queries | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20260930-053-web-bff-composite-endpoint-and-shared-cqrs-queries.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260930-053-web-bff-composite-endpoint-and-shared-cqrs-queries.md) |
| **ADR-054** | Shared CQRS Query Consolidation & Native Dispatcher Integration | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261001-054-shared-cqrs-query-consolidation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261001-054-shared-cqrs-query-consolidation.md) |
| **ADR-055** | Web Client Single-Roundtrip Hydration & Zero CLS Orchestration | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261001-055-web-client-single-roundtrip-hydration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261001-055-web-client-single-roundtrip-hydration.md) |
| **ADR-056** | Gated Premium Feature Button State & Underlying API Defense | `[SECURITY]` | `ACCEPTED` | [`ADR-20261001-056-gated-feature-button-state-and-api-defense.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261001-056-gated-feature-button-state-and-api-defense.md) |
| **ADR-057** | Centralize Food Estimation API & Eliminate Static Client Dictionary | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-057-centralize-food-estimation-api.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-057-centralize-food-estimation-api.md) |
| **ADR-058** | Deconstruct Monolithic UI Controller into Single Responsibility (SRP) ES Modules | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-058-modularize-ui-controllers.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-058-modularize-ui-controllers.md) |
| **ADR-059** | Phase 2 Two-Stage Responsive Web & Mobile Readiness Architecture | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-059-phase-2-responsive-and-mobile-readiness-architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-059-phase-2-responsive-and-mobile-readiness-architecture.md) |
| **ADR-060** | Re-sequence Responsive Web & Mobile BFF Ahead of Azure Deployment | `[ROADMAP]` | `ACCEPTED` | [`ADR-20261002-060-resequence-responsive-web-before-azure-deployment.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-060-resequence-responsive-web-before-azure-deployment.md) |
| **ADR-061** | Responsive UI Layout, Touch Ergonomics & Mobile Bottom Navigation | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-061-responsive-ui-layout-and-mobile-bottom-navigation.md) |
| **ADR-062** | Mobile BFF Composite Contracts, ETag Caching & Offline Idempotency | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-062-mobile-bff-composite-contracts-and-caching.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-062-mobile-bff-composite-contracts-and-caching.md) |
| **ADR-063** | Native Feature Classification Matrix & Responsive Exit Gate Signoff | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261002-063-native-feature-classification-and-responsive-exit-gate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261002-063-native-feature-classification-and-responsive-exit-gate.md) |
| **ADR-064** | Disable Public User Sign-Up & Codify DESIGN.md UI/UX Authority | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-064-disable-public-signup-and-codify-design-system-authority.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-064-disable-public-signup-and-codify-design-system-authority.md) |
| **ADR-065** | Mobile User Sign Out Repair & Meal Review Ergonomics Modernization | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-065-mobile-user-signout-and-meal-logger-ergonomics.md) |
| **ADR-066** | SuperAdmin-Only Tier Configuration Governance & Authorization | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-066-superadmin-tier-governance-authorization.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-066-superadmin-tier-governance-authorization.md) |
| **ADR-067** | Universal Mobile Modal Ergonomics, Layering Isolation & Dynamic AI Nutrition Recalculation | `[PRESENTATION]` | `ACCEPTED` | [`ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-067-universal-mobile-modal-ergonomics-and-ai-nutrition-recalculation.md) |
| **ADR-068** | Azure Container Apps Deployment with Persistent SQLite SMB Volume Mount & Custom Domain TLS | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-068-azure-container-apps-deployment-and-sqlite-smb-persistence.md) |
| **ADR-069** | Released Environment Privileged Demo User Prohibition & Showcase End-User Tier Isolation | `[SECURITY]` | `ACCEPTED` | [`ADR-20261003-069-released-environment-privileged-demo-user-prohibition.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261003-069-released-environment-privileged-demo-user-prohibition.md) |
| **ADR-070** | .NET Aspire Cloud Deployment Pipeline & Two-Stage Infra vs. Application Workflow | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-070-aspire-native-deployment-pipeline-and-two-stage-workflow.md) |
| **ADR-071** | Azure Key Vault Secret Governance & .NET Aspire Integration in Deployed Environments | `[SECURITY]` | `ACCEPTED` | [`ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md) |
| **ADR-072** | Centralized Application Configuration, Secret Governance & Strongly-Typed Ports | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md) |
| **ADR-073** | Strongly-Typed Options Pattern & FluentValidation Configuration Governance | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-073-options-pattern-and-fluentvalidation.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-073-options-pattern-and-fluentvalidation.md) |
| **ADR-074** | GitHub Environment Variables & Passwordless Azure OIDC Pipeline Authentication | `[DEVOPS]` | `ACCEPTED` | [`ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-074-github-environment-variables-and-azure-oidc-pipeline-authentication.md) |
| **ADR-075** | Complete Elimination of Raw Configuration Indexers for Strongly-Typed Options | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261008-075-complete-elimination-of-raw-configuration-indexers-for-options-classes.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-075-complete-elimination-of-raw-configuration-indexers-for-options-classes.md) |
| **ADR-076** | Configuration Architecture SDD & Process Boundary Specification | `[ARCHITECTURE]` | `ACCEPTED` | [`ADR-20261009-076-configuration-architecture-sdd-and-process-boundary-specification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261009-076-configuration-architecture-sdd-and-process-boundary-specification.md) |
| **ADR-077** | Secret-Only Governance for SuperAdmin Email and Mobile Verification | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-077-secret-only-governance-for-superadmin-email-and-mobile-verification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261010-077-secret-only-governance-for-superadmin-email-and-mobile-verification.md) |
| **ADR-078** | Sole-Authority Governance for Secrets and Environment Variables Restricted to @nikunjbanker | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-078-sole-authority-governance-for-secrets-and-environment-variables.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261010-078-sole-authority-governance-for-secrets-and-environment-variables.md) |
| **ADR-079** | Remediation of CodeQL Code Scanning Security Alerts | `[SECURITY]` | `ACCEPTED` | [`ADR-20261010-079-remediation-of-codeql-code-scanning-security-alerts.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261010-079-remediation-of-codeql-code-scanning-security-alerts.md) |


---

## 4. Historical Decision Archive

Historical decision entries from the initial project inception through the mobile strategy (`LOG-001` through `LOG-042`) are preserved with complete data integrity in the historical archive:

* **Historical Archive**: [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md)

---

## 5. How to Propose or Author a New ADR

1. Copy the standard template from [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md) (or use the specification block below).
2. Create a new atomic file named:
   `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md`
3. Document the context, considered options, decision outcome, trade-offs, and verification results.
4. Register the new ADR in this [`README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) table under Section 3 and in [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md).

### Standardized ADR Fragment Specification:

```markdown
# ADR-[YYYYMMDD-NNN]: [Short Title of Architectural Decision]

> **Date / Timestamp**: YYYY-MM-DDTHH:mm:ss+05:30  
> **Status**: [PROPOSED | ACCEPTED | SUPERSEDED | DEPRECATED]  
> **Driver / Agent / Deciders**: [AI Assistant & User Pair-Programming]  
> **Change Type**: [ARCHITECTURE | FEATURE | DEFECT_FIX | GOVERNANCE | SECURITY | DATABASE | TOKEN_ECONOMICS]  
> **Affected Subsystems**: [WebGateway | Application | Domain | Infrastructure | Mobile | Presentation]  
> **Associated PR & Stack**: PR #<number> (Base: <branch-name>)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_*.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/), [`docs/cft/*.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/)  

---

## 1. Executive Summary & Change Rationale
* <2-3 bullet points describing what changed and the technical rationale>
* <Key user, architecture, clinical, or performance motivation>

---

## 2. Context and Problem Statement
[Describe the context, technical challenges, and driving constraints that require an architectural decision. What problem are we solving?]

---

## 3. Decision Drivers
* [Driver 1: e.g., Token Economics - 0 baseline system prompt tokens]
* [Driver 2: e.g., Zero merge conflicts across concurrent/stacked PRs]
* [Driver 3: e.g., Clinical correctness / ICMR-NIN 2024 compliance]
* [Driver 4: e.g., Zero duplicate documentation or code]

---

## 4. Considered Options
* **Option 1**: [Description of Option 1]
* **Option 2**: [Description of Option 2]
* **Option 3**: [Description of Option 3]

---

## 5. Decision Outcome
* **Chosen Option**: [Option X: Title]
* **Justification**: [Explain why this option was chosen over alternatives. Detail the trade-offs accepted.]

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* [Positive impact 1]
* [Positive impact 2]

### Negative Consequences / Accepted Trade-Offs:
* [Negative impact or trade-off 1]
* [Mitigation for negative impact]

---

## 7. Verification & Compliance Results
* **Automated Builds & Tests**:
  - `dotnet build`: 0 warnings, 0 errors
  - `dotnet test`: 129 passed, 0 failed, 0 warnings
* **E2E Tier Validation**: Verified across user tiers (Free, Basic, Premium, Admin, SuperAdmin)
* **Living Documentation Synchronization**: Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md)
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
```
