<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261116-161: Automated Release Pipeline, Semantic Tagging & Azure Deployment

- **Status**: Accepted
- **Date**: 2026-11-16
- **Domain**: DevOps / CI-CD / Release Governance
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: #149

---

## 1. Context & Problem Statement
Prior to this architectural enhancement, Diet-Dost releases and deployments were managed via manual git tagging (`alpha-1.0.0.0` through `alpha-5.0.0.0`) and on-demand manual triggers of the `.github/workflows/azure-app-deploy.yml` workflow. There was no automated release pipeline to:
1. Detect and parse Conventional Commits (`feat!:`, `feat:`, `fix:`) on merge to `main`.
2. Automatically compute semantic version increments (Major vs Minor vs Patch) against historical tags.
3. Automatically cut and push isolated release branches (`release-alpha-<version>`) strictly from verified remote tracking commits (`origin/main`).
4. Trigger Azure Container Apps deployments with pre-deployment security and quality gates.
5. Verify live container health and increment/publish GitHub releases.

---

## 2. Decision & Implementation

### 2.1 Event-Driven Merge Trigger & Zero-Loop Governance
Implemented `.github/workflows/auto-release-on-merge.yml`:
- **Auto-Trigger**: Listens on `push: branches: [main]`, triggering on every merged pull request.
- **Zero-Loop Guarantee**: Tag creation pushes to `refs/tags/alpha-*` and release branch pushes to `refs/heads/release-alpha-*`, ensuring the workflow never causes recursive trigger cascades against `main`.

### 2.2 Conventional Commits Semantic Bump Engine
Codified in `scripts/release-deploy.ps1` and the workflow:
- **Major Increment (`alpha-(X+1).0.0.0`)**: Triggered by `BREAKING CHANGE:`, `feat!:`, `fix!:`, or PR label `release:major`. Resets minor, patch, and build to 0.
- **Minor Increment (`alpha-X.(Y+1).0.0`)**: Triggered by `feat:` or `feat(...)` or PR label `release:minor`. Resets patch and build to 0.
- **Patch Increment (`alpha-X.Y.(Z+1).0`)**: Triggered by `fix:`, `perf:`, `refactor:`, `chore:`, or PR label `release:patch`.
- **Branch Naming Invariant**: Every release branch strictly adheres to `release-alpha-<major>.<minor>.<patch>.<build>` (e.g. `release-alpha-6.0.0.0`).

### 2.3 Automated Deployment Orchestration & Dual-Actor Permission
Updated `.github/workflows/azure-app-deploy.yml` Stage 1, 2, and 3 condition gates:
- Permitted both repository owner `@nikunjbanker` and automated release actor `github-actions[bot]`, enabling programmatic dispatch via `gh workflow run` from the release orchestrator.

### 2.4 Comprehensive Contract Test Verification
Added `tests/Nutrition.EvalHarness.Tests/ReleasePipelineContractTests.cs` covering:
- PowerShell script license governance and parameter contract.
- Workflow YAML trigger and stage assertions.
- Deterministic JSON evaluation asserting Major, Minor, Patch bump calculations and release branch naming.

---

## 3. Verification & Compliance Evidence
- **Automated Tests**: `ReleasePipelineContractTests` passed 6/6 test cases with 0 failures.
- **Dry-Run Validation**: Tested `scripts/release-deploy.ps1 -DryRun` across breaking, feature, and fix commit messages, correctly generating `alpha-6.0.0.0`, `alpha-5.1.0.0`, and `alpha-5.0.1.0`.
- **License Compliance**: Dual AGPLv3 / SSPL v1 license header applied to all new scripts, workflows, and test files.

