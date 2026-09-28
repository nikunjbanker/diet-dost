<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>

[CmdletBinding()]
param (
    [string]$StateFilePath = "",
    [int]$IssueNumber = 0,
    [switch]$Poll,
    [int]$PollIntervalSeconds = 60,
    [switch]$Resume,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($StateFilePath)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
    $StateFilePath = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "../../state/issue_workflow_state.json"))
}

function Test-GitHubConnectivity {
    try {
        $authOutput = gh auth status 2>&1
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Load-WorkflowState {
    param ([string]$Path)
    if (Test-Path -Path $Path) {
        $json = Get-Content -Path $Path -Raw | ConvertFrom-Json
        return $json
    }
    return [PSCustomObject]@{
        version = "1.0.0"
        description = "Durable local state registry for Diet-Dost Autonomous Issue-Driven Development (IDD)"
        lastUpdated = (Get-Date).ToString("o")
        activeIssues = [PSCustomObject]@{}
        completedIssues = [PSCustomObject]@{}
        heldIssues = [PSCustomObject]@{}
    }
}

function Save-WorkflowState {
    param (
        [string]$Path,
        [PSCustomObject]$State
    )
    $State.lastUpdated = (Get-Date).ToString("o")
    $json = $State | ConvertTo-Json -Depth 10
    $parentDir = Split-Path -Path $Path -Parent
    if (-not (Test-Path -Path $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }
    Set-Content -Path $Path -Value $json -Force
}

function Invoke-IssueSync {
    param (
        [PSCustomObject]$State,
        [string]$StateFile
    )

    Write-Host "Checking GitHub connectivity..." -ForegroundColor Cyan
    if (-not (Test-GitHubConnectivity)) {
        Write-Warning "GitHub CLI is offline or unauthenticated. Holding all in-flight operations in local memory."
        return
    }

    Write-Host "Querying repository issues via GitHub CLI..." -ForegroundColor Cyan
    $issuesJson = gh issue list --limit 50 --json number,title,state,updatedAt,labels,body 2>$null
    if (-not $issuesJson) {
        Write-Host "No open issues found or GitHub returned empty set." -ForegroundColor Yellow
        return
    }

    $issues = $issuesJson | ConvertFrom-Json

    foreach ($issue in $issues) {
        $numStr = [string]$issue.number
        $currentRecord = $State.activeIssues.$numStr

        if ($null -eq $currentRecord) {
            # New Issue Detected
            Write-Host "==> DETECTED new issue #$($issue.number): '$($issue.title)'" -ForegroundColor Green
            $issueType = "feature"
            foreach ($label in $issue.labels) {
                if ($label.name -match "bug|defect|fix") { $issueType = "fix" }
                elseif ($label.name -match "doc|governance") { $issueType = "docs" }
                elseif ($label.name -match "arch|refactor") { $issueType = "arch" }
            }

            $slug = ($issue.title.ToLower() -replace '[^a-z0-9]+', '-').Trim('-')
            if ($slug.Length -gt 35) { $slug = $slug.Substring(0, 35).Trim('-') }
            $branchName = "$issueType/issue-$($issue.number)-$slug"

            $newRecord = [PSCustomObject]@{
                issueNumber = $issue.number
                title = $issue.title
                issueType = $issueType
                branchName = $branchName
                phase = "DETECTED"
                updatedAt = $issue.updatedAt
                detectedAt = (Get-Date).ToString("o")
                checkpoints = @("DETECTED")
                notes = "Auto-detected by issue_workflow_runner.ps1"
            }

            $State.activeIssues | Add-Member -NotePropertyName $numStr -NotePropertyValue $newRecord -Force
            Save-WorkflowState -Path $StateFile -State $State
            Write-Host "    Checkpointed issue #$($issue.number) in phase DETECTED." -ForegroundColor Gray
        } else {
            # Issue updated in GitHub
            if ($currentRecord.updatedAt -ne $issue.updatedAt) {
                Write-Host "==> DETECTED update on issue #$($issue.number): '$($issue.title)'" -ForegroundColor Yellow
                $currentRecord.updatedAt = $issue.updatedAt
                $currentRecord.notes = "Issue updated on GitHub: requires re-analysis (ACT phase)."
                if ($currentRecord.phase -ne "COMPLETED") {
                    $currentRecord.phase = "ACT_ANALYZING"
                    $currentRecord.checkpoints += "ACT_RETRIGGERED"
                }
                Save-WorkflowState -Path $StateFile -State $State
            }
        }
    }

    # Detect closed or deleted issues
    $activeKeys = @($State.activeIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($k in $activeKeys) {
        $matched = $issues | Where-Object { [string]$_.number -eq $k }
        if (-not $matched) {
            # Check issue status individually
            $single = gh issue view $k --json state 2>$null | ConvertFrom-Json
            if ($single.state -eq "CLOSED") {
                Write-Host "==> Issue #$k was CLOSED on GitHub. Archiving from active state." -ForegroundColor Magenta
                $rec = $State.activeIssues.$k
                $rec.phase = "COMPLETED"
                $rec.completedAt = (Get-Date).ToString("o")
                $State.completedIssues | Add-Member -NotePropertyName $k -NotePropertyValue $rec -Force
                $State.activeIssues.PSObject.Properties.Remove($k)
                Save-WorkflowState -Path $StateFile -State $State
            }
        }
    }
}

# Main Execution Entry
$state = Load-WorkflowState -Path $StateFilePath

if ($IssueNumber -gt 0) {
    Write-Host "Processing targeted single issue: #$IssueNumber" -ForegroundColor Cyan
    $numKey = [string]$IssueNumber
    $record = $state.activeIssues.$numKey
    if ($null -eq $record) {
        Write-Host "Issue #$IssueNumber not found in local active state. Querying GitHub..." -ForegroundColor Yellow
        $issue = gh issue view $IssueNumber --json number,title,state,updatedAt,labels,body 2>$null | ConvertFrom-Json
        if ($issue) {
            Invoke-IssueSync -State $state -StateFile $StateFilePath
            $state = Load-WorkflowState -Path $StateFilePath
        } else {
            Write-Error "Could not retrieve issue #$IssueNumber from GitHub."
            exit 1
        }
    }
    Write-Host "Active State for Issue #$IssueNumber`: $($state.activeIssues.$numKey | ConvertTo-Json)" -ForegroundColor Green
    exit 0
}

if ($Resume) {
    Write-Host "Resuming all held or incomplete issues from local state..." -ForegroundColor Cyan
    $heldKeys = @($state.heldIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($hk in $heldKeys) {
        $rec = $state.heldIssues.$hk
        Write-Host "Resuming held issue #$hk (Phase: $($rec.phase))..." -ForegroundColor Yellow
        $state.activeIssues | Add-Member -NotePropertyName $hk -NotePropertyValue $rec -Force
        $state.heldIssues.PSObject.Properties.Remove($hk)
    }
    Save-WorkflowState -Path $StateFilePath -State $state
    Invoke-IssueSync -State $state -StateFile $StateFilePath
    exit 0
}

if ($Poll) {
    Write-Host "Starting continuous local polling every $PollIntervalSeconds seconds. Press Ctrl+C to stop." -ForegroundColor Green
    while ($true) {
        try {
            $state = Load-WorkflowState -Path $StateFilePath
            Invoke-IssueSync -State $state -StateFile $StateFilePath
        } catch {
            Write-Warning "Encountered transient error during issue sync: $_. Holding state in local memory."
        }
        Start-Sleep -Seconds $PollIntervalSeconds
    }
} else {
    Invoke-IssueSync -State $state -StateFile $StateFilePath
    Write-Host "Issue synchronization completed successfully." -ForegroundColor Green
}
