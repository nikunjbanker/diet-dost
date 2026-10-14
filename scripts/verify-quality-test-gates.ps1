<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
<#
.SYNOPSIS
    Diet-Dost Automated Quality & Multi-Tier Test Gate Validator.
.DESCRIPTION
    Validates all 4 testing tiers against enforced quality thresholds:
      1. Unit Tests (Threshold: 90%)
      2. Integration Tests (Threshold: 80%)
      3. System Tests (Threshold: 75%)
      4. End-to-End (E2E) Acceptance Tests (Threshold: 70%)
    Emits structured console logs, generates a markdown report, and returns exit code 0 (Pass) or 1 (Fail).
.PARAMETER Tier
    The test tier to run: "All", "Unit", "Integration", "System", "E2E". Default is "All".
.PARAMETER BaseUrl
    Base URL for WebGateway when running System and E2E tests. Default: "http://127.0.0.1:5240".
.PARAMETER SkipServerStart
    If true, assumes WebGateway is already running on BaseUrl.
.PARAMETER OutputReport
    Optional path to write markdown summary report.
.EXAMPLE
    pwsh -File scripts/verify-quality-test-gates.ps1 -Tier All
#>

[CmdletBinding()]
param(
    [ValidateSet("All", "Unit", "Integration", "System", "E2E")]
    [string]$Tier = "All",

    [string]$BaseUrl = "http://127.0.0.1:5240",

    [switch]$SkipServerStart,

    [string]$OutputReport = ""
)

$ErrorActionPreference = "Continue"

# Resolve Repository Root
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Push-Location $RepoRoot

