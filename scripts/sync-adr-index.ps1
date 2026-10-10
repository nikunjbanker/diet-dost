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
