<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261019-116: SQLite Azure Files SMB Mount Journal Mode & Container App Startup Probes

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-19`  
> **Scope**: Persistence, Azure Container Apps, Health Probes, CIFS/SMB Storage, SQLite, Reliability  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

Following the merge of PR #76, PR #77, and PR #79 into `main`, the GitHub Actions deployment pipeline (`Aspire - Build & Deploy Application`) built and pushed container image `crdostdevskehxuagttzos.azurecr.io/diet-dost-web:0c7ce01bf1766a2195e7f9669243936599374f02` to Azure Container Apps as **Revision 5** (`app-dietdost-web-dev--0000005`).

However, users accessing `https://dev.diet-dost.in` reported that newly merged features (brand icon, favicon suite, splash pulse loading experience, personal branding footer, and Dussehra festival launch banner) were not visible.

### Deep Root Cause Analysis
Diagnostics across Azure CLI and Log Analytics (`ContainerAppSystemLogs_CL`) identified that:
1. **Revision 5 Failed Activation**:
   ```
   "runningState": "ActivationFailed",
   "healthState": "None",
   "replicas": 0
   ```
   Azure Container Apps automatically routed 100% of ingress traffic to the previous healthy **Revision 3** (`app-dietdost-web-dev--0000003`, built on pre-merge commit `e09dc15`), masking the deployment.
2. **SQLite WAL Mode Incompatibility on SMB Mount**:
   - In PR #75 (ADR-112), SQLite persistence was routed to the Azure Files SMB mount (`/app/data/diet_dost.db`).
   - However, [`SqlitePragmaInterceptor.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Persistence/SqlitePragmaInterceptor.cs) hardcoded `PRAGMA journal_mode=WAL;`.
   - SQLite Write-Ahead Logging (WAL) requires POSIX shared-memory memory-mapping (`.shm` mmap). Network filesystems (CIFS/SMB) in Linux containers do not support shared-memory mmap, causing SQLite operations to stall or block on byte-range locks during schema creation.
3. **Aggressive Liveness Probe & Missing Startup Probe**:
   - In [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep), the liveness probe had `initialDelaySeconds: 5` and `failureThreshold: 3` (totaling only 25 seconds).
   - In ASP.NET Core, `await app.InitializeAndSeedDatabaseAsync(...)` executes before `app.Run()`. Kestrel does not bind port 8080 until database seeding completes.
   - At T=25s, Kubernetes terminated the container with `reason: ProbeFailure`, repeating across restart attempts until marked `ActivationFailed`.

---

## 2. Decision & Architecture

### 1. Dynamic SQLite Journal Mode & SMB Detection
In [`SqlitePragmaInterceptor.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Persistence/SqlitePragmaInterceptor.cs), we introduced dynamic journal mode resolution:
```csharp
private static string ResolvePragmaScript(string? connectionString)
{
    var configuredMode = Environment.GetEnvironmentVariable("Database__JournalMode")
        ?? Environment.GetEnvironmentVariable("Database:JournalMode");

    if (!string.IsNullOrWhiteSpace(configuredMode))
    {
        return $"PRAGMA journal_mode={configuredMode}; PRAGMA busy_timeout=30000;";
    }

    var isNetworkMount = !string.IsNullOrWhiteSpace(connectionString) &&
        connectionString.Contains("/app/data", StringComparison.OrdinalIgnoreCase);

    var mode = isNetworkMount ? "DELETE" : "WAL";
    return $"PRAGMA journal_mode={mode}; PRAGMA busy_timeout=30000;";
}
```
- In local development and unit tests: Defaults to `WAL` mode for high-speed concurrent reads.
- On Azure Container Apps with Azure Files SMB mount (`/app/data/`): Dynamically resolves to `PRAGMA journal_mode=DELETE; PRAGMA busy_timeout=30000;`, adhering strictly to [`docs/sdd/05_devops_and_infrastructure.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/05_devops_and_infrastructure.md) §2.1.
- Configured `ENV Database__JournalMode="DELETE"` in [`Containerfile`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/Containerfile) and [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep).

### 2. Dedicated Kubernetes Startup Probe & Relaxed Liveness Tolerances
In [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep), we introduced a dedicated `Startup` probe to decouple cold-start initialization from operational runtime liveness:
```bicep
probes: [
  {
    type: 'Startup'
    httpGet: {
      path: '/healthz'
      port: 8080
    }
    initialDelaySeconds: 10
    periodSeconds: 5
    failureThreshold: 24 // 10s + (24 * 5s) = 130s total grace period for cold start & schema migration
  }
  {
    type: 'Liveness'
    httpGet: {
      path: '/healthz'
      port: 8080
    }
    initialDelaySeconds: 30
    periodSeconds: 15
    failureThreshold: 5
  }
  {
    type: 'Readiness'
    httpGet: {
      path: '/ready'
      port: 8080
    }
    initialDelaySeconds: 20
    periodSeconds: 10
    failureThreshold: 3
  }
]
```
- The `Startup` probe grants up to 130 seconds for cold-start database creation and seeding over network SMB.
- `Liveness` and `Readiness` probes do not begin evaluating until the `Startup` probe reports success.

---

## 3. Forward Roadmap Impact & Future Phase Compatibility

- **Phase 1 (Zero-Data-Loss SQLite on Azure Files)**: Guarantees deterministic container startup and revision activation during deployment cutovers.
- **Phase 2 (Mobile App & Offline Sync)**: Mobile clients syncing against the Web/Mobile BFF can rely on persistent database state on the host.
- **Phase 3 (Enterprise Cloud Database Migration)**: When transitioning from SQLite to Azure SQL Serverless, the `Startup` probe guarantees ample tolerance for EF Core schema migrations.

---

## 4. Verification & Compliance Evidence

- **Compiler Standard**: .NET 11, 0 errors, 0 warnings.
- **Unit & Contract Tests**:
  - `BicepIaCConfigurationContractTests.AppBicep_Declares_HealthCheck_Probes_For_ACA`: Asserts `type: 'Startup'`, `type: 'Liveness'`, `type: 'Readiness'`, and `Database__JournalMode`.
  - `OptionsPatternFluentValidationTests.SqlitePragmaInterceptor_ConfiguresJournalMode_And_ToleratesOverrides`: Validates dynamic pragma resolution and busy timeout configuration.
- **Full Test Suite**: 260/260 tests passed across all test suites.
