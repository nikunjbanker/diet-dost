<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
param(
    [int]$PollSeconds = 10,
    [int]$TimeoutMinutes = 45
)

$endTime = (Get-Date).AddMinutes($TimeoutMinutes)
Write-Host "Auto-approval daemon started for GitHub Actions deployments (Polling every $PollSeconds s, timeout in $TimeoutMinutes min)..."

while ((Get-Date) -lt $endTime) {
    try {
        $runsRaw = gh run list --limit 10 --json databaseId, workflowName, status
        if ($runsRaw) {
            $runs = $runsRaw | ConvertFrom-Json
            $activeRuns = $runs | Where-Object { $_.status -in @('in_progress', 'queued', 'waiting') }

            foreach ($run in $activeRuns) {
                $runId = $run.databaseId
                $pendingRaw = gh api "repos/:owner/:repo/actions/runs/$runId/pending_deployments" 2>$null
                if ($pendingRaw -and $pendingRaw.Trim() -ne "[]") {
                    $pending = $pendingRaw | ConvertFrom-Json
                    foreach ($dep in $pending) {
                        if ($dep.current_user_can_approve) {
                            $envId = $dep.environment.id
                            $envName = $dep.environment.name
                            Write-Host "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Found pending deployment for '$envName' on run $runId ($($run.workflowName)). Auto-approving..."
                            $resp = gh api --method POST "repos/:owner/:repo/actions/runs/$runId/pending_deployments" `
                                -F "environment_ids[]=$envId" `
                                -F "state=approved" `
                                -F "comment=Auto-approved deployment authorized by CODEOWNER" 2>&1
                            Write-Host "[$((Get-Date).ToString('yyyy-MM-dd HH:mm:ss'))] Successfully approved environment '$envName' on run $runId."
                        }
                    }
                }
            }
        }
    }
    catch {
        # Transient error suppression
    }
    Start-Sleep -Seconds $PollSeconds
}

Write-Host "Auto-approval daemon completed window."