# Cross-Platform Environment Defaults for CI & Headless Browsers
if (-not $env:BASE_URL) { $env:BASE_URL = $BaseUrl }
if (-not $env:TEMP) { $env:TEMP = [System.IO.Path]::GetTempPath().TrimEnd('\', '/') }
if (-not $env:CHROME_PATH -and [System.IO.File]::Exists('/usr/bin/google-chrome')) {
    $env:CHROME_PATH = '/usr/bin/google-chrome'
}

Write-Host "========================================================================" -ForegroundColor Cyan
Write-Host "   Diet-Dost Quality & Multi-Tier Test Gate Validator" -ForegroundColor Cyan
Write-Host "   Target Tier: $Tier | Root: $RepoRoot" -ForegroundColor Cyan
Write-Host "   Enforced Gates: Unit (90%), Integration (80%), System (75%), E2E (70%)" -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$gateResults = [System.Collections.Generic.List[PSCustomObject]]::new()
$webProcess = $null

# Helper: Start WebGateway in background if needed
function Start-WebGatewayIfNeeded {
    param([string]$Url)

    try {
        $testPing = Invoke-RestMethod -Uri "$Url/healthz" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($testPing) {
            Write-Host "  ℹ️ WebGateway already active at $Url" -ForegroundColor Green
            return $null
        }
    } catch { }

    # Reset local test SQLite database to give demo users fresh daily quotas
    Remove-Item -Path (Join-Path $RepoRoot "src/Nutrition.WebGateway/diettracker.db*") -Force -ErrorAction SilentlyContinue
    Remove-Item -Path (Join-Path $RepoRoot "diettracker.db*") -Force -ErrorAction SilentlyContinue

    Write-Host "  🚀 Launching WebGateway daemon on $Url for live verification..." -ForegroundColor Yellow
    $webProj = Join-Path $RepoRoot "src/Nutrition.WebGateway/Nutrition.WebGateway.csproj"
    $debugDll = Join-Path $RepoRoot "src/Nutrition.WebGateway/bin/Debug/net11.0/Nutrition.WebGateway.dll"
    $releaseDll = Join-Path $RepoRoot "src/Nutrition.WebGateway/bin/Release/net11.0/Nutrition.WebGateway.dll"

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:DOTNET_ENVIRONMENT = "Development"

    $stdoutFile = Join-Path ([System.IO.Path]::GetTempPath()) "webgateway_stdout_$([System.Guid]::NewGuid().ToString('N')).log"
    $stderrFile = Join-Path ([System.IO.Path]::GetTempPath()) "webgateway_stderr_$([System.Guid]::NewGuid().ToString('N')).log"

    $workingDir = Join-Path $RepoRoot "src/Nutrition.WebGateway"
    $argList = @()

    $baseArgs = @("--urls", $Url, "--environment", "Development", "--Auth:SuperAdminEmail", "superadmin@dietdost.app", "--Jwt:Key", "DietDost_SecretKey_For_Jwt_HMAC_SHA256_Authentication_2026_Minimum32BytesRequired!")
    if (Test-Path $debugDll) {
        $argList = @($debugDll) + $baseArgs
    } elseif (Test-Path $releaseDll) {
        $argList = @($releaseDll) + $baseArgs
    } else {
        $argList = @("run", "--project", $webProj, "-c", "Debug") + $baseArgs
    }

    $proc = Start-Process -FilePath "dotnet" -ArgumentList $argList -WorkingDirectory $workingDir -RedirectStandardOutput $stdoutFile -RedirectStandardError $stderrFile -PassThru

    # Wait up to 30 seconds for health probe
    $retries = 30
    $started = $false
    while ($retries -gt 0) {
        Start-Sleep -Seconds 1
        if ($proc.HasExited) {
            Write-Host "  ❌ Critical: WebGateway process exited prematurely with code $($proc.ExitCode)!" -ForegroundColor Red
            if (Test-Path $stdoutFile) { Write-Host "  STDOUT:`n$(Get-Content $stdoutFile -Raw)" -ForegroundColor DarkGray }
            if (Test-Path $stderrFile) { Write-Host "  STDERR:`n$(Get-Content $stderrFile -Raw)" -ForegroundColor DarkRed }
            break
        }
        try {
            $resp = Invoke-RestMethod -Uri "$Url/healthz" -Method Get -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($resp) {
                $started = $true
                break
            }
        } catch { }
        $retries--
    }

    if (-not $started) {
        Write-Host "  ⚠️ Warning: WebGateway did not respond to /healthz within 30s. Tests will attempt connection." -ForegroundColor DarkYellow
        if (Test-Path $stdoutFile) {
            $outContent = Get-Content $stdoutFile -Raw
            if ($outContent) { Write-Host "  WebGateway Output:`n$outContent" -ForegroundColor DarkGray }
        }
        if (Test-Path $stderrFile) {
            $errContent = Get-Content $stderrFile -Raw
            if ($errContent) { Write-Host "  WebGateway Errors:`n$errContent" -ForegroundColor DarkRed }
        }
    } else {
        Write-Host "  ✅ WebGateway daemon online and responsive at $Url" -ForegroundColor Green
    }

    return $proc
}

# Helper: Stop WebGateway
function Stop-WebGateway {
    param($proc)
    if ($proc -and -not $proc.HasExited) {
        Write-Host "  🛑 Terminating background WebGateway daemon (PID: $($proc.Id))..." -ForegroundColor Gray
        try {
            $proc.Kill($true)
            $proc.Dispose()
        } catch { }
    }
}

# ==============================================================================
# Tier 1: Unit Tests (Threshold: 90%)
# ==============================================================================
if ($Tier -in @("All", "Unit")) {
    Write-Host "`n[Tier 1/4] Executing Unit Tests (Threshold: >= 90%)..." -ForegroundColor Yellow
    $domainProj = Join-Path $RepoRoot "tests/Nutrition.Domain.Tests/Nutrition.Domain.Tests.csproj"
    $trxFile = Join-Path $RepoRoot "TestResults/unit_test_results.trx"
    
    $testOutput = dotnet test $domainProj --no-build --logger "trx;LogFileName=unit_test_results.trx" --collect:"XPlat Code Coverage" 2>&1 | Out-String

    $passedMatches = [regex]::Match($testOutput, "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)")
    $failed = 0
    $passed = 0
    $total = 0

    if ($passedMatches.Success) {
        $failed = [int]$passedMatches.Groups[1].Value
        $passed = [int]$passedMatches.Groups[2].Value
        $total = [int]$passedMatches.Groups[4].Value
    }

    $passRate = 0.0
    if ($total -gt 0) {
        $passRate = [math]::Round(($passed / $total) * 100, 2)
    }

    $unitGatePassed = ($failed -eq 0) -and ($passRate -ge 90.0)

    Write-Host "  • Total Unit Tests  : $total"
    Write-Host "  • Passed Tests      : $passed"
    Write-Host "  • Failed Tests      : $failed"
    Write-Host "  • Achieved Pass Rate: $passRate% (Enforced Gate: >= 90%)"
    if ($unitGatePassed) {
        Write-Host "  ✅ Tier 1 Unit Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 1 Unit Test Gate: FAIL (Below 90% threshold or failures detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 1: Unit Tests"
        Scope = "Domain Algorithms, Clinical Math, Nutrition Estimators"
        Required = ">= 90%"
        Achieved = "$passRate% ($passed/$total passed)"
        Passed = $unitGatePassed
    })
}

# ==============================================================================
# Tier 2: Integration Tests (Threshold: 80%)
# ==============================================================================
if ($Tier -in @("All", "Integration")) {
    Write-Host "`n[Tier 2/4] Executing Integration Tests (Threshold: >= 80%)..." -ForegroundColor Yellow
    $evalProj = Join-Path $RepoRoot "tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj"
    
    $testOutput = dotnet test $evalProj --no-build --logger "trx;LogFileName=integration_test_results.trx" --collect:"XPlat Code Coverage" 2>&1 | Out-String

    $passedMatches = [regex]::Match($testOutput, "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)")
    $failed = 0
    $passed = 0
    $total = 0

    if ($passedMatches.Success) {
        $failed = [int]$passedMatches.Groups[1].Value
        $passed = [int]$passedMatches.Groups[2].Value
        $total = [int]$passedMatches.Groups[4].Value
    }

    $passRate = 0.0
    if ($total -gt 0) {
        $passRate = [math]::Round(($passed / $total) * 100, 2)
    }

    $integGatePassed = ($failed -eq 0) -and ($passRate -ge 80.0)

    Write-Host "  • Total Integration Tests: $total"
    Write-Host "  • Passed Tests           : $passed"
    Write-Host "  • Failed Tests           : $failed"
    Write-Host "  • Achieved Pass Rate     : $passRate% (Enforced Gate: >= 80%)"
    if ($integGatePassed) {
        Write-Host "  ✅ Tier 2 Integration Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 2 Integration Test Gate: FAIL (Below 80% threshold or failures detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 2: Integration Tests"
        Scope = "Web BFF Composite, CQRS Handlers, Auth Schemes, SQLite EF Core"
        Required = ">= 80%"
        Achieved = "$passRate% ($passed/$total passed)"
        Passed = $integGatePassed
    })
}

