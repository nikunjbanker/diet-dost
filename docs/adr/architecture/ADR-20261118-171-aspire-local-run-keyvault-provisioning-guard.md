<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261118-171: Aspire Local Run Mode Azure Key Vault Provisioning Guard

- **Status**: Accepted
- **Date**: 2026-11-18
- **Domain**: Architecture / DevOps / Security
- **Deciders**: Diet-Dost Engineering, DevOps & Distributed Architecture Team
- **Tracking Issue**: #169

---

## 1. Context & Problem Statement

When launching the .NET Aspire AppHost in local development via `aspire run` or `dotnet run` (Run Mode), the dashboard reported:
```text
Azure provisioning: The model contains Azure resources that require an Azure Subscription.
web-gateway "Start" failed: Failed to apply configuration to executable web-gateway
Failed to resolve environment variable 'ConnectionStrings__dietdost-kv' for resource 'web-gateway'.
Aspire.Hosting.Azure.MissingConfigurationException: Azure provisioning options were not provided.
at Aspire.Hosting.Azure.Provisioning.Internal.RunModeProvisioningContextProvider.CreateProvisioningContextAsync
```

### Root Cause Analysis
1. In `Program.cs`, `var keyVault = builder.AddAzureKeyVault("dietdost-kv");` declared an Azure Key Vault resource in the Aspire application model.
2. In `WebGatewayResourceExtensions.cs`, `webGateway.WithReference(keyVault)` established a mandatory dependency on `keyVault`.
3. In Aspire's local **Run Mode** (`builder.ExecutionContext.IsRunMode`), Aspire attempts to resolve the connection strings (`ConnectionStrings__dietdost-kv` and `DIETDOST_KV_URI`) for any referenced Azure resource via `RunModeProvisioningContextProvider`.
4. Because local developers do not configure an active Azure subscription for local provisioning (and per **ADR-072**, local development uses the local SQLite `AppSecrets` table rather than live cloud Key Vault), Aspire threw `MissingConfigurationException`, failing the `web-gateway` launch.

---

## 2. Decision & Implementation

We introduced an execution-mode guard separating **Publish Mode** (`builder.ExecutionContext.IsPublishMode` for CI/CD manifest and Bicep synthesis) from **Run Mode** (`builder.ExecutionContext.IsRunMode` for local development):

### 2.1 Conditional Resource Declaration (`Program.cs`)
- Declared cloud resources (`dietdost-storage` and `dietdost-kv`) only when `builder.ExecutionContext.IsPublishMode` is true, or if `Azure:SubscriptionId` is explicitly configured in local settings:
```csharp
// 2. Declare Azure Storage for persistent SQLite SMB file share in deployed environments
var storage = builder.ExecutionContext.IsPublishMode || !string.IsNullOrWhiteSpace(builder.Configuration["Azure:SubscriptionId"])
    ? builder.AddAzureStorage("dietdost-storage")
    : null;

// 3. Declare Azure Key Vault for application secrets in deployed environments
var keyVault = builder.ExecutionContext.IsPublishMode || !string.IsNullOrWhiteSpace(builder.Configuration["Azure:SubscriptionId"])
    ? builder.AddAzureKeyVault("dietdost-kv")
    : null;
```

### 2.2 Defensive Reference Resolution (`WebGatewayResourceExtensions.cs`)
- In `WebGatewayResourceExtensions.cs`, only call `webGateway.WithReference(keyVault)` during Publish Mode or when an Azure subscription is configured for live provisioning.
- In local Run Mode without Azure provisioning, forward any explicitly configured local Key Vault URI (`KeyVault:VaultUri` or `KEY_VAULT_URI`) directly as an environment variable:
```csharp
if (keyVault != null && (builder.ExecutionContext.IsPublishMode || !string.IsNullOrWhiteSpace(builder.Configuration["Azure:SubscriptionId"])))
{
    webGateway.WithReference(keyVault);
}
else
{
    var kvUri = builder.Configuration["KeyVault:VaultUri"]
        ?? builder.Configuration["KEY_VAULT_URI"]
        ?? builder.Configuration.GetConnectionString("dietdost-kv");

    if (!string.IsNullOrWhiteSpace(kvUri))
    {
        webGateway.WithEnvironment("KeyVault__VaultUri", kvUri);
    }
}
```

---

## 3. Verification & Validation

1. **Local Run Mode Verification**:
   - Launched `Nutrition.AppHost` locally via `dotnet run`.
   - Verified that `web-gateway` transitioned to `Ready` and `Running` without any `MissingConfigurationException`.
   - Verified HTTP 200 "Healthy" responses on `http://localhost:5240/healthz` and `http://localhost:5240/ready`.
   - Executed live multi-tier test suite (`pwsh -File tests/validate_e2e_tiers.ps1`): **100% pass rate** across Free, Basic, Premium, Admin, and SuperAdmin tiers.
2. **Publish Mode Manifest Synthesis**:
   - Executed `aspire publish --apphost src/Nutrition.AppHost --output-path ./aspire-output --non-interactive`.
   - Verified 11/11 steps succeeded in 0.16s, cleanly writing all Bicep templates including `dietdost-kv.bicep` and `web-gateway-roles-dietdost-kv.bicep`.
3. **Automated Unit & Integration Tests**:
   - `dotnet test` passed 100% across all 488 tests in the solution.
