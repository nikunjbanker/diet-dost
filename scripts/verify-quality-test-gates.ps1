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
      1. Tier 1: Domain & Clinical Nutrition Unit Tests (Line Coverage Threshold: >= 44.0%, 0 failures)
      2. Tier 2: Subsystem & BFF Integration Tests (Line Coverage Threshold: >= 40.0%, 0 failures)
      3. Tier 3: Subsystem Contracts & Design System Tests (Suite Pass Rate Threshold: >= 75.0%)
      4. Tier 4: Multi-Tier User Acceptance & E2E CFT Suite (Suite Pass Rate Threshold: >= 70.0%)
    Emits structured console logs, generates a markdown report, and returns exit code 0 (Pass) or 1 (Fail).
.PARAMETER Tier
    The test tier to run: "All", "Unit", "Integration", "System", "E2E". Default is "All".
.PARAMETER BaseUrl
    Base URL for WebGateway when running System and E2E tests. Default: "http://127.0.0.1:5240".
.PARAMETER SkipServerStart
    If true, assumes WebGateway is already running on BaseUrl.
.PARAMETER OutputReport
    Optional path to write markdown summary report.
.PARAMETER UnitThreshold
    Line coverage percentage threshold for Tier 1 Unit Tests. Default: 44.0%.
.PARAMETER IntegrationThreshold
    Line coverage percentage threshold for Tier 2 Integration Tests. Default: 40.0%.
.PARAMETER SystemThreshold
    Suite pass rate percentage threshold for Tier 3 System Tests. Default: 75.0%.
.PARAMETER E2EThreshold
    Suite pass rate percentage threshold for Tier 4 E2E Acceptance Tests. Default: 70.0%.
.EXAMPLE
    pwsh -File scripts/verify-quality-test-gates.ps1 -Tier All
#>

[CmdletBinding()]
param(
    [ValidateSet("All", "Unit", "Integration", "System", "E2E")]
    [string]$Tier = "All",

    [string]$BaseUrl = "http://127.0.0.1:5240",

    [switch]$SkipServerStart,

    [string]$OutputReport = "",

    [double]$UnitThreshold = 44.0,

    [double]$IntegrationThreshold = 40.0,

    [double]$SystemThreshold = 75.0,

    [double]$E2EThreshold = 70.0,

    [string]$Configuration = ""
)

$ErrorActionPreference = "Continue"

# Support CI environment variable overrides
if ($env:UNIT_TEST_THRESHOLD) { $UnitThreshold = [double]$env:UNIT_TEST_THRESHOLD }
if ($env:INTEGRATION_TEST_THRESHOLD) { $IntegrationThreshold = [double]$env:INTEGRATION_TEST_THRESHOLD }
if ($env:SYSTEM_TEST_THRESHOLD) { $SystemThreshold = [double]$env:SYSTEM_TEST_THRESHOLD }
if ($env:E2E_TEST_THRESHOLD) { $E2EThreshold = [double]$env:E2E_TEST_THRESHOLD }

