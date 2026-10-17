<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-097: Azure Resource Group Lifecycle Idempotence and Admin Fast-Track Workflow Gates

> **ADR ID**: `ADR-20261017-097-azure-resource-group-lifecycle-and-admin-fast-track`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-17`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[DEVOPS]`, `[INFRASTRUCTURE]`, `[SECURITY]`  
> **Impacted Files**: [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml), [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml)  
> **Related Documents**: [`ADR-20261017-096-aspire-azure-infra-oidc-and-three-stage-provisioning.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/ADR-20261017-096-aspire-azure-infra-oidc-and-three-stage-provisioning.md)

---

## 1. Executive Summary & Problem Context

During the initial execution of the modernized Aspire Cloud Infrastructure Provisioning workflow ([Run 39012760233 / Job 117339559670](https://github.com/nikunjbanker/diet-dost/actions/runs/39012760233/job/117339559670)), Stage 1 (`Validate (Aspire & Bicep IaC)`) failed at Step 9 (`Live Azure ARM Template Validation`):

```text
Running live Azure Resource Manager pre-flight validation on 'rg-dietdost-dev'...
ERROR: {"code": "ResourceGroupNotFound", "message": "Resource group 'rg-dietdost-dev' could not be found."}
Process completed with exit code 1.
```

Additionally, operators noted that running infrastructure or application deployment workflows incurs a mandatory 5–6 minute delay due to pre-flight security scans and 4-tier test suites running ahead of deployment, with no mechanism for authorized administrators to fast-track provisioning when rapid infrastructure iterations are required.

---

## 2. Root Cause Analysis

### 2.1 Bash Exit Code Trap on `az group exists`
In both [`.github/workflows/azure-infra-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-infra-deploy.yml) and [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml), Step 8 checked Resource Group existence using:

```bash
if az group exists --name "$RG"; then
  echo "✅ Resource Group '$RG' already exists."
else
  echo "⚡ Resource Group '$RG' does not exist. Creating now in '$LOCATION'..."
  az group create --name "$RG" --location "$LOCATION" ...
fi
```

**The Bug**:
Azure CLI's `az group exists` command outputs the string `false` to `stdout`, but terminates with **exit code 0** (success) regardless of whether the group exists. In POSIX/Bash, `if <command>; then` evaluates the *exit status code* of the command (where `0` denotes truthy success). As a consequence:
1. Bash evaluated `if az group exists` as `true` even when `az group exists` printed `false`.
2. The `else` block (which invoked `az group create`) was never executed on clean/first-time environments.
3. The workflow immediately proceeded to Step 9 (`az deployment group validate --resource-group "$RG"`), which crashed with `ResourceGroupNotFound`.

---

## 3. Architectural Decisions & Remediation

### 3.1 Idempotent TSV Output Comparison & Post-Creation Assertion
Refactored the resource group verification step across all deployment workflows to explicitly capture stdout in TSV format:

```bash
RG_EXISTS=$(az group exists --name "$RG" --output tsv)
if [ "$RG_EXISTS" = "true" ]; then
  echo "✅ Resource Group '$RG' already exists."
else
  echo "⚡ Resource Group '$RG' does not exist. Creating now in '$LOCATION'..."
  az group create \
    --name "$RG" \
    --location "$LOCATION" \
    --tags environment="${{ inputs.environment }}" project="diet-dost" managed-by="aspire" \
    --output table
  echo "✅ Created Resource Group '$RG'."
fi

# Hard assertion gate ensuring RG existence before ARM API operations
if [ "$(az group exists --name "$RG" --output tsv)" != "true" ]; then
  echo "❌ Error: Failed to create or confirm access to Resource Group '$RG'."
  exit 1
fi
```

**Behavioral Guarantees**:
- **First-time Run**: Evaluates `RG_EXISTS="false"`, executes `az group create`, asserts creation, and passes to Stage 1 ARM validation without error.
- **Subsequent Runs**: Evaluates `RG_EXISTS="true"`, prints confirmation, skips creation, and passes to Stage 1 ARM validation.

