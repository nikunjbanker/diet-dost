<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261116-163: Manual Release Dispatch Orchestration and Admin Actor Gating

- **Status**: Accepted
- **Date**: 2026-11-16
- **Domain**: DevOps / CI-CD / Release Governance
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: #149
- **Refines**: ADR-161

---

## 1. Context & Problem Statement
In ADR-161, `.github/workflows/auto-release-on-merge.yml` was configured to automatically trigger upon every `push: branches: [main]`. While this provided zero-touch continuous deployment, deploying to production/development Azure Container Apps automatically on every small documentation, dependency, or minor PR merge introduces several operational risks:
1. **Unintended Cloud Deployments**: Every PR merge immediately initiates a full 19-minute Aspire container build, ACR push, and ACA revision deployment, consuming cloud and runner minutes unnecessarily.
2. **Lack of Human Release Discretion**: Repository maintainers require explicit release gates to bundle multiple PRs into a single cohesive release version rather than cutting a new release per commit.
3. **Actor Gating Parity**: Existing infrastructure deployment workflows (`.github/workflows/azure-app-deploy.yml`) enforce strict actor authorization checks (`github.actor == 'nikunjbanker'`). The release orchestrator needed identical actor security controls.

---

## 2. Decision & Implementation

### 2.1 Transition to Strictly On-Demand `workflow_dispatch`
- Removed the `push: branches: [main]` trigger from `.github/workflows/auto-release-on-merge.yml`.
- Replaced the workflow title with `Release Orchestrator & ACA Deployment`.
- The release pipeline now executes strictly upon manual dispatch via GitHub UI or GitHub CLI (`gh workflow run`).

### 2.2 Mandatory Maintainer Actor Gating (`github.actor == 'nikunjbanker'`)
In parity with `.github/workflows/azure-app-deploy.yml`, job-level execution gates were added across all three stages:
- **Stage 1 (`evaluate-and-tag`)**: `if: github.actor == 'nikunjbanker'`
- **Stage 2 (`deploy-application`)**: `if: always() && github.actor == 'nikunjbanker' && needs.evaluate-and-tag.result == 'success' && needs.evaluate-and-tag.outputs.dry_run != 'true' && needs.evaluate-and-tag.outputs.deploy_requested == 'true'`
- **Stage 3 (`publish-release`)**: `if: always() && github.actor == 'nikunjbanker' && needs.evaluate-and-tag.result == 'success' && needs.evaluate-and-tag.outputs.dry_run != 'true' && (needs.evaluate-and-tag.outputs.deploy_requested != 'true' || needs.deploy-application.result == 'success')`

Any attempt to dispatch the workflow by non-authorized users or bots will immediately skip all jobs without executing any git tagging, branching, or Azure deployment operations.

### 2.3 Maintained Semantic Bump & Dry-Run Simulation
The maintainer retains full flexibility during manual dispatch:
- **`bumpType`**: Choice of `Auto` (conventional commits), `Major`, `Minor`, `Patch`, `Build`.
- **`customTag`**: Optional manual tag override (e.g., `alpha-6.0.0.0`).
- **`environment`**: Target Azure environment (`dev`, `prod`).
- **`deploy`**: Boolean flag to enable or skip cloud deployment.
- **`dryRun`**: Boolean simulation flag to calculate tags and branches without pushing changes.

---

## 3. Verification & Compliance Evidence
- **Automated Tests**: Updated `tests/Nutrition.EvalHarness.Tests/ReleasePipelineContractTests.cs` to assert:
  - Absence of `push:` triggers.
  - Presence of `workflow_dispatch:` trigger.
  - Presence of `github.actor == 'nikunjbanker'` authorization gate.
  - All 7 contract tests passed with 0 failures.
- **Zero-Warning Standard**: Solution builds with 0 warnings and 0 errors under .NET 11.
- **AI Security Defense**: All 7 vectors verified and passed (`verify-ai-security-defense.ps1 -Mode All`).

