<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
# -----------------------------------------------------------------------------
# scripts/sync-architecture-diagrams.ps1
# Authoritative PowerShell wrapper for Architecture Diagram Synchronizer.
# Validates and synchronizes canonical Mermaid diagrams with the solution.
# -----------------------------------------------------------------------------
[CmdletBinding()]
param(
    [switch]$Sync,
    [switch]$Verify,
    [switch]$Lint
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PythonScript = Join-Path $ScriptDir "sync-architecture-diagrams.py"

$pythonCmd = $null
if (Get-Command python -ErrorAction SilentlyContinue) {
    $pythonCmd = "python"
} elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    $pythonCmd = "python3"
} else {
    Write-Error "Python 3 is required to run the architecture diagram synchronizer."
    exit 1
}

$argsList = @()
if ($Sync) {
    $argsList += "--sync"
} elseif ($Lint) {
    $argsList += "--lint"
} else {
    $argsList += "--verify"
}

& $pythonCmd $PythonScript @argsList
exit $LASTEXITCODE
