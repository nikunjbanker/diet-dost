<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-098: GitHub Actions DAG Downstream Status Propagation and Symmetrical Three-Stage Deployment Alignment

| Metadata | Details |
| :--- | :--- |
| **Status** | **ACCEPTED** |
| **Date** | 2026-10-17 |
| **Domain** | DevOps / CI/CD Deployment Pipelines |
| **Subsystem** | `.github/workflows/azure-infra-deploy.yml`, `.github/workflows/azure-app-deploy.yml` |
| **Impacted Files** | `.github/workflows/azure-infra-deploy.yml`, `.github/workflows/azure-app-deploy.yml`, `tests/Nutrition.EvalHarness.Tests/SecurityEnvironmentAndHeaderTests.cs` |

---

## 1. Context & Problem Statement

In GitHub Actions workflow run [39016741478](https://github.com/nikunjbanker/diet-dost/actions/runs/39016741478/job/117351140526), `Stage 1: Validate (Aspire & Bicep IaC)` ran and completed successfully in 4m 14s. However:
- `Stage 2: Preview (ARM What-If Analysis)` was skipped (0s).
- `Stage 3: Provision (Deploy Infrastructure via Bicep)` was skipped (0s).

### Root Cause Analysis (GitHub Actions DAG Transitive Status Trapping)
1. **The Fast-Track Gate**: When fast-track execution (`skipPreFlightGates: true` or `skipPreDeploymentGates: true`) is selected by authorized administrator `@nikunjbanker`, upstream jobs (`pre-provisioning-security-scan` and `pre-provisioning-quality-gate`) are skipped.
2. **Stage 1 Execution**: Stage 1 (`validate-infrastructure`) executed successfully because its condition explicitly used `always() && !contains(needs.*.result, 'failure') && !contains(needs.*.result, 'cancelled')`.
3. **The DAG Status Trap**: Stage 2 had `needs: [validate-infrastructure]` and `if: github.actor == 'nikunjbanker'`.
   - In GitHub Actions, when an `if` expression omits status functions, it implicitly evaluates to `success() && <condition>`.
   - In GitHub Actions DAG evaluation, `success()` returns `false` if **ANY** upstream ancestor in the transitive dependency tree was skipped.
   - Because ancestors (`pre-provisioning-security-scan`) were skipped, GitHub Actions' DAG scheduler evaluated `success()` as `false` for `preview-infrastructure`, automatically skipping Stage 2.
   - Stage 3 (`provision-infrastructure`) depended on `preview-infrastructure`, so it was also automatically skipped.

---

## 2. Decision & Architecture

We implemented explicit status propagation and symmetrical three-stage DAG alignment across both `azure-infra-deploy.yml` and `azure-app-deploy.yml`:

### 1. Explicit Status Propagation on Downstream Stages
Every downstream stage now includes explicit `always()` guardrails paired with specific upstream dependency result checks:

**Stage 2 (Preview)**:
```yaml
preview-infrastructure:
  name: 'Stage 2: Preview (ARM What-If Analysis)'
  needs: [validate-infrastructure]
  if: |
    always() &&
    github.actor == 'nikunjbanker' &&
    needs.validate-infrastructure.result == 'success'
```

**Stage 3 (Provision / Deploy)**:
```yaml
provision-infrastructure:
  name: 'Stage 3: Provision (Deploy Infrastructure via Bicep)'
  needs: [preview-infrastructure, validate-infrastructure]
  if: |
    always() &&
    github.actor == 'nikunjbanker' &&
    needs.validate-infrastructure.result == 'success' &&
    needs.preview-infrastructure.result == 'success'
```

### 2. Symmetrical Cross-Workflow Parity
Both `azure-infra-deploy.yml` and `azure-app-deploy.yml` now follow the identical, bulletproof 3-stage DAG progression:
- **Stage 1 (Validate)**: Model validation, secret scans, compilation, and live ARM validation. Runs when gates pass or when fast-tracked by `@nikunjbanker`.
- **Stage 2 (Preview)**: ARM What-If change detection. Runs only if Stage 1 succeeded. Never blocked by skipped pre-flight gates.
- **Stage 3 (Provision / Deploy)**: Azure Bicep provisioning or Container App revision deployment. Runs only if both Stage 1 and Stage 2 succeeded.

### 3. Subscription ID Fallback Resilience
Added defensive fallback in CLI script steps: if `AZURE_SUBSCRIPTION_ID` is omitted or unset, steps query `az account show --query id -o tsv` directly from the authenticated session context, preventing empty subscription ID paths in Resource Manager template parameters.

### 4. Security Test Assertion Governance
Updated `SecurityEnvironmentAndHeaderTests.cs` to assert `github.actor == 'nikunjbanker'` across workflow contents without enforcing single-line prefix constraints that would break multi-line GitHub Actions DAG conditions.

---

## 3. Verification & Compliance Evidence
- **Domain Tests**: 36/36 passed (`Nutrition.Domain.Tests`).
- **Evaluation Harness Tests**: 207/207 passed (`Nutrition.EvalHarness.Tests`).
- **AI Security Defense Verification**: All 6 vectors passed cleanly (`scripts/verify-ai-security-defense.ps1`).
- **DAG Correctness**: Verified that Stage 2 and Stage 3 will execute continuously regardless of whether pre-flight security/quality gates ran or were skipped by administrator fast-track.
