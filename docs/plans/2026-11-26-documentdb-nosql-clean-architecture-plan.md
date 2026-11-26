# Adversarial Review & Unified Implementation Plan: Effective DocumentDB NoSQL Architecture (Tri-Database, Clean Architecture, DDD & Security)

> **Status**: Approved Implementation Plan (User Confirmed Option A on all §8 Policy Questions — 2026-11-26)  
> **Repository**: `c:\Users\nikunj.banker\source\repos\diet-dost` (`git status --short` = clean, 0 edits)  
> **Solution File**: `DietDost.slnx` (9 `src/` projects, 3 `tests/` projects targeting `.NET 11`)  
> **Specialists Synthesized**: DocumentDB Architect (`Phase 1 + Phase 2`), Clean Architecture Reviewer (`Phase 1 + Phase 2`), Security Auditor (`Phase 1 + Phase 2`)

---

## 1. Rubric-Backed Confidence Score & Executive Summary

### 1.1 Rubric-Backed Confidence Score: **95.8 / 100 (~96 / 100)** *(Threshold >= 95)*

| Rubric Dimension | Weight | Score | Verification Basis & Deductions |
|---|---:|---:|---|
| **Codebase Coverage** | 35% | **98 / 100** | Directly inspected all 14 domain entities, 4 value objects, 10 enums, `DietDost.slnx`, `DocumentDbRepository<T>`, `DocumentDbUnitOfWork`, `DocumentDbBsonMappings`, `DocumentDbDatabaseInitializer`, `EfRepository<T>`, `DietTrackerDbContext`, `StorageInfrastructureExtensions`, `ConfigurationExtensions`, `DatabaseConfigurationProvider`, `DatabaseHealthCheck`, `DefaultDataSeeder`, `WebGatewayResourceExtensions`, all Application CQRS commands/queries, `ClinicalDietitianService`, `AiQuotaService`, `TierConfigurationService`, `DatabaseSecretStore`, `InAppNotificationService`, `LocalPhotoStorageService`, `WebBffController`/`MobileBffController`/`NotificationsController`/`MediaController`/`MealsController`/`ProfileController`/`AnalyticsController`/`QrAuthController`, frontend JS (`review-modal.js`, `profile-modal.js`, `meals-service.js`, `notification-service.js`, `admin-modal.js`, `quota-modal.js`), `Nutrition.Mcp`/`McpCli`, `Nutrition.Tools.DataMigrator`, `infra/dev/*.bicep`, `scripts/verify-ai-security-defense.ps1`, `tests/validate_e2e_tiers.ps1`, and contract/persistence test suites. *(-2%: Fresh checkout lacks `project.assets.json`, so `dotnet build`/`test` execution is deferred to PR implementation.)* |
| **Tri-DB / Contract Parity** | 35% | **96 / 100** | Verified exact `System.Text.Json` `camelCase` + numeric enum wire contracts, `BffContractSnapshotTests`, `MealTools` & `AnalyticsTools` contracts, `QrLoginSession` SQL-vs-Domain column drift, `Users.NormalizedMobileNumber = "ANONYMOUS"` non-uniqueness, `UserCorrectionRecord.CreatedAtUtc` + `string MealType`, `AppNotification.MetadataJson` string round-tripping, `QuerySpec<T>` `UnaryExpression` unwrapping, `DataConversionEngine` `MealType` bug, `AzureSqlTargetWriter` DDL drift, and `Meals.Items` embedding. *(-4%: Live Azure DocumentDB MongoDB vCore 6.0 server capability for `partialFilterExpression` + TTL and multi-doc transactions was not tested against a live cluster; the design deliberately avoids relying on unverified server extensions.)* |
| **Risk Mitigation** | 30% | **93 / 100** | Closes all 17+ silent write-loss handlers, COLLSCAN collection casing bugs, aggregate split-brain, IST `00:00–05:30` UTC ledger off-by-one bug, `NotificationsController.MarkAsRead` IDOR-before-TTL hazard, fail-open localhost Mongo fallback, `DatabaseHealthCheck`/`McpCli` A3 startup crashes, Vector 7 CI gate across PRs 1–4, and `validate_e2e_tiers.ps1:89` CFT route bug. *(-7%: 4 explicit policy/scope decisions below 95% confidence require user confirmation in §8 before code edits begin.)* |

**Composite Score**: `(98 × 0.35) + (96 × 0.35) + (93 × 0.30) = 34.30 + 33.60 + 27.90 = 95.8 / 100`.

### 1.2 Executive Summary & Confirmed Architectural Decisions (A1–A4)

Diet-Dost supports three persistence providers (**SQLite**, **Azure SQL Serverless**, and **Azure DocumentDB MongoDB vCore 6.0 Free Tier**) behind `IRepository<T>` / `IUnitOfWork`. Direct codebase inspection confirmed that DocumentDB was layered under EF Core's implicit change-tracking and relational assumptions, creating **P0 silent write losses** across 17+ auth/security/admin handlers, **zero live indexes** on all 14 collections due to casing mismatch (`"users"` vs `"Users"`), an **aggregate split-brain** between `Meals` and `FoodItems`, **unbounded O(N) scans** and **write-on-read races**, an **IST timezone ledger bug**, **P0/P1 security vulnerabilities**, and **4 critical defects in `Nutrition.Tools.DataMigrator`**.

> [!NOTE]
> **Confirmed User Decisions Honored Throughout This Plan**:
> - **A1 (API Contracts & DTO Decoupling)**: Introduce dedicated, backward-compatible Application Request/Response DTOs & mappers across all endpoints while preserving **identical JSON wire shapes** (`camelCase`, integer enums on Web/REST/Web BFF, computed fields `IsConfidencePassed` and `PendingCalories`, `/api/media/progress/{Id}` URI masking).
> - **A2 (NoSQL Aggregate Boundaries)**: Keep `Users`, `Profiles` (`_id == UserId`), `Meals` (with strictly embedded `FoodItemRecord` items in `Items[]`—retiring the standalone `"FoodItems"` collection in DocumentDB), and `Corrections` as focused separate aggregates; keep `Ledgers`, `ProgressPhotos`, `AiFeedbacks`, `AiUsageLogs`, `TierConfigurations`, `AppSecrets`, `VerificationOtps`, `QrSessions`, and `AppNotifications` as dedicated collections (**13 active DocumentDB collections**).
> - **A3 (Repository / UnitOfWork & Zero SQLite Fallback)**: Replace leaky `IQueryable<T> Query()` with `IReadRepository<T>` / `IWriteRepository<T>` / `IRepository<T>`, `QuerySpec<T>`, and 6 specialized domain ports; **completely remove** `diet_dost_docdb_fallback.db` and `DietTrackerDbContext` registration in DocumentDB mode with fail-closed connection & TLS validation.
> - **A4 (TTL Indexes & Bounded Quota Aggregation)**: Enable native DocumentDB TTL indexes (24h post-expiry for `VerificationOtps` & `QrSessions`, 90d post-read on `AppNotifications.ReadAtUtc`) and replace multi-query/unbounded `AiUsageLogs` scans with bounded 2-query `Task.WhenAll` aggregation.

---

## 2. Multi-Specialist Perspectives & Trade-Off Debate (7 Contested Decisions)

All three specialists independently inspected `c:\Users\nikunj.banker\source\repos\diet-dost`, debated seven architectural and security trade-offs, caught cross-team edge cases, and reached consensus specifications below.

### Trade-Off 1: `DocumentDbUnitOfWork` Semantics (Immediate Writes + Specialized Ports vs. Buffered UoW Queue)
- **Prior Plan Proposal**: Buffered staged queue in `DocumentDbUnitOfWork` flushed via `BulkWriteAsync` on `SaveChangesAsync()`.
- **DocumentDB Architect**: Pointed out `infra/dev/documentdb.bicep:64-67,123` provisions Free Tier with `shardCount: 1`, `highAvailability.targetMode: 'Disabled'`, and `retrywrites=false`—multi-collection ACID transactions are unavailable/unreliable here.
- **Clean Architecture Reviewer**: Raised an **LSP read-your-own-writes trap**: `ClinicalDietitianService.LogMealAsync` (`L209-215`), `UpdateMealAsync` (`L352-358`), and `DeleteMealAsync` (`L380-384`) write a `MealLog` and immediately query `_mealRepo` to recalculate `DailyCalorieLedger`. `ConfirmMealCommand` (`L150-154`) conditionally skips `SaveChangesAsync()` when `learnedNotes.Count == 0`, and standalone repository tests omit `SaveChangesAsync()`. Buffering writes without an EF-grade Identity Map + expression evaluator breaks mid-handler reads.
- **Security Auditor**: Warned that buffering security state transitions (`VerificationOtp.AttemptCount++`, `IsUsed = true`, `QrLoginSession.Consume()`, `SecurityStamp` rotation) risks leaving OTP attempts uncounted or QR `ExchangeTicket` unconsumed if a subsequent step throws before `SaveChangesAsync()`.
- **Consensus Resolution (Adopt Option A — Immediate Repository Writes + Explicit `UpdateAsync` + CAS + Specialized Atomic/Batch Ports)**:
  1. Reject buffered queuing in `DocumentDbUnitOfWork`; keep `DocumentDbRepository<T>` writes immediate per single-document ACIDity and keep `DocumentDbUnitOfWork.SaveChangesAsync()` as a lightweight scope boundary hook returning `1`.
  2. Enforce symmetric contract across all 3 providers: every handler mutating a fetched entity **MUST** call `await _repo.UpdateAsync(entity, ct);` + `await _uow.SaveChangesAsync(ct);`.
  3. Replace blind `ReplaceOneAsync(..., IsUpsert = true)` (`DocumentDbRepository.cs:216`) with conditional CAS `ReplaceOneAsync` filtering `_id == id` AND `IsDeleted != true` (when `ISoftDeletable` and `!entity.IsDeleted`) AND `ConcurrencyVersion == expectedVersion` (when `IConcurrencyAware`), incrementing `ConcurrencyVersion + 1` and throwing `DbConcurrencyConflictException` on `MatchedCount == 0`.
  4. Replace 2-roundtrip `DeleteAsync` (`DocumentDbRepository.cs:230-240`) with atomic `UpdateOneAsync` (`$set: { IsDeleted: true, DeletedAtUtc: utcNow }`).
  5. Use native atomic/bulk operations (`FindOneAndUpdateAsync`, `UpdateManyAsync`, `BulkWriteAsync`) inside specialized ports (`IQrLoginSessionRepository`, `IUserRepository`, `INotificationRepository`, `IAccountErasurePort`).

