<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
# -----------------------------------------------------------------------------
# scripts/sync-adr-index.ps1
# Synchronizes ADR metadata, generates docs/adr/index.json, and renders docs/adr/README.md.
# -----------------------------------------------------------------------------
[CmdletBinding()]
param()

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$PythonScript = Join-Path $ScriptDir "sync-adr-index.py"

if (Get-Command python -ErrorAction SilentlyContinue) {
    python $PythonScript
} elseif (Get-Command python3 -ErrorAction SilentlyContinue) {
    python3 $PythonScript
} else {
    Write-Error "Python 3 is required to run the ADR synchronization engine."
    exit 1
}
