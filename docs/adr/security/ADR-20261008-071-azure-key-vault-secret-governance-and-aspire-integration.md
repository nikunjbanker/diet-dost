<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261008-071: Azure Key Vault Secret Governance and .NET Aspire Integration in Deployed Environments

* **Status**: Accepted
* **Date**: 2026-10-08
* **Author**: AI Architectural Agent & nikunjbanker
* **Deciders**: Solution Engineering Architecture Board
* **Tags**: `azure-key-vault`, `managed-identity`, `aspire-keyvault`, `secret-governance`, `owasp-asvs`, `jwt-security`, `dotnet-11`, `forward-roadmap`

---

## 1. Context & Problem Statement

Prior to this decision:
1. **Hardcoded Container Secret Risk**: `Containerfile` contained a hardcoded fallback environment variable:
   ```dockerfile
   ENV Jwt__Key="DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!"
   ```
   While acceptable during early local prototype container builds, shipping hardcoded cryptographic signing secrets in production OCI images violates OWASP ASVS V2/V3 (Authentication & Cryptographic Architecture) and enterprise security governance.
2. **Missing Deployed Secret Engine**: In non-development / deployed environments (e.g. Azure Container Apps showcase and production), critical application secrets (`Jwt:Key`, `Auth:SuperAdminEmail`, `Auth:RequireMobileVerification`, `AI:GoogleAI:ApiKey`, `AI:AzureOpenAI:ApiKey`, `ConnectionStrings:DefaultConnection`) must be dynamically and securely retrieved from **Azure Key Vault** using **Passwordless Azure Managed Identity** rather than static plaintext files or container environment variables.
3. **Local Developer Ergonomics**: Developers running locally (`dotnet run`, debug mode in IDE) must not be forced to connect to Azure or have live Azure credentials to run or debug the solution.

---

## 2. Decision Drivers

1. **Zero Hardcoded Secrets in Production**: Eliminate all hardcoded cryptographic keys and credentials from `Containerfile`, repository commits, and release images.
2. **Passwordless Managed Identity Governance**: Authenticate to Azure Key Vault in deployed environments strictly via User-Assigned Managed Identity (`DefaultAzureCredential` with `AZURE_CLIENT_ID`), requiring zero client secrets or passwords.
3. **Transparent ASP.NET Core IConfiguration Hierarchy**: In .NET 11, Azure Key Vault secret names map double-dash (`--`) to colon (`:`) configuration paths (e.g. `Jwt--Key` $\to$ `Jwt:Key`, `AI--GoogleAI--ApiKey` $\to$ `AI:GoogleAI:ApiKey`, `Auth--SuperAdminEmail` $\to$ `Auth:SuperAdminEmail`). Key Vault provider must register with highest precedence, seamlessly overriding local database fallbacks and `appsettings.json`.
4. **Fail-Fast Defense-in-Depth**: In non-development/deployed environments (`!env.IsDevelopment()`), if `Jwt:Key` is missing or empty, the application must throw an immediate `InvalidOperationException` and abort startup, preventing insecure fallback execution.
5. **.NET Aspire Native Integration**: Declare Azure Key Vault in `Nutrition.AppHost` (`builder.AddAzureKeyVault("dietdost-kv")`) and wire it to `web-gateway` via `WithReference(keyVault)`, harmonizing Aspire deployment pipelines with Bicep infrastructure templates.

---

## 3. Decision Outcome & Architecture

### 3.1. Containerfile Hardcoded Secret Removal
Removed `ENV Jwt__Key=...` from `Containerfile`. Production containers now rely strictly on Azure Key Vault or container orchestration environment injection.

### 3.2. Application Configuration & Precedence Pipeline (`Nutrition.WebGateway`)
In `Program.cs`, registered `AddAzureKeyVault` after `AddDatabaseSecrets`:
```csharp
// 0a. Local database secret fallback (for offline developer ergonomics)
var dbConnectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";
builder.Configuration.AddDatabaseSecrets(dbConnectionString);

// 0b. Azure Key Vault Secrets Integration (Deployed / Cloud Environment)
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"]
    ?? builder.Configuration["KEY_VAULT_URI"]
    ?? builder.Configuration.GetConnectionString("dietdost-kv");

if (!string.IsNullOrWhiteSpace(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}
```

### 3.3. Non-Development Secret Gating
In `SecurityAndAuthExtensions.cs` and `JwtTokenService.cs`:
- When running in `Development` mode (`env.IsDevelopment()` / `appEnv.IsDevelopment`), safe development fallback keys are permitted for zero-friction local developer testing.
- When running in non-development mode (`Production`, `Staging`, deployed ACA), missing `Jwt:Key` throws:
  ```csharp
  throw new InvalidOperationException(
      "CRITICAL SECURITY CONFIGURATION ERROR: 'Jwt:Key' is not configured. " +
      "In non-development / deployed environments, the cryptographic JWT signing key MUST be provided via Azure Key Vault or secure environment variables.");
  ```

### 3.4. Unified Secret Store Prioritization (`DatabaseSecretStore`)
In `DatabaseSecretStore.cs`, `GetSecretAsync(key)` now prioritizes `IConfiguration[key]` (which pulls directly from Azure Key Vault or environment variables) before falling back to the local `AppSecrets` database table.

### 3.5. Aspire AppHost Integration (`Nutrition.AppHost`)
Added `Aspire.Hosting.Azure.KeyVault` (13.5.4) and declared:
```csharp
var keyVault = builder.AddAzureKeyVault("dietdost-kv");
builder.AddWebGateway(keyVault);
```
In `WebGatewayResourceExtensions.cs`:
```csharp
if (keyVault != null)
{
    webGateway.WithReference(keyVault);
}
```
Verified via `aspire publish`, which automatically generates `dietdost-kv.bicep`, `web-gateway-identity.bicep`, and `web-gateway-roles-dietdost-kv.bicep`.

