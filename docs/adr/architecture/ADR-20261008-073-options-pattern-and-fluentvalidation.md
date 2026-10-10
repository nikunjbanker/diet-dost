<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-073: Strongly-Typed Options Pattern & FluentValidation Configuration Governance

> **ADR ID**: `ADR-20261008-073-options-pattern-and-fluentvalidation`  
> **Status**: `ACCEPTED`  
> **Date**: `2026-10-08`  
> **Author**: `Diet-Dost Engineering Team`  
> **Category**: `[ARCHITECTURE]`, `[SECURITY]`, `[QUALITY]`  
> **Impacted Projects**: `Nutrition.Application`, `Nutrition.Infrastructure`, `Nutrition.WebGateway`, `Nutrition.EvalHarness.Tests`  
> **Related Documents**: [`ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20261008-072-centralized-configuration-and-key-vault-secret-governance.md), [`ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/ADR-20260930-052-reusability-and-forward-roadmap-compatibility-mandate.md)

---

## 1. Executive Summary & Context

To complement centralized configuration merging and secret retrieval (ADR-072), configuration settings must be accessible through the canonical **.NET Core Options Pattern** (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`).

Raw dictionary lookups (`configuration["Key"]`) lack compile-time type safety, intellisense, and schema grouping. Furthermore, declarative DataAnnotations validation (`[Required]`, `[MinLength]`) lacks advanced conditional logic (e.g. validating GoogleAI-specific keys vs AzureOpenAI-specific keys, or multi-byte cryptographic key length verification).

This ADR establishes:
1. Strongly-typed **Options classes** in `Nutrition.Application.Common.Options` (`JwtOptions`, `AuthOptions`, `AiOptions`, `DatabaseOptions`).
2. Comprehensive **FluentValidation validators** for each options group (`AbstractValidator<TOptions>`).
3. An integration bridge (`FluentValidationOptions<TOptions>` implementing `IValidateOptions<TOptions>`) combined with `.ValidateWithFluentValidation().ValidateOnStart()`.

---

## 2. Decision Drivers & Architecture

```
                                ┌────────────────────────────────────────┐
                                │     IConfiguration (Single Source)     │
                                └──────────────────┬─────────────────────┘
                                                   │
                       ┌───────────────────────────┴───────────────────────────┐
                       ▼                                                       ▼
        ┌─────────────────────────────┐                         ┌─────────────────────────────┐
        │       AddDietDostOptions    │                         │    AddValidatorsFromAssembly │
        │    .Bind(section)           │                         │   AbstractValidator<TOptions>│
        └──────────────┬──────────────┘                         └──────────────┬──────────────┘
                       │                                                       │
                       └───────────────────────────┬───────────────────────────┘
                                                   │
                                                   ▼
                               ┌───────────────────────────────────────┐
                               │     FluentValidationOptions<TOptions> │
                               │     (implements IValidateOptions<T>)  │
                               └───────────────────┬───────────────────┘
                                                   │
                                                   ▼
                               ┌───────────────────────────────────────┐
                               │       .ValidateOnStart()              │
                               │  - Fails fast during host startup     │
                               │  - Catches invalid configs on boot    │
                               └───────────────────┬───────────────────┘
                                                   │
                         ┌─────────────────────────┴─────────────────────────┐
                         ▼                                                   ▼
           ┌───────────────────────────┐                       ┌───────────────────────────┐
           │        IOptions<T>        │                       │    DietDostConfiguration  │
           │  (Jwt, Auth, AI, Database)│                       │   (Bridges Ports & Options)│
           └───────────────────────────┘                       └───────────────────────────┘
```

### 2.1 Options Classes
- **[`JwtOptions`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/JwtOptions.cs)**: `SectionName = "Jwt"`, `Issuer`, `Audience`, `Key`, `ExpiryMinutes`.
- **[`AuthOptions`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/AuthOptions.cs)**: `SectionName = "Auth"`, `AllowRegistration`, `SuperAdminEmail`, `RequireMobileVerification`, `TermsVersion`, `HealthConsentVersion`.
- **[`AiOptions`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/AiOptions.cs)**: `SectionName = "AI"`, `Provider`, `GoogleAI` (ModelId, FallbackModelId, Endpoint, ApiKey), `AzureOpenAI` (DeploymentName, Endpoint, ApiKey), `MaxTokens`, `Temperature`, `ShowModelDetails`.
- **[`DatabaseOptions`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/DatabaseOptions.cs)**: `SectionName = "Database"`, `Provider`, `ConnectionString`.

### 2.2 FluentValidation Validators
- **[`JwtOptionsValidator`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/Validators/JwtOptionsValidator.cs)**: Validates non-empty Issuer and Audience, positive ExpiryMinutes, and asserts `Key` is non-empty and at least 32 UTF-8 bytes (256 bits) for HMAC-SHA256 signing security.
- **[`AuthOptionsValidator`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/Validators/AuthOptionsValidator.cs)**: Validates SuperAdminEmail format and non-empty DPDPA 2023 legal consent version strings.
- **[`AiOptionsValidator`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/Validators/AiOptionsValidator.cs)**: Validates supported providers (`GoogleAI` or `AzureOpenAI`), positive tokens, valid temperature range ($0.0 - 2.0$), and conditional model ID completeness.
- **[`DatabaseOptionsValidator`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/Validators/DatabaseOptionsValidator.cs)**: Validates non-empty database provider and connection strings.

### 2.3 Options Pipeline Registration
In [`OptionsValidationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/OptionsValidationExtensions.cs):
```csharp
services.AddValidatorsFromAssemblyContaining<JwtOptionsValidator>();

services.AddOptions<JwtOptions>()
    .Bind(configuration.GetSection(JwtOptions.SectionName))
    .ValidateWithFluentValidation()
    .ValidateOnStart();
```

---

## 3. Forward Roadmap Impact & Phase Compatibility

- **Phase 2 Mobile BFF**: Direct consumption of `IOptions<JwtOptions>` and `IOptions<AiOptions>` across mobile endpoints.
- **Phase 3 Enterprise Cloud DB**: Smooth transition to PostgreSQL or Azure SQL with strong schema validation in `DatabaseOptionsValidator`.
- **Zero Technical Debt**: Fully integrated with ASP.NET Core dependency injection and existing `IDietDostConfiguration` port.

---

## 4. Verification & Compliance Results

- **Compiler Verification**: `dotnet build` succeeded with **0 warnings and 0 errors** on .NET 11 preview.
- **Unit Test Suite**: `dotnet test` passed **206 / 206 tests** (36 domain tests + 170 eval harness tests, including 10 new tests in [`OptionsPatternFluentValidationTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/OptionsPatternFluentValidationTests.cs)).
- **Live Customer Acceptance Test (CFT)**: `pwsh -File tests/validate_e2e_tiers.ps1` achieved **100% pass rate** across all 5 user tiers.
- **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