# Resolve Build Configuration (Release for CI, Debug for local development by default)
if (-not $Configuration) {
    if ($env:DOTNET_CONFIGURATION) { $Configuration = $env:DOTNET_CONFIGURATION }
    elseif ($env:CI) { $Configuration = "Release" }
    else { $Configuration = "Debug" }
}

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
Write-Host "   Enforced Gates: Tier 1 Unit (>= $UnitThreshold%), Tier 2 Integration (>= $IntegrationThreshold%), Tier 3 System (>= $SystemThreshold%), Tier 4 E2E (>= $E2EThreshold%)" -ForegroundColor Cyan
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
# Tier 1: Domain & Clinical Nutrition Unit Tests (Code Line Coverage Threshold: >= $UnitThreshold%, 0 failures)
# ==============================================================================
if ($Tier -in @("All", "Unit")) {
    Write-Host "`n[Tier 1/4] Executing Domain & Clinical Nutrition Unit Tests (Enforced Line Coverage: >= $UnitThreshold%, 0 failures)..." -ForegroundColor Yellow
    $domainProj = Join-Path $RepoRoot "tests/Nutrition.Domain.Tests/Nutrition.Domain.Tests.csproj"
    $unitResultsDir = Join-Path $RepoRoot "TestResults/Unit"
    if (Test-Path $unitResultsDir) { Remove-Item $unitResultsDir -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $unitResultsDir -Force | Out-Null
    
    $testOutput = dotnet test $domainProj -c $Configuration --logger "trx;LogFileName=unit_test_results.trx" --results-directory $unitResultsDir --collect:"XPlat Code Coverage" 2>&1 | Out-String

    $failed = 0
    $passed = 0
    $total = 0

    $trxFiles = Get-ChildItem -Path $unitResultsDir -Filter "*.trx" -Recurse | Sort-Object LastWriteTime -Descending
    if ($trxFiles -and $trxFiles.Count -gt 0) {
        try {
            [xml]$trxXml = Get-Content $trxFiles[0].FullName
            $counters = $trxXml.TestRun.ResultSummary.Counters
            if ($counters) {
                $total = [int]$counters.total
                $passed = [int]$counters.passed
                $failed = [int]$counters.failed
            }
        } catch { }
    }

    if ($total -eq 0) {
        $passedMatches = [regex]::Match($testOutput, "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)")
        if ($passedMatches.Success) {
            $failed = [int]$passedMatches.Groups[1].Value
            $passed = [int]$passedMatches.Groups[2].Value
            $total = [int]$passedMatches.Groups[4].Value
        } else {
            $altMatches = [regex]::Match($testOutput, "Total tests:\s+(\d+)[\s\S]*?Passed:\s+(\d+)")
            if ($altMatches.Success) {
                $total = [int]$altMatches.Groups[1].Value
                $passed = [int]$altMatches.Groups[2].Value
                $failMatch = [regex]::Match($testOutput, "Failed:\s+(\d+)")
                if ($failMatch.Success) {
                    $failed = [int]$failMatch.Groups[1].Value
                }
            }
        }
    }

    # Extract Code Coverage from Cobertura XML
    $covFiles = Get-ChildItem -Path $unitResultsDir -Filter "coverage.cobertura.xml" -Recurse | Sort-Object LastWriteTime -Descending
    $lineCoverage = 0.0
    $branchCoverage = 0.0
    $linesCovered = 0
    $linesValid = 0
    if ($covFiles -and $covFiles.Count -gt 0) {
        [xml]$covXml = Get-Content $covFiles[0].FullName
        $lineCoverage = [Math]::Round([double]$covXml.coverage.'line-rate' * 100, 2)
        $linesCovered = [int]$covXml.coverage.'lines-covered'
        $linesValid = [int]$covXml.coverage.'lines-valid'
        $branchCoverage = [Math]::Round([double]$covXml.coverage.'branch-rate' * 100, 2)
    }

    $unitGatePassed = ($failed -eq 0) -and ($total -gt 0) -and ($lineCoverage -ge $UnitThreshold)

    Write-Host "  • Total Unit Tests    : $total"
    Write-Host "  • Passed Tests        : $passed"
    Write-Host "  • Failed Tests        : $failed"
    Write-Host "  • Code Line Coverage  : $lineCoverage% ($linesCovered / $linesValid lines) [Enforced Gate: >= $UnitThreshold%]"
    Write-Host "  • Branch Coverage     : $branchCoverage%"
    if ($unitGatePassed) {
        Write-Host "  ✅ Tier 1 Domain & Clinical Nutrition Unit Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 1 Domain & Clinical Nutrition Unit Test Gate: FAIL (Line coverage $lineCoverage% is below $UnitThreshold% threshold or test failures detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 1: Domain & Clinical Nutrition Unit Tests"
        Scope = "Domain Algorithms, Clinical Math, Nutrition Estimators"
        Required = "Line Coverage >= $UnitThreshold% (0 failures)"
        Achieved = "$lineCoverage% coverage ($passed/$total passed, $failed failures)"
        Passed = $unitGatePassed
    })
}

