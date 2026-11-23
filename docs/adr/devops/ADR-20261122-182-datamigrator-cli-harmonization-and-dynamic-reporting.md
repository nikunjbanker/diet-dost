<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261122-182: Tri-Database DataMigrator CLI Harmonization and Dynamic Reporting

- **Status**: Accepted
- **Date**: 2026-11-22
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud Database Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #186
- **Related Issue**: #185

---

## 1. Context & Problem Statement

Diet-Dost supports three persistence engines: SQLite (local MVP / edge), Azure SQL (relational DTU), and Azure DocumentDB (MongoDB vCore Free Tier).
During live data migration from Azure SQL to Azure DocumentDB, friction was encountered:
1. `scripts/migrate-data.ps1` passed connection arguments as `--azure-sql-conn` and `--docdb-conn`, but `Nutrition.Tools.DataMigrator/Program.cs` only parsed `--azure-sql` and `--document-db`, causing null connection exceptions unless raw connection strings were explicitly passed.
2. The generated markdown reports and console summaries hardcoded "SQLite to Azure SQL Data Migration Report" headers and column labels, regardless of whether the migration was Azure SQL -> DocumentDB or SQLite -> DocumentDB.
3. Command-line parsing, endpoint resolution, and reporting logic were intertwined inside `Program.cs`, violating Single Responsibility and Open-Closed principles.

---

## 2. Decision & Implementation

### 2.1 Decoupled `MigrationCommandLineParser` (`Configuration/`)
Extracted CLI argument parsing into dedicated class `Nutrition.Tools.DataMigrator.Configuration.MigrationCommandLineParser`:
- Supports canonical flags and all environment/script aliases:
  - `--sqlite-path` / `--sqlite`
  - `--azure-sql-conn` / `--azure-sql`
  - `--docdb-conn` / `--document-db` / `--documentdb-conn` / `--docdb`
  - `--batch-size`
  - `--report` / `--report-path`
- Automatically resolves source and target endpoints based on `--source-type` and `--target-type` with intelligent fallback to environment variables (`SOURCE_AZURE_SQL_CONNECTION_STRING`, `TARGET_DOCUMENT_DB_CONNECTION_STRING`, `ConnectionStrings__*`).
- Supports both strategy naming conventions: `CleanInsert` / `TruncateTarget`, `SkipExisting` / `InsertOnly`, and `Upsert`.

### 2.2 Dynamic Source/Target Markdown & Console Reporting
Updated `MigrationRunSummary` and `MigrationReporter`:
- Added `SourceEngine` and `TargetEngine` metadata properties to `MigrationRunSummary`.
- Dynamically generates report titles, notes, and column headers:
  - `# {SourceEngine} to {TargetEngine} Data Migration Report`
  - `| Table Name | {SourceEngine} Source Rows | ... |`
  - `All source {SourceEngine} records were successfully converted...`
- Added pure `BuildMarkdownReport(MigrationRunSummary)` function enabling deterministic in-memory testing without filesystem writes.

### 2.3 Comprehensive Automated Test Coverage
Added unit tests in `Nutrition.EvalHarness.Tests/TriDatabaseMigrationTests.cs`:
- `CommandLineParser_RecognizesAllPairwiseConnectionAliases`: Asserts all flag aliases resolve correctly to typed settings.
- `MigrationReporter_GeneratesDynamicHeaderAndTableForAzureSqlToDocumentDb`: Asserts dynamic report generation for Azure SQL to DocumentDB scenarios.

---

## 3. Consequences

### Positive
- **Zero-Friction Script Execution**: `scripts/migrate-data.ps1` runs seamlessly without argument translation errors across any pairwise migration scenario.
- **Accurate Audit Trails**: Markdown reports accurately reflect the actual source and target persistence providers.
- **Maintainability & SOLID**: CLI parsing is isolated in its own domain module and fully covered by unit tests.

### Negative / Trade-offs
- None. Fully backward-compatible with existing CLI invocations.
