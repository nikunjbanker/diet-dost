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

$cStyleHeader = @"
/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

"@

$psStyleHeader = @"
<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>

"@

$mdStyleHeader = @"
<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->
"@

$mermaidStyleHeader = @"
%%
%% Copyright (c) 2026 diet-dost and/or its contributors.
%% Licensed under the "GNU Affero General Public License v3.0 only" and
%% the "Server Side Public License, v 1"; you may not use this file except
%% in compliance with, at your election, the "GNU Affero General Public
%% License v3.0 only" or the "Server Side Public License, v 1".
%%

"@

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

Write-Host "Discovered $($allFiles.Count) total eligible files (code + docs/skills/cfts/architecture)." -ForegroundColor Cyan

$updatedCount = 0
$skippedCount = 0

foreach ($file in $allFiles) {
    $rawContent = [System.IO.File]::ReadAllText($file.FullName)
    
    # Check if header is already present
    if ($rawContent -match 'Copyright \(c\) 2026 diet-dost') {
        $skippedCount++
        continue
    }

    $newContent = ""

    if ($file.Extension -eq '.ps1') {
        $newContent = $psStyleHeader + $rawContent
    }
    elseif ($file.Extension -eq '.mermaid') {
        $newContent = $mermaidStyleHeader + $rawContent
    }
    elseif ($file.Extension -eq '.md') {
        # Check if markdown starts with YAML frontmatter
        if ($rawContent -match '^(---[\r\n]+[\s\S]*?[\r\n]+---)([\r\n]+)([\s\S]*)$') {
            $frontmatter = $Matches[1]
            $body = $Matches[3]
            $newContent = $frontmatter + "`r`n`r`n" + $mdStyleHeader + "`r`n`r`n" + $body
        } else {
            $newContent = $mdStyleHeader + "`r`n`r`n" + $rawContent
        }
    }
    else {
        # .cs, .js, .ts, .css, .sql
        $newContent = $cStyleHeader + $rawContent
    }

    [System.IO.File]::WriteAllText($file.FullName, $newContent, [System.Text.Encoding]::UTF8)
    $updatedCount++
    Write-Host "Applied header to: $($file.FullName.Substring($RootPath.Length).TrimStart('\', '/'))" -ForegroundColor Green
}

Write-Host "`nLicense Header Application Summary:" -ForegroundColor Cyan
Write-Host "  Updated : $updatedCount" -ForegroundColor Green
Write-Host "  Skipped : $skippedCount (Already present)" -ForegroundColor Yellow
Write-Host "  Total   : $($allFiles.Count)" -ForegroundColor White
