<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261014-069: Mandatory Multi-Tier Pre-Deployment Quality & Test Validation Gates (Unit 90%, Integration 80%, System 75%, E2E 70%)

> **Date / Timestamp**: 2026-10-14T13:55:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Lead Architect, Quality & DevOps Expert & AI Coding Agent Pair-Programming  
> **Change Type**: [QUALITY | CI/CD | DEVOPS | TESTING | ARCHITECTURE]  
> **Affected Subsystems**: [GitHub Actions Workflows | Quality Test Gate | Azure App Deploy | Azure Infra Deploy | Evaluation Harness | Domain Tests | E2E Suites]  
> **Associated PR & Stack**: PR #53 (Branch: `feature/aca-deployment-sqlite-smb-dev-domain`, Base: `origin/main`)  
> **Relevant SDDs & Standards**: [`docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md), [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md) (Rule 23), [`.github/workflows/quality-test-gate.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/quality-test-gate.yml)  

---

## 1. Executive Summary & Change Rationale

* **Comprehensive Quality Gate**: Established a mandatory 5-job GitHub Actions reusable workflow [`.github/workflows/quality-test-gate.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/quality-test-gate.yml) mirroring the immutable security scan pipeline (`security-scan.yml`).
* **Multi-Tier Quality Thresholds**: Codified and strictly enforced four rigorous testing tiers prior to any cloud deployment or provisioning:
  1. **Unit Testing Tier**: $\ge 90\%$ line coverage / pass rate across core domain entities, value objects, and deterministic ICMR-NIN 2024 / WHO clinical algorithms (`Nutrition.Domain.Tests`).
  2. **Integration Testing Tier**: $\ge 80\%$ line coverage / pass rate across evaluation harnesses, security headers, options validation, and multi-tier authorization contracts (`Nutrition.EvalHarness.Tests`).
  3. **System Testing Tier**: $\ge 75\%$ pass rate verifying `Nutrition.WebGateway` runtime boot, Mobile BFF endpoints, composite schemas, and subsystem inter-service contracts (`tests/verify_mobile_bff.mjs`).
  4. **End-to-End (E2E) Testing Tier**: $\ge 70\%$ pass rate across the full 5-tier user journey acceptance suite (`tests/validate_e2e_tiers.ps1`), evaluating `free`, `basic`, `premium`, `admin.demo`, and `superadmin` tiers with live SQLite persistence and photo paywalls.
* **Pre-Deployment Blocking**: Both application deployment (`.github/workflows/azure-app-deploy.yml`) and infrastructure provisioning (`.github/workflows/azure-infra-deploy.yml`) now mandate both security and quality gates (`needs: [pre-deployment-security-scan, pre-deployment-quality-gate]` / `needs: [pre-provisioning-security-scan, pre-provisioning-quality-gate]`). Any failure halts downstream container builds or Bicep deployments immediately (`exit 1`).
* **Governance & Supply Chain Pinning**: Codified Rule 23 in [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md), protected workflow ownership in `.github/CODEOWNERS` for `@nikunjbanker`, and cryptographically pinned all GitHub Actions to 40-character immutable commit SHAs.

---

## 2. Context and Problem Statement

Prior to this decision, Diet-Dost enforced an immutable 8-job static analysis security scanning gate (`security-scan.yml`) covering Gitleaks, ESLint, SecurityCodeScan SAST, Trivy, Checkov, actionlint, AI prompt defense, and automated PR verdicts. However:
1. **Unprotected Deployment Path**: While security scanning was strictly gated, deployment pipelines did not enforce an equivalent mandatory pre-deployment multi-tier quality gate for Unit, Integration, System, and E2E tests.
2. **Missing Quantitative Coverage Thresholds**: Individual test suites ran ad-hoc without formal quantitative thresholds (Unit 90%, Integration 80%, System 75%, E2E 70%) blocking deployment on regressions.
3. **Absence of Consolidated Quality Scorecards**: Pull requests lacked automated multi-tier quality verdict tables detailing exact test counts, durations, and pass rates before merges into `main` or production deployments.

---

## 3. Decision Drivers

* **Zero Defect Deployment Standard**: No code or infrastructure change may deploy to Azure Container Apps or provision cloud resources without passing all quality tiers.
* **Granular Tier Separation**: Clear distinction between isolated domain unit tests, integration harnesses, system runtime contracts, and end-to-end customer functional acceptance tests (CFT).
* **Parity with Security Gating**: Quality assurance must match the rigor, immutability, automated PR commenting, and pre-deployment blocking established by the security-scan pipeline.
* **Supply Chain Security**: All CI/CD actions must be pinned to 40-character commit hashes to eliminate supply chain tampering.
* **Local Developer Ergonomics**: Developers must be able to run the exact same multi-tier quality evaluation locally (`scripts/verify-quality-test-gates.ps1`) before pushing branches.

---

## 4. Considered Options

* **Option 1: Monolithic Test Step inside App Deploy Workflow**: Add `dotnet test` directly inside `azure-app-deploy.yml`.
  - *Drawbacks*: Fails to run on infrastructure deployments (`azure-infra-deploy.yml`), lacks multi-tier threshold governance, doesn't produce standalone PR scorecards, and conflates deployment logic with test orchestration.
* **Option 2: Ad-Hoc PR Status Checks without Gated Deployments**: Run tests in a separate CI workflow without wiring `needs: pre-deployment-quality-gate` into deployment workflows.
  - *Drawbacks*: Allows accidental deployment of unvalidated builds if a deployment workflow is triggered independently via workflow_dispatch or manual merge.
* **Option 3: Modular Reusable Quality Gate (`quality-test-gate.yml`) + Four-Tier Thresholds + Gated Deployment Pipelines + Local CLI Runner (Selected)**:
  - Implement `.github/workflows/quality-test-gate.yml` with 4 parallel tier jobs and an aggregating summary job.
  - Require both `pre-deployment-security-scan` and `pre-deployment-quality-gate` before deployment begins.
  - Provide `scripts/verify-quality-test-gates.ps1` for local multi-tier testing.
  - Codify Rule 23 in `AGENTS.md` and protect in `.github/CODEOWNERS`.

---

## 5. Decision Outcome

Selected **Option 3**.

### Architectural Changes & Deliverables:
1. **Reusable Workflow (`.github/workflows/quality-test-gate.yml`)**:
   - `unit-tests`: `Nutrition.Domain.Tests` with Coverlet, threshold $\ge 90\%$.
   - `integration-tests`: `Nutrition.EvalHarness.Tests` with Coverlet, threshold $\ge 80\%$.
   - `system-tests`: Boots `Nutrition.WebGateway`, runs `tests/verify_mobile_bff.mjs`, threshold $\ge 75\%$.
   - `e2e-tests`: Boots `Nutrition.WebGateway`, runs `tests/validate_e2e_tiers.ps1` across 5 user tiers, threshold $\ge 70\%$.
   - `quality-gate-summary`: Evaluates all 4 tiers, generates GitHub Step Summary, posts PR comment via `gh pr comment`, and exits with code 1 on any failure.
2. **Deployment Pipeline Integration**:
   - `.github/workflows/azure-app-deploy.yml`: Added `pre-deployment-quality-gate` and updated `needs: [pre-deployment-security-scan, pre-deployment-quality-gate]`.
   - `.github/workflows/azure-infra-deploy.yml`: Added `pre-provisioning-quality-gate` and updated `needs: [pre-provisioning-security-scan, pre-provisioning-quality-gate]`.
3. **Local CLI Runner (`scripts/verify-quality-test-gates.ps1`)**:
   - Supports `-Tier All`, `-Tier Unit`, `-Tier Integration`, `-Tier System`, `-Tier E2E`.
   - Automates background WebGateway daemon lifecycle, port readiness polling, and ANSI colorized terminal scorecards.
4. **Governance & Automated CI Assertion**:
   - Added Rule 23 to `AGENTS.md`.
   - Added `DeploymentWorkflows_MustGateOnQualityTestGates_AndEnforceThresholds()` in `SecurityEnvironmentAndHeaderTests.cs`.
   - Added `/.github/workflows/quality-test-gate.yml @nikunjbanker` in `.github/CODEOWNERS`.

---

## 6. Pros and Cons

### Pros
* **Bulletproof Quality Shield**: Cloud environments are guaranteed to receive only builds that meet or exceed all 4 quality thresholds.
* **Rapid Fault Localization**: Distinct job separation allows immediate identification of whether an issue is domain logic, integration wiring, system contract drift, or end-to-end tier gating.
* **Automated Audit Trail**: Comprehensive PR comments provide a permanent record of quality metrics for compliance and peer review.
* **Zero Supply Chain Risk**: 40-character commit SHA pinning across all workflow actions prevents tag hijacking.

### Cons / Trade-offs
* **Increased CI Duration**: Running all 4 tiers takes ~3-4 minutes in GitHub Actions runners (mitigated by parallel execution of unit and integration jobs).

---

## 7. Compliance and Verification Plan

* **Automated Test**: `SecurityEnvironmentAndHeaderTests.DeploymentWorkflows_MustGateOnQualityTestGates_AndEnforceThresholds` validates that workflows require `quality-test-gate.yml` and assert the 90%, 80%, 75%, and 70% thresholds.
* **Pre-Commit Verification**: `scripts/verify-ai-security-defense.ps1` confirms package namespace vetting, ADR-086 alignment, and security defense compliance.
* **PR Verification**: The automated `quality-gate-summary` job publishes the live multi-tier scorecard on every Pull Request.
