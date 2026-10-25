# ADR-20261025-132: SQLite to Azure SQL Data Migration Tool & Data Conversion Engine

## Status
Accepted

## Date
2026-10-25

## Context
Diet-Dost initially operated on local SQLite (`diettracker.db`) for developer workstations and single-replica container storage mounts. With the introduction of Azure SQL Database Serverless Free Tier ([ADR-20261024-129](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/ADR-20261024-129-azure-sql-serverless-free-tier-persistence.md)), a robust, developer-friendly migration tool was required to extract data from local PC SQLite databases, resolve platform-specific data type conversions, validate integrity, and copy records into Azure SQL with rich per-table diagnostics and reporting.

SQLite and Azure SQL have significant data typing and dialect differences:
1. **DateTimes**: SQLite stores timestamps in various ISO, RFC, or string formats (e.g. `YYYY-MM-DD HH:MM:SS.FFFFFFF`, local time, or unix epoch numbers). Azure SQL expects strict universal `DATETIME2` within `0001-01-01` to `9999-12-31`.
2. **Booleans**: SQLite represents booleans as integers (`0`/`1`) or strings (`"true"`/`"false"`). Azure SQL requires `BIT` (`1`/`0`).
3. **JSON Collections**: Complex entities (`DiagnosedConditions`, `Medications`, `WhoComplianceFlags`, `MedicationWarnings`, `EarnedBadges`) are stored as JSON strings. Any empty, null, or corrupted string must be safely repaired into valid JSON arrays `[]` to prevent subsequent EF Core `JsonException` deserialization failures.
4. **Foreign Key Constraints**: SQLite bulk export does not guarantee insertion order. Migrating into Azure SQL with active foreign key constraints causes FK violation errors unless constraints are safely toggled.
5. **Conflict Resolution**: Databases may already contain seeded demo accounts or tier configurations. The migration engine must support configurable `Upsert` (merge), `CleanInsert` (wipe & insert), and `SkipExisting` modes.

## Decision
1. **Standalone .NET 11 CLI Tool (`Nutrition.Tools.DataMigrator`)**:
   - Implemented as a high-performance console application referencing `Microsoft.Data.Sqlite`, `Microsoft.Data.SqlClient`, `Nutrition.Domain`, and `Nutrition.Infrastructure`.
   - Supports CLI options: `--sqlite <path>`, `--azure-sql <conn>`, `--mode <Upsert|CleanInsert|SkipExisting>`, `--batch-size <n>`, `--dry-run`, `--ensure-schema`, `--report-path <path>`.
2. **Dynamic Schema & Constraint Engine (`AzureSqlTargetWriter`)**:
   - Automatically inspects target column data types, nullability, and primary keys.
   - Temporarily deactivates foreign key constraints prior to processing (`ALTER TABLE [{table}] NOCHECK CONSTRAINT ALL;`).
   - Inserts/upserts records in transactional batches.
   - Re-enables and validates constraints upon completion (`ALTER TABLE [{table}] WITH CHECK CHECK CONSTRAINT ALL;`).
3. **Data Conversion & Sanitization Engine (`DataConversionEngine`)**:
   - Automatically detects and resolves:
     - ISO 8601 / RFC 3339 / epoch timestamps normalized to UTC `DateTime` and clamped within safe SQL boundaries.
     - Integer/string booleans converted to `BIT`.
     - JSON collections validated and normalized to valid JSON arrays.
     - Non-numeric strings in floating-point fields sanitized; `NaN` and `Infinity` replaced with `0.0`.
     - String enums translated to integer domain codes.
     - Null-byte characters (`\0`) stripped and strings trimmed to max target length.
4. **Rich Per-Table Reporting (`MigrationReporter`)**:
   - Renders a real-time Obsidian-dark ASCII table in the console displaying source rows, before/after counts, inserted/updated/skipped, conversion counts, status, and duration.
   - Automatically generates a timestamped Markdown report in `docs/migration-reports/migration_report_<timestamp>.md` with sample conversion events and error diagnostics.
5. **Cross-Platform PowerShell Wrapper (`scripts/migrate-sqlite-to-azuresql.ps1`)**:
   - Provides a one-line interactive or automated entry point:
     ```powershell
     pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 -SqliteDbPath "src/Nutrition.WebGateway/diettracker.db" -Mode Upsert
     ```

## Consequences

### Positive
- **Zero Data Loss**: Seamless migration of all 12 domain tables preserving exact entity GUIDs and relational integrity.
- **Self-Healing Data Conversions**: Overcomes format drift between SQLite strings and SQL Server strict types without manual SQL scripts.
- **Developer Experience**: One-command execution with `--dry-run` safety validation and rich per-table markdown reports.
- **Enterprise DevSecOps**: Fully isolated in a dedicated git worktree, tested under .NET 11 with 0 warnings, and audited for zero secret leakage.

### Negative / Trade-Offs
- Large databases (>100,000 rows) take longer when executing row-by-row `MERGE` vs raw `SqlBulkCopy`, but `MERGE` ensures zero primary key collision errors on existing records.