# ==============================================================================
# Tier 3: System Tests (Threshold: 75%)
# ==============================================================================
if ($Tier -in @("All", "System")) {
    Write-Host "`n[Tier 3/4] Executing System Tests (Threshold: >= 75%)..." -ForegroundColor Yellow

    if (-not $SkipServerStart -and -not $webProcess) {
        $webProcess = Start-WebGatewayIfNeeded -Url $BaseUrl
    }

    $systemTests = @(
        @{ Name = "Mobile BFF Caching & Composite Contracts"; Script = "tests/verify_mobile_bff.mjs" },
        @{ Name = "Dynamic Registration Modes & System Config"; Script = "tests/test_dynamic_config_modes.mjs" },
        @{ Name = "Admin Tier RBAC & SuperAdmin Governance"; Script = "tests/verify_admin_tier_governance.mjs" },
        @{ Name = "Auth Gate Presentation & Quick-Fill Design"; Script = "tests/verify_auth_gate_design.mjs" },
        @{ Name = "Design System Surface Ladder & Tokens"; Script = "tests/audit_design_system_compliance.mjs" }
    )

    $sysTotal = $systemTests.Count
    $sysPassed = 0

    foreach ($st in $systemTests) {
        $scriptPath = Join-Path $RepoRoot $st.Script
        Write-Host "  • Running: $($st.Name) ($($st.Script))..." -ForegroundColor Gray
        $res = node $scriptPath 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✓ $($st.Name): Passed" -ForegroundColor Green
            $sysPassed++
        } else {
            Write-Host "    ✗ $($st.Name): Failed" -ForegroundColor Red
            Write-Host "    Output: $res" -ForegroundColor DarkRed
        }
    }

    $sysPassRate = 0.0
    if ($sysTotal -gt 0) {
        $sysPassRate = [math]::Round(($sysPassed / $sysTotal) * 100, 2)
    }

    $sysGatePassed = ($sysPassRate -ge 75.0)

    Write-Host "  • Total System Test Suites: $sysTotal"
    Write-Host "  • Passed Suites           : $sysPassed"
    Write-Host "  • Achieved Pass Rate      : $sysPassRate% (Enforced Gate: >= 75%)"
    if ($sysGatePassed) {
        Write-Host "  ✅ Tier 3 System Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 3 System Test Gate: FAIL (Below 75% threshold)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 3: System Tests"
        Scope = "Subsystem Wiring, Mobile BFF ETag Caching, System Invariants"
        Required = ">= 75%"
        Achieved = "$sysPassRate% ($sysPassed/$sysTotal suites passed)"
        Passed = $sysGatePassed
    })
}

