<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-072: Centralized Application Configuration, Secret Governance & Strongly-Typed Ports

> **ADR ID**: `ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-08`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[ARCHITECTURE]`, `[SECURITY]`, `[DEVOPS]`  
> **Impacted Projects**: `Nutrition.Application`, `Nutrition.Infrastructure`, `Nutrition.WebGateway`, `.github/workflows/azure-app-deploy.yml`  
> **Related Documents**: [`ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-071-azure-key-vault-secret-governance-and-aspire-integration.md), [`ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md)

---

## 1. Executive Summary & Context

Prior to this architectural enhancement, secret and configuration extraction (`Jwt:Key`, `Auth:SuperAdminEmail`, `Auth:RequireMobileVerification`, `AI:GoogleAI:ApiKey`, etc.) was distributed across numerous components (`AuthController.cs`, `SecurityAndAuthExtensions.cs`, `JwtTokenService.cs`, `DatabaseInitializationExtensions.cs`, `AiProviderOptions.cs`). Each file maintained ad-hoc fallback logic (`||`, `if (isDev)`, environment variable lookups, or manual 32-byte validations).

This distributed pattern violated the Single Responsibility Principle (SRP) and the Don't Repeat Yourself (DRY) principle, created maintenance hazards, and led to inconsistent configuration state across environments.

Furthermore, developers testing locally required a safe, Git-ignored `appsettings.Development.json` that would not leak secrets into source control, while deployed environments (Azure Container Apps) required mandatory retrieval from **Azure Key Vault** using Passwordless User-Assigned Managed Identity.

---

## 2. Decision Drivers & Requirements

1. **Centralized Configuration & Single Source of Truth**:
   Wherever `IConfiguration` is injected across the solution, callers must receive pre-merged, normalized, and validated configuration data without writing repetitive fallback code.
2. **Environment-Specific Secret Sourcing**:
   - **Deployed Environment (`!env.IsDevelopment()`)**: Secrets MUST be fetched dynamically from **Azure Key Vault** using `DefaultAzureCredential` via `AddAzureKeyVault`.
   - **Local Development (`env.IsDevelopment()`)**: Secrets are loaded from `appsettings.Development.json`, local SQLite database (`AppSecrets` table), and safe developer defaults.
3. **Zero Git Bleed for Local Development Secrets**:
   `appsettings.Development.json` is strictly ignored by Git (`.gitignore` line 33), allowing local development with custom keys without risking Git commits.
4. **Strongly-Typed Application Port (`IDietDostConfiguration`)**:
   Provide a clean domain-layer port abstraction in `Nutrition.Application.Common.Interfaces` for typed configuration access, backed by `DietDostConfiguration` in `Nutrition.Infrastructure.Configuration`.
5. **Fail-Fast Startup Validation**:
   Enforce immediate fail-fast termination during application startup (`ValidateRequiredDeployedSecrets`) in non-development environments if critical cryptographic keys (`Jwt:Key` $\ge 32$ bytes) are missing.
6. **Aspire Pipeline Secret Synchronization**:
   Ensure CI/CD workflows (`.github/workflows/azure-app-deploy.yml`) synchronize secrets into Azure Key Vault during the Aspire deployment cycle.

---

## 3. Architecture & Implementation

### 3.1 Centralized Configuration Provider (`ConfigurationExtensions.cs`)
```csharp
public static IConfigurationBuilder AddDietDostAppConfiguration(
    this IConfigurationBuilder builder,
    IHostEnvironment env)
{
    // Local: connects SQLite database secrets provider
    // Deployed: connects Azure Key Vault via DefaultAzureCredential
    // Normalizes aliases: Auth:SuperAdminEmail <-> SuperAdminEmail
    // Normalizes aliases: Auth:RequireMobileVerification <-> RequireMobileVerification
    // Normalizes aliases: AI:GoogleAI:ApiKey <-> AI:ApiKey
    // Registers safe developer fallbacks in dev mode
}
```

### 3.2 Strongly-Typed Port (`IDietDostConfiguration`)
```csharp
public interface IDietDostConfiguration
{
    string JwtKey { get; }
    string JwtIssuer { get; }
    string JwtAudience { get; }
    int JwtExpiryMinutes { get; }
    string SuperAdminEmail { get; }
    bool RequireMobileVerification { get; }
    bool AllowRegistration { get; }
    string DefaultConnection { get; }
    string DatabaseProvider { get; }
    string AiProvider { get; }
    string GoogleAiModelId { get; }
    string GoogleAiFallbackModelId { get; }
    string? GoogleAiEndpoint { get; }
    string? GoogleAiApiKey { get; }
    string AzureOpenAiDeploymentName { get; }
    string? AzureOpenAiEndpoint { get; }
    string? AzureOpenAiApiKey { get; }
    int AiMaxTokens { get; }
    double AiTemperature { get; }
    bool AiShowModelDetails { get; }
    string? this[string key] { get; }
}
```

### 3.3 Consumer Code De-duplication
- **`AuthController.cs`**: Streamlined `GetAuthConfig` to read `_configuration.GetValue<bool>("Auth:RequireMobileVerification", false)`.
- **`SecurityAndAuthExtensions.cs`**: Simplified `Jwt:Key` retrieval to rely on pre-validated, normalized configuration.
- **`JwtTokenService.cs`**: Cleaned constructor, removing redundant fallback checks.
- **`DatabaseInitializationExtensions.cs`**: Standardized on `configuration["Auth:SuperAdminEmail"]`.

---

## 4. Forward Roadmap Impact & Future Phase Compatibility

Adhering to [ADR-052 (Zero-Throwaway Engineering)](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md):
- **Phase 2 Mobile BFF**: Direct access to `IDietDostConfiguration` and pre-normalized `IConfiguration` without duplicate mobile auth setup.
- **Phase 3 Enterprise Cloud DB**: Transitioning connection strings from SQLite to Azure SQL Serverless / PostgreSQL requires zero consumer code modifications; `ConfigurationExtensions` seamlessly routes the updated connection string from Key Vault.

---

## 5. Verification & Compliance Results

- **Compiler Verification**: `dotnet build` succeeded with **0 warnings and 0 errors** on .NET 11 preview.
- **Unit & Domain Test Suite**: `dotnet test` passed **196 / 196 tests** (36 domain tests + 160 eval harness tests, including 3 new centralized configuration tests).
- **Live Customer & Functional Acceptance Test (CFT)**: `pwsh -File tests/validate_e2e_tiers.ps1` achieved **100% pass rate** across all 5 user tiers (`Free`, `Basic`, `Premium`, `Admin`, `SuperAdmin`).
- **Git Hygiene**: Verified `src/Nutrition.WebGateway/appsettings.Development.json` is completely ignored by Git and never committed to source control.
- **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
