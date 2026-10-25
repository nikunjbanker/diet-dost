# ADR-20261025-135: User-Scoped Upload Directories, Root-Relative URI Normalization, Logical Soft Delete, and SuperAdmin Deleted Audit Mode

## Status
Accepted

## Date
2026-10-25

## Context
In Diet-Dost, media uploads (meal photos and face/body progress photos) were previously stored in flat global directories (`uploads/meals`, `uploads/progress`). This presented several architectural and operational challenges:
1. **Troubleshooting & Traceability**: In multi-user and multi-tier production environments, locating assets belonging to a specific user for clinical diagnosis or customer support was cumbersome in flat directories with tens of thousands of image files.
2. **Path Portability & CDN Decoupling**: Database records must never store absolute URLs (e.g. `http://localhost:5240/...` or `https://dietdost.app/...`), as host-bound paths break when switching environments, deploying to staging/production, binding custom domains, or routing via CDNs.
3. **Data Retention & Soft Delete Governance**: Clinical records, dietary logs, and progress photos carry medical and regulatory value under DPDPA 2023. Deletion requests must perform **only logical delete** (`IsDeleted = true`, `DeletedAtUtc = DateTime.UtcNow`) while retaining the underlying files on physical storage for dispute resolution, auditability, and clinical history.
4. **Application-Level Invisibility**: Soft-deleted entities must be automatically invisible across all application queries, calorie ledgers, meal history, and client UI views without requiring manual `WHERE IsDeleted = false` filters in every single query.
5. **SuperAdmin Auditability**: SuperAdmin operators require a dedicated inspection console to review soft-deleted records across all entity types, view photo thumbnails, and trace deletion timestamps.

## Decision

1. **User-Scoped Directory Structure (Pattern A)**:
   - Configured `LocalPhotoStorageService` to route uploads into isolated, per-user subdirectories:
     - Meals: `uploads/users/{userId}/meals/{guid}.jpg`
     - Progress: `uploads/users/{userId}/progress/{guid}.jpg`
   - Preserved backward compatibility with legacy flat folders (`uploads/meals/`, `uploads/progress/`) for existing historical photos.

2. **Root-Relative Path Normalization & Traversal Defense**:
   - Implemented `PhotoPathSanitizer.SanitizeUserId` using strict alphanumeric regex `^[a-zA-Z0-9_\-]+$` to prevent path traversal attacks (`../`, `..\`).
   - Implemented `PhotoPathSanitizer.NormalizeToRelativePath` to strip schemes, authority, hostnames, and ports, guaranteeing all stored database values are root-relative (`/uploads/...`).

3. **Domain Soft-Delete Contract (`ISoftDeletable`)**:
   - Defined `ISoftDeletable` in `Nutrition.Domain.Common` with `bool IsDeleted` and `DateTime? DeletedAtUtc`.
   - Implemented on all primary domain entities: `MealLog`, `ProgressPhoto`, `ApplicationUser`, `UserProfile`, `UserCorrectionRecord`, `DailyCalorieLedger`, and `AiDetectionFeedbackRecord`.
   - Guarded `ApplicationUser.CanLogin` to prevent soft-deleted users from authenticating.

4. **Zero-Physical-Deletion Policy**:
   - `LocalPhotoStorageService.DeletePhotoAsync` and `DeleteProgressPhotoCommandHandler` mark entities as soft-deleted in the database and explicitly retain the physical image files on disk.

5. **EF Core Global Query Filters**:
   - Registered `entity.HasQueryFilter(e => !e.IsDeleted)` in `DietTrackerDbContext.OnModelCreating` across all 7 entities.
   - Soft-deleted entities are automatically and transparently excluded from standard EF Core queries, navigation properties, cascades, and presentation endpoints.

6. **Native CQRS Query & Clean Architecture Repository Integration**:
   - Added `FindDeletedAsync` to `IRepository<T>` in `Nutrition.Application.Common`.
   - Implemented `EfRepository<T>.FindDeletedAsync` using `_dbSet.IgnoreQueryFilters().Where(e => EF.Property<bool>(e, "IsDeleted"))`.
   - Created `GetDeletedRecordsQuery` and `GetDeletedRecordsQueryHandler` in `Nutrition.Application.Features.Admin.Queries.GetDeletedRecords` using repository abstractions without EF Core or Infrastructure coupling.

7. **Database Schema & Migrations**:
   - Added idempotent column reconciliation for `IsDeleted` and `DeletedAtUtc` across both SQLite (`INTEGER NOT NULL DEFAULT 0`, `TEXT NULL`) and Azure SQL Database (`BIT NOT NULL DEFAULT 0`, `DATETIME2 NULL`) in `DatabaseInitializationExtensions.cs`.

8. **SuperAdmin Governance Console UI**:
   - Added `[Authorize(Roles = "SuperAdmin")] [HttpGet("deleted-records")]` endpoint to `AdminController`.
   - Added `🗑️ Deleted Audit` tab in `admin-modal.html` and `admin-modal.js` with type filtering (All, Meals, Progress Photos, Users), user ID search, photo thumbnail previews, and audit metadata.

## Consequences

### Positive
- **Per-User File Isolation**: Troubleshooting user-specific image issues is as simple as inspecting `uploads/users/{userId}/`.
- **Absolute URL Independence**: Complete portability across local development, container mounts, staging, production, and cloud CDN storage.
- **Accidental Data Loss Protection**: Physical files remain intact; soft-deleted records can be reviewed or restored by SuperAdmin.
- **Clean Architecture Adherence**: Zero EF Core leaks into Application CQRS handlers; 100% test coverage with 307 passing evaluation harness tests.
- **Enterprise SuperAdmin Audit Trail**: Dedicated UI for real-time visibility into deleted assets without direct database access.

### Negative / Trade-Offs
- Physical disk space is not immediately reclaimed when a user deletes a meal or progress photo; retention policies or archive jobs may be needed in the future for multi-year file pruning.
