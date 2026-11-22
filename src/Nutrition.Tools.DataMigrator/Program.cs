/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Nutrition.Tools.DataMigrator.Reporting;
using Nutrition.Tools.DataMigrator.Services;

namespace Nutrition.Tools.DataMigrator;

public enum DatabaseEngineType
{
    Sqlite,
    AzureSql,
    DocumentDb
}

public static class Program
{
    private static readonly List<string> PreferredTableOrder = new()
    {
        "Users",
        "Profiles",
        "Meals",
        "FoodItems",
        "Ledgers",
        "ProgressPhotos",
        "Corrections",
        "AiFeedbacks",
        "TierConfigurations",
        "AiUsageLogs",
        "AppSecrets",
        "VerificationOtps",
        "QrSessions",
        "AppNotifications"
    };

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine(@"
   ___  _     __     ___             __     ___       __           __  ____                __            
  / _ \(_)__ / /_   / _ \___  ___ __/ /_   / _ \___ _/ /____ _    /  |/  (_)__ ________ _/ /____  ____ 
 / // / / -_) __/  / // / _ \(_-</ __/    / // / _ `/ __/ _ `/   / /|_/ / / _ `/ __/ _ `/ __/ _ \/ __/ 
/____/_/\__/\__/  /____/\___/___/\__/    /____/\_,_/\__/\_,_/   /_/  /_/_/\_, /_/  \_,_/\__/\___/_/    
                                                                          /___/                          
        ");

        var parsed = ParseCommandLineArgs(args);
        if (parsed.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        // Resolve source settings
        var sourceType = parsed.SourceType;
        var sourcePathOrConn = parsed.Source;

        if (string.IsNullOrWhiteSpace(sourcePathOrConn))
        {
            if (sourceType == DatabaseEngineType.Sqlite)
            {
                sourcePathOrConn = parsed.SqlitePath;
                if (string.IsNullOrWhiteSpace(sourcePathOrConn))
                {
                    sourcePathOrConn = Path.Combine(Directory.GetCurrentDirectory(), "src", "Nutrition.WebGateway", "diettracker.db");
                    if (!File.Exists(sourcePathOrConn))
                    {
                        sourcePathOrConn = Path.Combine(Directory.GetCurrentDirectory(), "diettracker.db");
                    }
                }
            }
            else if (sourceType == DatabaseEngineType.AzureSql)
            {
                sourcePathOrConn = parsed.AzureSqlConn
                                   ?? Environment.GetEnvironmentVariable("SOURCE_AZURE_SQL_CONNECTION_STRING")
                                   ?? Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING");
            }
            else if (sourceType == DatabaseEngineType.DocumentDb)
            {
                sourcePathOrConn = parsed.DocumentDbConn
                                   ?? Environment.GetEnvironmentVariable("SOURCE_DOCUMENT_DB_CONNECTION_STRING")
                                   ?? Environment.GetEnvironmentVariable("DOCUMENT_DB_CONNECTION_STRING");
            }
        }

        // Resolve target settings
        var targetType = parsed.TargetType;
        var targetConn = parsed.Target;

        if (string.IsNullOrWhiteSpace(targetConn))
        {
            if (targetType == DatabaseEngineType.AzureSql)
            {
                targetConn = parsed.AzureSqlConn
                             ?? Environment.GetEnvironmentVariable("TARGET_AZURE_SQL_CONNECTION_STRING")
                             ?? Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING")
                             ?? Environment.GetEnvironmentVariable("ConnectionStrings__AzureSqlConnection");
            }
            else if (targetType == DatabaseEngineType.DocumentDb)
            {
                targetConn = parsed.DocumentDbConn
                             ?? Environment.GetEnvironmentVariable("TARGET_DOCUMENT_DB_CONNECTION_STRING")
                             ?? Environment.GetEnvironmentVariable("DOCUMENT_DB_CONNECTION_STRING")
                             ?? Environment.GetEnvironmentVariable("ConnectionStrings__DocumentDbConnection");
            }
        }

        if (sourceType == DatabaseEngineType.Sqlite && !File.Exists(sourcePathOrConn))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] SQLite source database file not found at: {sourcePathOrConn}");
            Console.ResetColor();
            Console.WriteLine("Specify the SQLite file path using: --source <path-to-db-file> or --sqlite <path>");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(targetConn) && !parsed.IsDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Target connection string is required for migration into {targetType}.");
            Console.ResetColor();
            Console.WriteLine($"Provide it via --target \"<connection_string>\", or use target-specific flags (--azure-sql, --document-db), or use --dry-run.");
            return 1;
        }