# ==============================================================================
# Tier 2: Subsystem & BFF Integration Tests (Code Line Coverage Threshold: >= $IntegrationThreshold%, 0 failures)
# ==============================================================================
if ($Tier -in @("All", "Integration")) {
    Write-Host "`n[Tier 2/4] Executing Subsystem & BFF Integration Tests (Enforced Line Coverage: >= $IntegrationThreshold%, 0 failures)..." -ForegroundColor Yellow
    $evalProj = Join-Path $RepoRoot "tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj"
    $integResultsDir = Join-Path $RepoRoot "TestResults/Integration"
    if (Test-Path $integResultsDir) { Remove-Item $integResultsDir -Recurse -Force -ErrorAction SilentlyContinue }
    New-Item -ItemType Directory -Path $integResultsDir -Force | Out-Null
    
    $testOutput = dotnet test $evalProj -c $Configuration --logger "trx;LogFileName=integration_test_results.trx" --results-directory $integResultsDir --collect:"XPlat Code Coverage" 2>&1 | Out-String

    $failed = 0
    $passed = 0
    $total = 0

    $trxFiles = Get-ChildItem -Path $integResultsDir -Filter "*.trx" -Recurse | Sort-Object LastWriteTime -Descending
    if ($trxFiles -and $trxFiles.Count -gt 0) {
        try {
            [xml]$trxXml = Get-Content $trxFiles[0].FullName
            $counters = $trxXml.TestRun.ResultSummary.Counters
            if ($counters) {
                $total = [int]$counters.total
                $passed = [int]$counters.passed
                $failed = [int]$counters.failed
            }
        } catch { }
    }

    if ($total -eq 0) {
        $passedMatches = [regex]::Match($testOutput, "Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)")
        if ($passedMatches.Success) {
            $failed = [int]$passedMatches.Groups[1].Value
            $passed = [int]$passedMatches.Groups[2].Value
            $total = [int]$passedMatches.Groups[4].Value
        } else {
            $altMatches = [regex]::Match($testOutput, "Total tests:\s+(\d+)[\s\S]*?Passed:\s+(\d+)")
            if ($altMatches.Success) {
                $total = [int]$altMatches.Groups[1].Value
                $passed = [int]$altMatches.Groups[2].Value
                $failMatch = [regex]::Match($testOutput, "Failed:\s+(\d+)")
                if ($failMatch.Success) {
                    $failed = [int]$failMatch.Groups[1].Value
                }
            }
        }
    }

    # Extract Code Coverage from Cobertura XML
    $covFiles = Get-ChildItem -Path $integResultsDir -Filter "coverage.cobertura.xml" -Recurse | Sort-Object LastWriteTime -Descending
    $lineCoverage = 0.0
    $branchCoverage = 0.0
    $linesCovered = 0
    $linesValid = 0
    $packageSummaries = [System.Collections.Generic.List[string]]::new()
    if ($covFiles -and $covFiles.Count -gt 0) {
        [xml]$covXml = Get-Content $covFiles[0].FullName
        $lineCoverage = [Math]::Round([double]$covXml.coverage.'line-rate' * 100, 2)
        $linesCovered = [int]$covXml.coverage.'lines-covered'
        $linesValid = [int]$covXml.coverage.'lines-valid'
        $branchCoverage = [Math]::Round([double]$covXml.coverage.'branch-rate' * 100, 2)
        foreach ($pkg in $covXml.coverage.packages.package) {
            $pRate = [Math]::Round([double]$pkg.'line-rate' * 100, 1)
            $packageSummaries.Add("$($pkg.name): $pRate%")
        }
    }

    $integGatePassed = ($failed -eq 0) -and ($total -gt 0) -and ($lineCoverage -ge $IntegrationThreshold)

    Write-Host "  • Total Integration Tests: $total"
    Write-Host "  • Passed Tests           : $passed"
    Write-Host "  • Failed Tests           : $failed"
    Write-Host "  • Code Line Coverage     : $lineCoverage% ($linesCovered / $linesValid lines) [Enforced Gate: >= $IntegrationThreshold%]"
    Write-Host "  • Branch Coverage        : $branchCoverage%"
    if ($packageSummaries.Count -gt 0) {
        Write-Host "  • Package Breakdown      : $($packageSummaries -join ', ')" -ForegroundColor DarkGray
    }
    if ($failed -gt 0) {
        Write-Host "  ❌ Failed Integration Tests ($failed):" -ForegroundColor Red
        if ($trxFiles -and $trxFiles.Count -gt 0) {
            try {
                [xml]$trxXml = Get-Content $trxFiles[0].FullName
                $failedResults = @($trxXml.TestRun.Results.UnitTestResult) | Where-Object { $_.outcome -eq "Failed" }
                foreach ($fr in $failedResults) {
                    Write-Host "     • $($fr.testName)" -ForegroundColor Red
                    if ($fr.Output.ErrorInfo.Message) {
                        Write-Host "       $($fr.Output.ErrorInfo.Message.Trim())" -ForegroundColor DarkRed
                    }
                }
            } catch { }
        }
    }
    if ($integGatePassed) {
        Write-Host "  ✅ Tier 2 Subsystem & BFF Integration Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 2 Subsystem & BFF Integration Test Gate: FAIL (Line coverage $lineCoverage% is below $IntegrationThreshold% threshold or test failures detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 2: Subsystem & BFF Integration Tests"
        Scope = "Web BFF Composite, CQRS Handlers, Auth Schemes, SQLite EF Core"
        Required = "Line Coverage >= $IntegrationThreshold% (0 failures)"
        Achieved = "$lineCoverage% coverage ($passed/$total passed, $failed failures)"
        Passed = $integGatePassed
    })
}

