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
        "VerificationOtps"
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

        var sqlitePath = parsed.SqlitePath;
        if (string.IsNullOrWhiteSpace(sqlitePath))
        {
            sqlitePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Nutrition.WebGateway", "diettracker.db");
            if (!File.Exists(sqlitePath))
            {
                sqlitePath = Path.Combine(Directory.GetCurrentDirectory(), "diettracker.db");
            }
        }

        if (!File.Exists(sqlitePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] SQLite source database file not found at: {sqlitePath}");
            Console.ResetColor();
            Console.WriteLine("Specify the SQLite file path using: --sqlite <path-to-db-file>");
            return 1;
        }

        var azureSqlConn = parsed.AzureSqlConn;
        if (string.IsNullOrWhiteSpace(azureSqlConn))
        {
            azureSqlConn = Environment.GetEnvironmentVariable("AZURE_SQL_CONNECTION_STRING")
                           ?? Environment.GetEnvironmentVariable("ConnectionStrings__AzureSqlConnection");
        }

        if (string.IsNullOrWhiteSpace(azureSqlConn) && !parsed.IsDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ERROR] Azure SQL connection string is required for migration.");
            Console.ResetColor();
            Console.WriteLine("Provide it via --azure-sql \"<connection_string>\", or set AZURE_SQL_CONNECTION_STRING environment variable, or use --dry-run.");
            return 1;
        }

        Console.WriteLine($"• SQLite Source:        {Path.GetFullPath(sqlitePath)}");
        Console.WriteLine($"• Strategy Mode:        {parsed.Mode}");
        Console.WriteLine($"• Dry-Run Mode:         {(parsed.IsDryRun ? "YES (Validations only - no writes)" : "NO (Live database write)")}");
        Console.WriteLine($"• Auto-Ensure Schema:   {parsed.EnsureSchema}");
        Console.WriteLine();

        var startTime = DateTime.UtcNow;
        var tableResults = new List<TableMigrationResult>();
        var conversionEngine = new DataConversionEngine();

        try
        {
            using var sqliteReader = new SqliteSourceReader(sqlitePath);
            await sqliteReader.OpenAsync();

            Console.WriteLine("--> Inspecting SQLite schema and discovering tables...");
            var tables = await sqliteReader.DiscoverTablesAsync();
            Console.WriteLine($"    Found {tables.Count} domain table(s) in SQLite database.");

            // Sort tables based on foreign key precedence: parent tables first
            var sortedTables = tables
                .OrderBy(t =>
                {
                    var idx = PreferredTableOrder.FindIndex(p => p.Equals(t.TableName, StringComparison.OrdinalIgnoreCase));
                    return idx >= 0 ? idx : 999;
                })
                .ThenBy(t => t.TableName)
                .ToList();

            AzureSqlTargetWriter? targetWriter = null;
            string targetDbName = "DryRun / None";

            if (!string.IsNullOrWhiteSpace(azureSqlConn))
            {
                var connBuilder = new SqlConnectionStringBuilder(azureSqlConn);
                targetDbName = $"{connBuilder.DataSource} / {connBuilder.InitialCatalog}";
                targetWriter = new AzureSqlTargetWriter(azureSqlConn);
                Console.WriteLine($"--> Connecting to target Azure SQL database ({targetDbName})...");
                await targetWriter.OpenAsync();
                Console.WriteLine("    Azure SQL connection established successfully.");

                if (parsed.EnsureSchema && !parsed.IsDryRun)
                {
                    Console.WriteLine("--> Verifying target schema and ensuring tables exist...");
                    foreach (var tbl in sortedTables)
                    {
                        await targetWriter.EnsureSchemaForTableAsync(tbl.TableName);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("--> Executing table migration pipeline...");

            foreach (var tbl in sortedTables)
            {
                Console.Write($"    Migrating [{tbl.TableName,-18}] ({tbl.RowCount,4} rows)... ");
                var rowsStream = sqliteReader.StreamRowsAsync(tbl.TableName);

                if (targetWriter != null)
                {
                    var result = await targetWriter.MigrateTableAsync(
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
                    // Dry run without target connection
                    var sw = Stopwatch.StartNew();
                    var stats = new TableConversionStats { TableName = tbl.TableName };
                    int rowCount = 0;
                    await foreach (var row in rowsStream)
                    {
                        rowCount++;
                        stats.TotalRowsRead++;
                        var rowKey = tbl.PrimaryKeyColumns.Count > 0 && row.TryGetValue(tbl.PrimaryKeyColumns[0], out var pk) ? pk?.ToString() ?? "row" : "row";
                        foreach (var (colName, colVal) in row)
                        {
                            var colMeta = tbl.Columns.FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase));
                            var declType = colMeta?.DeclaredType ?? "TEXT";
                            conversionEngine.ConvertValue(tbl.TableName, colName, declType, !colMeta?.IsNotNull ?? true, null, colVal, rowKey, stats);
                        }
                    }
                    sw.Stop();
                    tableResults.Add(new TableMigrationResult(tbl.TableName, rowCount, 0, rowCount, 0, 0, rowCount, stats, new List<string>(), sw.Elapsed));
                    Console.WriteLine($"[DRY-RUN] - Rows: {rowCount}, Converted: {stats.TotalConversions} ({sw.Elapsed.TotalSeconds:F2}s)");
                }
            }

            targetWriter?.Dispose();

            var summary = new MigrationRunSummary(
                SourceDbPath: sqlitePath,
                TargetDatabaseName: targetDbName,
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
        string? SqlitePath,
        string? AzureSqlConn,
        MigrationMode Mode,
        bool IsDryRun,
        bool EnsureSchema,
        string? ReportPath,
        bool ShowHelp
    );

    private static CliArgs ParseCommandLineArgs(string[] args)
    {
        string? sqlitePath = null;
        string? azureSqlConn = null;
        MigrationMode mode = MigrationMode.Upsert;
        bool isDryRun = false;
        bool ensureSchema = false;
        string? reportPath = null;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--sqlite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                sqlitePath = args[++i];
            }
            else if (arg.Equals("--azure-sql", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                azureSqlConn = args[++i];
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

        return new CliArgs(sqlitePath, azureSqlConn, mode, isDryRun, ensureSchema, reportPath, showHelp);
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Usage:
  dotnet run --project src/Nutrition.Tools.DataMigrator -- [options]
  pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 [parameters]

Options:
  --sqlite <path>        Path to source SQLite database file (.db)
                         (Default: src/Nutrition.WebGateway/diettracker.db)
  --azure-sql <conn>     Target Azure SQL Connection String
                         (Can also be set via AZURE_SQL_CONNECTION_STRING env var)
  --mode <strategy>      Migration conflict strategy:
                         - Upsert (Default): Updates existing records by PK, inserts new records
                         - CleanInsert: Clears target table before inserting records
                         - SkipExisting: Inserts new records only, skips if PK already exists
  --dry-run              Runs schema discovery and data conversions without committing to Azure SQL
  --ensure-schema        Auto-creates missing domain tables and columns in Azure SQL before migration
  --report-path <path>   Custom path to output the timestamped Markdown report
  -h, --help             Show this help screen and usage details
        ");
    }
}
