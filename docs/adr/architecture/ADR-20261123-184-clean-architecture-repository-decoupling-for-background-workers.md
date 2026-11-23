<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261123-184: Clean Architecture Repository Decoupling of Background Workers and Application Services

> **Date / Timestamp**: 2026-11-23T12:55:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Nikunj Banker & Antigravity Agent  
> **Change Type**: ARCHITECTURE | SOLID | REFACTORING  
> **Affected Subsystems**: WebGateway | Infrastructure | Application | EvalHarness.Tests  
> **Associated PR & Stack**: PR #186 (Branch: `feature/datamigrator-cli-harmonization`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md), [`docs/sdd/04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md), [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md)  

---

## 1. Executive Summary & Change Rationale
* **Zero Concrete EF Core Coupling in Background Workers & Services**: Refactors `QrSessionCleanupService`, `InAppNotificationService`, `NotificationsController`, `TierConfigurationService`, `AiQuotaService`, and `DatabaseSecretStore` from direct `DietTrackerDbContext` coupling to domain `IRepository<T>` and `IUnitOfWork` abstractions.
* **Tri-Database Portability (SQLite, Azure SQL, DocumentDB)**: Resolves container startup failures where `DietTrackerDbContext` fallback was uninitialized during Azure DocumentDB (MongoDB vCore) runtime operation, causing `SQLite Error 1: 'no such table: QrSessions'`.
* **Clean Architecture & DIP Compliance**: Aligns infrastructure and presentation layers strictly with the Dependency Inversion Principle (DIP). Entities `QrLoginSession`, `AppNotification`, `TierFeatureConfiguration`, `AiUsageLog`, and `AppSecret` query their respective repositories polymorphically.
* **Azure Container Apps Reverse Proxy & Data Protection Armor**:
  - Configures `ForwardedHeadersOptions` with `KnownIPNetworks.Clear()` and `KnownProxies.Clear()` prior to `UseHttpsRedirection()`, eliminating `Failed to determine the https port for redirect`.
  - Configures `AddDataProtection().PersistKeysToFileSystem()` to `/app/data/keys` when `/app/data` is mounted, preventing ephemeral key warnings and authentication ticket invalidation across container restarts.

---

## 2. Context and Problem Statement
During container startup validation on Azure Container Apps (Revision 57) in Azure DocumentDB mode:
1. `QrSessionCleanupService` executed immediately on startup, calling `db.QrSessions.Where(...)` on `DietTrackerDbContext`. Because DocumentDB mode bypassed EF Core table migrations, `DietTrackerDbContext` utilized an in-memory SQLite fallback without tables, causing immediate startup failure: `SQLite Error 1: 'no such table: QrSessions'`.
2. An architectural audit revealed 5 additional services querying `DietTrackerDbContext` directly:
   - `InAppNotificationService` (`db.Notifications`)
   - `NotificationsController` (`_db.Notifications`)
   - `TierConfigurationService` (`_db.TierConfigurations`)
   - `AiQuotaService` (`_db.AiUsageLogs`)
   - `DatabaseSecretStore` (`_db.AppSecrets`)
3. `UseHttpsRedirection()` emitted warnings behind Azure Container Apps envoy reverse proxy due to missing or untrusted `X-Forwarded-Proto` headers.
4. ASP.NET Data Protection keys were kept in container ephemeral storage (`/root/.aspnet/DataProtection-Keys`), causing key loss and session drops upon container scaling or restart.

---

## 3. Decision Drivers
1. **Zero Runtime Exceptions**: The container must start reliably across all three database providers (SQLite, Azure SQL, DocumentDB).
2. **Strict SOLID & Clean Architecture**: Domain operations must depend on abstractions (`IRepository<T>`, `IUnitOfWork`), not concrete persistence details.
3. **No Ambiguous Constructors in Microsoft DI**: Public constructors must be unique to prevent `CallSiteFactory.CreateConstructorCallSite` ambiguity exceptions when resolving services.
4. **Full Test Suite Compatibility**: 100% of existing unit and integration tests must pass without modifications to test assertions.

---

## 4. Architectural Decisions

### 4.1 Dependency Inversion in Background Workers & Services
Each service now injects its specific `IRepository<T>` and `IUnitOfWork`:
```csharp
// QrSessionCleanupService
using var scope = _serviceProvider.CreateScope();
var repo = scope.ServiceProvider.GetService<IRepository<QrLoginSession>>();
var uow = scope.ServiceProvider.GetService<IUnitOfWork>();
if (repo != null && uow != null)
{
    var expiredSessions = await repo.FindAsync(s => s.ExpiresAtUtc < cutoffUtc, stoppingToken);
    // ... delete and commit via uow.CommitAsync(stoppingToken)
}
```

### 4.2 Constructor Disambiguation for DI Containers
To prevent `CallSiteFactory` ambiguous constructor exceptions in containers registering both `DietTrackerDbContext` and `IRepository<T>`:
```csharp
// Single public constructor for DI:
public TierConfigurationService(
    IRepository<TierFeatureConfiguration> tierRepo,
    IUnitOfWork unitOfWork) { ... }

// Internal constructor for test fixtures:
internal TierConfigurationService(DietTrackerDbContext db)
    : this(new EfRepository<TierFeatureConfiguration>(db, NullLogger<EfRepository<TierFeatureConfiguration>>.Instance),
           new EfUnitOfWork(db, NullLogger<EfUnitOfWork>.Instance)) { ... }
```

### 4.3 Anti-Fragile Fallback SQLite Database
In DocumentDB mode, `StorageInfrastructureExtensions` initializes a dedicated persistent SQLite database (`diet_dost_docdb_fallback.db`) with `Database.EnsureCreated()`, guaranteeing that unmigrated legacy EF queries always find valid tables.

### 4.4 Azure Container Apps Ingress & Key Persistence
- `KnownIPNetworks.Clear()` and `KnownProxies.Clear()` ensure ACA proxy headers are honored.
- `PersistKeysToFileSystem(new DirectoryInfo(keysPath))` writes keys to persistent SMB storage.

---

## 5. Verification & Test Evidence
- **456 / 456 Unit & EvalHarness Tests Passed** (0 failures, 0 warnings).
- **CleanArchitectureDecouplingTests.cs**: 7 dedicated test cases verifying decoupled behavior across SQLite, SQL Server, and DocumentDB.
- **AI Security Defense Gate**: All 7 vectors verified clean via `scripts/verify-ai-security-defense.ps1 -Mode All`.
- **Zero Warnings**: `dotnet build -warnaserror` succeeded across all 7 projects.