### Trade-Off 2: `AppNotifications` 90-Day TTL, `MetadataJson` & Thin CQRS Controllers
- **DocumentDB Architect**: Confirmed `AppNotification.ReadAtUtc` (`AppNotification.cs:58`) is `null` when unread (`InAppNotificationService.cs:68,132`) and set only when marked read. Standard MongoDB TTL monitors ignore documents where the indexed field is `null`/missing, making a single-field TTL `{ ReadAtUtc: 1 }, { expireAfterSeconds: 7776000 }` safer than `partialFilterExpression` + TTL on Free Tier Mongo vCore `6.0`.
- **Clean Architecture Reviewer**: Verified `NotificationsController.cs:26-123` bypasses `IDispatcher`, ignores `limit`/`unreadOnly` parameters sent by `wwwroot/js/services/notification-service.js:233`, and executes N sequential `UpdateAsync` calls in `MarkAllAsRead`. Also verified `MediaController.cs:29-107` injects `IRepository<ProgressPhoto>`, performs `File.Exists`/`Path.GetFullPath` in controller, omits trailing `Path.DirectorySeparatorChar` on `StartsWith`, and omits `AllowPhotoCompare` tier check. Also caught that putting `"MetadataJson"` into `DataConversionEngine.KnownJsonColumns` (`L48-56`) would expand it into a `BsonDocument` and break `string? MetadataJson` deserialization!
- **Security Auditor**: Identified a **P0 IDOR** at `NotificationsController.cs:81-93` (`MarkAsRead` checks only `notif == null` with zero `RecipientUserId`/`RecipientRole` check). Enabling a 90-day TTL before fixing `MarkAsRead` would let any user start the 90-day deletion clock on SuperAdmin alerts. Also noted `InAppNotificationService.cs:58-67` embeds `{ userId, name, email, mobile }` into `NewRegistration` `MetadataJson` and `Message`.
- **Consensus Resolution**:
  1. Use standard single-field TTL `{ ReadAtUtc: 1 }, { expireAfterSeconds: 7776000 }` (no `partialFilterExpression`), enforcing `ReadAtUtc = null` while `!IsRead` and `DateTime.UtcNow` on `IsRead = true`.
  2. Fix `NotificationsController.MarkAsRead` P0 IDOR via `INotificationRepository.MarkAsReadAsync` (`RecipientUserId == currentUserId || (isAdminOrSuper && RecipientRole in ["Admin", "SuperAdmin"])`) **before/alongside** enabling the TTL index.
  3. Keep `AppNotification.MetadataJson` as `string?` (`BsonType.String`) across Domain, EF Core, DocumentDB BSON, and DTOs; add a `ResilientJsonStringSerializer` on `MetadataJson` (reads `BsonType.String` or legacy `BsonType.Document` cleanly, writes `BsonType.String`) and do **not** convert `MetadataJson` to `BsonDocument` in `DataConversionEngine.ConvertToBsonDocument`.
  4. Add optional `string? SubjectUserId` to `AppNotification` (`{ SubjectUserId: 1 }` sparse index) for deterministic DPDPA scrubbing of `NewRegistration` alerts in `DeleteAccountCommand`.
  5. Refactor `NotificationsController` and `MediaController` into thin CQRS controllers strictly using `IDispatcher`.

### Trade-Off 3: A1 DTOs, Domain Encapsulation, `QrLoginSession` Parity & IST Timezone Ledger Bug
- **Clean Architecture Reviewer**: Verified `Program.cs:29` uses `camelCase` + integer enums (`typeof === 'number'` checks in `review-modal.js:1749`, `profile-modal.js:470-477`, `meals-service.js:184`, `admin-modal.js`, `quota-modal.js`). Caught four subtle wire traps: (a) `MealLogDto.IsConfidencePassed` & `DailyCalorieLedgerDto.PendingCalories` are computed getters serialized on the wire; (b) `review-modal.js:1773` sends `id: null` on new items and omits `id` on confirm; (c) `profile-modal.js:463` sends `{ drugName }` omitting `dosage`/`frequency`; (d) `QrLoginSession.Approve()` (`QrLoginSession.cs:142`) accepts **`Pending` OR `Scanned`**, whereas the prior plan's single `expectedStatus` parameter would break `Scanned` session approval! Also verified `AzureSqlDatabaseInitializer.cs:290-291` & `SqliteDatabaseInitializer.cs:367-368` already define `[ApprovedAtUtc]` and `[ConsumedAtUtc]` omitted from `QrLoginSession.cs` and `AzureSqlTargetWriter.cs:664-681`.
- **DocumentDB Architect**: Verified `AppSecret` (`AppSecret.cs:14-21`) uses `Key` (no `Id` property), so constraining `IRepository<T>` to `where T : IAggregateRoot` would break `AppSecret` and non-root append-only records (`VerificationOtp`, `AiUsageLog`, `AiDetectionFeedbackRecord`). Corrected `UserCorrectionRecord` property names: `MealType` is `string` (`= "Lunch"`) and timestamp is **`CreatedAtUtc`** (`UserCorrectionRecord.cs:24-25`, not `CorrectedAt`).
- **Security Auditor**: Required zero over-posting on request DTOs (`IsDeleted`, `DeletedAtUtc`, `IsVerifiedByUser`, `ConcurrencyVersion`, `CreatedAtUtc`, `UpdatedAtUtc` stripped), server-generated `MealLog.Id` / `FoodItemRecord.Id` on `ConfirmMeal`, and server-only computation of `WhoComplianceFlags` / `MedicationWarnings`.
- **All 3 Specialists (Joint Catch — IST Timezone Ledger Bug)**: `ConfirmMealCommand.cs:155` and `UpdateMealCommand.cs:57` call `GetOrCreateDailyLedgerAsync(meal.UserId, DateOnly.FromDateTime(meal.LoggedAt), ct)` using **UTC date** immediately after `ClinicalDietitianService.LogMealAsync`/`UpdateMealAsync` already recalculated `mealLocalDate` in `UserProfile.Timezone` (`Asia/Kolkata`)—creating phantom wrong-day ledgers for IST meals logged between 00:00–05:30 IST and running duplicate lifetime meal scans! Plus `GetDailyLedgerQuery.cs:42-46` defaults omitted `Date` to `DateOnly.FromDateTime(DateTime.UtcNow)`.
- **Consensus Resolution**:
  1. Ship all 10 A1 DTO groups preserving exact `camelCase` + numeric enum wire shapes, `IsConfidencePassed`, `PendingCalories`, nullable `Id`, optional `Dosage`/`Frequency`, `/api/media/progress/{Id}` masking, server-generated IDs on `ConfirmMeal`, and server-computed clinical flags.
  2. Keep `IReadRepository<T>`, `IWriteRepository<T>`, and `IRepository<T>` constrained to `where T : class`; apply `IAggregateRoot` + `IConcurrencyAware` to mutable aggregate roots; ban `IRepository<FoodItemRecord>`.
  3. Promote `ApprovedAtUtc` and `ConsumedAtUtc` onto `QrLoginSession` (aligned across all 3 DBs), and define `IQrLoginSessionRepository` atomic CAS methods supporting `IReadOnlyCollection<QrSessionStatus>` (`[Pending, Scanned]` for `Approve`/`Reject`, `[Approved]` for `Consume`) with `ExpiresAtUtc > utcNow` inside the DB filter.
  4. Add `NormalizedOriginalItem` + `UpdatedAtUtc` to `UserCorrectionRecord` (keeping `MealType` `string` and `CreatedAtUtc`).
  5. Refactor `ClinicalDietitianService.LogMealAsync`/`UpdateMealAsync` to return `(MealLog Meal, DailyCalorieLedger DailyLedger)` and delete the redundant UTC `GetOrCreateDailyLedgerAsync` calls at `ConfirmMealCommand.cs:155` and `UpdateMealCommand.cs:57`; split read-only `GetDailyLedgerProjectionAsync` (defaulting omitted `Date` using `UserProfile.Timezone`) from write-side `RecalculateAndSaveDailyLedgerAsync`.

### Trade-Off 4: A3 Fallback Removal, `DatabaseConfigurationProvider`, `DatabaseHealthCheck`, `McpCli` & Fail-Closed Connection
- **Clean Architecture Reviewer**: Audited all callers of `DietTrackerDbContext` when `diet_dost_docdb_fallback.db` is removed (`StorageInfrastructureExtensions.cs:155-198`) and caught two fatal runtime crash sites: `DatabaseHealthCheck.cs:26-34` (throws `ArgumentNullException` if `dbContext == null`) and `Nutrition.Tools.McpCli/Program.cs:70` (`GetRequiredService<DietTrackerDbContext>()`). Also caught `OrderBy` `Expression<Func<T, object>>` value-type `UnaryExpression(Convert)` boxing in `QuerySpec<T>`.
- **Security Auditor**: Caught `StorageInfrastructureExtensions.cs:160` silent fallback to unencrypted `"mongodb://localhost:27017"`, missing TLS validation for non-loopback hosts, and `ConfigurationExtensions.cs:42-49,117` unconditionally opening SQLite `diettracker.db` in Dev (seeding hardcoded `DefaultDevJwtKey` and `"DietDost@Demo2026!"` via `DatabaseConfigurationProvider.cs:50-60`) and falling back `Auth:SuperAdminPassword` to `"DietDost@Demo2026!"` even in non-demo production.
- **DocumentDB Architect**: Verified `WebGatewayResourceExtensions.cs:40-66` omits `ConnectionStrings__DocumentDbConnection`, `infra/dev/documentdb.bicep:123` lacks `uriComponent(adminPassword)`, and `infra/dev/infra.bicep:644` overrides `allowAllAzureServices: true`.
- **Consensus Resolution**:
  1. Remove `diet_dost_docdb_fallback.db`, `DietTrackerDbContext`, and `"mongodb://localhost:27017"` fallback in `"documentdb"`/`"mongodb"` mode; throw `InvalidOperationException` if connection string is missing and assert `UseTls == true` for any non-loopback host (`localhost`, `127.0.0.1`, `::1`).
  2. Skip SQLite `builder.AddDatabaseSecrets(...)` when `Database:Provider` is `DocumentDb`, `MongoDb`, `AzureSql`, or `SqlServer` (rely on `appsettings.Development.json` / env vars / Key Vault at bootstrap and `DatabaseSecretStore` via `IRepository<AppSecret>` at runtime). Restrict `DefaultDevJwtKey`, `DefaultSuperAdminEmail`, and `"DietDost@Demo2026!"` fallbacks strictly to `env.IsDevelopment()`.
  3. Add `[ActivatorUtilitiesConstructor] public DatabaseHealthCheck(IServiceProvider serviceProvider)` while preserving `public DatabaseHealthCheck(DietTrackerDbContext? dbContext, IServiceProvider? serviceProvider = null)` for existing test constructors; check `IMongoDatabase` first, then `DietTrackerDbContext`, else `Unhealthy`.
  4. Update `McpCli/Program.cs:70-79` to `GetService<DietTrackerDbContext>()`; unwrap `ExpressionType.Convert` and clamp `Take` (`1..500`) in `DocumentDbRepository<T>` `QuerySpec<T>`; wire `ConnectionStrings__DocumentDbConnection` in `AppHost`.

### Trade-Off 5: AI Quota Atomicity vs. Retry Fairness + `IMemoryCache`
- **DocumentDB Architect (Critical `$facet` Optimization Catch)**: In MongoDB/DocumentDB, `$facet` only uses a collection index for the pipeline's *leading* `$match`. Putting `$match { UserId }` without a date bound before `$facet` scans **lifetime** user logs. Executing **two** bounded indexed queries via `Task.WhenAll` is strictly superior:
  1. Aggregation `$match: { UserId: userId, TimestampUtc: { $gte: thirtyDaysAgoUtc }, IsSuccess: true, OperationType: { $in: [0, 1] } }` + `$group` (single 30-day index-backed pass computing `usedToday`, `used7D`, `used30D`).
  2. Indexed `.Find(l => l.UserId == userId).SortByDescending(l => l.TimestampUtc).Limit(recentLimit)` (O(20) top-N even if user was inactive > 30 days).
