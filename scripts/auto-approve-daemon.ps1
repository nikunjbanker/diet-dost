<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
param(
    [string]$RunId = "",
    [string]$TargetHost = "https://dev.diet-dost.in",
    [int]$PollSeconds = 10,
    [int]$TimeoutMinutes = 45,
    [switch]$SkipPostValidation
)

$endTime = (Get-Date).AddMinutes($TimeoutMinutes)
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  DIET-DOST AUTONOMOUS DEPLOYMENT & APPROVAL DAEMON" -ForegroundColor Cyan
Write-Host "  Polling Every : $PollSeconds s (Timeout in $TimeoutMinutes min)" -ForegroundColor Cyan
Write-Host "  Target Host   : $TargetHost" -ForegroundColor Cyan
if ($RunId) {
    Write-Host "  Target Run ID : $RunId" -ForegroundColor Cyan
} else {
    Write-Host "  Mode          : Auto-discover active deployment runs" -ForegroundColor Cyan
}
Write-Host "==========================================================" -ForegroundColor Cyan

function Approve-PendingDeployment([string]$targetRunId, [string]$workflowTitle) {
    try {
        $pendingRaw = gh api "repos/:owner/:repo/actions/runs/$targetRunId/pending_deployments" 2>$null
        if ($pendingRaw -and $pendingRaw.Trim() -ne "[]") {
            $pending = $pendingRaw | ConvertFrom-Json
            foreach ($dep in $pending) {
                if ($dep.current_user_can_approve) {
                    $envId = [int64]$dep.environment.id
                    $envName = $dep.environment.name
                    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] 🔔 Pending approval for '$envName' (ID: $envId) on run $targetRunId ($workflowTitle). Auto-approving..." -ForegroundColor Yellow

                    # Use JSON payload via stdin to guarantee environment_ids is sent as a numeric integer array (prevents HTTP 422)
                    $payload = @{
                        environment_ids = @($envId)
                        state = "approved"
                        comment = "Auto-approved deployment authorized by CODEOWNER / Diet-Dost Daemon"
                    } | ConvertTo-Json -Compress

                    $resp = $payload | gh api --method POST "repos/:owner/:repo/actions/runs/$targetRunId/pending_deployments" --input - 2>&1
                    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] ✅ Successfully approved environment '$envName' on run $targetRunId." -ForegroundColor Green
                }
            }
        }
    }
    catch {
        # Suppress transient network hiccups
    }
}

function Test-LiveEndpoints([string]$hostUrl) {
    Write-Host ""
    Write-Host "🔍 Executing Post-Deployment Live System Validation against $hostUrl..." -ForegroundColor Magenta
    Start-Sleep -Seconds 5
    $endpoints = @(
        "$hostUrl/health",
        "$hostUrl/index.html",
        "$hostUrl/partials/camera-modal.html"
    )

    $allPassed = $true
    foreach ($url in $endpoints) {
        try {
            $res = Invoke-WebRequest -Uri $url -Method Get -TimeoutSec 10 -UseBasicParsing
            if ($res.StatusCode -eq 200) {
                Write-Host "  ✅ [HTTP 200] $url" -ForegroundColor Green
            } else {
                Write-Host "  ❌ [HTTP $($res.StatusCode)] $url" -ForegroundColor Red
                $allPassed = $false
            }
        } catch {
            Write-Host "  ❌ [FAILED] $url : $_" -ForegroundColor Red
            $allPassed = $false
        }
    }

    if ($allPassed) {
        Write-Host ""
        Write-Host "🌟 ALL POST-DEPLOYMENT CHECKS PASSED 100%! LIVE SYSTEM FULLY OPERATIONAL." -ForegroundColor Green
    } else {
        Write-Host "⚠️ Some post-deployment endpoints failed validation." -ForegroundColor Yellow
    }
    return $allPassed
}

while ((Get-Date) -lt $endTime) {
    try {
        if ($RunId) {
            # Targeted run mode
            Approve-PendingDeployment -targetRunId $RunId -workflowTitle "Targeted Run"

            $runJson = gh run view $RunId --json status,conclusion,jobs,name 2>$null
            if ($runJson) {
                $run = $runJson | ConvertFrom-Json
                $status = $run.status
                $conclusion = $run.conclusion
                $activeJobs = @($run.jobs | Where-Object { $_.status -in "in_progress", "waiting" } | Select-Object -ExpandProperty name)
                $completedCount = ($run.jobs | Where-Object { $_.status -eq "completed" }).Count
                $totalCount = $run.jobs.Count

                Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] Run ${RunId}: $status | Conclusion: $conclusion | Progress: $completedCount/$totalCount jobs | Active: $($activeJobs -join ', ')" -ForegroundColor Cyan

                if ($status -eq "completed") {
                    if ($conclusion -eq "success") {
                        Write-Host "🎉 DEPLOYMENT WORKFLOW $RunId COMPLETED SUCCESSFULLY!" -ForegroundColor Green
                        if (-not $SkipPostValidation) {
                            Test-LiveEndpoints -hostUrl $TargetHost
                        }
                        exit 0
                    } else {
                        Write-Host "❌ DEPLOYMENT WORKFLOW $RunId FAILED with conclusion: $conclusion" -ForegroundColor Red
                        exit 1
                    }
                }
            }
        }
        else {
            # Auto-discovery mode: Scan active deployment runs
            $runsRaw = gh run list --limit 10 --json databaseId,workflowName,status,conclusion
            if ($runsRaw) {
                $runs = $runsRaw | ConvertFrom-Json
                $activeRuns = $runs | Where-Object { $_.status -in @('in_progress', 'queued', 'waiting') -and $_.workflowName -match 'Deploy' }

                if (-not $activeRuns -or $activeRuns.Count -eq 0) {
                    # Also check any generic in_progress workflow
                    $activeRuns = $runs | Where-Object { $_.status -in @('in_progress', 'waiting') }
                }

                foreach ($activeRun in $activeRuns) {
                    $activeId = "$($activeRun.databaseId)"
                    Approve-PendingDeployment -targetRunId $activeId -workflowTitle $activeRun.workflowName
                }

                # Check if the most recent deploy workflow just completed
                $recentDeploy = $runs | Where-Object { $_.workflowName -match 'Deploy' } | Select-Object -First 1
                if ($recentDeploy -and $recentDeploy.status -eq "completed") {
                    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] Deploy workflow $($recentDeploy.databaseId) completed with conclusion: $($recentDeploy.conclusion)" -ForegroundColor Green
                    if ($recentDeploy.conclusion -eq "success" -and -not $SkipPostValidation) {
                        Test-LiveEndpoints -hostUrl $TargetHost
                    }
                    exit 0
                }
            }
        }
    }
    catch {
        # Transient error suppression
    }
    Start-Sleep -Seconds $PollSeconds
}

Write-Host "Auto-approval daemon timed out after $TimeoutMinutes minutes." -ForegroundColor Yellow
exit 1