# ==============================================================================
# Tier 3: Subsystem Contracts & Design System Tests (Threshold: >= $SystemThreshold%)
# ==============================================================================
if ($Tier -in @("All", "System")) {
    Write-Host "`n[Tier 3/4] Executing Subsystem Contracts & Design System Tests (Threshold: >= $SystemThreshold%)..." -ForegroundColor Yellow

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

    $sysGatePassed = ($sysPassRate -ge $SystemThreshold)

    Write-Host "  • Total System Test Suites: $sysTotal"
    Write-Host "  • Passed Suites           : $sysPassed"
    Write-Host "  • Achieved Pass Rate      : $sysPassRate% (Enforced Gate: >= $SystemThreshold%)"
    if ($sysGatePassed) {
        Write-Host "  ✅ Tier 3 Subsystem Contracts & Design System Test Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 3 Subsystem Contracts & Design System Test Gate: FAIL (Below $SystemThreshold% threshold)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 3: Subsystem Contracts & Design System Tests"
        Scope = "Subsystem Wiring, Mobile BFF ETag Caching, System Invariants, Design Tokens"
        Required = "Pass Rate >= $SystemThreshold%"
        Achieved = "$sysPassRate% ($sysPassed/$sysTotal suites passed)"
        Passed = $sysGatePassed
    })
}

# ==============================================================================
# Tier 4: Multi-Tier User Acceptance & E2E CFT Suite (Threshold: >= $E2EThreshold%)
# ==============================================================================
if ($Tier -in @("All", "E2E")) {
    Write-Host "`n[Tier 4/4] Executing Multi-Tier User Acceptance & E2E CFT Suite (Threshold: >= $E2EThreshold%)..." -ForegroundColor Yellow

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

    $e2eGatePassed = ($e2ePassRate -ge $E2EThreshold)

    Write-Host "  • Total E2E Test Suites   : $e2eTotal"
    Write-Host "  • Passed E2E Suites       : $e2ePassed"
    Write-Host "  • Achieved E2E Pass Rate  : $e2ePassRate% (Enforced Gate: >= $E2EThreshold%)"
    if ($e2eGatePassed) {
        Write-Host "  ✅ Tier 4 Multi-Tier User Acceptance & E2E CFT Gate: PASS" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Tier 4 Multi-Tier User Acceptance & E2E CFT Gate: FAIL (Below $E2EThreshold% threshold or errors detected)" -ForegroundColor Red
    }

    $gateResults.Add([PSCustomObject]@{
        Tier = "Tier 4: Multi-Tier User Acceptance & E2E CFT Suite"
        Scope = "5-Tier Customer Acceptance, AI Quotas, Paywalls & SuperAdmin"
        Required = "Pass Rate >= $E2EThreshold%"
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