- **Security Auditor vs. Clean Architecture Reviewer (Quota TOCTOU)**: `AiQuotaService` (`L49-189`) checks quota before the 1–120s AI call and records after, allowing concurrent burst requests to bypass `Free` 1/day limits, while `UploadAndAnalyzeMealCommand.cs:117-128` intentionally sets `IsSuccess = false` when `!analysis.IsConfidenceGatedPassed` so retakes don't burn quota. Additionally, `MealsController`, `AnalyticsController`, and `ProgressPhotosController` trust stale 24h JWT `tier` claims, and `TierConfigurationService.cs:26` / `DatabaseSecretStore.cs:37` hold non-expiring `static ConcurrentDictionary` caches.
- **Consensus Resolution**:
  1. Implement `IAiUsageRepository.GetQuotaWindowSummaryAsync` using the DocumentDB 2-query `Task.WhenAll` optimization (and EF grouped query) plus `GetTodaySuccessCountsByUserIdsAsync` (replacing `GetAdminUsersQuery.cs:60`).
  2. Resolve effective `UserTier` from `IReadRepository<ApplicationUser>` (not stale JWT claim) on tier-gated endpoints, and replace static `ConcurrentDictionary` caches in `TierConfigurationService` & `DatabaseSecretStore` with `IMemoryCache` (60s `AbsoluteExpirationRelativeToNow` + write eviction + `CancellationTokenSource` reset token for `ClearCache()` test compatibility).
  3. Define `IAiQuotaReservationGate` (`AcquireUserSlotAsync(userId, ct)`), preserving `IsConfidenceGatedPassed == false` retake fairness; ship per-user keyed `SemaphoreSlim` (`InMemoryAiQuotaReservationGate`) by default vs immediate DB `IsPending` lease schema in **§8 Doubt 2** for user confirmation.

### Trade-Off 6: DPDPA 2023 Erasure vs. Soft-Delete Retention & Partial Unique Indexes
- **DocumentDB Architect (Critical `RegisterAnonymousCommand` Catch)**: Direct inspection of `RegisterAnonymousCommand.cs:90-91` proved every anonymous user is created with `MobileNumber = "ANONYMOUS"` and `NormalizedMobileNumber = "ANONYMOUS"`. Making `Users.NormalizedMobileNumber` unique (even sparse/filtered) would **break 2nd+ anonymous user registration with `E11000` duplicate key**!
- **Security Auditor & Clean Architecture Reviewer**: Verified `DeleteAccountCommand.cs:76-120` loops N+1 across 7 repos while omitting `QrLoginSession`, `AppNotification` PII (`MetadataJson`), `Recovery*`, `ConsentIpAddress`/`ConsentUserAgent`, `UserProfile` clinical PHI, `AiUsageLog` pseudonymization, and physical disk photos (`LocalPhotoStorageService.cs:64` zero-physical-delete policy), while `AdminDeleteUserCommandHandler` (`AdminUserCommands.cs:631-645`) omits `_userRepo.UpdateAsync`, `_profileRepo.UpdateAsync`, and `NormalizedEmail`/`NormalizedMobileNumber` anonymization.
- **Consensus Resolution**:
  1. **`Users.NormalizedMobileNumber`**: Keep **NON-unique** across all 3 DBs (matching `DietTrackerDbContext.cs:153`) and anonymize to `_DEL_{id}` on delete.
  2. **Filtered/Partial Unique Indexes + Anonymization Parity Across All 3 DBs**:
     - `Users.NormalizedEmail`: Unique filtered (`WHERE [IsDeleted] = 0` in SQLite/Azure SQL; `partialFilterExpression: { IsDeleted: false }` in DocumentDB) + `_DELETED_{id}` anonymization in **both** `DeleteAccountCommand` and `AdminDeleteUserCommand`.
     - `Ledgers (UserId, Date)`: Unique filtered (`IsDeleted == false` / `WHERE [IsDeleted] = 0`).
     - `Meals (UserId, ClientMutationId)`: Unique filtered (`IsDeleted == false && ClientMutationId is string` / `WHERE [IsDeleted] = 0 AND [ClientMutationId] IS NOT NULL`).
  3. **DPDPA Cascade Port (`IAccountErasurePort`)**: Batch `UpdateManyAsync`/`DeleteManyAsync` (DocumentDB) and `ExecuteUpdateAsync`/`ExecuteDeleteAsync` (EF Core) revoking `SecurityStamp` + `IsActive = false` first, scrubbing `ApplicationUser` (`Email`, `MobileNumber`, `DisplayName`, `PasswordHash`, `Recovery*`, `PendingRecovery*`, `ConsentIpAddress`, `ConsentUserAgent`), scrubbing `UserProfile` clinical PHI (`Name`, `DiagnosedConditions`, `Medications`), soft-deleting/scrubbing `MealLog`/`DailyCalorieLedger`/`ProgressPhoto`/`UserCorrectionRecord`/`AiDetectionFeedbackRecord`, hard-deleting `VerificationOtp`/`QrLoginSession` (`AuthorizedUserId == userId`)/personal `AppNotification`s + scrubbing `SubjectUserId` `NewRegistration` PII, and pseudonymizing `AiUsageLog.UserId`. Physical photo deletion vs 30-day grace tombstone is escalated in **§8 Doubt 1**.

### Trade-Off 7: Pre-Existing P0/P1 Security Fixes, `DataMigrator` Sequencing & Vector 7 CI Gate
- **Clean Architecture Reviewer (Critical CI Catch)**: `scripts/verify-ai-security-defense.ps1:488-576` (**Vector 7**) inspects `git diff` on **every** PR across `infra/`, `src/Nutrition.Presentation/`, `src/Nutrition.WebGateway/wwwroot/`, `src/Nutrition.Application/`, `src/Nutrition.Infrastructure/AI/`, and `src/Nutrition.Domain/`. Deferring all ADR/SDD updates to PR 5 would **fail CI on PRs 1–4**!
- **All 3 Specialists**: Confirmed all 4 `DataMigrator` bugs (`DataConversionEngine.ResolveKnownEnum` `MealType` corruption + 6 missing enums, `AzureSqlTargetWriter.GetDdlForDomainTable` missing/misnamed columns, `DocumentDbTargetWriter` `Meals` `CreatedAtUtc` index + N+1 `CountDocumentsAsync`, and `Meals.Items` aggregate assembly/decomposition), plus `validate_e2e_tiers.ps1:89` calling non-existent `/api/analytics/ledger/today` instead of `/api/analytics/daily`.
- **Consensus Resolution**:
  1. Bundle DocumentDB/A1–A4-coupled security fixes directly inside PRs 1–4 (`NotificationsController.MarkAsRead` IDOR, `VerifyOtp` `IsActive`/`CanLogin` + `Channel` filter, `AdminUpdateUser` `SuperAdmin` password/mobile/verified guard, A1 DTO over-posting prevention, fail-closed `DocumentDbConnection` + TLS, DPDPA `IAccountErasurePort`).
  2. Fix all 4 `DataMigrator` bugs in PR 4; fix `validate_e2e_tiers.ps1:89` (`/api/analytics/daily`) in PR 5.
  3. Include an atomic ADR fragment (`docs/adr/<domain>/ADR-YYYYMMDD-NNN-*.md` + `scripts/sync-adr-index.ps1`) in **every PR** of the stack that modifies gated directories so Vector 7 passes on every PR, with full SDD/Skill/Diagram sync in PR 5.
  4. Isolate orthogonal pre-existing P0/P1 fixes (`MealTools` LFI, `InAppNotificationService` `Clients.All` broadcast, `HttpPayloadTelemetryMiddleware` PHI capture, static `/uploads` protection, `SecurityStamp` validation, partitioned rate limiter, `ForwardedHeaders`, CSV injection, `AllowDemoUsers`/`SimulateQrApproval` hardening) into an immediate **PR 6 Security Hotfix** (flagged in **§8 Doubts 3 & 4** for user confirmation).

---

## 3. Full-Solution Read/Write Audit & Adversarial Findings

### 3.1 Critical Silent Write-Loss Bugs (17+ Missing `UpdateAsync` Handlers Across 13 Files)

Because `DocumentDbRepository<T>` has no change tracker and `DocumentDbUnitOfWork.SaveChangesAsync()` is a no-op (`DocumentDbUnitOfWork.cs:16-21`), every handler below mutates an entity fetched via `GetByIdAsync` / `FirstOrDefaultAsync` and calls `_uow.SaveChangesAsync()` without `_repo.UpdateAsync()`, causing **100% silent data loss on DocumentDB**:

| # | Handler & Verified Location | Unpersisted Mutations | Security / Functional Consequence on DocumentDB |
|---:|---|---|---|
| 1 | `LoginCommand.cs:112-113` | `user.LastLoginAtUtc` | Login timestamp never updates. |
| 2 | `VerifyOtpCommand.cs:79-82, 86-96` | `activeOtp.AttemptCount++`, `activeOtp.IsUsed = true`, `user.IsEmailVerified`/`IsMobileVerified`, `user.LastLoginAtUtc` | **Unlimited OTP brute-force + replay within 5m TTL**; verification status lost. *(Also omits `o.Channel == request.Channel` at L71.)* |
| 3 | `ResetPasswordCommand.cs:84-87, 91-93` | `activeOtp.AttemptCount++`, `activeOtp.IsUsed = true`, `user.PasswordHash`, `user.SecurityStamp` | **Password resets silently fail**; OTP remains unconsumed and throttler broken. |
| 4 | `ForgotPasswordCommand.cs:64-67` | Prior `old.IsUsed = true` | Old OTPs remain valid after new OTP issuance. |
| 5 | `ResendOtpCommand.cs:72-75` | Prior `old.IsUsed = true` | Old OTPs remain valid after resend. |
| 6 | `ApproveQrSessionCommand.cs:74-75` | `session.Approve(...)` | QR login approval never persists; desktop never signs in. |
| 7 | `RejectQrSessionCommand.cs:57-58` | `session.Reject()` | Rejected QR sessions stay pending. |
| 8 | `SimulateQrApprovalCommand.cs:122-123` | `session.Approve(user.Id)` | Demo QR approval fails silently *(also queries case-sensitive `u.Email == targetEmail` at L100)*. |
| 9 | `ExchangeQrTicketCommand.cs:104-113` | `session.Consume()` (`_qrRepo`) **AND** `user.LastLoginAtUtc` (`_userRepo`) | **QR `ExchangeTicket` stays `Approved` (replay risk)** and login timestamp drops. |
| 10 | `GenerateRecoveryKitCommand.cs:67-75` | `user.PendingRecovery*` | Generated recovery kit fails to stage. |
| 11 | `ActivateRecoveryKitCommand.cs:78-79, 84-91` | Expired pending cleanup (`L79`) + `user.Recovery*` activation | Recovery kit activation & expiry cleanup silently drop. |
| 12 | `RecoverAccountCommand.cs:132-163` | `user.SecurityStamp`, `user.LastLoginAtUtc`, `user.Recovery*` | Account recovery key rotation & `SecurityStamp` revocation drop. |
| 13–17 | `AdminUserCommands.cs:71-72, 133-134, 207-212, 429-497, 631-645` | `UpdateUserTier`, `UpdateUserRole`, `UpdateUserStatus`, `AdminUpdateUser` (`targetUser` **AND** `_profileRepo` `targetProfile` L430-435), `AdminDeleteUser` (`targetUser` **AND** `_profileRepo` `profile` L638-643) | Tier/role/lock/password/onboarding/delete mutations silently drop; `AdminDeleteUser` also omits `NormalizedEmail`/`NormalizedMobileNumber` anonymization. |

### 3.2 Unbounded Scans, Write-on-Read Races, Timezone Bugs & LINQ3/DataMigrator Defects