### 3.6. Infrastructure as Code Automation (`infra/`)
- **`infra/infra.bicep`**:
  - Provisions User-Assigned Managed Identity `id-dietdost-${environment}`.
  - Provisions Azure Key Vault `kvdietdost<suffix>` with RBAC authorization (`enableRbacAuthorization: true`).
  - Assigns `Key Vault Secrets User` role (`4633458b-17de-408a-b874-0445c86b69e6`) to the managed identity.
  - Exports `keyVaultName`, `keyVaultUri`, `managedIdentityId`, `managedIdentityClientId`.
- **`infra/app.bicep`**:
  - Attaches the User-Assigned Managed Identity to the ACA Container App.
  - Injects `KeyVault__VaultUri` and `AZURE_CLIENT_ID` into container environment variables.
- **`.github/workflows/azure-infra-deploy.yml` & `azure-app-deploy.yml`**:
  - Resolves and passes Key Vault parameters to Bicep deployments automatically.

---

## 4. Key Vault Secret Mapping Matrix

| Configuration Path (.NET) | Key Vault Secret Name | Purpose | Deployed Policy | Local / Debug Policy |
| :--- | :--- | :--- | :--- | :--- |
| `Jwt:Key` | `Jwt--Key` | Cryptographic JWT HMAC-SHA256 signing key ($\ge 32$ bytes) | **Mandatory** from Key Vault | Dev default fallback |
| `Jwt:Issuer` | `Jwt--Issuer` | JWT Token Issuer | Key Vault / Config (`DietDostGateway`) | `DietDostGateway` |
| `Jwt:Audience` | `Jwt--Audience` | JWT Token Audience | Key Vault / Config (`DietDostClient`) | `DietDostClient` |
| `Jwt:ExpiryMinutes` | `Jwt--ExpiryMinutes` | Token expiration in minutes | Key Vault / Config (default 1440) | 1440 min (24h) |
| `Auth:SuperAdminEmail` | `Auth--SuperAdminEmail` / `SuperAdminEmail` | Seeded superadmin email identity | Key Vault / Config | `superadmin@dietdost.app` |
| `Auth:RequireMobileVerification` | `Auth--RequireMobileVerification` / `RequireMobileVerification` | Mandatory mobile SMS verification gate | Key Vault / Config | Config / false |
| `AI:GoogleAI:ApiKey` | `AI--GoogleAI--ApiKey` | Google Gemini Vision API Key | Key Vault / Container Secret | Config / User Secrets |
| `AI:AzureOpenAI:ApiKey` | `AI--AzureOpenAI--ApiKey` | Azure OpenAI API Key | Key Vault / Container Secret | Config / User Secrets |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings--DefaultConnection` | Persistent database connection string | Key Vault / SMB Mount Config | `Data Source=diettracker.db` |
| `Database:Provider` | `Database--Provider` | Database engine (`Sqlite`, `SqlServer`, etc.) | Key Vault / Config (`Sqlite`) | `Sqlite` |

---

## 5. Verification & Test Evidence

1. **Compilation**: Built entire solution with `.NET 11` (0 warnings, 0 errors).
2. **Unit Test Suite**: **193 / 193 unit and domain tests passed (100% pass rate)**.
   - Added `JwtTokenService_InDeployedEnvironment_WithoutJwtKey_ThrowsInvalidOperationException` (PASS).
   - Added `JwtTokenService_InDevelopmentEnvironment_WithoutJwtKey_UsesFallbackKey` (PASS).
   - Added `SecurityAndAuthExtensions_InDeployedEnvironment_WithoutJwtKey_ThrowsInvalidOperationException` (PASS).
   - Added `SecurityAndAuthExtensions_InDevelopmentEnvironment_WithoutJwtKey_Succeeds` (PASS).
   - Added `GetSecretAsync_PrioritizesConfigurationOverDatabaseTable` (PASS).
3. **Bicep Manifest Validation**:
   - `az bicep build --file infra/infra.bicep` (PASS, exit code 0).
   - `az bicep build --file infra/app.bicep` (PASS, exit code 0).
   - `az bicep build --file infra/main.bicep` (PASS, exit code 0).
4. **Aspire CLI Publish Pipeline**:
   - `aspire publish --apphost src/Nutrition.AppHost` (11/11 steps succeeded, exit code 0).
   - Verified generation of `dietdost-kv.bicep`, `web-gateway-identity.bicep`, and `web-gateway-roles-dietdost-kv.bicep`.

---

## 6. Forward Roadmap Impact & Future Phase Compatibility

1. **Phase 2 Mobile BFF & App Compatibility**:
   Mobile BFF endpoints (`/api/mobile/v1/*`) rely on the same `IJwtTokenService` and `ISecretStore` instances. By eliminating hardcoded keys, Mobile BFF tokens generated in deployed environments are cryptographically validated against secrets sourced from Azure Key Vault.
2. **Phase 3 Enterprise Cloud Persistence (Azure SQL Serverless)**:
   When transitioning from SQLite SMB mount to Azure SQL Serverless Free Tier, the database connection string (`ConnectionStrings--DefaultConnection`) will be injected directly from Azure Key Vault without modifying application code or container images.
3. **Zero Technical Debt**:
   Adheres to all repository rules in `AGENTS.md` (Zero-Warning, Zero Direct-to-Main, Fragment Pattern, Passwordless Managed Identity).
