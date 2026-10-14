<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261014-094: Domain-Aligned Tier Naming Conventions for Multi-Tier Quality Test Gates

- **Status**: Accepted
- **Date**: 2026-10-14
- **Author**: Diet-Dost Solution Architecture & Quality Engineering
- **Deciders**: Lead Architect, Principal DevOps Engineer, Lead QA Engineer
- **Consulted**: Core Contributors, DevSecOps Team
- **Informed**: All Contributors, Autonomous CI/CD Runners, AI Subagents

---

## 1. Context and Problem Statement

In ADR-092 and ADR-093, Diet-Dost established a mandatory 4-tier pre-deployment quality gate architecture enforced by `scripts/verify-quality-test-gates.ps1` and `.github/workflows/quality-test-gate.yml`.

However, the initial tier naming utilized generic testing terminology:
- Tier 1: "Unit Tests"
- Tier 2: "Integration Tests"
- Tier 3: "System Tests"
- Tier 4: "End-to-End Tests"

While familiar, these generic labels introduced architectural ambiguity:
1. **Lack of Domain Precision**: Generic "Unit Tests" does not convey that Tier 1 rigorously guards ICMR-NIN 2024 algorithms, Mifflin-St Jeor math, and clinical nutrition estimators.
2. **Subsystem and BFF Ambiguity**: Generic "Integration Tests" and "System Tests" obscured the distinction between in-memory CQRS/BFF evaluations (Tier 2) and live HTTP/gRPC contracts, mobile ETag caching, and design system surface audits (Tier 3).
3. **User Acceptance Intent**: Generic "End-to-End Tests" failed to clarify that Tier 4 executes the live Customer & Functional Acceptance Test (CFT) suite validating all 5 user entitlement tiers, photo paywalls, and AI quota ceilings.
4. **Backward Compatibility Guard**: Renaming CLI parameters or GitHub Actions job identifiers (`unit-tests`, `integration-tests`, etc.) would break deployment callers (`azure-app-deploy.yml`, `azure-infra-deploy.yml`) and regression tests (`SecurityEnvironmentAndHeaderTests.cs`).

---

## 2. Decision Outcome

We adopted domain-aligned naming conventions for all four quality tiers while preserving complete backward compatibility across CLI parameters and CI job identifiers:

### 2.1 Domain-Aligned Tier Naming Matrix

| Tier Identifier | Domain-Aligned Display Name | Scope & Subsystems Evaluated | Enforced Quality Metric |
| :--- | :--- | :--- | :--- |
| **Tier 1** | **Tier 1: Domain & Clinical Nutrition Unit Tests** | Pure domain aggregates, ICMR-NIN 2024 guidelines, Mifflin-St Jeor formulas, WHO Asian-Indian BMI cutoffs, and starvation safety floors (`Nutrition.Domain.Tests`). | $\ge 44.0\%$ Code Line Coverage & 0 failures |
| **Tier 2** | **Tier 2: Subsystem & BFF Integration Tests** | CQRS dispatcher handlers, Web BFF composite hydration, Mobile BFF aggregation, Options pattern validation, security environment guards, and multimodal vision evaluation harnesses (`Nutrition.EvalHarness.Tests`). | $\ge 40.0\%$ Code Line Coverage & 0 failures |
| **Tier 3** | **Tier 3: Subsystem Contracts & Design System Tests** | Subsystem wireup, WebGateway runtime health checks, Mobile BFF ETag 304 caching contracts, admin RBAC boundaries, and Linear.app Obsidian design token compliance (`tests/*.mjs`). | $\ge 75.0\%$ Suite Pass Rate |
| **Tier 4** | **Tier 4: Multi-Tier User Acceptance & E2E CFT Suite** | Live Customer & Functional Acceptance Test (CFT) across 5 user tiers (Free, Basic, Premium, Admin, SuperAdmin), photo paywall gating, AI quota midnight reset, and registration OTP journeys (`tests/validate_e2e_tiers.ps1` & E2E suites). | $\ge 70.0\%$ Suite Pass Rate |

### 2.2 Backward Compatibility & Zero-Disruption Guarantees

1. **PowerShell Runner (`scripts/verify-quality-test-gates.ps1`)**:
   - The CLI parameter `[ValidateSet("All", "Unit", "Integration", "System", "E2E")] [string]$Tier = "All"` remains strictly identical, ensuring no disruption to existing developer habits, scripts, or automated pipelines.
   - Display banners, per-tier execution headers, gate result objects (`$gateResults.Add(...)`), and generated markdown summaries emit the refined domain-aligned names.

2. **GitHub Actions Pipeline (`.github/workflows/quality-test-gate.yml`)**:
   - Job IDs (`unit-tests:`, `integration-tests:`, `system-tests:`, `e2e-tests:`, `quality-gate-summary:`) remain unchanged, preserving all downstream dependencies in `azure-app-deploy.yml`, `azure-infra-deploy.yml`, and `SecurityEnvironmentAndHeaderTests.cs`.
   - Job display names (`name:`), console summary logs, and automated PR comment scorecards display the refined domain-aligned tier names.

3. **Governance & Documentation**:
   - Synchronized `AGENTS.md` Rule 23, `docs/sdd/00_sdd_index.md`, and `docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`.

---

## 3. Consequences

### Positive
- **Architectural Clarity**: Reviewers and stakeholders immediately understand what each tier verifies without needing to inspect underlying scripts.
- **Enhanced PR Transparency**: The automated PR scorecard clearly reports on Clinical Nutrition, BFF Integration, Subsystem Contracts, and Multi-Tier User Acceptance.
- **Zero Breakage**: Zero regressions in deployment workflows or unit tests due to preserving job IDs and parameter bindings.

### Neutral / Operational
- Developers running individual tiers locally continue using familiar flags: `-Tier Unit`, `-Tier Integration`, `-Tier System`, `-Tier E2E`, or `-Tier All`.