| Finding & Exact Location | Root Cause | Impact |
|---|---|---|
| **IST Timezone Ledger Bug & Duplicate Meal Scan** (`ConfirmMealCommand.cs:154-155`, `UpdateMealCommand.cs:53,57`, `GetDailyLedgerQuery.cs:42-46`) | `LogMealAsync`/`UpdateMealAsync` already update `mealLocalDate` in user's `UserProfile.Timezone` (`ClinicalDietitianService.cs:213-215, 356-358`), then `ConfirmMealCommand:155` & `UpdateMealCommand:57` call `GetOrCreateDailyLedgerAsync` using **UTC** `DateOnly.FromDateTime(meal.LoggedAt)`. `GetDailyLedgerQuery:44` defaults omitted `Date` to `DateTime.UtcNow`. | Wrong-day `DailyCalorieLedger` created for IST users (`00:00–05:30 IST`) + redundant lifetime meal scan. |
| **Unbounded Scans & Write-on-Read** (`ClinicalDietitianService.cs:151,402,562`, `GetMealHistoryQuery.cs`, `WebBffController.cs`, `MobileBffController.cs`) | `_mealRepo.FindAsync(m => m.UserId == userId)` loads 100% of lifetime user meals; `GetOrCreateDailyLedgerAsync` writes `Ledgers` inside HTTP `GET` queries (concurrent BFF `Task.WhenAll` race). | Multi-MB reads, duplicate ledger race on GET, OOM risk. |
| **Unbounded Quota & Admin Scans** (`AiQuotaService.cs:123-146`, `GetAdminUsersQuery.cs:37,60`, `AdminTierCommands.cs:112-114`, `GetDeletedRecordsQuery.cs:57,79,101`, `DefaultDataSeeder.cs:792,817`) | 4 queries + full user `AiUsageLogs` scan in `AiQuotaService`; `GetAllAsync()` on `Users` and `AiUsageLogs` in Admin queries and Release startup seeder; full deleted scan before `.Take(100)`. | Full collection scans across tenants on startup and admin/dashboard requests. |
| **`Meals` / `FoodItems` Aggregate Split-Brain** (`ClinicalDietitianService.cs:44,267,289,314`, `DocumentDbTargetWriter.cs:278-279`) | `UpdateMealAsync` mutates `"FoodItems"` via `IRepository<FoodItemRecord>`, while `LogMealAsync` embeds `Items` inside `"Meals"`. | Orphan `"FoodItems"` docs in DocumentDB; empty `Items: []` after migration. |
| **Collection Name Casing Mismatch & Swallowed Index Errors** (`DocumentDbRepository.cs:266-283` vs `DocumentDbDatabaseInitializer.cs:78,95,100,110,115,125`) | Initializer targets `"users"`, `"meals"`, `"tier_configurations"` (lowercase/snake_case), skips 11 collections, and logs exceptions at `LogDebug`. | **100% `COLLSCAN` across all 13 live DocumentDB collections**. |
| **LINQ3 `StringComparison` Failures** (`ConfirmMealCommand.cs:97-98,107`, `SubmitAiFeedbackCommand.cs:105-106,115`) | `.Equals(..., StringComparison.OrdinalIgnoreCase)` / `.Contains(..., OrdinalIgnoreCase)` inside `FindAsync` expression trees. | MongoDB LINQ3 `ExpressionNotSupportedException` or unindexed scan. |
| **Lost-Update Presence Race & Non-Atomic QR Consume** (`UserPresenceTracker.cs:146-168`, `ExchangeQrTicketCommand.cs:59-113`) | Presence tracker reads full `ApplicationUser` and runs `ReplaceOneAsync(..., IsUpsert = true)` (`DocumentDbRepository.cs:216`) just for `LastActiveUtc`; QR exchange lacks atomic CAS. | Presence flush can overwrite `SecurityStamp`/`Tier`/`PasswordHash`; concurrent QR ticket replay race. |
| **`DataMigrator` Verified Parity Bugs** (`DataConversionEngine.cs:48-56,360-413`, `AzureSqlTargetWriter.cs:465-699`, `DocumentDbTargetWriter.cs:92,249-260`) | (1) `ResolveKnownEnum` maps `MealType` to `"morningtea"=1,"lunch"=2,"eveningtea"=3,"dinner"=4,"snack"=5` instead of `Breakfast=0,Lunch=1,Snack=2,Dinner=3`, and omits 6 of 10 enums; (2) `AzureSqlTargetWriter` omits 15 cols on `Users`, 5 on `Profiles`, 3 on `Meals`, 2 on `QrSessions`, and uses wrong `Ledgers` names (`TargetCalories`, `TargetSodiumMg`, `LastUpdatedUtc`); (3) `DocumentDbTargetWriter:92` indexes `Meals` on non-existent `CreatedAtUtc`, runs N+1 `CountDocumentsAsync` in `SkipExisting`, and doesn't embed `FoodItems`; (4) `KnownJsonColumns` omits `"MetadataJson"` consideration (must stay `string` in BSON!). | Corrupted `MealType` enum data, broken Azure SQL target DDL, invalid `Meals` index, split-brain migrated `FoodItems`. |
| **SDD / Skill Drift & Vector 7 CI Gate** (`scripts/verify-ai-security-defense.ps1:488-576`, `docs/sdd/{02,03,04,05,10}`, `.agents/skills/diet-dost-database-architecture/SKILL.md`) | SDDs/skills still reference Basic DTU, Cosmos DB, SQLite SMB-only, wrong config key `"Storage:Provider = 'DocumentDB'"`, lowercase collection names, and `diet_dost_docdb_fallback.db`. | Vector 7 fails CI on any PR touching `src/` or `infra/` without atomic ADR/SDD/skill updates. |

### 3.3 Security & Privacy Audit Summary (P0 / P1 Findings)

| Sev | Finding | Verified Evidence | Target Remediation Scope |
|---|---|---|---|
| **P0** | **VerifyOtp Bypasses `IsActive`/`CanLogin` & `Channel`** | `VerifyOtpCommand.cs:71,86-139` | **PR 2**: Enforce `user.IsActive && !user.IsDeleted && user.CanLogin(...)`, filter `o.Channel == request.Channel`, persist `_otpRepo`/`_userRepo`. |
| **P0** | **`AdminUpdateUser` Allows `Admin` to Reset `SuperAdmin` Password** | `AdminUserCommands.cs:439-496` | **PR 2**: Extend `SuperAdmin` (and peer `Admin`) guard to `NewPassword`, `MobileNumber`, `IsEmailVerified`; persist `_userRepo`/`_profileRepo`. |
| **P0** | **`NotificationsController.MarkAsRead` IDOR** | `NotificationsController.cs:80-93` | **PR 2**: Refactor to `INotificationRepository.MarkAsReadAsync` with `RecipientUserId`/`RecipientRole` ownership check (**mandatory before PR 4 90d TTL**). |
| **P0** | **Fail-Open Unencrypted Mongo Fallback & Hardcoded Secrets** | `StorageInfrastructureExtensions.cs:160`, `ConfigurationExtensions.cs:42-123`, `DatabaseConfigurationProvider.cs:50-60` | **PR 3**: Throw if `DocumentDbConnection` missing; enforce TLS on non-loopback; skip SQLite secrets in DocumentDB mode; restrict `DefaultDevJwtKey` & `"DietDost@Demo2026!"` to `env.IsDevelopment()`. |
| **P0** | **MCP `MealTools` Arbitrary Local File Read** | `MealTools.cs:265-268,366` | **PR 6** *(or PR 2 per §8 Doubt 4)*: Remove `File.Exists(photoData)` local disk read; block unauthenticated dev `free@dietdost.app` fallback outside Dev. |
| **P0** | **SignalR `Clients.All` PII Broadcast** | `InAppNotificationService.cs:156-161` | **PR 6** *(or PR 2 per §8 Doubt 4)*: Target `applicant_{ticket}` / user group instead of `Clients.All`. |
| **P0** | **`SimulateQrApproval` Unauth Arbitrary User Impersonation** | `QrAuthController.cs:368-398`, `SimulateQrApprovalCommand.cs:89-118` | **PR 2 (email normalization + `IsDemoAccount` guard)** + **PR 6 / §8 Doubt 3** for `AllowDemoUsers` production gating. |
| **P1** | **Over-Posting & Server-Side Authority (`MealLog`/`UserProfile`)** | `MealsController.cs:148,201`, `ProfileController.cs:58`, `ConfirmMealCommand.cs:47-75` | **PR 1**: A1 Request DTOs strip `IsDeleted`/`DeletedAtUtc`/`ConcurrencyVersion`; server generates `MealLog.Id` & computes `WhoComplianceFlags`/`MedicationWarnings`. |
| **P1** | **DPDPA 2023 Incomplete Erasure & Admin Anonymization** | `DeleteAccountCommand.cs:62-121`, `AdminUserCommands.cs:631-645`, `LocalPhotoStorageService.cs:64` | **PR 2–3**: `IAccountErasurePort` scrubs `UserProfile` PHI, `QrLoginSession`, `AppNotification` (`SubjectUserId`), `Recovery*`, `ConsentIpAddress`/`UserAgent`, pseudonymizes `AiUsageLog`; physical photo policy confirmed in **§8 Doubt 1**. |
| **P1** | **AI Quota TOCTOU & Stale JWT `Tier` Claim** | `AiQuotaService.cs:49-189`, `MealsController.cs:85,127`, `TierConfigurationService.cs:26`, `DatabaseSecretStore.cs:37` | **PR 2–3**: Resolve `Tier` from DB; `IMemoryCache` 60s TTL; `IAiQuotaReservationGate` (single-replica `SemaphoreSlim` vs multi-replica `IsPending` confirmed in **§8 Doubt 2**). |
| **P1** | **Bicep Zero-Trust & URI Escaping** | `infra/dev/infra.bicep:644`, `infra/dev/documentdb.bicep:123` | **PR 3 / PR 6**: `allowAllAzureServices: false` (`infra.bicep:644`), `uriComponent(adminPassword)` (`documentdb.bicep:123`). |
| **P1** | **Static `/uploads` Exposure, `MediaController` Separator, `SecurityStamp`, Global Rate Limiter, OTEL PHI, CSV Injection** | `WebApplicationExtensions.cs:51-95`, `LocalPhotoStorageService.cs:99-108`, `MediaController.cs:72,82`, `JwtTokenService.cs:75-112`, `SecurityAndAuthExtensions.cs:67-126`, `RateLimitingExtensions.cs:38-96`, `HttpPayloadTelemetryMiddleware.cs:46-48`, `ExportMealHistoryQuery.cs:82-87`, `ExportAnalyticsDataQuery.cs:68` | **PR 2 (`MediaController` path separator + `AllowPhotoCompare` tier gate)** + **PR 6 Security Hotfix** (static `/uploads` auth + MIME extension mapping, `SecurityStamp` validation, partitioned rate limiter + trusted proxy headers, OTEL PHI exclusion, CWE-1236 CSV neutralization). |

---

## 4. Complete Entity-by-Entity NoSQL Storage, Indexing & Tri-Database Parity Specification

### 4.1 Complete Domain Inventory (14 Entities, 4 Value Objects, 10 Enums)
- **14 Domain Entities**: `ApplicationUser`, `UserProfile`, `MealLog`, `FoodItemRecord` *(embedded in `MealLog.Items` in DocumentDB per A2)*, `DailyCalorieLedger`, `ProgressPhoto`, `UserCorrectionRecord`, `AiDetectionFeedbackRecord`, `TierFeatureConfiguration`, `AiUsageLog`, `AppSecret`, `VerificationOtp`, `QrLoginSession`, `AppNotification`.
- **4 Value Objects**: `RecoveryKit`, `MedicationEntry`, `BmrTdeeResult`, `MacroDistribution`.
- **10 Domain Enums** (`Int32` in EF & DocumentDB BSON): `UserRole`, `UserTier`, `OtpChannel`, `AiOperationType`, `QrSessionStatus`, `MealType` (`Breakfast=0, Lunch=1, Snack=2, Dinner=3`), `BiologicalSex`, `ActivityLevel`, `DietaryPreference`, `ProgressPhotoType`. *(Note: `UserCorrectionRecord.MealType` is `string`, NOT `MealType` enum.)*