---

### 3.2 Sole Authority Admin Fast-Track Toggle (`skipPreFlightGates`)
To allow the repository owner (`@nikunjbanker`) to bypass waiting for long security and test suites when performing iterative cloud provisioning or hotfix deployments:

1. **Workflow Dispatch Input**:
   ```yaml
   inputs:
     skipPreFlightGates:
       description: '⚡ Skip Pre-Flight Security & Quality Gates (Admin Only: nikunjbanker)'
       required: false
       type: boolean
       default: false
   ```

2. **Actor-Restricted Gating**:
   ```yaml
   pre-provisioning-security-scan:
     name: Mandatory Pre-Provisioning Security Gates
     if: inputs.skipPreFlightGates != true || github.actor != 'nikunjbanker'
     uses: ./.github/workflows/security-scan.yml
     secrets: inherit

   pre-provisioning-quality-gate:
     name: Mandatory Pre-Provisioning Quality & Test Gates
     if: inputs.skipPreFlightGates != true || github.actor != 'nikunjbanker'
     uses: ./.github/workflows/quality-test-gate.yml
     secrets: inherit

   validate-infrastructure:
     name: 'Stage 1: Validate (Aspire & Bicep IaC)'
     needs: [pre-provisioning-security-scan, pre-provisioning-quality-gate]
     if: |
       always() &&
       github.actor == 'nikunjbanker' &&
       !contains(needs.*.result, 'failure') &&
       !contains(needs.*.result, 'cancelled')
   ```

3. **Skipped Dependency Resolution**:
   In GitHub Actions, jobs with `needs: [jobA, jobB]` default to `success()`, which causes dependent jobs to be skipped if any dependency was skipped. By employing `always() && !contains(needs.*.result, 'failure') && !contains(needs.*.result, 'cancelled')`, Stage 1 begins execution immediately when pre-flight gates are skipped, while continuing to block deployment if an actual test or security failure occurs.

4. **Security Invariant**:
   If an unauthorized actor triggers the workflow with `skipPreFlightGates: true`, the condition `github.actor != 'nikunjbanker'` forces the security and quality gates to execute anyway, maintaining zero-trust pipeline integrity.

---

### 3.3 Three-Stage Application Deployment Architecture (`azure-app-deploy.yml`)
Decomposed the monolithic `build-test-and-deploy` job in [`.github/workflows/azure-app-deploy.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/azure-app-deploy.yml) into three distinct stages mirroring the infrastructure pipeline:

1. **Stage 1: Validate (`validate-application`)**:
   - Aspire AppHost model evaluation and manifest generation (`aspire publish --apphost`).
   - NuGet dependency vulnerability audit (OWASP A06).
   - Pre-deployment secret scanning (Gitleaks) & client-side ESLint.
   - Bicep template compilation and live Azure ARM validation for `infra/app.bicep`.
   - Idempotent resource group verification and creation.
2. **Stage 2: Preview (`preview-application`)**:
   - Azure ARM What-If change detection (`az deployment group what-if --template-file ./infra/app.bicep`).
   - Summarizes planned container app revisions and ingress modifications in `$GITHUB_STEP_SUMMARY`.
3. **Stage 3: Deploy (`deploy-application`)**:
   - Build OCI Container image via Podman.
   - Container vulnerability scan via Trivy (`--severity CRITICAL`).
   - Push image to Azure Container Registry (ACR).
   - Deploy Container App workload via `Azure/arm-deploy`.
   - Live revision health check and FQDN reporting.

---

## 4. Verification Evidence

1. **Local Test Suite**:
   - `Nutrition.Domain.Tests`: 36 / 36 passed (172 ms)
   - `Nutrition.EvalHarness.Tests`: 207 / 207 passed (4 s)
   - `SecurityEnvironmentAndHeaderTests`: Asserted governance constraints and `@nikunjbanker` sole authority across both deployment workflows.
2. **Bash Syntax Verification**:
   - Validated that `RG_EXISTS=$(az group exists ...)` evaluates correctly for both `true` and `false` states.
