<#
.SYNOPSIS
    Tri-Database Data Migration Runner for Diet-Dost (.NET 11).
    Supports pairwise migration between SQLite, Azure SQL, and Azure DocumentDB (Free Tier).

.DESCRIPTION
    Executes the Nutrition.Tools.DataMigrator CLI to discover, convert, and migrate
    domain entities and records across persistence providers with full schema creation,
    BSON mapping, UTC date serialization, and comprehensive markdown audit reporting.

.PARAMETER Source
    Source persistence provider: 'Sqlite', 'AzureSql', or 'DocumentDb'. Default is 'Sqlite'.

.PARAMETER Target
    Target persistence provider: 'AzureSql', 'DocumentDb', or 'None' (for dry-run analysis). Default is 'DocumentDb'.

.PARAMETER SqlitePath
    Path to the source/target SQLite database file. Default is 'src/Nutrition.WebGateway/diettracker.db'.

.PARAMETER AzureSqlConn
    ADO.NET connection string for Azure SQL. If omitted, uses 'ConnectionStrings__AzureSqlConnection' env var.

.PARAMETER DocumentDbConn
    MongoDB connection string for Azure DocumentDB (Free Tier). If omitted, uses 'ConnectionStrings__DocumentDbConnection' env var.

.PARAMETER Mode
    Migration mode: 'Upsert', 'InsertOnly', or 'TruncateTarget'. Default is 'Upsert'.

.PARAMETER DryRun
    When specified, reads and converts data in memory without executing writes against the target database.

.PARAMETER EnsureSchema
    Ensures target tables, collections, and indexes exist before migrating rows. Default is $true.

.PARAMETER BatchSize
    Number of rows per bulk insertion batch. Default is 100.

.PARAMETER ReportPath
    Path to output the detailed Markdown audit report. Default is 'docs/reports/migration_report.md'.

.EXAMPLE
    pwsh -File scripts/migrate-data.ps1 -Source Sqlite -Target DocumentDb -DryRun
    pwsh -File scripts/migrate-data.ps1 -Source Sqlite -Target AzureSql
    pwsh -File scripts/migrate-data.ps1 -Source AzureSql -Target DocumentDb
#>
[CmdletBinding()]
param(
    [ValidateSet('Sqlite', 'AzureSql', 'DocumentDb')]
    [string]$Source = 'Sqlite',

    [ValidateSet('AzureSql', 'DocumentDb', 'None')]
    [string]$Target = 'DocumentDb',

    [string]$SqlitePath = 'src/Nutrition.WebGateway/diettracker.db',
    [string]$AzureSqlConn = $env:ConnectionStrings__AzureSqlConnection,
    [string]$DocumentDbConn = $env:ConnectionStrings__DocumentDbConnection,

    [ValidateSet('Upsert', 'InsertOnly', 'TruncateTarget')]
    [string]$Mode = 'Upsert',

    [switch]$DryRun,
    [bool]$EnsureSchema = $true,
    [int]$BatchSize = 100,
    [string]$ReportPath = 'docs/reports/migration_report.md'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "   Diet-Dost Tri-Database Migration Engine Runner (.NET 11)" -ForegroundColor Cyan
Write-Host "===================================================================" -ForegroundColor Cyan
Write-Host "Source Engine : $Source" -ForegroundColor Yellow
Write-Host "Target Engine : $Target" -ForegroundColor Yellow
Write-Host "Mode          : $Mode" -ForegroundColor Yellow
Write-Host "Dry-Run       : $DryRun" -ForegroundColor Yellow
Write-Host "Ensure Schema : $EnsureSchema" -ForegroundColor Yellow
Write-Host "Batch Size    : $BatchSize" -ForegroundColor Yellow
Write-Host "Report Path   : $ReportPath" -ForegroundColor Yellow
Write-Host "===================================================================" -ForegroundColor Cyan

$cliArgs = @(
    "--source-type", $Source,
    "--target-type", $Target,
    "--mode", $Mode,
    "--batch-size", $BatchSize.ToString(),
    "--report", $ReportPath
)

if (![string]::IsNullOrWhiteSpace($SqlitePath)) {
    $cliArgs += @("--sqlite-path", $SqlitePath)
}

if (![string]::IsNullOrWhiteSpace($AzureSqlConn)) {
    $cliArgs += @("--azure-sql-conn", $AzureSqlConn)
}

if (![string]::IsNullOrWhiteSpace($DocumentDbConn)) {
    $cliArgs += @("--docdb-conn", $DocumentDbConn)
}

if ($DryRun) {
    $cliArgs += "--dry-run"
}

if ($EnsureSchema) {
    $cliArgs += "--ensure-schema"
}

Write-Host "--> Running Nutrition.Tools.DataMigrator..." -ForegroundColor Green
dotnet run --project src/Nutrition.Tools.DataMigrator --no-build -- $cliArgs

$exitCode = $LASTEXITCODE
if ($exitCode -eq 0) {
    Write-Host "`n✅ Tri-Database migration finished successfully (Exit Code: 0)." -ForegroundColor Green
} else {
    Write-Host "`n❌ Tri-Database migration completed with errors (Exit Code: $exitCode)." -ForegroundColor Red
}

exit $exitCode