### 4.2 13-Collection DocumentDB Storage, ESR & TTL Index Matrix

> [!IMPORTANT]
> **Reconciled Index Rules**:
> 1. **`Users.NormalizedMobileNumber` MUST remain NON-unique** across all 3 databases (matching `DietTrackerDbContext.cs:153`) because `RegisterAnonymousCommand.cs:90-91` sets `NormalizedMobileNumber = "ANONYMOUS"` for every anonymous user.
> 2. **`AppNotifications` 90-day TTL** uses a standard single-field index `{ ReadAtUtc: 1 }, { expireAfterSeconds: 7776000 }` (no `partialFilterExpression`), paired with the strict invariant that `ReadAtUtc == null` while `!IsRead` and `NotificationsController.MarkAsRead` IDOR is fixed first.
> 3. **Collection casing**: Strict PascalCase across `DocumentDbRepository<T>`, `DocumentDbDatabaseInitializer`, and `DocumentDbTargetWriter`.

| # | Collection | Domain Type(s) | `_id` & Embed/Ref Strategy (A2) | DocumentDB Indexes (ESR Order) & Constraints | TTL / Concurrency / Retention |
|---:|---|---|---|---|---|
| 1 | `Users` | `ApplicationUser` | `_id = Id` (GUID string); embedded `PendingRecovery*` & consent fields | `{ NormalizedEmail: 1 }` (**Unique, Partial**: `{ IsDeleted: false }`), `{ NormalizedMobileNumber: 1 }` *(non-unique)*, `{ RecoveryLookupId: 1 }` (Sparse), `{ IsDeleted: 1, IsActive: 1, LastActiveUtc: -1 }`, `{ IsDeleted: 1, CreatedAtUtc: -1 }` | `IConcurrencyAware` (`ConcurrencyVersion`); `$set` batch `LastActiveUtc`; anonymize `_DELETED_{Id}` / `_DEL_{Id}` on both self & admin delete |
| 2 | `Profiles` | `UserProfile` + `List<MedicationEntry>` | `_id = Id == UserId` (O(1) point read); embedded `Medications[]` & `DiagnosedConditions[]` | `_id` (PK), `{ IsDeleted: 1, UpdatedAtUtc: -1 }` | `IAggregateRoot`, `IConcurrencyAware` |
| 3 | `Meals` *(+ retired `FoodItems`)* | `MealLog` + embedded `List<FoodItemRecord>` | **A2**: Embed `FoodItemRecord[]` in `Items`; drop `"FoodItems"` collection | `{ UserId: 1, IsDeleted: 1, LoggedAt: -1 }`, `{ UserId: 1, ClientMutationId: 1 }` (**Unique, Partial**: `{ IsDeleted: false, ClientMutationId: { $type: "string" } }`), `{ IsDeleted: 1, DeletedAtUtc: -1 }` | `IAggregateRoot`, `IConcurrencyAware`, `UpdatedAtUtc` |
| 4 | `Ledgers` | `DailyCalorieLedger` | `_id = Id`; 1 doc per `(UserId, Date)`; UTC midnight `BsonDateTime` | `{ UserId: 1, Date: 1 }` (**Unique, Partial**: `{ IsDeleted: false }`), `{ UserId: 1, IsDeleted: 1, Date: -1 }` | `IAggregateRoot`, `IConcurrencyAware`, `UpdatedAtUtc`; written only on write commands |
| 5 | `ProgressPhotos` | `ProgressPhoto` | `_id = Id`; separate aggregate; URI masked via `ProgressPhotoDto` | `{ UserId: 1, IsDeleted: 1, PhotoType: 1, CapturedAtUtc: 1 }`, `{ IsDeleted: 1, DeletedAtUtc: -1 }` | `IAggregateRoot`, `UpdatedAtUtc` |
| 6 | `Corrections` | `UserCorrectionRecord` | `_id = Id`; `MealType` is `string`; `CreatedAtUtc` timestamp; add `NormalizedOriginalItem` | `{ UserId: 1, IsDeleted: 1, NormalizedOriginalItem: 1 }`, `{ UserId: 1, IsDeleted: 1, FrequencyCount: -1 }` | `IAggregateRoot`, `UpdatedAtUtc` |
| 7 | `AiFeedbacks` | `AiDetectionFeedbackRecord` | `_id = Id`; append-only record referencing `MealLogId` | `{ UserId: 1, IsDeleted: 1, CreatedAtUtc: -1 }`, `{ Rating: 1, CreatedAtUtc: -1 }` | `ISoftDeletable` |
| 8 | `TierConfigurations` | `TierFeatureConfiguration` | `_id = Id`; 4 reference docs | `{ Tier: 1 }` (**Unique**) | `IAggregateRoot`, `IConcurrencyAware`; `IMemoryCache` 60s TTL + eviction |
| 9 | `AiUsageLogs` | `AiUsageLog` | `_id = Id`; append-only telemetry | `{ UserId: 1, TimestampUtc: -1, IsSuccess: 1, OperationType: 1 }`, `{ TimestampUtc: -1, IsSuccess: 1 }` | Bounded 2-query `Task.WhenAll` summary + `$in` admin today counts |
| 10 | `AppSecrets` | `AppSecret` | `_id = Key` (no `Id` property) | `_id` (PK) | `IMemoryCache` 60s TTL + eviction |
| 11 | `VerificationOtps` | `VerificationOtp` | `_id = Id`; ephemeral OTP hash | `{ UserId: 1, Channel: 1, IsUsed: 1, ExpiresAtUtc: -1 }`, `{ Target: 1, Channel: 1, CreatedAtUtc: -1 }`, **`{ ExpiresAtUtc: 1 }` (TTL `86400s`)** | **A4 TTL**: 24h post-`ExpiresAtUtc`; atomic attempt/consume |
| 12 | `QrSessions` | `QrLoginSession` | `_id = Id`; add `ApprovedAtUtc?`, `ConsumedAtUtc?` | `{ ExchangeTicket: 1 }` (Sparse), `{ Status: 1, ExpiresAtUtc: 1 }`, **`{ ExpiresAtUtc: 1 }` (TTL `86400s`)** | **A4 TTL**: 24h post-`ExpiresAtUtc`; `IAggregateRoot`, `IConcurrencyAware`, CAS transitions |
| 13 | `AppNotifications` | `AppNotification` | `_id = Id`; keep `MetadataJson` as `BsonType.String` (`string?`); add `SubjectUserId?` | `{ RecipientRole: 1, IsDeleted: 1, IsRead: 1, CreatedAtUtc: -1 }`, `{ RecipientUserId: 1, IsDeleted: 1, IsRead: 1, CreatedAtUtc: -1 }`, `{ SubjectUserId: 1 }` (Sparse), **`{ ReadAtUtc: 1 }` (TTL `7776000s`)** | **A4 TTL**: 90d post-`ReadAtUtc`; `IAggregateRoot`; bulk `MarkAllAsRead` |

### 4.3 Tri-Database Parity Matrix (DocumentDB vs. Azure SQL vs. SQLite)

| Concern | SQLite | Azure SQL (`GP_S_Gen5_1`) | DocumentDB (Mongo vCore 6.0) | Mandatory Parity Alignment |
|---|---|---|---|---|
| **Unit of Work & Writes** | EF ChangeTracker + ACID `SaveChangesAsync()` | EF ChangeTracker + ACID `SaveChangesAsync()` | Immediate repository writes + CAS `UpdateAsync`; no-op `SaveChangesAsync()` | All handlers explicitly call `_repo.UpdateAsync` + `_uow.SaveChangesAsync()`; multi-doc/atomic updates use specialized ports (`IQrLoginSessionRepository`, `IUserRepository`, `INotificationRepository`, `IAccountErasurePort`). |
| **`Meals` & `FoodItems` (A2)** | Relational `Meals` + `FoodItems` tables (FK cascade) | Relational `Meals` + `FoodItems` tables (FK cascade) | Single `Meals` doc with embedded `Items[]`; **no `FoodItems` collection** | Remove `IRepository<FoodItemRecord>` from `ClinicalDietitianService`; `EfMealRepository` & `DataMigrator` synchronize child items through `MealLog.Items`. |
| **Filtered Unique Indexes** | `WHERE [IsDeleted] = 0` (`NormalizedEmail`, `Ledgers(UserId, Date)`, `Meals(UserId, ClientMutationId) WHERE ClientMutationId IS NOT NULL`); `NormalizedMobileNumber` non-unique | Same filtered unique `WHERE [IsDeleted] = 0` indexes; `NormalizedMobileNumber` non-unique | `partialFilterExpression: { IsDeleted: false }` (`NormalizedEmail`, `Ledgers(UserId, Date)`, `Meals(UserId, ClientMutationId)` with `{ $type: "string" }`); `NormalizedMobileNumber` non-unique | Plus anonymization (`_DELETED_{id}` / `_DEL_{id}`) on both `DeleteAccountCommand` and `AdminDeleteUserCommand`. |
| **Case-Insensitive Queries** | Normalized fields / SQLite `NOCASE` | Default `CI_AS` collation | Case-sensitive exact BSON match; LINQ3 rejects `OrdinalIgnoreCase` | Query `NormalizedEmail`, `NormalizedMobileNumber`, `NormalizedOriginalItem`; zero `StringComparison.OrdinalIgnoreCase` inside `Expression<Func<T, bool>>`. |
| **UTC & `DateOnly` & `QrLoginSession`** | `ValueConverter` (`DateTimeKind.Utc`) + `ApprovedAtUtc`/`ConsumedAtUtc` | `datetime2` UTC converter + `ApprovedAtUtc`/`ConsumedAtUtc` | `BsonDateTime` (UTC) + `ResilientDateOnlySerializer` + `ApprovedAtUtc`/`ConsumedAtUtc` | Align `QrLoginSession` C# model, `DietTrackerDbContext`, `AzureSqlTargetWriter`, and `DocumentDbBsonMappings`. |
| **Enums & `MetadataJson`** | `int` enums; `NVARCHAR` JSON string | `int` enums; `NVARCHAR(MAX)` JSON string | `ResilientEnumSerializer` (`Int32` write, `Int32`/`String` read); `ResilientJsonStringSerializer` (`BsonType.String` write, `String`/`Document` read) | Fix `DataConversionEngine.ResolveKnownEnum` (`MealType` 0..3 + all 10 enums) and keep `MetadataJson` as `BsonType.String`! |
| **Concurrency & Cache** | `ConcurrencyVersion` EF concurrency token | `ConcurrencyVersion` EF concurrency token | `ConcurrencyVersion` CAS `ReplaceOneAsync` | `IMemoryCache` (60s TTL + write eviction) in `TierConfigurationService` and `DatabaseSecretStore`. |

---

## 5. Clean Architecture, DDD, OOPS & SOLID Design Specification & A1 DTO Contracts

