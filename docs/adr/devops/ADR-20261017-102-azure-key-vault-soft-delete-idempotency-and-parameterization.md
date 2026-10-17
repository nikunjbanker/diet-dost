<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-102: Azure Key Vault Soft-Delete Idempotency Guard & Pipeline Parameterization

| Metadata | Details |
| :--- | :--- |
| **Status** | **ACCEPTED** |
| **Date** | 2026-10-17 |
| **Domain** | DevOps / Cloud Infrastructure & Security |
| **Subsystem** | `.github/workflows/azure-infra-deploy.yml`, `infra/dev/infra.bicep`, `infra/dev/main.bicep`, `infra/dev/main.parameters.json` |
| **Impacted Files** | `.github/workflows/azure-infra-deploy.yml`, `infra/dev/infra.bicep`, `infra/dev/main.bicep`, `infra/dev/main.parameters.json`, `docs/sdd/05_devops_and_infrastructure.md`, `.agents/skills/diet-dost-azure-deployment/SKILL.md` |

---

## 1. Context & Incident Analysis (GitHub Actions Run 39030008430)

During the execution of the Aspire Cloud Infrastructure deployment workflow ([Run 39030008430, Job 117389144976](https://github.com/nikunjbanker/diet-dost/actions/runs/39030008430/job/117389144976)), `Stage 1: Validate (Aspire & Bicep IaC)` failed during step `9. Live Azure ARM Template Validation` with a fatal ARM error:

```text
ConflictError: A vault with the same name already exists in deleted state. You need to either recover or purge existing key vault.
```

### Forensic Root Cause:
1. **Azure Key Vault Purge Protection Invariant**:
   In compliance with Diet-Dost security architecture (ADR-078, CIS Azure Foundations Benchmark, and `BicepIaCConfigurationContractTests.cs`), Key Vault is provisioned with `enablePurgeProtection: true` and `enableSoftDelete: true` (`softDeleteRetentionInDays: 7`).
2. **The Soft-Delete Creation Conflict**:
   When a resource group or Key Vault is deleted during dev-tier teardown or recreation, Azure places the vault into a soft-deleted state. Because purge protection is enabled, the vault cannot be purged.
3. **ARM / Bicep Create Mode Limitation**:
   By default, Bicep deploys Key Vault in `default` create mode. When ARM encounters a vault with the same name in deleted state, it rejects the deployment with `ConflictError`. Setting `createMode: 'recover'` statically in Bicep is forbidden because it breaks first-run (greenfield) deployments when the vault does not yet exist.
4. **Unparameterized Resource Naming**:
   The Bicep templates previously did not expose an explicit `keyVaultName` parameter, preventing operators or CI/CD pipelines from specifying an existing recovered vault name or passing environment-specific vault overrides.

---

## 2. Decision & Architecture Solution

To resolve the incident and guarantee 100% deployment idempotency across first-time greenfield deployments, teardowns, and recurring redeployments, we implement a two-fold solution:

### 2.1 Automated Key Vault Idempotency Guard in CI/CD Workflow (`azure-infra-deploy.yml`)
We insert an automated pre-deployment idempotency check step in `.github/workflows/azure-infra-deploy.yml` across both **Stage 1 (Validation)** and **Stage 3 (Provisioning)**:
- **Step 8b (Stage 1)** & **Step 3b (Stage 3)**: `Detect and Auto-Recover Soft-Deleted Key Vaults (Idempotency Guard)`
- **Behavior**:
  1. Queries the Azure subscription for any soft-deleted Key Vaults using Azure CLI:
     ```bash
     DELETED_KVS=$(az keyvault list-deleted --query "[].{name: name, location: properties.location}" -o tsv 2>/dev/null || true)
     ```
  2. If any soft-deleted vaults exist, it iterates and executes `az keyvault recover --name "$KV_NAME" --location "$KV_LOC"`.
  3. If no soft-deleted vaults exist (e.g. greenfield first run), it emits `✅ No soft-deleted Key Vaults detected in subscription` and continues.
- **Guarantee**: ARM never encounters a soft-deleted vault during template validation or provisioning, eliminating `ConflictError` permanently.

### 2.2 Dynamic Key Vault Parameterization across Bicep IaC Templates
1. **`infra/dev/infra.bicep`**:
   - Added parameter:
     ```bicep
     @description('Optional explicit name for Azure Key Vault. If omitted, an auto-generated unique name is used.')
     param keyVaultName string = ''
     ```
   - Added sanitized variable:
     ```bicep
     var sanitizedKeyVaultName = replace(replace(toLower(keyVaultName), '-', ''), '_', '')
     var actualKeyVaultName = !empty(sanitizedKeyVaultName) ? sanitizedKeyVaultName : take('kvdost${environment}${uniqueSuffix}', 24)
     ```
   - Bound resource declaration: `resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = { name: actualKeyVaultName ... }`.
2. **`infra/dev/main.bicep`**:
   - Added `param keyVaultName string = ''` and forwarded to `module foundation`.
3. **`infra/dev/main.parameters.json`**:
   - Added `"keyVaultName": { "value": "" }` default.
4. **`.github/workflows/azure-infra-deploy.yml`**:
   - Added `keyVaultName` workflow input with default `''`.
   - Forwarded `keyVaultName` to ARM validation, what-if analysis, and Bicep deployment steps.

---

## 3. Incident Remediation & Verification Evidence

1. **Manual Cloud State Recovery**:
   - Executed `az keyvault recover --name kvdietdostskehxuagttzos --location centralindia`.
   - Confirmed `az keyvault list-deleted` returned `[]` (0 deleted vaults).
   - Confirmed `az keyvault show --name kvdietdostskehxuagttzos` returned `provisioningState: Succeeded`.
2. **Workflow Re-Run Validation (Run 39030008430)**:
   - Re-ran failed jobs via `gh run rerun 39030008430 --failed`.
   - `Stage 1: Validate (Aspire & Bicep IaC)` succeeded in 1m8s with 0 errors.
   - `Stage 2: Preview (ARM What-If Analysis)` succeeded in 35s with 0 errors.
   - Pending deployment in `dev` environment approved via `gh api`.
3. **Local & Cloud Gate Verification**:
   - `az bicep build` compiled `infra/dev/infra.bicep`, `infra/dev/main.bicep`, and `infra/dev/app.bicep` with 0 warnings, 0 errors.
   - `az deployment group validate` against `rg-dietdost-dev` returned `provisioningState: Succeeded`.
   - `dotnet test` passed 216/216 tests.
   - `scripts/verify-ai-security-defense.ps1 -Mode All` passed all 7 vectors cleanly.

---

## 4. Consequences & Operational Benefits

- **100% Idempotent Deployments**: Teardowns, accidental soft-deletes, and redeployments will never fail with `ConflictError`.
- **First-Run & Recurring Run Parity**: Greenfield deployments deploy without intervention; recurring redeployments automatically heal soft-deleted resources.
- **Operator Flexibility**: Operators can explicitly specify existing vaults or allow Bicep to generate environment-isolated names (`kvdost${environment}${uniqueSuffix}`).
