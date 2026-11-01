# ADR-20261101-153: Idempotent Database Seeding, EF Core Global Query Filter Bypass, and Primary Key Conflict Defense

## Status
Accepted

## Date
2026-11-01

## Context & Problem Statement
During container startup in Azure Container Apps (revisions `app-dietdost-web-dev--0000036` and `0000038`), the container crashed with an unhandled `DbUpdateException` during startup database seeding in `InitializeAndSeedDatabaseAsync` -> `SeedDemoUsersAsync` at line 1428 (`await db.SaveChangesAsync()`):

```
Microsoft.Data.SqlClient.SqlException (0x80131904): Violation of PRIMARY KEY constraint 'PK_Profiles'. Cannot insert duplicate key in object 'dbo.Profiles'. The duplicate key value is (usr_87c369c1e3f8).
Violation of PRIMARY KEY constraint 'PK_Users'. Cannot insert duplicate key in object 'dbo.Users'. The duplicate key value is (usr_87c369c1e3f8).
The statement has been terminated.
   at Nutrition.WebGateway.Extensions.DatabaseInitializationExtensions.SeedDemoUsersAsync(...) in DatabaseInitializationExtensions.cs:line 1428
   at Nutrition.WebGateway.Extensions.DatabaseInitializationExtensions.InitializeAndSeedDatabaseAsync(...) in DatabaseInitializationExtensions.cs:line 127
   at Program.<Main>$(String[] args) in Program.cs:line 43
```

### Root Cause Analysis
1. **Global Query Filter Masking**: `DietTrackerDbContext` configures global query filters for soft-deletable entities: `entity.HasQueryFilter(e => !e.IsDeleted)` on `ApplicationUser`, `UserProfile`, `DailyCalorieLedger`, `ProgressPhoto`, `MealLog`, etc.
2. **Invisible Soft-Deleted Records**: When `SeedDemoUsersAsync` queried whether the demo anonymous zero-PII user existed via `db.Users.FirstOrDefaultAsync(u => u.Id == "usr_87c369c1e3f8" || u.RecoveryLookupId == derivedLookupId)`, EF Core automatically injected `WHERE [IsDeleted] = 0`. Because the user had previously been soft-deleted (e.g., during ADR-149 SuperAdmin User Deletion testing or maintenance operations), the query returned `null`.
3. **Spurious Duplicate INSERT Generation**: Because the query returned `null`, the seeding logic erroneously determined that the user did not exist, instantiated new `ApplicationUser` and `UserProfile` entities with primary key `usr_87c369c1e3f8`, and invoked `db.Users.Add` and `db.Profiles.Add`.
4. **Physical Primary Key Collision**: Upon calling `SaveChangesAsync()`, SQL Server rejected the duplicate primary key insert into `[Users]` and `[Profiles]`, throwing an unhandled `SqlException` (error 2627) and terminating the application container.
5. **Systemic Scope**: The exact same vulnerability existed across all demo user tiers (`user-free`, `user-basic`, `user-premium`, `user-admin`, `user-superadmin-demo`), the root SuperAdmin account (`superadmin-root`), and orphan `UserProfile` records.
6. **SQL Server Schema Drift**: The raw SQL Server `CREATE TABLE [Users]` definition script was missing `[LastActiveUtc] DATETIME2 NULL` (introduced in ADR-150), relying solely on downstream incremental column addition.

## Decision Drivers
- **Zero-Downtime Container Startup**: The web gateway container must start deterministically under all database states (clean database, populated database, or database with soft-deleted demo accounts).
- **Idempotency & Soft-Delete Recovery**: Demo user accounts and root SuperAdmin must be automatically resurrected (`IsDeleted = false`, `DeletedAtUtc = null`, `IsActive = true`) during startup when demo accounts are permitted.
- **Defensive Profile Checking**: Never attempt to insert a profile without checking for physical existence in the database, preventing orphan profile collisions.
- **Schema Alignment**: Ensure raw SQL Server `CREATE TABLE [Users]` DDL includes all current entity properties (`LastActiveUtc`).

## Decision Outcome
Accepted. Implemented `.IgnoreQueryFilters()` on all database existence checks during bootstrap seeding, added automated resurrection of soft-deleted demo and root accounts, added orphan profile reconciliation, and aligned the SQL Server table creation script.

### Key Implementation Details:
1. **EF Core Query Filter Bypass (`DatabaseInitializationExtensions.cs`)**:
   - Updated anonymous user lookup to use `db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(...)`.
   - Updated standard demo user lookups (`demoSpecs`) to use `db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(...)` for both normalized email and primary key ID.
   - Updated root SuperAdmin lookup (`EnsureSuperAdminAccountAsync`) to use `db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(...)`.
   - Updated all `UserProfile` lookups to use `db.Profiles.IgnoreQueryFilters().FirstOrDefaultAsync(...)`.

2. **Soft-Delete Resurrection & Credential Reconciliation**:
   - When an existing record is located with `IsDeleted == true`, the seeding logic resets `IsDeleted = false`, `DeletedAtUtc = null`, and `IsActive = true`.
   - Reconciles BIP-39 deterministic recovery credentials (`RecoveryLookupId`, `RecoveryKeyHash`, `RecoverySalt`).
   - If an existing profile is found (including soft-deleted or orphan profiles), updates its attributes rather than attempting a duplicate insert.

3. **DDL Alignment**:
   - Added `[LastActiveUtc] DATETIME2 NULL` to the SQL Server `CREATE TABLE [Users]` script.
   - Explicitly assigned `NormalizedMobileNumber = "ANONYMOUS"` in the anonymous user entity initializer.

4. **Public Surface & Testability**:
   - Promoted `SeedDemoUsersAsync` and `EnsureSuperAdminAccountAsync` from `private` to `public static` to enable end-to-end unit and integration testing.

5. **Automated Regression Suite (`DatabaseSeedingSoftDeleteResilienceTests.cs`)**:
   - Added comprehensive integration tests covering:
     - Anonymous user resurrection after soft-deletion without duplicate key errors.
     - Standard multi-tier demo user resurrection after soft-deletion.
     - Root SuperAdmin restoration after soft-deletion.
     - Orphan profile reconciliation without `PK_Profiles` collisions.

## Verification
- `DatabaseSeedingSoftDeleteResilienceTests`: 4/4 tests passed.
- Solution-wide test suite (`dotnet test`): 429 tests passed, 0 failures across Domain, Application, and EvalHarness test suites.
- Live CFT acceptance suite: Verified across demo tiers.