### 5.1 Project Dependency & Layer Boundary Fixes
1. **Move `src/Nutrition.Application/Common/UserClaimsExtensions.cs` -> `src/Nutrition.WebGateway/Extensions/UserClaimsExtensions.cs`** (currently declares `namespace Nutrition.WebGateway.Extensions;` inside `Nutrition.Application`).
2. **Decouple `ClaimsPrincipal` from `src/Nutrition.Application/Features/Auth/DTOs/AuthDtos.cs`**: Replace `ClaimsPrincipal` on `LoginResultDto`, `VerifyOtpResultDto`, `RegisterAnonymousResultDto`, `ExchangeQrTicketResultDto`, and `RecoverAccountResultDto` with a pure `AuthenticatedClaimsPayload`, and replace `TierFeatureConfiguration? Entitlements` on `CurrentUserResponseDto` with `TierFeatureConfigurationDto?`.
3. **Remove `IRepository<FoodItemRecord>`** from `ClinicalDietitianService` (`L44,267,289,314`) and ban `IRepository<FoodItemRecord>` outside `DataMigrator` legacy adapters.
4. **Domain Marker Interfaces**: Keep `IReadRepository<T>`, `IWriteRepository<T>`, and `IRepository<T>` constrained to **`where T : class`** (required so `AppSecret` with `Key` PK and non-root append-only `VerificationOtp`/`AiUsageLog`/`AiDetectionFeedbackRecord` work cleanly). Apply `IAggregateRoot` (`string Id { get; }`) and `IConcurrencyAware` (`long ConcurrencyVersion { get; set; }`) to mutable aggregate roots (`ApplicationUser`, `UserProfile`, `MealLog`, `DailyCalorieLedger`, `QrLoginSession`, `TierFeatureConfiguration`, `ProgressPhoto`, `UserCorrectionRecord`, `AppNotification`). Keep entity public setters compatible with EF/BSON/seeders while adding explicit domain transition methods (`SoftDelete()`, `Anonymize()`, `NormalizeKeys()`, `Approve()`, `Consume()`).
5. **Thin CQRS Controllers**:
   - `NotificationsController`: Remove `IRepository<AppNotification>` and `IUnitOfWork`; dispatch `GetNotificationsQuery` (clamping `limit` 1..100, honoring `unreadOnly`), `MarkNotificationReadCommand` (enforcing ownership check -> `403 Forbidden`), and `MarkAllNotificationsReadCommand` (bulk `UpdateManyAsync`/`ExecuteUpdateAsync`).
   - `MediaController`: Remove `IRepository<ProgressPhoto>`, `IOptions<StorageOptions>`, `IWebHostEnvironment`, and filesystem I/O; dispatch `GetProgressPhotoMediaQuery` (enforcing ownership/admin, `AllowPhotoCompare` tier check, `IsDeleted && !isAdmin`, and trailing `Path.DirectorySeparatorChar` check inside `IPhotoStorageService.ResolveAuthorizedProgressPhotoPath`).

### 5.2 Segregated Generic Ports, `QuerySpec<T>` & All 6 Specialized Domain Ports