# ==============================================================================
# Tier 4: End-to-End (E2E) Acceptance Tests (Threshold: 70%)
# ==============================================================================
if ($Tier -in @("All", "E2E")) {
    Write-Host "`n[Tier 4/4] Executing End-to-End Acceptance Tests (Threshold: >= 70%)..." -ForegroundColor Yellow

    if (-not $SkipServerStart -and -not $webProcess) {
        $webProcess = Start-WebGatewayIfNeeded -Url $BaseUrl
    }

    $e2eSuites = @(
        @{ Name = "Multi-Tier User Acceptance (5 Tiers)"; Type = "ps1"; Script = "tests/validate_e2e_tiers.ps1" },
        @{ Name = "E2E Core Meal Flow, Ledger & Quotas"; Type = "node"; Script = "tests/verify_cft_core_features.mjs" },
        @{ Name = "Multi-Platform Responsive Viewports"; Type = "node"; Script = "tests/verify_cft_viewports.mjs" },
        @{ Name = "Mobile Touch Flows & Photo AI Logging"; Type = "node"; Script = "tests/verify_mobile_cft_core_flows.mjs" },
        @{ Name = "User Onboarding, Health Consent & OTP"; Type = "node"; Script = "tests/verify_registration_and_cft.mjs" }
    )

    $e2eTotal = $e2eSuites.Count
    $e2ePassed = 0

    foreach ($es in $e2eSuites) {
        $scriptPath = Join-Path $RepoRoot $es.Script
        Write-Host "  • Executing: $($es.Name) ($($es.Script))..." -ForegroundColor Gray
        
        $res = $null
        if ($es.Type -eq "ps1") {
            $res = pwsh -File $scriptPath -BaseUrl $BaseUrl 2>&1 | Out-String
        } else {
            $res = node $scriptPath 2>&1 | Out-String
        }

        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✓ $($es.Name): Passed" -ForegroundColor Green
            $e2ePassed++
        } else {
            Write-Host "    ✗ $($es.Name): Failed" -ForegroundColor Red
            Write-Host "    Output: $res" -ForegroundColor DarkRed
        }
    }

    $e2ePassRate = 0.0
    if ($e2eTotal -gt 0) {
        $e2ePassRate = [math]::Round(($e2ePassed / $e2eTotal) * 100, 2)
    }

    $e2eGatePassed = ($e2ePassRate -ge 70.0)

    Write-Host "  • Total E2E Test Suites   : $e2eTotal"
    Write-Host "  • Passed E2E Suites       : $e2ePassed"
    Write-Host "  • Achieved E2E Pass Rate  : $e2ePassRate% (Enforced Gate: >= 70%)"
    if ($e2eGatePassed) {
        Write-Host "  ✅ Tier 4 End-to-End Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 4 End-to-End Test Gate: FAIL (Below 70% threshold or errors detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 4: End-to-End Tests"
        Scope = "5-Tier Customer Acceptance, AI Quotas, Paywalls & SuperAdmin"
        Required = ">= 70%"
        Achieved = "$e2ePassRate% ($e2ePassed/$e2eTotal suites passed)"
        Passed = $e2eGatePassed
    })
}

