<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>

[CmdletBinding()]
param (
    [string]$RootPath = (Get-Location).Path
)

$excludedRegex = '\\(bin|obj|\.git|\.vs|\.idea|node_modules|logs|archive)\\'

# 1. Source code files
$sourceFiles = Get-ChildItem -Path $RootPath -Recurse -File | Where-Object {
    $_.FullName -notmatch $excludedRegex -and
    $_.Extension -match '^\.(cs|js|ts|css|sql|ps1)$'
}

# 2. Agentic, Architecture, SDD, and CFT markdown documentation (excluding living logs & archives)
$docFiles = Get-ChildItem -Path $RootPath -Recurse -File | Where-Object {
    $_.FullName -notmatch $excludedRegex -and
    ($_.FullName -match '\\(\.agents|docs)\\' -or $_.Name -eq 'AGENTS.md') -and
    $_.Extension -match '^\.(md|mermaid)$'
}

$allFiles = ($sourceFiles + $docFiles) | Sort-Object -Property FullName -Unique

$missing = @()

foreach ($file in $allFiles) {
    $rawContent = [System.IO.File]::ReadAllText($file.FullName)
    if ($rawContent -notmatch 'Copyright \(c\) 2026 diet-dost') {
        $missing += $file.FullName.Substring($RootPath.Length).TrimStart('\', '/')
    }
}

if ($missing.Count -eq 0) {
    Write-Host "SUCCESS: All $($allFiles.Count) eligible source and documentation files contain the required license header." -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAILURE: The following $($missing.Count) file(s) are missing the required license header:" -ForegroundColor Red
    foreach ($m in $missing) {
        Write-Host "  - $m" -ForegroundColor Red
    }
    exit 1
}