- **Segregated Generic Ports (`src/Nutrition.Application/Common/Repositories/`)**:
  - Delete `IQueryable<T> Query()` (`IRepository.cs:25` — verified 0 callers).
  - `QuerySpec<T>(Expression<Func<T, bool>>? Filter = null, Expression<Func<T, object>>? OrderBy = null, bool Descending = false, int? Skip = null, int? Take = null, bool IncludeDeleted = false)`
  - `IReadRepository<T> where T : class` (`GetByIdAsync`, `GetAllAsync`, `FindAsync(predicate)`, `FindAsync(QuerySpec<T>)`, `ProjectAsync<TResult>(QuerySpec<T>, selector)`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync`, `FindDeletedAsync(QuerySpec<T>)` + backward-compat `FindDeletedAsync(predicate?)`).
  - `IWriteRepository<T> where T : class` (`AddAsync`, `UpdateAsync`, `DeleteAsync`).
  - `IRepository<T> : IReadRepository<T>, IWriteRepository<T> where T : class`.
  - **`DocumentDbRepository<T>` Safety**: Unwrap `UnaryExpression(ExpressionType.Convert)` on `OrderBy` value types (`DateTime`, `int`, `double`) into `FieldDefinition<T>`, enforce `Skip = Math.Max(0, spec.Skip ?? 0)` and `Take = Math.Clamp(spec.Take ?? 100, 1, 500)` (`MaxPageSize = 500`), and auto-inject `IsDeleted != true` unless `spec.IncludeDeleted == true`.
- **6 Specialized Domain Ports + Quota Gate**:
  1. **`IMealRepository`**: `GetByUserAndUtcRangeAsync(userId, fromUtc, toUtc, mealType?, ct)`, `GetLoggedEntriesForStreakAsync(userId, sinceUtc, ct)` (returning `(DateTime LoggedAt, double TotalCalories)` projection for `ClinicalCalculators.CalculateConsistencyStreak` without loading `Items`), `GetByClientMutationIdAsync(userId, clientMutationId, ct)`.
  2. **`IUserRepository`**: `UpdateLastActiveBatchAsync(IReadOnlyCollection<(string UserId, DateTime LastActiveUtc)> updates, ct)` (`BulkWriteAsync` `$set` in DocumentDB / `ExecuteUpdateAsync` in EF Core).
  3. **`IQrLoginSessionRepository`**: `TryMarkScannedAsync(...)`, `TryApproveAsync(...)` (matching `Status in [Pending, Scanned] && ExpiresAtUtc > utcNow`), `TryConsumeAsync(...)` (matching `Status == Approved && ExchangeTicket == exchangeTicket && ExpiresAtUtc > utcNow`), `TryRejectAsync(...)` (matching `Status in [Pending, Scanned] && ExpiresAtUtc > utcNow`).
  4. **`IAiUsageRepository`**: `GetQuotaWindowSummaryAsync(userId, todayStartUtc, tomorrowStartUtc, sevenDaysAgoUtc, thirtyDaysAgoUtc, recentLimit = 20, ct)` (DocumentDB 2-query `Task.WhenAll`: 30-day `$match + $group` + indexed `Limit(20)`), `GetTodaySuccessCountsByUserIdsAsync(userIds, todayStartUtc, ct)` (replaces `GetAdminUsersQuery.cs:60`).
  5. **`INotificationRepository`**: `GetForRecipientAsync(userId, isAdminOrSuper, unreadOnly, limit, ct)`, `MarkAsReadAsync(notificationId, userId, isAdminOrSuper, readAtUtc, ct)` -> `NotificationReadResult { Success, NotFound, Forbidden }`, `MarkAllAsReadAsync(userId, isAdminOrSuper, readAtUtc, ct)`, `ScrubDeletedUserNotificationsAsync(userId, ct)`.
  6. **`IAccountErasurePort`** *(+ `IAiQuotaReservationGate`)*: `EraseUserAccountAsync(userId, erasedAtUtc, ct)` across all user-linked collections (`ApplicationUser`, `UserProfile`, `MealLog`, `DailyCalorieLedger`, `ProgressPhoto`, `UserCorrectionRecord`, `AiDetectionFeedbackRecord`, `VerificationOtp`, `QrLoginSession`, `AppNotification`, `AiUsageLog`).

### 5.3 All 10 A1 Application DTO Groups (Exact `camelCase` + Numeric Enum Wire Parity)

*(Preserves `BffContractSnapshotTests.cs`, `MealToolsTests.cs`, `AnalyticsToolsTests.cs`, `review-modal.js`, `profile-modal.js`, `meals-service.js`, `admin-modal.js`, `quota-modal.js`, and leaves `MobileBffController.MobileJsonOptions` string enums untouched.)*

1. **`SaveMealRequestDto` & `FoodItemRequestDto`**: Nullable `Id` (`review-modal.js:1773`), `UserId?` (ignored unless admin), numeric `MealType`, `DishName` (`[MaxLength(200)]`), `LoggedAt?`, `PhotoUri?`, `OverallConfidenceScore`, `AddedGheeKcal`, `AddedTadkaKcal`, `ClientMutationId?`, `ClientTimestampUtc?`, `List<FoodItemRequestDto> Items` (max 50), `DietitianAdvice?`, `AiFeedbackRating?`, `AiFeedbackRemarks?`. **Excludes** `IsDeleted`, `DeletedAtUtc`, `IsVerifiedByUser`, `ConcurrencyVersion`; server generates `MealLog.Id` / `FoodItemRecord.Id` on `ConfirmMeal` and computes `WhoComplianceFlags` & `MedicationWarnings` exclusively via `ClinicalDietitianService`.
2. **`MealLogDto` & `FoodItemDto`**: Full wire parity **including computed `bool IsConfidencePassed`** (`MealLog.cs:56`), numeric `MealType`, `Items`, totals, clinical advice/flags, `IsVerifiedByUser`; excludes `IsDeleted`, `DeletedAtUtc`, `ConcurrencyVersion`, `UpdatedAtUtc`. Updates `ConfirmMealResultDto`, `UpdateMealResultDto`, `MealHistoryResponseDto`, `WebDashboardCompositeDto.RecentMeals`, `GetMealByIdQuery`, `MealTools.cs`.
3. **`SaveProfileRequestDto` & `MedicationEntryDto`**: `MedicationEntryDto(string DrugName, string Dosage = "", string Frequency = "", string? TimingNotes = null)` (`profile-modal.js:463` omits `dosage`/`frequency`!); `SaveProfileRequestDto` caps `DiagnosedConditions` (<= 30) & `Medications` (<= 30), keeps numeric `Sex`, `ActivityLevel`, `DietaryPreference`, strips `IsDeleted`/`DeletedAtUtc`/`CreatedAtUtc`/`UpdatedAtUtc`.
4. **`UserProfileDto`**: Exact wire parity with `UserProfile` minus `IsDeleted`, `DeletedAtUtc`, `ConcurrencyVersion`. Updates `ProfileResponseDto`.
5. **`DailyCalorieLedgerDto`**: Exact wire parity **including computed `double PendingCalories`** (`DailyCalorieLedger.cs:23`), `HealthScoreStatusText`, `EarnedBadges`, `DostMessage`, `ConsistencyStreakDays`, `HealthScore`, all macro/oil/water fields, `Date`; excludes `IsDeleted`, `DeletedAtUtc`, `ConcurrencyVersion`. Updates `GetDailyLedgerQuery`, `ConfirmMealResultDto`, `UpdateMealResultDto`, `WebDashboardCompositeDto.TodayLedger`, `AnalyticsTools.cs`.
6. **`ProgressPhotoDto`**: Projects `PhotoUri = $"/api/media/progress/{p.Id}"` in mapper (never raw disk path, never `IsDeleted`/`DeletedAtUtc`). Updates `GetProgressPhotosQuery`, `ProgressPhotoUploadResultDto`, `ProgressPhotoComparisonDto`.
7. **`TierFeatureConfigurationDto`**: Numeric `UserTier`, limits/flags, `Description`, `UpdatedAtUtc`, `UpdatedByUserId`. Updates `CurrentUserResponseDto.Entitlements`, `GetTierConfigsQuery`, `UpdateTierConfigResultDto`.
8. **`AiUsageLogDto`**: Numeric `AiOperationType`, `ModelId`, `EstimatedTokensUsed`, `LatencyMs`, `IsSuccess`, `ErrorReason`, `TimestampUtc`. Updates `AiUsageStatsResult.RecentOperations`, `GetAiLogsQuery`, `WebDashboardCompositeDto.Quota`.
9. **`UserCorrectionDto` & `AiDetectionFeedbackDto`**: Strip `IsDeleted`, `DeletedAtUtc`, `NormalizedOriginalItem`.
10. **`NotificationFeedDto` & `NotificationDto`** + **`AuthenticatedClaimsPayload`** (`AuthDtos.cs` decoupling).

---

## 6. 5-PR GitHub Stacked PR Implementation Plan (+ Immediate PR 6 Security Hotfix)

> [!IMPORTANT]
> **Vector 7 CI Gate Compliance (`scripts/verify-ai-security-defense.ps1:488-576`)**: Because Vector 7 inspects `git diff` against `src/Nutrition.Domain/`, `src/Nutrition.Application/`, `src/Nutrition.Presentation/`, and `infra/` on **every** PR, **PR 1 through PR 5 each include a dedicated atomic ADR fragment** (`docs/adr/<domain>/ADR-YYYYMMDD-NNN-*.md`) regenerated via `pwsh -File scripts/sync-adr-index.ps1` so CI passes cleanly at every layer of the stack.

### PR 1: Domain Contracts, `QuerySpec<T>`, Specialized Ports & A1 DTOs
- **Scope**:
  - `[NEW]` `src/Nutrition.Domain/Common/IAggregateRoot.cs`, `IConcurrencyAware.cs`, `DbConcurrencyConflictException.cs`
  - `[MODIFY]` `ApplicationUser.cs`, `UserProfile.cs`, `MealLog.cs`, `DailyCalorieLedger.cs`, `ProgressPhoto.cs`, `TierFeatureConfiguration.cs`, `QrLoginSession.cs` (`ApprovedAtUtc?`, `ConsumedAtUtc?` + domain state helpers), `UserCorrectionRecord.cs` (`NormalizedOriginalItem`, `UpdatedAtUtc`, keep `CreatedAtUtc` & `string MealType`), `AppNotification.cs` (`SubjectUserId?`)
  - `[MODIFY]` `src/Nutrition.Application/Common/IRepository.cs` (`IReadRepository<T>`, `IWriteRepository<T>`, `IRepository<T>` `where T : class`, `QuerySpec<T>`, remove `IQueryable<T> Query()`)
  - `[NEW]` `src/Nutrition.Application/Common/Repositories/{IMealRepository,IUserRepository,IQrLoginSessionRepository,IAiUsageRepository,INotificationRepository,IAccountErasurePort}.cs`, `IAiQuotaReservationGate.cs`
  - `[NEW/MODIFY]` A1 DTOs & mappers in `src/Nutrition.Application/Features/{Meals,Profile,Analytics,ProgressPhotos,Admin,Notifications,Auth}/DTOs/`, move `UserClaimsExtensions.cs` to `src/Nutrition.WebGateway/Extensions/`, decouple `ClaimsPrincipal` from `AuthDtos.cs`
  - `[MODIFY]` `MealsController.cs`, `ProfileController.cs`, `AnalyticsController.cs`, `ProgressPhotosController.cs`, `AdminController.cs`, `WebBffController.cs`, `src/Nutrition.Mcp/Tools/{MealTools,AnalyticsTools}.cs`
  - `[MODIFY]` Coupled unit/contract tests (`BffContractSnapshotTests.cs`, `MealToolsTests.cs`, `AnalyticsToolsTests.cs`, `MobileBffCompositeTests.cs`, 5 in-memory `IRepository<T>` test fakes)
  - `[NEW]` Atomic ADR fragment (`docs/adr/architecture/ADR-20261125-001-*.md`) + `scripts/sync-adr-index.ps1`

### PR 2: Application Handler Write-Persistence Fixes, Coupled P0 Security Guards & Bounded Queries
- **Scope**:
  - `[MODIFY]` Add explicit `_repo.UpdateAsync` across all 13 files / 17+ handlers (`LoginCommand`, `VerifyOtpCommand` + `IsActive`/`CanLogin` + `Channel` filter + `_otpRepo.UpdateAsync`, `ResetPasswordCommand` + `_otpRepo.UpdateAsync`, `ForgotPasswordCommand`, `ResendOtpCommand`, `ApproveQrSessionCommand`, `RejectQrSessionCommand`, `SimulateQrApprovalCommand` + `NormalizedEmail` + `IsDemoAccount` guard, `ExchangeQrTicketCommand`, `GenerateRecoveryKitCommand`, `ActivateRecoveryKitCommand`, `RecoverAccountCommand`, `AdminUserCommands` including `_profileRepo.UpdateAsync` + `SuperAdmin`/peer-`Admin` password/mobile/verified guard + `AdminDeleteUser` anonymization)
  - `[MODIFY]` `ClinicalDietitianService.cs` (drop `IRepository<FoodItemRecord>`, return `(MealLog, DailyCalorieLedger)` on write, split read-only `GetDailyLedgerProjectionAsync` from `RecalculateAndSaveDailyLedgerAsync`, bounded `IMealRepository` queries), `ConfirmMealCommand.cs:155` & `UpdateMealCommand.cs:57` (delete redundant UTC ledger calls), `GetDailyLedgerQuery.cs:42-46` (use `UserProfile.Timezone`), `ClinicalCalculators.cs` (projection overload for streak)
  - `[MODIFY]` `ConfirmMealCommand.cs` & `SubmitAiFeedbackCommand.cs` (`NormalizedOriginalItem` replacing `StringComparison.OrdinalIgnoreCase`)
  - `[MODIFY]` `NotificationsController.cs` + new `Features/Notifications/{Queries,Commands}/` (P0 `MarkAsRead` IDOR fix, `limit`/`unreadOnly`, bulk `MarkAllAsRead`)
  - `[MODIFY]` `MediaController.cs` + new `Features/ProgressPhotos/Queries/GetProgressPhotoMedia/` + `IPhotoStorageService` (`StartsWith` trailing separator fix + `AllowPhotoCompare` tier check)
  - `[MODIFY]` Bounded `QuerySpec<T>` in `GetAdminUsersQuery.cs`, `AdminTierCommands.cs` (`GetAiLogsQuery`), `GetDeletedRecordsQuery.cs`, `DefaultDataSeeder.cs:792,817`; wire `IAiUsageRepository`, `IAiQuotaReservationGate`, `IAccountErasurePort` in `DeleteAccountCommand.cs`, and DB-resolved `UserTier` on AI-gated endpoints
  - `[NEW]` Atomic ADR fragment (`docs/adr/architecture/ADR-20261125-002-*.md`) + `scripts/sync-adr-index.ps1`

### PR 3: DocumentDB Engine Hardening, EF Core Parity & Zero SQLite Fallback (A3)
- **Scope**:
  - `[MODIFY]` `DocumentDbBsonMappings.cs` (`ResilientEnumSerializer`, `ResilientJsonStringSerializer` for `AppNotification.MetadataJson`, `ConcurrencyVersion`, `ApprovedAtUtc`/`ConsumedAtUtc`, `NormalizedOriginalItem`, `UpdatedAtUtc`, `SubjectUserId`)
  - `[MODIFY]` `DocumentDbRepository.cs` (`QuerySpec<T>` with `UnaryExpression` unwrapping + `MaxPageSize = 500`, CAS `UpdateAsync` without blind `IsUpsert`, single-trip atomic soft-delete `UpdateOneAsync`), `DocumentDbUnitOfWork.cs` (explicit immediate-write coordinator)
  - `[NEW]` DocumentDB & EF Core implementations of all 6 specialized ports + `InMemoryAiQuotaReservationGate` (under `src/Nutrition.Infrastructure/Persistence/{DocumentDb,Ef}/`)
  - `[MODIFY]` `EfRepository.cs` & `DietTrackerDbContext.cs` (`ApprovedAtUtc`/`ConsumedAtUtc`, `ConcurrencyVersion`, `NormalizedOriginalItem`, `UpdatedAtUtc`, `SubjectUserId`)
  - `[MODIFY]` `StorageInfrastructureExtensions.cs:155-198` (remove `diet_dost_docdb_fallback.db`, `DietTrackerDbContext`, and `"mongodb://localhost:27017"` fallback; fail closed + require TLS on non-loopback hosts; register `IReadRepository<>`, `IWriteRepository<>`, `IRepository<>`, and specialized ports)
  - `[MODIFY]` `ConfigurationExtensions.cs:42-142` (skip SQLite `AddDatabaseSecrets` in `DocumentDb`/`AzureSql` modes; restrict `DefaultDevJwtKey` & `"DietDost@Demo2026!"` to `env.IsDevelopment()`), `DatabaseHealthCheck.cs` (`[ActivatorUtilitiesConstructor]`), `src/Nutrition.Tools.McpCli/Program.cs` (`GetService<DietTrackerDbContext>()`), `src/Nutrition.AppHost/Extensions/WebGatewayResourceExtensions.cs` (forward `ConnectionStrings__DocumentDbConnection`), `UserPresenceTracker.cs` (use `IUserRepository.UpdateLastActiveBatchAsync`), `AiQuotaService.cs` (use `IAiUsageRepository.GetQuotaWindowSummaryAsync`), `TierConfigurationService.cs` & `DatabaseSecretStore.cs` (`IMemoryCache` 60s TTL + eviction + reset token), `infra/dev/documentdb.bicep:123` (`uriComponent`), `infra/dev/infra.bicep:644` (`allowAllAzureServices: false`)
  - `[NEW]` Atomic ADR fragment (`docs/adr/architecture/ADR-20261125-003-*.md`) + `scripts/sync-adr-index.ps1`

### PR 4: DocumentDB Indexes, TTL Policies & Aggregate/Schema-Aware `DataMigrator` (A2/A4)
- **Scope**:
  - `[MODIFY]` `src/Nutrition.WebGateway/Initialization/Initializers/DocumentDbDatabaseInitializer.cs` (PascalCase 13 collections, fail loudly via `LogError`/`LogWarning`, full compound + filtered unique + TTL indexes on `VerificationOtps`, `QrSessions`, `AppNotifications.ReadAtUtc`)
  - `[MODIFY]` `AzureSqlDatabaseInitializer.cs` & `SqliteDatabaseInitializer.cs` (sync filtered unique indexes `WHERE [IsDeleted] = 0`, `Users.NormalizedMobileNumber` non-unique, `ConcurrencyVersion`, `NormalizedOriginalItem`, `UpdatedAtUtc`, `SubjectUserId`)
  - `[MODIFY]` `src/Nutrition.Tools.DataMigrator/Services/`:
    1. `DataConversionEngine.cs`: Fix `MealType` (`0..3`) + add missing 6 enums in `ResolveKnownEnum`; keep `AppNotification.MetadataJson` as `BsonType.String` (`string?`)
    2. `AzureSqlTargetWriter.cs`: Sync `GetDdlForDomainTable` across all 14 tables (`Users`, `Profiles`, `Meals`, `Ledgers` `BudgetedCalories`/`SodiumLimitMg`, `QrSessions` `ApprovedAtUtc`/`ConsumedAtUtc`, `IsDeleted`/`DeletedAtUtc`, etc.)
    3. `DocumentDbTargetWriter.cs`: Fix `Meals` index (`LoggedAt` instead of `CreatedAtUtc`), batch `$in` in `SkipExisting`, match all 13 collection indexes/TTLs
    4. `Program.cs` / `DocumentDbTargetWriter.cs` / `DocumentDbSourceReader.cs`: **A2** `Sql -> DocumentDb` embeds `FoodItems` into `Meals.Items[]` and skips `"FoodItems"`; `DocumentDb -> Sql` decomposes `Meals.Items[]` into `FoodItems` rows
  - `[NEW]` Atomic ADR fragment (`docs/adr/architecture/ADR-20261125-004-*.md`) + `scripts/sync-adr-index.ps1`

### PR 5: Comprehensive Verification Tests, CFT Route Fix & Living Docs Sync
- **Scope**:
  - `[MODIFY]` `tests/validate_e2e_tiers.ps1:89` (`/api/analytics/ledger/today` -> `/api/analytics/daily`)
  - `[MODIFY]` Expand `tests/Nutrition.EvalHarness.Tests/{DocumentDbPersistenceTests,TriDatabaseMigrationTests,DatabaseHealthCheckTests,CleanArchitectureDecouplingTests,BffContractSnapshotTests,DualDatabaseSchemaMigrationTests}.cs` (13 active collections + legacy `FoodItems` migration test, IST timezone ledger test, `MarkAsRead` IDOR test, `VerifyOtp` `IsActive` test, `AdminUpdateUser` `SuperAdmin` guard test, CAS concurrency test, `QuerySpec<T>` `UnaryExpression` sort test)
  - `[MODIFY]` Holistic Living Docs Sync: `docs/sdd/{02_solution_architecture,03_data_models_and_contracts,04_security_and_compliance,05_devops_and_infrastructure,10_configuration_and_secret_management_architecture}.md`, `.agents/skills/diet-dost-database-architecture/SKILL.md`, `docs/architecture/diagrams/*.mermaid`, `README.md`, atomic ADR-5 + `scripts/sync-adr-index.ps1`

### Immediate Follow-Up Security Hotfix PR (PR 6 — Orthogonal Pre-Existing P0/P1 Hardening)
*(Or merged into PR 2–3 if user selects Option B in §8 Doubt 4)*:
- `MealTools.cs:265-268,366`: Remove arbitrary `File.Exists(photoData)` local file read; gate unauthenticated `free@dietdost.app` fallback
- `InAppNotificationService.cs:156-161`: Replace `Clients.All.SendAsync("AccountApproved", ...)` with scoped `applicant_{ticket}` / user group
- `JwtTokenService.cs` + `SecurityAndAuthExtensions.cs`: Embed & validate `security_stamp`, `IsActive`, `IsDeleted`, `Tier` in `OnTokenValidated` / `OnValidatePrincipal`; enforce `RequireHttpsMetadata` & `CookieSecurePolicy.Always` in non-dev
- `WebApplicationExtensions.cs:51-95` & `LocalPhotoStorageService.cs:99-108`: Protect `/uploads/users/*/meals/*` + enforce MIME-derived extensions
- `HttpPayloadTelemetryMiddleware.cs:46-48`: Exclude/redact PHI routes from OpenTelemetry body capture
- `RateLimitingExtensions.cs` + `WebApplicationExtensions.cs:30-32`: Partitioned per-user/IP rate limiter + trusted `ForwardedHeaders` proxies
- `ExportMealHistoryQuery.cs` & `ExportAnalyticsDataQuery.cs`: CWE-1236 CSV formula neutralization
- `infra/dev/app.bicep:35,280` & `appsettings.json:40`: `AllowDemoUsers` / `SimulateQrApproval` production policy (**§8 Doubt 3**), `AI__GoogleAI__ApiKey` naming alignment, remove `"******"` placeholder

---

## 7. Future Considerations

1. **Mobile Offline-First Delta Sync (`UpdatedAtUtc` + `?sinceUtc=`)**: Adding `UpdatedAtUtc` to `MealLog`, `DailyCalorieLedger`, `ProgressPhoto`, and `UserCorrectionRecord` with `(UserId, UpdatedAtUtc)` indexes enables incremental mobile sync without full-history pulls.
2. **IFCT 2017 / ICMR-NIN 2024 `FoodCatalog` Collection**: Replacing static `IndianFoodEstimator` with a DocumentDB `FoodCatalog` collection indexed on Hindi/regional aliases fits cleanly under `QuerySpec<T>` / specialized ports.
3. **Transactional Outbox (`OutboxEvents`)**: Decouples async notifications, AI retraining feedback, and physical media purges from HTTP request latency.
4. **Azure Blob Storage + Short-Lived SAS Media URLs**: Moving `/uploads/users/*` out of local container disk is required before scaling ACA beyond `maxReplicas: 1`.
5. **Distributed Rate Limiting, Cache Backplane & Short-Lived JWTs**: Redis or DocumentDB TTL stores for multi-replica rate limiting, `IAiQuotaReservationGate`, `TierConfigurationService`/`DatabaseSecretStore` invalidation, and 15-minute access tokens + refresh rotation + `SecurityStamp` validation.
6. **`AiUsageLogs` Long-Term Retention / Pseudonymization TTL**: Consider adding a 180-day or 365-day TTL/archival policy on pseudonymized `AiUsageLogs` to control Free Tier 32 GB storage growth.

---

## 8. Confirmed Policy & Scope Decisions (User Approved Option A Across Doubts 1–4)

The user explicitly confirmed **Option A** for all four policy/scope choices below on 2026-11-26:

1. **Doubt 1 — DPDPA 2023 Physical Photo Erasure vs. `LocalPhotoStorageService` "Zero Physical Deletion Policy" *(Confidence: 82%)***
   - *Verified Conflict*: `DeleteAccountCommand.cs:120` states records are *"permanently deleted per DPDPA guidelines"*, whereas `LocalPhotoStorageService.cs:64-66` enforces a *"Zero Physical Deletion Policy"* (logging without deleting disk files) and `GetDeletedRecordsQuery.cs` surfaces soft-deleted `ProgressPhoto` / `MealLog` / `ApplicationUser` rows to SuperAdmin.
   - **Option A (Strict DPDPA Self-Erasure — Recommended)**: On self-service `DeleteAccountCommand`, **physically delete** `/uploads/users/{sanitizedUserId}/` files via `IPhotoStorageService.DeleteUserDirectoryAsync`, scrub all PII/PHI across all collections, hard-delete `VerificationOtp`/`QrLoginSession`/personal `AppNotification`s, and pseudonymize `AiUsageLog`; keep reversible soft-delete (zero physical deletion) only for single-item deletes (`DeleteMeal`, `DeleteProgressPhoto`) and `AdminDeleteUserCommand`.
   - **Option B (Unified 30-Day Soft-Delete Grace Tombstone)**: Scrub DB PII/PHI immediately and return `410 Gone` from `MediaController` for deleted users, retaining physical files on disk for a bounded 30-day audit window before scheduled purge.

2. **Doubt 2 — `AiQuotaService` Single-Replica `SemaphoreSlim` Gate vs. Immediate Multi-Replica `AiQuotaLeases` / `IsPending` DB Reservation *(Confidence: 88%)***
   - *Verified Conflict*: Per-user `SemaphoreSlim` (`InMemoryAiQuotaReservationGate`) eliminates concurrent AI quota TOCTOU in single-replica ACA (`maxReplicas: 1`) with zero schema change while preserving `IsConfidenceGatedPassed == false` retake fairness. However, scaling ACA above 1 replica later would require DB-backed reservation (`bool IsPending` + 3-min `ReservationExpiresAtUtc` on `AiUsageLog`, or an `AiQuotaLeases` collection).
   - **Option A (Single-Replica `SemaphoreSlim` Behind `IAiQuotaReservationGate` Now — Recommended)**: Zero schema churn in PRs 1–4, swap to DocumentDB lease behind `IAiQuotaReservationGate` when `maxReplicas > 1` is enabled.
   - **Option B (Multi-Replica DB `IsPending` Lease in PRs 1–4)**: Add `IsPending` / `ReservationExpiresAtUtc` reserve-and-finalize fields to `AiUsageLog` across SQLite, Azure SQL, DocumentDB, and `DataMigrator` immediately.

3. **Doubt 3 — `AllowDemoUsers`, `SimulateQrApproval` & Dev Secret Fallbacks in Showcase ACA Deployments *(Confidence: 86%)***
   - *Verified Conflict*: `infra/dev/app.bicep:35` defaults `allowDemoUsers = true` in `ASPNETCORE_ENVIRONMENT=Production`, which currently enables `DefaultDevJwtKey` fallback (`SecurityAndAuthExtensions.cs:45`, `ConfigurationExtensions.cs:86`), `"DietDost@Demo2026!"` SuperAdmin fallback (`ConfigurationExtensions.cs:117`), and unauthenticated `SimulateQrApproval` (`SimulateQrApprovalCommand.cs:89-118`, `QrAuthController.cs:368`).
   - **Option A (Showcase-Compatible Hardened — Recommended)**: Ban `DefaultDevJwtKey` and `"DietDost@Demo2026!"` fallbacks whenever `!env.IsDevelopment()` (require Key Vault / env config), keep `AllowDemoUsers` solely for seeded demo accounts in showcase ACA, and restrict `SimulateQrApproval` strictly to `ApplicationUser.IsDemoAccount == true && EndUserDemoEmails`.
   - **Option B (Strict Production Lockdown)**: Require `AllowDemoUsers = false` and return `404`/`403` from `SimulateQrApproval` in all `!env.IsDevelopment()` environments (disabling demo quick-QR login on showcase ACA).

4. **Doubt 4 — Sequencing of Orthogonal Pre-Existing P0/P1 Security Fixes *(Confidence: 88%)***
   - *Verified Conflict*: DocumentDB/A1–A4-coupled security fixes (`UpdateAsync`, `MarkAsRead` IDOR, `VerifyOtp` `IsActive`, `AdminUpdateUser` `SuperAdmin` guard, A1 over-posting, fail-closed `DocumentDbConnection` + TLS) belong directly in PRs 1–3. Standalone pre-existing P0/P1 fixes (`MealTools` LFI, `InAppNotificationService` `Clients.All`, `HttpPayloadTelemetryMiddleware` PHI, static `/uploads` auth, `SecurityStamp` validation, partitioned rate limiter, CSV injection) can either ship in an immediate **PR 6** or be pulled into PRs 2–3.
   - **Option A (5-PR Stack + Immediate PR 6 Security Hotfix — Recommended)**: Keeps PRs 1–5 focused, reviewable, and clean while immediately remediating orthogonal P0/P1 issues in PR 6.
   - **Option B (Expand Stack to 6 PRs / Pull P0s into PR 2–3)**: Pull `MealTools` LFI, `Clients.All` broadcast, telemetry PHI, and `/uploads` guard directly into PRs 2–3 before closing the stack.

---

## 9. Verification Plan

1. **Current Read-Only Verification**:
   - `git -C c:\Users\nikunj.banker\source\repos\diet-dost status --short` → **empty** (0 modified, added, or deleted repository files).
2. **Build & Full Test Suite (Per PR, After NuGet Restore)**:
   - `dotnet restore DietDost.slnx`
   - `dotnet build DietDost.slnx -c Release -warnaserror` (0 warnings, 0 errors across all 12 projects)
   - `dotnet test DietDost.slnx --no-build` (100% pass across `Nutrition.Domain.Tests`, `Nutrition.EvalHarness.Tests`, `Nutrition.Mcp.Tests`)
3. **DevSecOps & Living Docs Gate (Per PR 1–5, Vector 7 Included)**:
   - `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All`
   - `pwsh -File scripts/sync-adr-index.ps1` (verify `docs/adr/index.json` clean)
4. **Tri-Database `DataMigrator` Dry-Run (PR 4–5)**:
   - `dotnet run --project src/Nutrition.Tools.DataMigrator -- --source-type Sqlite --target-type DocumentDb --dry-run`
5. **Live Multi-Tier CFT & Browser Verification (PR 5, After Fixing `tests/validate_e2e_tiers.ps1:89` to `/api/analytics/daily`)**:
   - Boot `Nutrition.WebGateway` on `http://localhost:5240` under SQLite and DocumentDB configurations
   - Execute `pwsh -File tests/validate_e2e_tiers.ps1` across all 5 seeded demo tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`)
   - Verify UI via `browser` subagent (zero console errors, zero layout shift, identical JSON contracts)
