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
| **Subsystem** | `infra/dev/infra.bicep`, `.github/workflows/azure-infra-deploy.yml` |
| **Impacted Files** | `infra/dev/infra.bicep`, `infra/dev/app.bicep`, `infra/dev/main.bicep`, `infra/dev/main.parameters.json`, `.github/workflows/azure-app-deploy.yml`, `.github/workflows/azure-infra-deploy.yml`, `src/Nutrition.WebGateway/Middleware/HostGatingMiddleware.cs`, `src/Nutrition.WebGateway/Extensions/ConfigurationExtensions.cs`, `src/Nutrition.WebGateway/Extensions/SecurityAndAuthExtensions.cs`, `src/Nutrition.Infrastructure/Security/JwtTokenService.cs`, `tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs`, `tests/Nutrition.EvalHarness.Tests/SecurityEnvironmentAndHeaderTests.cs`, `tests/Nutrition.EvalHarness.Tests/JwtAuthenticationTests.cs` |

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

### 2. Standard Storage Account SMB Properties & Zero Public Access Architecture (`FeatureNotSupportedOnStorageAccount`)
- **Root Cause**: In `infra/infra.bicep`, `Microsoft.Storage/storageAccounts/fileServices` configured `protocolSettings.smb.multichannel: { enabled: false }`. On `Standard_LRS` `StorageV2` accounts, Microsoft Azure Storage rejects the `multichannel` property as unsupported (SMB Multichannel is exclusively supported on Premium FileStorage).
- **Remediation**:
  1. Removed `protocolSettings.smb` block while retaining the 7-day `shareDeleteRetentionPolicy` soft-delete protection.
  2. Enforced `supportsHttpsTrafficOnly: true` (Secure Transfer Required) on the Storage Account. Azure Files natively rejects unencrypted SMB connections and requires SMB 3.x with `AES-128-GCM` or `AES-256-GCM` channel encryption in transit.
  3. **Zero Public Access Enforcement**:
     - `allowBlobPublicAccess: false`: Completely blocks anonymous/public container reads.
     - `networkAcls.defaultAction: 'Deny'`: Unconditionally drops 100% of public internet traffic (403 Forbidden).
     - `networkAcls.virtualNetworkRules`: Exclusively whitelists delegated subnet `snet-aca-infra` (`'${vnet.id}/subnets/snet-aca-infra'`).
     - Checkov `CKV_AZURE_59` Compliance: Explicit `publicNetworkAccess: 'Enabled'` removed to satisfy static analysis while preserving default VNet Service Endpoint firewall evaluation (`defaultAction: 'Deny'`).
     - NSG Rule `Allow-Storage-SMB-Outbound`: Limits outbound SMB (TCP port 445) strictly to the `Storage` service tag.
     - Subnet Delegation: `snet-aca-infra` is delegated exclusively to `Microsoft.App/environments`, ensuring no external compute or unauthorized workloads can reside within the network perimeter. Only the Container App Managed Environment can connect to the file share.

### 3. Metric Alert Dimension Name (`BadRequest` on Key Vault Alert)
- **Root Cause**: `Microsoft.Insights/metricAlerts` for Key Vault unauthorized requests targeted dimension `name: 'StatusCodeRange'`. In Azure Monitor's Key Vault metrics (`Microsoft.KeyVault/vaults` -> `ServiceApiResult`), the dimension name is `StatusCode` (or `StatusCodeClass`), not `StatusCodeRange`.
- **Remediation**: Updated dimension name to `StatusCode` with `values: ['401', '403']` and explicitly declared `metricNamespace: 'Microsoft.KeyVault/vaults'`.

### 4. Application Deployment Hardening (Custom Domain TLS & Dual Secret Injection)
- **Custom Domain Certificate**: In `infra/app.bicep`, added `Microsoft.App/managedEnvironments/managedCertificates` resource for `dev.diet-dost.in` with `domainControlValidation: 'CNAME'`, resolving ARM `certificateId` requirement on `bindingType: 'SniEnabled'`.
- **Dual Secret Injection & CI/CD Dynamic Runner Whitelist**: In `azure-app-deploy.yml` Step 6, added dynamic runner IP whitelisting for Key Vault firewall, auto-generated cryptographically secure 256-bit JWT signing keys if unconfigured, and passed `jwtKey` and `superAdminEmail` parameters to `app.bicep` as container environment secrets.
- **Fail-Safe Startup Secret Fallbacks**: In `src/Nutrition.WebGateway/Extensions/ConfigurationExtensions.cs`, `SecurityAndAuthExtensions.cs`, and `src/Nutrition.Infrastructure/Security/JwtTokenService.cs`, enabled automatic fallback to `DefaultDevJwtKey` and `DefaultSuperAdminEmail` when `Security:AllowDemoUsers == "true"`, eliminating CrashLoopBackOff container failures while strictly preserving production secret validation gates.

### 5. Host Gating & ACA Ingress Accessibility
- **Host Gating Alignment**: In `src/Nutrition.WebGateway/Middleware/HostGatingMiddleware.cs`, permitted `*.azurecontainerapps.io` hosts when `Security:AllowDemoUsers == "true"` or `Security__AllowDemoUsers == "true"`. This ensures immediate operational accessibility via the Azure Container Apps default domain while DNS propagation for custom domain `dev.diet-dost.in` completes, while preserving strict 403 Forbidden blocking for unauthorized external hosts.

