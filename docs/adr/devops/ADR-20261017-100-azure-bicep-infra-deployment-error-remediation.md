<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-100: Azure Bicep Infrastructure Deployment Error Remediation (ACR Naming, Standard Storage SMB, and Metric Alert Dimensions)

| Metadata | Details |
| :--- | :--- |
| **Status** | **ACCEPTED** |
| **Date** | 2026-10-17 |
| **Domain** | DevOps / Cloud Infrastructure as Code |
| **Subsystem** | `infra/infra.bicep`, `.github/workflows/azure-infra-deploy.yml` |
| **Impacted Files** | `infra/infra.bicep`, `.github/workflows/azure-infra-deploy.yml` |

---

## 1. Context & Problem Statement

In GitHub Actions workflow run [39020523546](https://github.com/nikunjbanker/diet-dost/actions/runs/39020523546/job/117362263045), `Stage 1: Validate` and `Stage 2: Preview` executed and succeeded. During `Stage 3: Provision (Deploy Infrastructure via Bicep)`, Azure Resource Manager deployment `infra` failed with three distinct resource provider errors:

```json
{
  "code": "DeploymentFailed",
  "details": [
    {
      "code": "FeatureNotSupportedOnStorageAccount",
      "message": "Feature is not supported on this storage account."
    },
    {
      "code": "BadRequest",
      "target": "/providers/Microsoft.Insights/metricAlerts/alert-kv-unauthorized-dev",
      "message": "The metric ServiceApiResult specifies a dimension StatusCodeRange which was not found."
    },
    {
      "code": "ResourceNameInvalid",
      "message": "Invalid resource name: 'acr-dietdost-dev'. Resource names may contain alpha numeric characters only and must be between 5 and 50 characters."
    }
  ]
}
```

---

## 2. Root Cause Analysis & Remediation

### 1. ACR Naming Validation (`ResourceNameInvalid`)
- **Root Cause**: The user input or workflow parameter provided `acrName: 'acr-dietdost-dev'`. Azure Container Registry naming specifications strictly require alphanumeric characters only (no hyphens, lowercase letters and numbers only, 5–50 characters).
- **Remediation**: In `infra/infra.bicep`, added defensive normalization:
  ```bicep
  var sanitizedAcrName = replace(replace(toLower(acrName), '-', ''), '_', '')
  var actualAcrName = !empty(sanitizedAcrName) ? sanitizedAcrName : take('crdietdost${uniqueSuffix}', 50)
  ```
  This guarantees that any input containing hyphens (e.g. `acr-dietdost-dev`) is automatically sanitized to valid alphanumeric format (e.g. `acrdietdostdev`).
  Updated `.github/workflows/azure-infra-deploy.yml` input description to guide users towards alphanumeric names.

### 2. Standard Storage Account SMB Properties (`FeatureNotSupportedOnStorageAccount`)
- **Root Cause**: In `infra/infra.bicep`, `Microsoft.Storage/storageAccounts/fileServices` configured `protocolSettings.smb.multichannel: { enabled: false }`. On `Standard_LRS` `StorageV2` accounts, Microsoft Azure Storage rejects the `multichannel` property as unsupported (SMB Multichannel is exclusively supported on Premium FileStorage).
- **Remediation**: Removed `protocolSettings.smb` block while retaining the 7-day `shareDeleteRetentionPolicy` soft-delete protection. Azure Files SMB 3.1.1 encryption is natively utilized by Azure Container Apps volume mounts.

### 3. Metric Alert Dimension Name (`BadRequest` on Key Vault Alert)
- **Root Cause**: `Microsoft.Insights/metricAlerts` for Key Vault unauthorized requests targeted dimension `name: 'StatusCodeRange'`. In Azure Monitor's Key Vault metrics (`Microsoft.KeyVault/vaults` -> `ServiceApiResult`), the dimension name is `StatusCode` (or `StatusCodeClass`), not `StatusCodeRange`.
- **Remediation**: Updated dimension name to `StatusCode` with `values: ['401', '403']` and explicitly declared `metricNamespace: 'Microsoft.KeyVault/vaults'`.

---

## 3. Verification & Compliance
- **Bicep Compilation**: `az bicep build --file infra/infra.bicep` and `az bicep build --file infra/app.bicep` compiled with 0 errors and 0 warnings.
- **Domain Tests**: 36/36 passed (`Nutrition.Domain.Tests`).
- **Evaluation Harness Tests**: 207/207 passed (`Nutrition.EvalHarness.Tests`).
- **AI Security Defense Verification**: All 6 vectors passed cleanly (`scripts/verify-ai-security-defense.ps1`).