# Cleanup background process if started by script
if ($webProcess) {
    Stop-WebGateway -proc $webProcess
}

# ==============================================================================
# Summary & Gate Enforcement
# ==============================================================================
Write-Host "`n========================================================================" -ForegroundColor Cyan
Write-Host "         QUALITY & MULTI-TIER TEST GATE SUMMARY VERDICT                 " -ForegroundColor Cyan
Write-Host "========================================================================" -ForegroundColor Cyan

$allGatesPassed = $true
foreach ($r in $gateResults) {
    $statusText = if ($r.Passed) { "🟢 PASS" } else { "🔴 FAIL" }
    if (-not $r.Passed) { $allGatesPassed = $false }
    Write-Host ("{0,-26} | Required: {1,-7} | Achieved: {2,-28} | {3}" -f $r.Tier, $r.Required, $r.Achieved, $statusText)
}
Write-Host "========================================================================" -ForegroundColor Cyan

if ($allGatesPassed) {
    Write-Host "✅ ALL QUALITY & TEST GATES PASSED CLEANLY" -ForegroundColor Green
    Write-Host "   Solution is certified for deployment under all 4 quality tiers." -ForegroundColor Green
} else {
    Write-Host "❌ CRITICAL QUALITY GATE FAILURE" -ForegroundColor Red
    Write-Host "   One or more quality tiers fell below the required threshold." -ForegroundColor Red
}

# Optional markdown output
if ($OutputReport) {
    $md = [System.Text.StringBuilder]::new()
    [void]$md.AppendLine("### 🧪 Quality & Multi-Tier Test Gate Summary")
    [void]$md.AppendLine("")
    [void]$md.AppendLine("| Test Tier | Target Scope & Subsystems | Required Threshold | Achieved Metric | Verdict |")
    [void]$md.AppendLine("| :--- | :--- | :--- | :--- | :--- |")
    foreach ($r in $gateResults) {
        $statusText = if ($r.Passed) { "🟢 PASS" } else { "🔴 FAIL" }
        [void]$md.AppendLine("| **$($r.Tier)** | $($r.Scope) | `$($r.Required)` | $($r.Achieved) | $statusText |")
    }
    [void]$md.AppendLine("")
    $overallVerdict = if ($allGatesPassed) { "✅ **ALL QUALITY GATES PASSED: Solution Certified for Deployment**" } else { "❌ **QUALITY GATE FAILURE: Deployment Blocked**" }
    [void]$md.AppendLine("> **Status**: $overallVerdict")
    
    [System.IO.File]::WriteAllText($OutputReport, $md.ToString())
    Write-Host "`n📄 Written markdown quality report to: $OutputReport" -ForegroundColor Gray
}

Pop-Location

if ($allGatesPassed) {
    exit 0
} else {
    exit 1
}