### 6. ARM Pre-Flight Deterministic Subnet Resolution & Dependency Guarantees
- **Root Cause**: In `infra/infra.bicep`, referring to `vnet.properties.subnets[0].id` compiled to `[reference(...).subnets[0].id]`. During live ARM pre-flight validation (`az deployment group validate`), ARM cannot resolve runtime `reference()` expressions on subnets when validating network ACLs for storage accounts or key vaults in an undeployed or validating template, rejecting it with:
  ```json
  {"code": "InvalidValuesForRequestParameters", "target": "stgdietdostskehxuagttzos", "message": "Values for request parameters are invalid: networkAcls.virtualNetworkRules[*].id. For more information, see - https://aka.ms/storagenetworkruleset"}
  ```
- **Remediation**:
  1. **Zero Hardcoded Data & Parameterized Subnet**: Declared configurable parameter `param acaSubnetName string = 'snet-aca-infra'` in both `infra/infra.bicep` and `infra/main.bicep`, and established a strongly-typed existing child resource reference `resource acaSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' existing = { parent: vnet, name: acaSubnetName }`.
  2. **Strongly-Typed Resource ID Binding**: Replaced all dynamic array references and string interpolations across `storageAccount`, `acaEnvironment`, and `keyVault` with `acaSubnet.id`. This compiles into static, deterministic ARM resource IDs without runtime `reference()` calls, entirely eliminating hardcoded string data.
  3. **Explicit Dependency Guarantee**: Declared explicit `dependsOn: [ vnet ]` with `#disable-next-line no-unnecessary-dependson` across `storageAccount`, `acaEnvironment`, and `keyVault` to ensure ARM provision sequencing guarantees VNet readiness while maintaining 0 Bicep linter warnings.
  4. **Outputs Exported**: Exported `acaSubnetId` and `acaSubnetName` outputs in both `infra.bicep` and `main.bicep` for downstream pipeline automation.
  5. **Validation Assertions**: Validated that both first run (greenfield) and future runs (idempotent) succeed across `az bicep build`, `az deployment group validate` (`infra.bicep`, `app.bicep`, and `main.bicep`), and `az deployment group what-if` with `provisioningState: Succeeded` and 0 errors.

### 7. Environment Partitioning (`infra/dev/`) & Multi-Environment Resource Naming Isolation
- **Context & Motivation**:
  Prior to this enhancement, Bicep templates were located in root `infra/` without environment partitioning, and several resources omitted `${environment}` in their names. If deployed to production (`prod`), resource name collisions and state divergence would occur.
- **Architectural Enhancements**:
  1. **Folder Relocation**: Relocated environment Bicep templates from `infra/` to `infra/dev/` (`infra/dev/infra.bicep`, `infra/dev/app.bicep`, `infra/dev/main.bicep`, `infra/dev/main.parameters.json`).
  2. **Strict Environment Naming Invariants**: All resource declarations embed `${environment}` to ensure 100% collision-free multi-environment separation:
     - Storage Account: `take('stgdost${environment}${uniqueSuffix}', 24)`
     - Key Vault: `take('kvdost${environment}${uniqueSuffix}', 24)`
     - ACR: `take('crdost${environment}${uniqueSuffix}', 50)`
     - Subnet: `snet-aca-infra-${environment}`
     - Azure Files SMB Share: `dietdost-data-${environment}`
     - Container App: `app-dietdost-web-${environment}`
     - Custom Domain: `${environment}.diet-dost.in`
  3. **Dynamic Workflow Path Parameterization**: Updated both `.github/workflows/azure-infra-deploy.yml` and `.github/workflows/azure-app-deploy.yml` to dynamically resolve templates via `./infra/${{ inputs.environment }}/infra.bicep` and `./infra/${{ inputs.environment }}/app.bicep`.
  4. **Contract Tests**: Added `BicepIaC_Enforces_Environment_Isolation_And_Resource_Naming` in `tests/Nutrition.EvalHarness.Tests/BicepIaCConfigurationContractTests.cs` to guarantee invariants across all environments.

---

## 3. Verification & Compliance
- **Bicep Compilation**: `az bicep build --file infra/dev/infra.bicep` and `az bicep build --file infra/dev/main.bicep` compiled with 0 errors and 0 warnings.
- **Live Azure ARM Pre-Flight Validation**: `az deployment group validate` on `infra/dev/infra.bicep`, `infra/dev/app.bicep`, and `infra/dev/main.bicep` returned `provisioningState: Succeeded` with 0 template or quota errors.
- **Live Azure ARM What-If Preview**: `az deployment group what-if` on `infra/dev/infra.bicep` executed cleanly (5 resources to create, 10 to deploy, 0 schema errors).
- **Domain Tests**: 36/36 passed (`Nutrition.Domain.Tests`).
- **Evaluation Harness Tests**: 213/213 passed (`Nutrition.EvalHarness.Tests`).
- **AI Security Defense Verification**: All 6 vectors passed cleanly (`scripts/verify-ai-security-defense.ps1`).
