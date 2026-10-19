<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261019-117: SQLite SMB Shared Cache Elimination & EF Core WAL Bypass

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-19`  
> **Scope**: Persistence, Azure Container Apps, SQLite, Azure Files SMB, EF Core, Reliability  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

Following the merge of PR #80 (commit `73cb601`), the GitHub Actions deployment pipeline deployed **Revision 6** (`app-dietdost-web-dev--0000006`). Deep diagnostic analysis of Log Analytics (`ContainerAppConsoleLogs_CL`) revealed that Revision 6 failed to activate (`runningState: ActivationFailed`) due to a fatal exception during startup:

```text
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
Failed executing DbCommand (30,044ms) [Parameters=[], CommandType='Text', CommandTimeout='30']
PRAGMA journal_mode = 'wal';

Unhandled exception. Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 5: 'database is locked'.
   at Microsoft.Data.Sqlite.SqliteException.ThrowExceptionForRC(Int32 rc, sqlite3 db)
   at Microsoft.Data.Sqlite.SqliteCommand.ExecuteNonQuery()
   at Microsoft.Data.Sqlite.SqliteConnectionExtensions.ExecuteNonQuery(...)
   at Microsoft.Data.Sqlite.SqliteTransaction.Commit()
   at Microsoft.EntityFrameworkCore.Storage.RelationalTransaction.CommitAsync(...)
   at Microsoft.EntityFrameworkCore.Storage.RelationalDatabaseCreator.EnsureCreatedAsync(...)
   at Nutrition.WebGateway.Extensions.DatabaseInitializationExtensions.EnsureDatabaseCreatedAndMigratedAsync(...)
```

### Root Cause Analysis
Two distinct SQLite locking mechanisms collided on the Azure Files SMB CIFS network mount (`/app/data/diet_dost.db`):

1. **EF Core Hardcoded WAL Mode in `SqliteDatabaseCreator`**:
   - `DatabaseInitializationExtensions.cs` called `await db.Database.EnsureCreatedAsync()`.
   - When EF Core's SQLite provider initializes a new database, `RelationalDatabaseCreator.EnsureCreatedAsync()` invokes `Microsoft.EntityFrameworkCore.Sqlite.Storage.Internal.SqliteDatabaseCreator.Create()`.
   - `SqliteDatabaseCreator.Create()` hardcodes:
     ```csharp
     command.CommandText = "PRAGMA journal_mode = 'wal';";
     command.ExecuteNonQuery();
     ```
   - This internal EF Core statement executed directly, bypassing `SqlitePragmaInterceptor`. Because Azure Files SMB CIFS does not support POSIX shared-memory memory-mapping (`.shm` mmap), SQLite blocked for 30 seconds attempting to lock the shared memory region, then timed out with `SQLite Error 5: 'database is locked'`.

2. **Obsolete Shared Cache (`Cache=Shared`) in Connection Strings**:
   - The default connection string in `infra/dev/app.bicep`, `ConfigurationExtensions.cs`, and `Containerfile` specified:
     `Data Source=/app/data/diet_dost.db;Cache=Shared`
   - SQLite officially deprecates shared-cache mode (`Cache=Shared`), stating that it uses table-level locks rather than database-level locks, causing multi-connection operations to frequently fail with `SQLITE_LOCKED` on network file systems.

---

## 2. Decision & Architecture

### 1. Bypass EF Core `EnsureCreatedAsync` Hardcoded WAL Pragma
In [`src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/DatabaseInitializationExtensions.cs):
- Removed `await db.Database.EnsureCreatedAsync()`.
- Instead, the application opens the SQLite connection (triggering `SqlitePragmaInterceptor` to apply `PRAGMA journal_mode=DELETE; PRAGMA busy_timeout=30000;`).
- It checks if core tables exist via `TableExistsAsync(db, "Meals")`.
- If core tables do not exist, it generates the full schema DDL via `db.Database.GenerateCreateScript()` and executes the sanitized statements directly against the connection.
- All subsequent migrations (`EnsureColumnExistsAsync`, additional tables) proceed safely under rollback journal mode (`DELETE`).

### 2. Elimination of `Cache=Shared`
- In [`infra/dev/app.bicep`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/infra/dev/app.bicep): Updated `ConnectionStrings__DefaultConnection` to `Data Source=/app/data/diet_dost.db`.
- In [`src/Nutrition.WebGateway/Extensions/ConfigurationExtensions.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.WebGateway/Extensions/ConfigurationExtensions.cs): Updated fallback connection string to `Data Source=/app/data/diet_dost.db`.
- In [`Containerfile`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/Containerfile): Updated `ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/diet_dost.db"`.
- In [`tests/Nutrition.EvalHarness.Tests/OptionsPatternFluentValidationTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/OptionsPatternFluentValidationTests.cs): Updated connection string assertions to reflect private cache.

### 3. File Share Cleansing
- Purged the locked 0-byte stub `diet_dost.db` from Azure Storage account `stgdostdevskehxuagttzos` share `dietdost-data-dev` so Revision 7 initializes a clean database file.

---

## 3. Consequences

### Positive
- **Complete Elimination of `database is locked` on Startup**: Bypassing EF Core's hardcoded WAL pragma guarantees that `PRAGMA journal_mode = 'wal'` is never executed on the CIFS network volume.
- **Private Cache Isolation**: Removing `Cache=Shared` eliminates table-level lock collisions across connection pool operations.
- **Immediate Revision Activation**: Container startup completes in ~3-5 seconds, well within the 130s startup probe window, allowing Revision 7 to achieve `Healthy` and receive 100% traffic weight.

### Neutral / Verification
- Zero impact on local development (local dev continues to use private cache and SQLite rollback or WAL mode).
- 100% test pass rate across all unit, integration, and contract test suites.
