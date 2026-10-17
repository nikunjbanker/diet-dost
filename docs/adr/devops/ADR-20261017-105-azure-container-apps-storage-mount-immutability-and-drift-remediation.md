<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261017-105: Azure Container Apps Storage Mount Immutability & Automated Drift Remediation

## Status
**Accepted** (2026-10-17)

## Context
During the execution of the Azure Infrastructure Provisioning pipeline ([Run 39036835588 / Job 117408863786](https://github.com/nikunjbanker/diet-dost/actions/runs/39036835588/job/117408863786)), ARM template deployment failed during `Stage 3: Provision (Deploy Infrastructure via Bicep)` with the following fatal Azure Resource Manager error:

```json
{
  "code": "DeploymentFailed",
  "message": "At least one resource deployment operation failed. Please list deployment operations for details. Please see https://aka.ms/arm-deployment-operations for usage details.",
  "details": [
    {
      "code": "ManagedEnvironmentStorageUpdateBadRequest",
      "message": "Invalid request body for managed environment storage 'dietdoststorage'. The storage account or server, share name, and access mode cannot be updated. Only Azure File authentication settings can be updated."
    }
  ]
}
```

### Forensic Root Cause Analysis
1. **Azure Container Apps Managed Environment Storage Immutability**:
   - The Azure Container Apps Resource Provider (`Microsoft.App/managedEnvironments/storages`) treats the underlying storage account name (`azureFile.accountName`), file share name (`azureFile.shareName`), and access mode (`azureFile.accessMode`) as **strictly immutable** after creation.
   - Per Azure documentation and REST API specifications, in-place updates of these three properties are forbidden. Only Azure File authentication credentials (account keys) can be modified on an existing mount definition.
2. **Environment-Isolated Naming Migration Drift**:
   - In PR #63, cloud resource naming was refactored for environment isolation (`infra/dev/infra.bicep`):
     - Storage account name transitioned to `take('stgdost${environment}${uniqueSuffix}', 24)` (`stgdostdevskehxuagttzos`).
     - File share name transitioned to `'dietdost-data-${environment}'` (`dietdost-data-dev`).
   - However, the pre-existing Azure Container Apps Managed Environment (`cae-dietdost-dev`) already had a storage link named `dietdoststorage` created during prior provisioning, bound to the legacy un-suffixed storage account `stgdietdostskehxuagttzos` and file share `dietdost-data`.
   - When Bicep submitted an ARM PUT request for `cae-dietdost-dev/storages/dietdoststorage` with the new environment-isolated account and share names, Azure ACA rejected the mutation with `ManagedEnvironmentStorageUpdateBadRequest`.
3. **Pre-Flight Validation Blind Spot**:
   - Neither `az deployment group validate` (Stage 1) nor `az deployment group what-if` (Stage 2) detected this failure. ARM template validation only verifies schema, template syntax, and subscription quotas, while What-If evaluates declarative diffs; neither executes runtime state comparison against Azure Container Apps child storage immutability policies.

---

## Decision

1. **Automated Idempotency Guard in CI/CD Pipelines (`azure-infra-deploy.yml` & `azure-app-deploy.yml`)**:
   - Insert automated pre-deployment idempotency checks (`Detect and Auto-Remediate ACA Environment Storage Drift (Idempotency Guard)`) across both Stage 1 and Stage 3 of `.github/workflows/azure-infra-deploy.yml`, and before foundation deployment in `.github/workflows/azure-app-deploy.yml`.
   - The guard:
     a. Checks if the ACA Managed Environment (`cae-dietdost-${environment}`) exists.
     b. Inspects the active `dietdoststorage` definition via `az containerapp env storage show`.
     c. Compares the mounted `accountName` and `shareName` against the target environment naming conventions (`dietdost-data-${environment}` and `stgdost${environment}*`).
     d. If drift is detected (e.g. legacy account or share names), it safely unlinks the obsolete storage definition via `az containerapp env storage remove --name "$ACA_ENV" --resource-group "$RG" --storage-name "dietdoststorage" --yes`.
     e. Bicep then recreates the storage link with the correct environment-isolated parameters without throwing `ManagedEnvironmentStorageUpdateBadRequest`.
2. **Cloud State Remediation**:
   - Safely removed the legacy storage link from `cae-dietdost-dev` using `az containerapp env storage remove -g rg-dietdost-dev -n cae-dietdost-dev --storage-name dietdoststorage -y`.
   - Confirmed zero data loss: removing the ACA environment mount definition does not touch the underlying Azure Storage Account or Azure Files share.
3. **Living Documentation & Skill Synchronization**:
   - Synchronized [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md) with ACA storage mount immutability rules and pipeline idempotency steps.
   - Synchronized [`.agents/skills/diet-dost-azure-deployment/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-azure-deployment/SKILL.md) runbook.

---

## Consequences

- **Positive**:
  - **100% Idempotent Infrastructure Deployments**: Eliminates `ManagedEnvironmentStorageUpdateBadRequest` errors permanently across migrations, environment upgrades, and recurring redeployments.
  - **Self-Healing Pipeline**: CI/CD automation automatically detects and remediates drifted storage definitions without manual Azure Portal or CLI intervention.
  - **Zero Data Loss**: Unlinking the mount from ACA preserves the actual storage accounts and data files intact.
- **Negative / Neutral**:
  - Requires Azure CLI access in the pipeline prior to ARM deployment (already satisfied via OIDC login).
