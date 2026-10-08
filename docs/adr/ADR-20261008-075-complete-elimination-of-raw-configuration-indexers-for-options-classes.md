/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261008-075: Complete Elimination of Raw Configuration Indexers in Favor of Strongly-Typed Options Classes

- **Status**: Accepted
- **Date**: 2026-10-08
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Security Engineer, Core Contributor
- **Consulted**: Application & Infrastructure Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
Following the introduction of the strongly-typed Options pattern with FluentValidation (ADR-20261008-073), residual usages of direct string indexer access `configuration["<key>"]` (such as `configuration["Jwt:Issuer"]`, `configuration["Jwt:Audience"]`, `_config["Auth:TermsVersion"]`, etc.) remained in several components including `JwtTokenService`, `RegisterUserCommandHandler`, `DietDostConfiguration`, `DatabaseInitializationExtensions`, `AiProviderOptions`, and `MicrosoftAgentFoodVisionService`.

Direct configuration indexer access violates clean architecture principles:
1. **Lack of Type Safety**: Raw string keys are susceptible to silent typos and lack compile-time refactoring support.
2. **Bypassing FluentValidation**: Reading directly from raw configuration bypasses startup-time FluentValidation checks.
3. **Ambiguous DI Constructors**: Retaining multi-parameter constructors accepting both `IConfiguration` and `IOptions<T>` causes ASP.NET Core `ServiceProvider` startup failure (`System.InvalidOperationException: The following constructors are ambiguous`) when both types are present in the DI container.

---

## 2. Decision Drivers
- **100% Options Pattern Fidelity**: All services requiring configuration data must consume strongly-typed options (`IOptions<JwtOptions>`, `IOptions<AuthOptions>`, `IOptions<AiOptions>`, `IOptions<DatabaseOptions>`).
- **Zero Ambiguous DI Constructors**: Domain and application command/query handlers and infrastructure services must provide a single unambiguous primary constructor targeting `IOptions<T>`.
- **Preserve Unit Test Usability**: Unit test suites instantiate options cleanly using `Microsoft.Extensions.Options.Options.Create(options)`.
- **Zero Runtime Warnings/Errors**: Maintain 0 build warnings/errors on `.NET 11` and ensure 100% test pass rate across unit, integration, and live CFT harnesses.

---

## 3. Considered Options
1. **Option A (Retain both constructors with `[ActivatorUtilitiesConstructor]`)**:
   - *Pros*: Tests using `new Service(configuration)` continue compiling without modification.
   - *Cons*: `IServiceProvider.ValidateOnBuild` in ASP.NET Core minimal APIs and assembly scanners still flags ambiguity if service resolution occurs outside `ActivatorUtilities`; perpetuates deprecated access paths.
2. **Option B (Complete elimination of raw configuration constructors and indexers)**:
   - *Pros*: Clean architectural boundaries, single constructor per service, 100% test alignment with production DI semantics, zero ambiguity.
   - *Cons*: Required minor test refactoring to pass `Options.Create(options)`.

---

## 4. Decision Outcome
**Chosen Option**: **Option B**.

### Key Refactorings:
1. **`JwtTokenService`**:
   - Refactored to inject `IOptions<JwtOptions>` exclusively as the primary constructor.
   - Removed the deprecated `IConfiguration` constructor.
   - Preserved non-zero expiration times (`options.ExpiryMinutes != 0 ? options.ExpiryMinutes : 1440`) to maintain negative-expiry support for token expiration tests.
2. **`RegisterUserCommandHandler`**:
   - Refactored to inject `IOptions<AuthOptions> authOptions` directly.
   - Eliminated redundant `IConfiguration` constructor and removed raw string lookups `_config["Auth:TermsVersion"]`.
3. **`DietDostConfiguration`**:
   - Refactored all property accessors to delegate 100% to typed options instances (`_jwtOptions`, `_authOptions`, `_aiOptions`, `_databaseOptions`).
4. **`DatabaseInitializationExtensions`**:
   - Bound `JwtOptions`, `AuthOptions`, and `AiOptions` for seeding `AppSecrets` and superadmin demo accounts, eliminating raw configuration indexer calls.
5. **`AiProviderOptions` & `MicrosoftAgentFoodVisionService`**:
   - Eliminated raw fallback loops indexing `_config["AI:..."]`, binding cleanly to `AiOptions`.
6. **`Nutrition.AppHost` Isolation**:
   - Implemented self-contained section binding in `AppHostAiOptions` without taking an invalid project reference to `Nutrition.Application`.

---

## 5. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile BFF**: Seamlessly shares `IOptions<JwtOptions>` and `IOptions<AuthOptions>` with 0 duplicate configuration logic.
- **Phase 3 Azure SQL / Cosmos DB**: Database provider switching is governed strictly through `DatabaseOptions` validated via FluentValidation.
- **Zero Throwaway Engineering**: Eliminates all technical debt associated with configuration drift.

---

## 6. Verification & Validation Evidence
- **Build Output**: 0 Warnings, 0 Errors across all 7 projects targeting `.NET 11`.
- **Unit/Eval Tests**: 206 / 206 tests passing (36 Domain Tests, 170 EvalHarness Tests).
- **Live CFT Suite (`tests/validate_e2e_tiers.ps1`)**: 100% pass across all 5 demo user tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`).
