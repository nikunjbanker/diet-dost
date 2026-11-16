# ADR-20261116-156: Upgrade Solution Code and Dependencies to .NET 11 General Availability (Release 11.0.0)

## Status
Accepted

## Date
2026-11-16

## Context & Problem Statement
The Diet-Dost solution previously targeted .NET 11 using pre-release Release Candidate packages (`11.0.0-rc.1.26425.128`) for core Microsoft libraries (`Microsoft.Extensions.*`, `Microsoft.EntityFrameworkCore.*`, `Microsoft.AspNetCore.*`). Following the official General Availability (GA) launch of .NET 11 (November 10, 2026), official production-ready NuGet packages versioned `11.0.0` targeting `net11.0` became available.

Remaining on pre-release release candidates in production environments poses potential maintenance burdens, deprecation risks, and unnecessary build-time warning suppressions. Furthermore, the previous pinning of `System.Security.Cryptography.Pkcs` to `9.0.21` (introduced during RC1 for ASVS compliance) resulted in package downgrade warnings (`NU1605`) when paired with GA `Microsoft.EntityFrameworkCore.SqlServer 11.0.0`, which requires `System.Security.Cryptography.Pkcs >= 10.0.13` (now resolved by upgrading Pkcs to official GA `11.0.0`).

## Decision Drivers
- **Production Runtime Stability**: Migrate all .NET packages from release-candidate builds (`11.0.0-rc.1.26425.128`) to the official, generally available `11.0.0` release versions.
- **OWASP A06 / ASVS Vulnerability Compliance**: Ensure all direct and transitive NuGet dependencies resolve to verified, non-vulnerable GA packages (`System.Security.Cryptography.Pkcs 11.0.0`) with zero `NU1605` downgrade errors.
- **Zero-Warning Standard**: Maintain 100% compliance with `<TreatWarningsAsErrors>` across both local developer workstations and CI/CD pipelines (0 errors, 0 warnings).
- **Session & Worktree Isolation**: Perform all upgrade work, restores, builds, test executions, and living documentation updates in an isolated Git worktree (`.worktrees/feature-upgrade-dotnet-11-release`) to prevent any collisions with in-flight session branches.

## Decision Outcome
Accepted. Upgraded the entire Diet-Dost solution to official .NET 11 GA (`11.0.0`):

1. **Package References Updated to `11.0.0`**:
   - `src/Nutrition.Application/Nutrition.Application.csproj`:
     - `Microsoft.Extensions.Configuration.Abstractions`: `11.0.0`
     - `Microsoft.Extensions.Configuration.Binder`: `11.0.0`
     - `Microsoft.Extensions.DependencyInjection.Abstractions`: `11.0.0`
     - `Microsoft.Extensions.Logging.Abstractions`: `11.0.0`
     - `Microsoft.Extensions.Options`: `11.0.0`
     - `Microsoft.Extensions.Options.ConfigurationExtensions`: `11.0.0`
   - `src/Nutrition.Infrastructure/Nutrition.Infrastructure.csproj`:
     - `Microsoft.EntityFrameworkCore.Sqlite`: `11.0.0`
     - `Microsoft.EntityFrameworkCore.SqlServer`: `11.0.0`
     - `Microsoft.Extensions.Hosting.Abstractions`: `11.0.0`
     - `Microsoft.Extensions.Http`: `11.0.0`
     - `System.Security.Cryptography.Pkcs`: `11.0.0` (resolves `NU1605` package downgrade warning)
   - `src/Nutrition.Tools.DataMigrator/Nutrition.Tools.DataMigrator.csproj`:
     - `Microsoft.Extensions.Configuration`: `11.0.0`
     - `Microsoft.Extensions.Configuration.Json`: `11.0.0`
     - `Microsoft.Extensions.Configuration.EnvironmentVariables`: `11.0.0`
   - `src/Nutrition.WebGateway/Nutrition.WebGateway.csproj`:
     - `Microsoft.AspNetCore.Authentication.JwtBearer`: `11.0.0`
     - `Microsoft.AspNetCore.OpenApi`: `11.0.0`
     - `Microsoft.EntityFrameworkCore.Design`: `11.0.0`
   - `tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj`:
     - `Microsoft.Extensions.Configuration`: `11.0.0`
     - `Microsoft.EntityFrameworkCore.SqlServer`: `11.0.0`
2. **Aspire AppHost Sdk Alignment**:
   - Maintained standalone `Aspire.AppHost.Sdk/13.5.4` and Aspire hosting packages per `AGENTS.md` Rule 3.
3. **Contract & Regression Tests**:
   - Synchronized `ReleasePipelineContractTests.cs` to test semantic patch increments from existing repository tags (`alpha-5.0.2.0`).
   - Verified all 451 unit, integration, and architecture contract tests pass with 0 failures.
4. **Living Documentation & Governance**:
   - Updated `README.md`, `docs/sdd/00_sdd_index.md`, `docs/sdd/10_configuration_and_secret_management_architecture.md`, `docs/sdd/12_cross_device_qr_login_and_zero_pii_recovery_architecture.md`, and `.agents/skills/indian-diet-calorie-tracker/SKILL.md` to reflect .NET 11 GA.
   - Synchronized `docs/adr/index.json` and `docs/adr/README.md` via `scripts/sync-adr-index.ps1`.

## Consequences
- **Positive**:
  - All core dependencies run on Microsoft's official .NET 11 GA release.
  - Zero pre-release warnings or package downgrades during compilation and restore.
  - Full compatibility verified across all 5 user tiers and automated test harnesses.
- **Negative / Operational**:
  - Developers on older preview SDKs should update to the release `11.0.100` SDK for local development, though builds remain fully supported and warning-free on RC1 SDK.
