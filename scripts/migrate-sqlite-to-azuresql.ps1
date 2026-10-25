<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
<#
.SYNOPSIS
    Copies and migrates domain data from a local SQLite database to Azure SQL Database.
.DESCRIPTION
    Launches the .NET 11 Nutrition.Tools.DataMigrator CLI to introspect tables, resolve
    data type conversions (UTC DateTimes, Boolean BITs, JSON collection repairs, float invariants),
    execute batch merge/upserts, and generate per-table reports.
.PARAMETER SqliteDbPath
    Path to local SQLite .db file. Defaults to 'src/Nutrition.WebGateway/diettracker.db'.
.PARAMETER TargetConnectionString
    Azure SQL Connection String. If omitted, checks $env:AZURE_SQL_CONNECTION_STRING or Azure Key Vault.
.PARAMETER Mode
    Migration conflict strategy: 'Upsert' (default), 'CleanInsert', or 'SkipExisting'.
.PARAMETER DryRun
    If set, validates schemas and data conversions without committing writes to Azure SQL.
.PARAMETER EnsureSchema
    If set (default $true), auto-creates missing domain tables and columns in Azure SQL before copying data.
.PARAMETER ReportPath
    Custom path to output the timestamped Markdown report.
.EXAMPLE
    pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 -DryRun
.EXAMPLE
    pwsh -File scripts/migrate-sqlite-to-azuresql.ps1 -SqliteDbPath "C:\path\to\my_data.db" -Mode Upsert
#>

param(
    [string]$SqliteDbPath,
    [string]$TargetConnectionString,
    [ValidateSet("Upsert", "CleanInsert", "SkipExisting")]
    [string]$Mode = "Upsert",
    [switch]$DryRun,
    [switch]$EnsureSchema = $true,
    [string]$ReportPath
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot

# Resolve SQLite path
if ([string]::IsNullOrWhiteSpace($SqliteDbPath)) {
    $defaultDb = Join-Path $repoRoot "src" "Nutrition.WebGateway" "diettracker.db"
    if (Test-Path $defaultDb) {
        $SqliteDbPath = $defaultDb
    } else {
        $fallbackDb = Join-Path $repoRoot "diettracker.db"
        if (Test-Path $fallbackDb) {
            $SqliteDbPath = $fallbackDb
        } else {
            Write-Host "⚠️ Warning: Default SQLite file not found at '$defaultDb'." -ForegroundColor Yellow
            $SqliteDbPath = Read-Host "Please enter the path to your SQLite .db file"
        }
    }
}

if (-not (Test-Path $SqliteDbPath)) {
    Write-Error "SQLite database file not found at: $SqliteDbPath"
    exit 1
}

# Resolve Azure SQL Connection String
if ([string]::IsNullOrWhiteSpace($TargetConnectionString) -and -not $DryRun) {
    if (-not [string]::IsNullOrWhiteSpace($env:AZURE_SQL_CONNECTION_STRING)) {
        $TargetConnectionString = $env:AZURE_SQL_CONNECTION_STRING
    } elseif (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__AzureSqlConnection)) {
        $TargetConnectionString = $env:ConnectionStrings__AzureSqlConnection
    } else {
        # Check Azure Key Vault if az cli is authenticated
        Write-Host "🔍 Checking Azure Key Vault for 'azure-sql-connection-string'..." -ForegroundColor Cyan
        try {
            $vaultName = az keyvault list -g rg-dietdost-dev --query "[0].name" -o tsv 2>$null
            if (-not [string]::IsNullOrWhiteSpace($vaultName)) {
                $secretVal = az keyvault secret show --vault-name $vaultName --name "azure-sql-connection-string" --query value -o tsv 2>$null
                if (-not [string]::IsNullOrWhiteSpace($secretVal)) {
                    $TargetConnectionString = $secretVal
                    Write-Host "✅ Retrieved Azure SQL connection string from Key Vault '$vaultName'." -ForegroundColor Green
                }
            }
        } catch {
            # Azure CLI not logged in or key vault unavailable
        }

        if ([string]::IsNullOrWhiteSpace($TargetConnectionString)) {
            Write-Host "⚠️ No Azure SQL connection string provided." -ForegroundColor Yellow
            Write-Host "Options:"
            Write-Host "  1. Enter the connection string now"
            Write-Host "  2. Run in Dry-Run mode to validate data and generate the conversion report without connecting to Azure SQL"
            $choice = Read-Host "Select option (1/2) [Default: 2]"
            if ($choice -eq "1") {
                $TargetConnectionString = Read-Host "Enter Azure SQL Connection String"
            } else {
                $DryRun = $true
                Write-Host "Running in Dry-Run mode." -ForegroundColor Cyan
            }
        }
    }
}

# Build arguments for Nutrition.Tools.DataMigrator
$cliArgs = @(
    "run",
    "--project", (Join-Path $repoRoot "src" "Nutrition.Tools.DataMigrator" "Nutrition.Tools.DataMigrator.csproj"),
    "--",
    "--sqlite", $SqliteDbPath,
    "--mode", $Mode
)

if (-not [string]::IsNullOrWhiteSpace($TargetConnectionString)) {
    $cliArgs += @("--azure-sql", $TargetConnectionString)
}

if ($DryRun) {
    $cliArgs += "--dry-run"
}

if ($EnsureSchema) {
    $cliArgs += "--ensure-schema"
}

if (-not [string]::IsNullOrWhiteSpace($ReportPath)) {
    $cliArgs += @("--report-path", $ReportPath)
}

Write-Host "🚀 Launching Diet-Dost SQLite -> Azure SQL Migrator..." -ForegroundColor Cyan
dotnet @cliArgs
$exitCode = $LASTEXITCODE

if ($exitCode -eq 0) {
    Write-Host "✅ Data migration pipeline completed successfully." -ForegroundColor Green
} else {
    Write-Host "❌ Data migration pipeline completed with errors (Exit Code $exitCode)." -ForegroundColor Red
}

exit $exitCode
