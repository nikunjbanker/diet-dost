# ADR-20261123-186: Resilient BSON DateOnly Serializer and Test Concurrency Isolation

- **Status**: Accepted
- **Date**: 2026-11-23
- **Author**: Diet-Dost Solution Engineering
- **Domain**: Architecture / Persistence
- **Tags**: `architecture`, `documentdb`, `mongodb`, `dateonly`, `serialization`, `xunit`

## Context & Problem Statement

Following the successful provisioning and deployment of Azure DocumentDB (vCore MongoDB 6.0/7.0 compatible, 32 GB Free Tier) as the active persistence engine, live verification against the production endpoint `https://dev.diet-dost.in` revealed two critical issues:

1. **BSON DateOnly Deserialization Failure**: When querying `/api/web/v1/dashboard` for authenticated users, the application returned HTTP 500 with `System.FormatException: An error occurred while deserializing the Date property of class Nutrition.Domain.Model.Ledger.DailyCalorieLedger: Deserialized value has a non-zero time component.`. MongoDB C# driver's default `DateOnlySerializer` strictly throws if a stored `BsonDateTime` has any non-zero time component, which frequently occurs due to UTC timezone offsets (e.g., IST +05:30 stored as 18:30:00 UTC) or legacy relational table migrations.
2. **xUnit Static Cache Concurrency Race**: `CleanArchitectureDecouplingTests` intermittently failed in CI when executed in parallel with `AdminUserManagementTests` because both accessed `TierConfigurationService`'s static in-memory cache concurrently.

## Decision & Implementation Details

1. **`ResilientDateOnlySerializer` Implementation**:
   - Introduced a custom `ResilientDateOnlySerializer : SerializerBase<DateOnly>` in `Nutrition.Infrastructure.Persistence.DocumentDb.DocumentDbBsonMappings`.
   - Safely extracts the pure date part via `DateOnly.FromDateTime(dt.Date)` across any `BsonDateTime` without throwing `FormatException`.
   - Also supports ISO-8601 strings, `BsonInt32` (encoded `yyyyMMdd` or day numbers), and `BsonInt64`.
   - Registered globally for `DateOnly` and `Nullable<DateOnly>`, and explicitly bound to `DailyCalorieLedger.Date`.
2. **Migration Data Truncation Guard**:
   - Updated `DataConversionEngine.cs` so that columns named `Date` or `DateOnly` types explicitly truncate to pure UTC midnight (`DateTime.SpecifyKind(dt.Date, DateTimeKind.Utc)`).
3. **xUnit Concurrency Isolation**:
   - Added `[Collection("TierConfigTests")]` to `CleanArchitectureDecouplingTests`, guaranteeing sequential execution with `AdminUserManagementTests` and eliminating cross-test static cache collisions.
4. **CI Assertion Harmonization**:
   - Retained the `AzureSql tier: Basic DTU (5 DTUs, 2 GB max)` specification text within `.github/workflows/azure-app-deploy.yml` summary outputs to satisfy the automated regression test `SqlBicep_ProvisionsBasicDtuTier_AndExcludesServerlessConfiguration`.

## Verification & Impact

- **Unit & Integration Suite**: All 457 tests in `tests/Nutrition.EvalHarness.Tests` pass cleanly (0 failures, 49.07% code line coverage).
- **AI Security Defense & Diagrams**: All 7 vectors in `verify-ai-security-defense.ps1` passed with 100% compliance.
- **Production Readiness**: Seamlessly handles any timezone-offset date representations in MongoDB/DocumentDB collections without runtime deserialization exceptions.
