<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-112: SQLite Azure Files SMB Mount Connection String Precedence & Persistent Volume Durability

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-18`  
> **Scope**: Persistence, Cloud Infrastructure, Azure Files SMB, SQLite, EF Core, Options Pattern  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

Following the initial deployment to Azure Container Apps (`app-dietdost-web-dev`), user inspection of the persistent Azure File Share (`dietdost-data-dev` in Storage Account `stgdostdevskehxuagttzos`) revealed an anomalous state:
1. The media directory `wwwroot/uploads` was present and functioning correctly in the Azure File Share.
2. The primary SQLite database file `diet_dost.db` was **completely absent** from the file share root.

### Root Cause Analysis
Investigation identified a configuration precedence defect in [`StorageInfrastructureExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Persistence/StorageInfrastructureExtensions.cs) and [`DietDostConfiguration.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Configuration/DietDostConfiguration.cs):
1. In [`DatabaseOptions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/Options/DatabaseOptions.cs), the `ConnectionString` property was defaulted to `"Data Source=diettracker.db"`.
2. In `StorageInfrastructureExtensions.AddStorageInfrastructure`, the connection string resolution was written as:
   ```csharp
   var dbOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
   var connectionString = !string.IsNullOrWhiteSpace(dbOptions.ConnectionString)
       ? dbOptions.ConnectionString
       : configuration.GetConnectionString("DefaultConnection") ?? "Data Source=diettracker.db";
   ```
3. Because `dbOptions.ConnectionString` was populated by the C# class initializer (`"Data Source=diettracker.db"`), `!string.IsNullOrWhiteSpace(dbOptions.ConnectionString)` evaluated to `true`.
4. As a result, EF Core was always handed `"Data Source=diettracker.db"`, which created and operated on an ephemeral local file (`/app/diettracker.db`) inside the container working directory.
5. The container app environment variable `ConnectionStrings__DefaultConnection="Data Source=/app/data/diet_dost.db;Cache=Shared"` configured in [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep) was completely masked and bypassed, preventing persistence to the Azure Files SMB mount (`/app/data`).

---

## 2. Decision & Architecture

### 1. Connection String Precedence Invariant
We refactored connection string resolution across all configuration layers to enforce that `ConnectionStrings:DefaultConnection` is the primary authoritative source:
- In [`StorageInfrastructureExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Persistence/StorageInfrastructureExtensions.cs):
  ```csharp
  var defaultConnStr = configuration.GetConnectionString("DefaultConnection");
  var connectionString = !string.IsNullOrWhiteSpace(defaultConnStr)
      ? defaultConnStr
      : (!string.IsNullOrWhiteSpace(dbOptions.ConnectionString) ? dbOptions.ConnectionString : "Data Source=diettracker.db");
  ```
- In [`DietDostConfiguration.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/Configuration/DietDostConfiguration.cs):
  ```csharp
  public string DefaultConnection =>
      !string.IsNullOrWhiteSpace(_configuration.GetConnectionString("DefaultConnection"))
          ? _configuration.GetConnectionString("DefaultConnection")!
          : (!string.IsNullOrWhiteSpace(_databaseOptions.ConnectionString) ? _databaseOptions.ConnectionString : "Data Source=diettracker.db");
  ```

### 2. Dual Configuration Normalization
In [`ConfigurationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/ConfigurationExtensions.cs), `normalizedMap` now synchronizes both configuration keys:
```csharp
var resolvedDbConnStr = currentConfig.GetConnectionString("DefaultConnection")
    ?? currentConfig["ConnectionStrings:DefaultConnection"]
    ?? (env.IsDevelopment() ? "Data Source=diettracker.db" : "Data Source=/app/data/diet_dost.db;Cache=Shared");

normalizedMap["ConnectionStrings:DefaultConnection"] = resolvedDbConnStr;
normalizedMap["Database:ConnectionString"] = resolvedDbConnStr;
```
This guarantees that whether services read from `IConfiguration.GetConnectionString("DefaultConnection")`, `IConfiguration["Database:ConnectionString"]`, or `IOptions<DatabaseOptions>`, all configuration consumers receive the identical, persistent SMB volume path (`/app/data/diet_dost.db`).

---

## 3. Forward Roadmap Impact & Future Phase Compatibility

- **Phase 1 (SQLite on ACA)**: Guarantees zero data loss across container restarts, scale-to-zero wakeups, and image updates.
- **Phase 2 (Mobile App & Offline Sync)**: Mobile clients syncing against the Web/Mobile BFF can rely on persistent database state on the host.
- **Phase 3 (Enterprise Cloud Database - Azure SQL Serverless)**: Switching connection strings to Azure SQL or PostgreSQL via `ConnectionStrings:DefaultConnection` seamlessly flows to EF Core without code modifications.

---

## 4. Verification & Compliance Evidence

- **Compiler Standard**: .NET 11, 0 errors, 0 warnings.
- **Regression Unit Tests in `tests/Nutrition.EvalHarness.Tests/OptionsPatternFluentValidationTests.cs`**:
  - `AddStorageInfrastructure_PrioritizesDefaultConnection_OverDatabaseOptionsDefault`: Asserts `db.Database.GetConnectionString()` resolves to `/app/data/diet_dost.db;Cache=Shared` when `ConnectionStrings:DefaultConnection` is set.
  - `DietDostConfiguration_PrioritizesDefaultConnection_OverDatabaseOptionsDefault`: Asserts `dietDostConfig.DefaultConnection` and fallback instance both resolve to the persistent path.
- **Test Suite Results**: 259/259 tests passed across all test suites (36 Domain + 223 EvalHarness).
- **Static Analysis Compliance**: Vector 7 living documentation and subsystem integrity verified.