        Console.WriteLine($"• Source Provider:     {sourceType} ({sourcePathOrConn})");
        Console.WriteLine($"• Target Provider:     {targetType} ({(string.IsNullOrWhiteSpace(targetConn) ? "DryRun" : "Target Configured")})");
        Console.WriteLine($"• Strategy Mode:       {parsed.Mode}");
        Console.WriteLine($"• Dry-Run Mode:        {(parsed.IsDryRun ? "YES (Validations only - no writes)" : "NO (Live database write)")}");
        Console.WriteLine($"• Auto-Ensure Schema:  {parsed.EnsureSchema}");
        Console.WriteLine();

        var startTime = DateTime.UtcNow;
        var tableResults = new List<TableMigrationResult>();
        var conversionEngine = new DataConversionEngine();

        try
        {
            List<SqliteTableMetadata> tables;
            Func<string, IAsyncEnumerable<Dictionary<string, object?>>> rowsStreamer;
            IDisposable sourceDisposable;

            // 1. Initialize Source Reader
            if (sourceType == DatabaseEngineType.Sqlite)
            {
                var sqliteReader = new SqliteSourceReader(sourcePathOrConn!);
                await sqliteReader.OpenAsync();
                tables = await sqliteReader.DiscoverTablesAsync();
                rowsStreamer = sqliteReader.StreamRowsAsync;
                sourceDisposable = sqliteReader;
            }
            else if (sourceType == DatabaseEngineType.AzureSql)
            {
                var sqlReader = new AzureSqlSourceReader(sourcePathOrConn!);
                await sqlReader.OpenAsync();
                tables = await sqlReader.DiscoverTablesAsync();
                rowsStreamer = sqlReader.StreamRowsAsync;
                sourceDisposable = sqlReader;
            }
            else // DocumentDb
            {
                var docDbReader = new DocumentDbSourceReader(sourcePathOrConn!, engine: conversionEngine);
                await docDbReader.OpenAsync();
                tables = await docDbReader.DiscoverTablesAsync();
                rowsStreamer = docDbReader.StreamRowsAsync;
                sourceDisposable = docDbReader;
            }

            string targetDbName = "DryRun / None";

            using (sourceDisposable)
            {
                Console.WriteLine($"--> Discovering domain entities from {sourceType}...");
                Console.WriteLine($"    Found {tables.Count} domain table/collection(s).");

                var sortedTables = tables
                    .OrderBy(t =>
                    {
                        var idx = PreferredTableOrder.FindIndex(p => p.Equals(t.TableName, StringComparison.OrdinalIgnoreCase));
                        return idx >= 0 ? idx : 999;
                    })
                    .ThenBy(t => t.TableName)
                    .ToList();

                AzureSqlTargetWriter? azureSqlTarget = null;
                DocumentDbTargetWriter? docDbTarget = null;

                if (!string.IsNullOrWhiteSpace(targetConn))
                {
                    if (targetType == DatabaseEngineType.AzureSql)
                    {
                        var connBuilder = new SqlConnectionStringBuilder(targetConn);
                        targetDbName = $"{connBuilder.DataSource} / {connBuilder.InitialCatalog}";
                        azureSqlTarget = new AzureSqlTargetWriter(targetConn);
                        Console.WriteLine($"--> Connecting to target Azure SQL database ({targetDbName})...");
                        await azureSqlTarget.OpenAsync();
                        Console.WriteLine("    Azure SQL connection established successfully.");
                        await azureSqlTarget.WarmupAndVerifyAsync();

                        if (parsed.EnsureSchema && !parsed.IsDryRun)
                        {
                            Console.WriteLine("--> Ensuring schema and tables exist in Azure SQL...");
                            foreach (var tbl in sortedTables)
                            {
                                await azureSqlTarget.EnsureSchemaForTableAsync(tbl.TableName);
                            }
                        }
                    }
                    else if (targetType == DatabaseEngineType.DocumentDb)
                    {
                        targetDbName = "Azure DocumentDB (Free Tier)";
                        docDbTarget = new DocumentDbTargetWriter(targetConn);
                        Console.WriteLine($"--> Connecting to target Azure DocumentDB ({targetDbName})...");
                        await docDbTarget.OpenAsync();
                        Console.WriteLine("    Azure DocumentDB connection established successfully.");
                        await docDbTarget.WarmupAndVerifyAsync();

                        if (parsed.EnsureSchema && !parsed.IsDryRun)
                        {
                            Console.WriteLine("--> Ensuring collections and indexes exist in Azure DocumentDB...");
                            foreach (var tbl in sortedTables)
                            {
                                await docDbTarget.EnsureSchemaForTableAsync(tbl.TableName);
                            }
                        }
                    }
                }

                Console.WriteLine();
                Console.WriteLine("--> Executing table migration pipeline...");

                foreach (var tbl in sortedTables)
                {
                    Console.Write($"    Migrating [{tbl.TableName,-18}] ({tbl.RowCount,4} rows)... ");
                    var rowsStream = rowsStreamer(tbl.TableName);

                    if (azureSqlTarget != null)
                    {
                        var result = await azureSqlTarget.MigrateTableAsync(
                            tbl,
                            rowsStream,
                            conversionEngine,
                            parsed.Mode,
                            parsed.IsDryRun,
                            onRowProgress: (p) => { Console.Write($"\r    Migrating [{tbl.TableName,-18}] ({p}/{tbl.RowCount} rows)... "); });

                        tableResults.Add(result);
                        var status = result.Errors.Count > 0 ? "FAILED" : (result.ConversionStats.TotalConversions > 0 ? "CONVERTED" : "DONE");
                        Console.WriteLine($"[{status}] - Inserted: {result.InsertedRows}, Updated: {result.UpdatedRows}, Converted: {result.ConversionStats.TotalConversions} ({result.Elapsed.TotalSeconds:F2}s)");
                    }
                    else if (docDbTarget != null)
                    {
                        var result = await docDbTarget.MigrateTableAsync(
                            tbl,
                            rowsStream,
                            conversionEngine,
                            parsed.Mode,
                            parsed.IsDryRun,
                            onRowProgress: (p) => { Console.Write($"\r    Migrating [{tbl.TableName,-18}] ({p}/{tbl.RowCount} rows)... "); });

                        tableResults.Add(result);
                        var status = result.Errors.Count > 0 ? "FAILED" : (result.ConversionStats.TotalConversions > 0 ? "CONVERTED" : "DONE");
                        Console.WriteLine($"[{status}] - Inserted: {result.InsertedRows}, Updated: {result.UpdatedRows}, Converted: {result.ConversionStats.TotalConversions} ({result.Elapsed.TotalSeconds:F2}s)");
                    }
                    else
                    {
                        // Dry-run mode without target connection
                        var sw = Stopwatch.StartNew();
                        var stats = new TableConversionStats { TableName = tbl.TableName };
                        int rowCount = 0;
                        await foreach (var row in rowsStream)
                        {
                            rowCount++;
                            stats.TotalRowsRead++;
                            var rowKey = tbl.PrimaryKeyColumns.Count > 0 && row.TryGetValue(tbl.PrimaryKeyColumns[0], out var pk) ? pk?.ToString() ?? "row" : "row";

                            if (targetType == DatabaseEngineType.DocumentDb)
                            {
                                conversionEngine.ConvertToBsonDocument(tbl.TableName, row, rowKey, stats);
                            }
                            else
                            {
                                foreach (var (colName, colVal) in row)
                                {
                                    var colMeta = tbl.Columns.FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                                    var declType = colMeta?.DeclaredType ?? "TEXT";
                                    conversionEngine.ConvertValue(tbl.TableName, colName, declType, !colMeta?.IsNotNull ?? true, null, colVal, rowKey, stats);
                                }
                            }
                        }
                        sw.Stop();
                        tableResults.Add(new TableMigrationResult(tbl.TableName, rowCount, 0, rowCount, 0, 0, rowCount, stats, new List<string>(), sw.Elapsed));
                        Console.WriteLine($"[DRY-RUN] - Rows: {rowCount}, Converted: {stats.TotalConversions} ({sw.Elapsed.TotalSeconds:F2}s)");
                    }
                }

                azureSqlTarget?.Dispose();
                docDbTarget?.Dispose();
            }

            var summary = new MigrationRunSummary(
                SourceDbPath: $"{sourceType} ({sourcePathOrConn})",
                TargetDatabaseName: $"{targetType} ({targetDbName})",
                Mode: parsed.Mode,
                IsDryRun: parsed.IsDryRun,
                StartedAtUtc: startTime,
                CompletedAtUtc: DateTime.UtcNow,
                TableResults: tableResults
            );

            MigrationReporter.PrintConsoleReport(summary);
            var reportFilePath = await MigrationReporter.GenerateMarkdownReportAsync(summary, parsed.ReportPath);
            Console.WriteLine($"📄 Detailed Markdown Report generated: {Path.GetFullPath(reportFilePath)}");

            int totalErrors = tableResults.Sum(t => t.Errors.Count);
            return totalErrors == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine();
            Console.WriteLine($"[FATAL] Migration aborted due to an unhandled exception: {ex.Message}");
            Console.WriteLine(ex.ToString());
            Console.ResetColor();
            return 1;
        }
    }

    private record CliArgs(
        DatabaseEngineType SourceType,
        string? Source,
        DatabaseEngineType TargetType,
        string? Target,
        string? SqlitePath,
        string? AzureSqlConn,
        string? DocumentDbConn,
        MigrationMode Mode,
        bool IsDryRun,
        bool EnsureSchema,
        string? ReportPath,
        bool ShowHelp
    );

    private static CliArgs ParseCommandLineArgs(string[] args)
    {
        DatabaseEngineType sourceType = DatabaseEngineType.Sqlite;
        DatabaseEngineType targetType = DatabaseEngineType.AzureSql;
        string? source = null;
        string? target = null;
        string? sqlitePath = null;
        string? azureSqlConn = null;
        string? documentDbConn = null;
        MigrationMode mode = MigrationMode.Upsert;
        bool isDryRun = false;
        bool ensureSchema = false;
        string? reportPath = null;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--source-type", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (Enum.TryParse<DatabaseEngineType>(args[++i], true, out var parsedSourceType))
                {
                    sourceType = parsedSourceType;
                }
            }
            else if (arg.Equals("--target-type", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (Enum.TryParse<DatabaseEngineType>(args[++i], true, out var parsedTargetType))
                {
                    targetType = parsedTargetType;
                }
            }
            else if (arg.Equals("--source", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                source = args[++i];
            }
            else if (arg.Equals("--target", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                target = args[++i];
            }
            else if (arg.Equals("--sqlite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                sqlitePath = args[++i];
                source = sqlitePath;
                sourceType = DatabaseEngineType.Sqlite;
            }
            else if (arg.Equals("--azure-sql", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                azureSqlConn = args[++i];
                target = azureSqlConn;
                targetType = DatabaseEngineType.AzureSql;
            }
            else if (arg.Equals("--document-db", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                documentDbConn = args[++i];
                target = documentDbConn;
                targetType = DatabaseEngineType.DocumentDb;
            }
            else if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var modeStr = args[++i];
                if (Enum.TryParse<MigrationMode>(modeStr, true, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                isDryRun = true;
            }
            else if (arg.Equals("--ensure-schema", StringComparison.OrdinalIgnoreCase))
            {
                ensureSchema = true;
            }
            else if (arg.Equals("--report-path", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                reportPath = args[++i];
            }
            else if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
        }

        return new CliArgs(
            sourceType,
            source,
            targetType,
            target,
            sqlitePath,
            azureSqlConn,
            documentDbConn,
            mode,
            isDryRun,
            ensureSchema,
            reportPath,
            showHelp
        );
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Usage:
  dotnet run --project src/Nutrition.Tools.DataMigrator -- [options]
  pwsh -File scripts/migrate-data.ps1 [parameters]

Pairwise Migration Scenarios:
  1. SQLite to Azure SQL:
     dotnet run --project src/Nutrition.Tools.DataMigrator -- --source-type Sqlite --target-type AzureSql --source diettracker.db --target ""<conn>""
  
  2. Azure SQL to Azure DocumentDB:
     dotnet run --project src/Nutrition.Tools.DataMigrator -- --source-type AzureSql --target-type DocumentDb --source ""<sql-conn>"" --target ""<docdb-conn>""

  3. SQLite to Azure DocumentDB:
     dotnet run --project src/Nutrition.Tools.DataMigrator -- --source-type Sqlite --target-type DocumentDb --source diettracker.db --target ""<docdb-conn>""

Options:
  --source-type <type>   Source provider: Sqlite, AzureSql, DocumentDb (Default: Sqlite)
  --target-type <type>   Target provider: AzureSql, DocumentDb, Sqlite (Default: AzureSql)
  --source <conn|path>   Source connection string or SQLite database path
  --target <conn|path>   Target connection string or SQLite database path
  --sqlite <path>        Alias for SQLite path (Default: src/Nutrition.WebGateway/diettracker.db)
  --azure-sql <conn>     Alias for Azure SQL Connection String
  --document-db <conn>   Alias for Azure DocumentDB Connection String
  --mode <strategy>      Migration conflict strategy:
                         - Upsert (Default): Updates existing records by PK, inserts new records
                         - CleanInsert: Clears target table/collection before inserting records
                         - SkipExisting: Inserts new records only, skips if PK already exists
  --dry-run              Runs schema discovery and data conversions without committing writes
  --ensure-schema        Auto-creates missing domain tables/collections and indexes before migration
  --report-path <path>   Custom path to output the timestamped Markdown report
  -h, --help             Show this help screen and usage details
        ");
    }
}
