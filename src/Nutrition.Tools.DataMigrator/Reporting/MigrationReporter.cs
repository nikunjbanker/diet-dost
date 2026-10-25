/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
using System.Text;
using Nutrition.Tools.DataMigrator.Services;

namespace Nutrition.Tools.DataMigrator.Reporting;

public record MigrationRunSummary(
    string SourceDbPath,
    string TargetDatabaseName,
    MigrationMode Mode,
    bool IsDryRun,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    List<TableMigrationResult> TableResults
);

public class MigrationReporter
{
    public static void PrintConsoleReport(MigrationRunSummary summary)
    {
        Console.WriteLine();
        Console.WriteLine("===============================================================================================================");
        Console.WriteLine("  DIET-DOST DATA MIGRATION ENGINE : SQLITE -> AZURE SQL REPORT");
        Console.WriteLine("===============================================================================================================");
        Console.WriteLine($"  Source SQLite File:    {summary.SourceDbPath}");
        Console.WriteLine($"  Target Database:       {summary.TargetDatabaseName}");
        Console.WriteLine($"  Migration Strategy:    {summary.Mode} (DryRun: {summary.IsDryRun})");
        Console.WriteLine($"  Execution Time:        {summary.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC -> {summary.CompletedAtUtc:HH:mm:ss} UTC ({(summary.CompletedAtUtc - summary.StartedAtUtc).TotalSeconds:F2}s)");
        Console.WriteLine("---------------------------------------------------------------------------------------------------------------");
        Console.WriteLine(string.Format("{0,-18} | {1,7} | {2,7} | {3,8} | {4,7} | {5,7} | {6,11} | {7,-8} | {8,8}",
            "Table", "Source", "Before", "Inserted", "Updated", "Skipped", "Conversions", "Status", "Duration"));
        Console.WriteLine("---------------------------------------------------------------------------------------------------------------");

        int totalSource = 0;
        int totalBefore = 0;
        int totalInserted = 0;
        int totalUpdated = 0;
        int totalSkipped = 0;
        int totalConversions = 0;
        int totalErrors = 0;

        foreach (var r in summary.TableResults)
        {
            totalSource += r.SourceRows;
            totalBefore += r.TargetRowsBefore;
            totalInserted += r.InsertedRows;
            totalUpdated += r.UpdatedRows;
            totalSkipped += r.SkippedRows;
            totalConversions += r.ConversionStats.TotalConversions;
            totalErrors += r.Errors.Count;

            string status = r.Errors.Count > 0 ? "[FAIL]" : (r.ConversionStats.TotalConversions > 0 ? "[WARN]" : "[OK]");
            string convDetails = $"{r.ConversionStats.TotalConversions} (D:{r.ConversionStats.DateTimeConversions}/B:{r.ConversionStats.BooleanConversions}/J:{r.ConversionStats.JsonConversions})";

            Console.WriteLine(string.Format("{0,-18} | {1,7} | {2,7} | {3,8} | {4,7} | {5,7} | {6,11} | {7,-8} | {8,7:F2}s",
                r.TableName,
                r.SourceRows,
                r.TargetRowsBefore,
                r.InsertedRows,
                r.UpdatedRows,
                r.SkippedRows,
                r.ConversionStats.TotalConversions,
                status,
                r.Elapsed.TotalSeconds));
        }

        Console.WriteLine("---------------------------------------------------------------------------------------------------------------");
        Console.WriteLine(string.Format("{0,-18} | {1,7} | {2,7} | {3,8} | {4,7} | {5,7} | {6,11} | {7,-8} | {8,7:F2}s",
            "TOTAL",
            totalSource,
            totalBefore,
            totalInserted,
            totalUpdated,
            totalSkipped,
            totalConversions,
            totalErrors > 0 ? "[FAIL]" : "[SUCCESS]",
            (summary.CompletedAtUtc - summary.StartedAtUtc).TotalSeconds));
        Console.WriteLine("===============================================================================================================");

        // Conversion event samples
        var tablesWithConversions = summary.TableResults.Where(t => t.ConversionStats.TotalConversions > 0).ToList();
        if (tablesWithConversions.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("--> DATA CONVERSIONS RESOLVED PER TABLE:");
            foreach (var t in tablesWithConversions)
            {
                Console.WriteLine($"    [{t.TableName}]: {t.ConversionStats.TotalConversions} total resolved (Date: {t.ConversionStats.DateTimeConversions}, Bool: {t.ConversionStats.BooleanConversions}, Json: {t.ConversionStats.JsonConversions}, Numeric: {t.ConversionStats.NumericConversions}, String: {t.ConversionStats.StringTruncations})");
                foreach (var s in t.ConversionStats.SampleConversions.Take(3))
                {
                    Console.WriteLine($"      * Col '{s.ColumnName}' ({s.ConversionType}): '{Trunc(s.OriginalValue)}' -> '{Trunc(s.ResolvedValue)}' ({s.Details})");
                }
            }
        }

        // Error Report Section
        var tablesWithErrors = summary.TableResults.Where(t => t.Errors.Count > 0).ToList();
        if (tablesWithErrors.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================================================================");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  CRITICAL MIGRATION ERRORS DETECTED ({totalErrors} total error(s)):");
            Console.ResetColor();
            Console.WriteLine("===============================================================================================================");
            foreach (var t in tablesWithErrors)
            {
                Console.WriteLine($"  Table: [{t.TableName}] - {t.Errors.Count} Error(s):");
                foreach (var err in t.Errors.Take(15))
                {
                    Console.WriteLine($"    ❌ {err}");
                }
                if (t.Errors.Count > 15)
                {
                    Console.WriteLine($"    ... and {t.Errors.Count - 15} more error(s)");
                }
            }
            Console.WriteLine("===============================================================================================================");
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("  ✅ MIGRATION VERDICT: ALL TABLES PROCESSED CLEANLY WITH ZERO ERRORS!");
            Console.ResetColor();
        }
        Console.WriteLine();
    }

    public static async Task<string> GenerateMarkdownReportAsync(MigrationRunSummary summary, string? customReportPath = null)
    {
        var sb = new StringBuilder();
        var timestamp = summary.CompletedAtUtc.ToString("yyyyMMdd_HHmmss");

        sb.AppendLine($"# SQLite to Azure SQL Data Migration Report — {summary.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("> [!NOTE]");
        sb.AppendLine($"> **Source Database**: `{summary.SourceDbPath}`  ");
        sb.AppendLine($"> **Target Azure SQL Database**: `{summary.TargetDatabaseName}`  ");
        sb.AppendLine($"> **Strategy**: `{summary.Mode}` (DryRun: `{summary.IsDryRun}`)  ");
        sb.AppendLine($"> **Total Elapsed**: `{(summary.CompletedAtUtc - summary.StartedAtUtc).TotalSeconds:F2} seconds`");
        sb.AppendLine();

        int totalErrors = summary.TableResults.Sum(t => t.Errors.Count);
        if (totalErrors > 0)
        {
            sb.AppendLine($"> [!CAUTION]");
            sb.AppendLine($"> **{totalErrors} Error(s) Encountered** during migration. Review the Diagnostics & Error Log below for details.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("> [!TIP]");
            sb.AppendLine("> **100% Zero-Error Execution**: All tables and rows were successfully processed and converted.");
            sb.AppendLine();
        }

        sb.AppendLine("## 1. Table Migration & Row Count Metrics");
        sb.AppendLine();
        sb.AppendLine("| Table Name | SQLite Source Rows | Target Rows (Before) | Inserted | Updated | Skipped | Target Rows (After) | Conversions Resolved | Status | Duration |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var r in summary.TableResults)
        {
            string statusBadge = r.Errors.Count > 0 ? "🔴 FAILED" : (r.ConversionStats.TotalConversions > 0 ? "🟡 WARN (Converted)" : "🟢 SUCCESS");
            sb.AppendLine($"| `{r.TableName}` | {r.SourceRows:N0} | {r.TargetRowsBefore:N0} | {r.InsertedRows:N0} | {r.UpdatedRows:N0} | {r.SkippedRows:N0} | {r.TargetRowsAfter:N0} | {r.ConversionStats.TotalConversions:N0} | {statusBadge} | {r.Elapsed.TotalSeconds:F2}s |");
        }

        int totalSource = summary.TableResults.Sum(t => t.SourceRows);
        int totalBefore = summary.TableResults.Sum(t => t.TargetRowsBefore);
        int totalInserted = summary.TableResults.Sum(t => t.InsertedRows);
        int totalUpdated = summary.TableResults.Sum(t => t.UpdatedRows);
        int totalSkipped = summary.TableResults.Sum(t => t.SkippedRows);
        int totalAfter = summary.TableResults.Sum(t => t.TargetRowsAfter);
        int totalConversions = summary.TableResults.Sum(t => t.ConversionStats.TotalConversions);
        string finalVerdict = totalErrors > 0 ? "🔴 FAILED" : "🟢 100% SUCCESS";

        sb.AppendLine($"| **TOTAL** | **{totalSource:N0}** | **{totalBefore:N0}** | **{totalInserted:N0}** | **{totalUpdated:N0}** | **{totalSkipped:N0}** | **{totalAfter:N0}** | **{totalConversions:N0}** | **{finalVerdict}** | **{(summary.CompletedAtUtc - summary.StartedAtUtc).TotalSeconds:F2}s** |");
        sb.AppendLine();

        sb.AppendLine("## 2. Resolved Data Conversions Per Table");
        sb.AppendLine();
        sb.AppendLine("| Table Name | Total Conversions | Date / Time UTC | Boolean -> BIT | JSON Collections | Numeric / Float | String Truncations |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var r in summary.TableResults)
        {
            var c = r.ConversionStats;
            sb.AppendLine($"| `{r.TableName}` | {c.TotalConversions} | {c.DateTimeConversions} | {c.BooleanConversions} | {c.JsonConversions} | {c.NumericConversions} | {c.StringTruncations} |");
        }
        sb.AppendLine();

        // Sample conversions
        var tablesWithEvents = summary.TableResults.Where(t => t.ConversionStats.SampleConversions.Count > 0).ToList();
        if (tablesWithEvents.Count > 0)
        {
            sb.AppendLine("### Sample Data Conversion Events");
            sb.AppendLine();
            foreach (var t in tablesWithEvents)
            {
                sb.AppendLine($"#### Table: `{t.TableName}`");
                sb.AppendLine("| Column | Row Key | Type | Original SQLite Value | Resolved Azure SQL Value | Details |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- |");
                foreach (var s in t.ConversionStats.SampleConversions)
                {
                    sb.AppendLine($"| `{s.ColumnName}` | `{Trunc(s.RowKey, 20)}` | `{s.ConversionType}` | `{Trunc(s.OriginalValue, 35)}` | `{Trunc(s.ResolvedValue, 35)}` | {s.Details} |");
                }
                sb.AppendLine();
            }
        }

        // Error Diagnostics Section
        sb.AppendLine("## 3. Diagnostics & Error Log");
        sb.AppendLine();
        if (totalErrors == 0)
        {
            sb.AppendLine("✅ **Zero errors recorded.** All source SQLite records were successfully converted, mapped, and committed.");
        }
        else
        {
            foreach (var t in summary.TableResults.Where(t => t.Errors.Count > 0))
            {
                sb.AppendLine($"### Table: `{t.TableName}` ({t.Errors.Count} Errors)");
                sb.AppendLine();
                foreach (var err in t.Errors)
                {
                    sb.AppendLine($"- ❌ {err}");
                }
                sb.AppendLine();
            }
        }

        string reportPath = customReportPath ?? Path.Combine("docs", "migration-reports", $"migration_report_{timestamp}.md");
        var dir = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await File.WriteAllTextAsync(reportPath, sb.ToString());
        return reportPath;
    }

    private static string Trunc(string? str, int maxLen = 40)
    {
        if (string.IsNullOrEmpty(str)) return "";
        var sanitized = str.Replace("\r", " ").Replace("\n", " ").Trim();
        return sanitized.Length <= maxLen ? sanitized : sanitized[..maxLen] + "...";
    }
}
