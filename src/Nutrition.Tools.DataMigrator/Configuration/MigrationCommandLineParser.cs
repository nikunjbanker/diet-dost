/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

namespace Nutrition.Tools.DataMigrator.Configuration;

public record MigrationCommandLineArgs(
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
    int BatchSize,
    string? ReportPath,
    bool ShowHelp
);

public static class MigrationCommandLineParser
{
    public static MigrationCommandLineArgs Parse(string[] args)
    {
        var sourceType = DatabaseEngineType.Sqlite;
        var targetType = DatabaseEngineType.AzureSql;
        string? source = null;
        string? target = null;
        string? sqlitePath = null;
        string? azureSqlConn = null;
        string? documentDbConn = null;
        var mode = MigrationMode.Upsert;
        var isDryRun = false;
        var ensureSchema = false;
        var batchSize = 100;
        string? reportPath = null;
        var showHelp = false;

        for (var i = 0; i < args.Length; i++)
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
                var val = args[++i];
                if (val.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    isDryRun = true;
                }
                else if (Enum.TryParse<DatabaseEngineType>(val, true, out var parsedTargetType))
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
            else if ((arg.Equals("--sqlite", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--sqlite-path", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                sqlitePath = args[++i];
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = sqlitePath;
                }
            }
            else if ((arg.Equals("--azure-sql", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--azure-sql-conn", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                azureSqlConn = args[++i];
            }
            else if ((arg.Equals("--document-db", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--documentdb-conn", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--docdb-conn", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--docdb", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                documentDbConn = args[++i];
            }
            else if (arg.Equals("--mode", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var modeStr = args[++i];
                if (modeStr.Equals("InsertOnly", StringComparison.OrdinalIgnoreCase) ||
                    modeStr.Equals("SkipExisting", StringComparison.OrdinalIgnoreCase))
                {
                    mode = MigrationMode.SkipExisting;
                }
                else if (modeStr.Equals("TruncateTarget", StringComparison.OrdinalIgnoreCase) ||
                         modeStr.Equals("CleanInsert", StringComparison.OrdinalIgnoreCase))
                {
                    mode = MigrationMode.CleanInsert;
                }
                else if (Enum.TryParse<MigrationMode>(modeStr, true, out var parsedMode))
                {
                    mode = parsedMode;
                }
            }
            else if (arg.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                isDryRun = true;
            }
            else if (arg.StartsWith("--ensure-schema", StringComparison.OrdinalIgnoreCase))
            {
                if (arg.Contains(':', StringComparison.Ordinal))
                {
                    var parts = arg.Split(':');
                    ensureSchema = parts.Length > 1 && bool.TryParse(parts[1], out var es) ? es : true;
                }
                else if (i + 1 < args.Length && bool.TryParse(args[i + 1], out var esVal))
                {
                    ensureSchema = esVal;
                    i++;
                }
                else
                {
                    ensureSchema = true;
                }
            }
            else if (arg.Equals("--batch-size", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var bs))
                {
                    batchSize = bs;
                }
            }
            else if ((arg.Equals("--report", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("--report-path", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
            {
                reportPath = args[++i];
            }
            else if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
        }

        return new MigrationCommandLineArgs(
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
            batchSize,
            reportPath,
            showHelp
        );
    }

    public static (string? SourceEndpoint, string? TargetEndpoint) ResolveConnectionEndpoints(MigrationCommandLineArgs parsed)
    {
        // 1. Resolve source settings
        var sourceType = parsed.SourceType;
        var sourcePathOrConn = parsed.Source;

        if (string.IsNullOrWhiteSpace(sourcePathOrConn))
        {
            if (sourceType == DatabaseEngineType.Sqlite)
            {
                sourcePathOrConn = parsed.SqlitePath;
                if (string.IsNullOrWhiteSpace(sourcePathOrConn))
                {
                    var webGatewayDb = Path.Combine(Directory.GetCurrentDirectory(), "src", "Nutrition.WebGateway", "diettracker.db");
                    sourcePathOrConn = File.Exists(webGatewayDb)
                        ? webGatewayDb
                        : Path.Combine(Directory.GetCurrentDirectory(), "diettracker.db");
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

        // 2. Resolve target settings
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
            else if (targetType == DatabaseEngineType.Sqlite)
            {
                targetConn = parsed.SqlitePath
                             ?? Path.Combine(Directory.GetCurrentDirectory(), "src", "Nutrition.WebGateway", "diettracker.db");
            }
        }

        return (sourcePathOrConn, targetConn);
    }
}
