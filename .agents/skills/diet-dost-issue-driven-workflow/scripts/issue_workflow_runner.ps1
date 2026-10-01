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
    [switch]$DryRun,
    [int]$ParentPR = 0,
    [string]$ParentBranch = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($StateFilePath)) {
    $scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
    $StateFilePath = [System.IO.Path]::GetFullPath((Join-Path $scriptDir "../../../state/issue_workflow_state.json"))
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
        if (-not $json.queuedIssues) {
            $json | Add-Member -NotePropertyName "queuedIssues" -NotePropertyValue ([PSCustomObject]@{}) -Force
        }
        return $json
    }
    return [PSCustomObject]@{
        version = "1.0.0"
        description = "Durable local state registry for Diet-Dost Autonomous Issue-Driven Development (IDD)"
        lastUpdated = (Get-Date).ToString("o")
        activeIssues = [PSCustomObject]@{}
        queuedIssues = [PSCustomObject]@{}
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

function Get-Codeowners {
    param ([string]$RepoRoot)
    $codeownersFile = Join-Path $RepoRoot ".github/CODEOWNERS"
    $owners = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -Path $codeownersFile) {
        $lines = Get-Content -Path $codeownersFile
        foreach ($line in $lines) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith("#") -or [string]::IsNullOrWhiteSpace($trimmed)) { continue }
            $parts = $trimmed -split '\s+'
            foreach ($p in $parts) {
                if ($p.StartsWith("@")) {
                    $owners.Add($p.TrimStart('@')) | Out-Null
                }
            }
        }
    }
    if ($owners.Count -eq 0) {
        $owners.Add("nikunjbanker") | Out-Null
    }
    return $owners
}

$script:ProjectBoardCache = $null

function Get-ProjectBoardMetadata {
    param (
        [int]$ProjectNumber = 1,
        [string]$Owner = "nikunjbanker"
    )

    if ($script:ProjectBoardCache) { return $script:ProjectBoardCache }

    try {
        $projViewJson = gh project view $ProjectNumber --owner $Owner --format json 2>$null
        if (-not $projViewJson) { return $null }
        $projView = $projViewJson | ConvertFrom-Json

        $fieldListJson = gh project field-list $ProjectNumber --owner $Owner --format json 2>$null
        if (-not $fieldListJson) { return $null }
        $fieldList = $fieldListJson | ConvertFrom-Json

        $statusField = $fieldList.fields | Where-Object { $_.name -eq "Status" }
        if (-not $statusField) { return $null }

        $optionsMap = @{}
        foreach ($opt in $statusField.options) {
            $optionsMap[$opt.name.ToLowerInvariant()] = $opt.id
        }

        $script:ProjectBoardCache = [PSCustomObject]@{
            ProjectNumber = $ProjectNumber
            Owner         = $Owner
            ProjectId     = $projView.id
            StatusFieldId = $statusField.id
            StatusOptions = $optionsMap
        }
        return $script:ProjectBoardCache
    } catch {
        return $null
    }
}

function Set-ProjectBoardStatus {
    param (
        [int]$Number,
        [string]$TargetStatus, # "Todo", "In Progress", "Done"
        [string]$ItemType = "issues",
        [string]$Repo = "nikunjbanker/diet-dost"
    )

    $meta = Get-ProjectBoardMetadata
    if (-not $meta) { return }

    $targetKey = $TargetStatus.ToLowerInvariant()
    if (-not $meta.StatusOptions.ContainsKey($targetKey)) { return }
    $optionId = $meta.StatusOptions[$targetKey]

    try {
        $itemsJson = gh project item-list $meta.ProjectNumber --owner $meta.Owner --format json 2>$null
        if (-not $itemsJson) { return }
        $itemsObj = $itemsJson | ConvertFrom-Json

        $matchedItem = $itemsObj.items | Where-Object {
            $_.content -and $_.content.number -eq $Number
        }

        if (-not $matchedItem) {
            $url = "https://github.com/$Repo/$ItemType/$Number"
            $addJson = gh project item-add $meta.ProjectNumber --owner $meta.Owner --url $url --format json 2>$null
            if ($addJson) {
                $addedObj = $addJson | ConvertFrom-Json
                $itemId = $addedObj.id
            } else {
                return
            }
        } else {
            $itemId = $matchedItem.id
            if ($matchedItem.status -and $matchedItem.status.ToLowerInvariant() -eq $targetKey) {
                return
            }
        }

        gh project item-edit --id $itemId --field-id $meta.StatusFieldId --project-id $meta.ProjectId --single-select-option-id $optionId 2>$null | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    [PROJECT BOARD] Set #$Number status to '$TargetStatus'." -ForegroundColor Cyan
        }
    } catch {
        Write-Warning "Could not update board status for #$($Number): $_"
    }
}

function Test-IssueCodeownerApproval {
    param (
        [PSCustomObject]$Issue,
        [System.Collections.Generic.HashSet[string]]$Codeowners
    )
    $author = if ($Issue.author -and $Issue.author.login) { $Issue.author.login } else { "" }
    
    # 1. Author is a recognized CODEOWNER -> Pre-authorized
    if ($Codeowners.Contains($author)) {
        return [PSCustomObject]@{
            IsApproved = $true
            Approver = $author
            Method = "AUTHOR_IS_CODEOWNER"
            Reason = "Issue created by recognized CODEOWNER '@$author'."
        }
    }

    # 2. Check comments by a CODEOWNER for /approve or /proceed
    $commentsJson = gh issue view $Issue.number --json comments 2>$null
    if ($commentsJson) {
        $commentData = $commentsJson | ConvertFrom-Json
        if ($commentData -and $commentData.comments) {
            foreach ($c in $commentData.comments) {
                $commentAuthor = if ($c.author -and $c.author.login) { $c.author.login } else { "" }
                if ($Codeowners.Contains($commentAuthor)) {
                    if ($c.body -match "(?i)/(?:approve|proceed|start|lgtm)") {
                        return [PSCustomObject]@{
                            IsApproved = $true
                            Approver = $commentAuthor
                            Method = "CODEOWNER_COMMENT"
                            Reason = "Approved via comment by CODEOWNER '@$commentAuthor'."
                        }
                    }
                }
            }
        }
    }

    # 3. Check for 'approved-by-codeowner' or 'status:approved' label
    if ($Issue.labels) {
        foreach ($lbl in $Issue.labels) {
            if ($lbl.name -eq "approved-by-codeowner" -or $lbl.name -eq "status:approved") {
                return [PSCustomObject]@{
                    IsApproved = $true
                    Approver = "CODEOWNER_LABEL"
                    Method = "CODEOWNER_LABEL"
                    Reason = "Issue tagged with approval label '$($lbl.name)'."
                }
            }
        }
    }

    return [PSCustomObject]@{
        IsApproved = $false
        Approver = $null
        Method = "NONE"
        Reason = "Created by non-codeowner '@$author'. Requires CODEOWNER approval via comment ('/approve') or label ('approved-by-codeowner')."
    }
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

    $repoRoot = try { (git rev-parse --show-toplevel 2>$null).Trim() } catch { "" }
    if (-not $repoRoot -or -not (Test-Path -Path $repoRoot)) {
        $repoRoot = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $StateFile) "../../.."))
    }
    $codeowners = Get-Codeowners -RepoRoot $repoRoot
    Write-Host "Resolved CODEOWNERS: @($($codeowners -join ', @'))" -ForegroundColor DarkCyan

    Write-Host "Querying repository issues via GitHub CLI..." -ForegroundColor Cyan
    $issuesJson = gh issue list --limit 50 --json number,title,state,updatedAt,labels,body,author 2>$null
    if (-not $issuesJson) {
        Write-Host "No open issues found or GitHub returned empty set." -ForegroundColor Yellow
        return
    }

    $issues = $issuesJson | ConvertFrom-Json

    # 1. Inspect currently HELD issues for newly granted CODEOWNER approval
    $heldKeys = @($State.heldIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($hk in $heldKeys) {
        $heldRec = $State.heldIssues.$hk
        $matched = $issues | Where-Object { [string]$_.number -eq $hk }
        if (-not $matched) {
            $single = gh issue view $hk --json number,title,state,updatedAt,labels,body,author 2>$null | ConvertFrom-Json
            if ($single -and $single.state -eq "CLOSED") {
                Write-Host "==> Held issue #$hk was CLOSED on GitHub. Archiving." -ForegroundColor Magenta
                $heldRec.phase = "COMPLETED"
                $heldRec | Add-Member -NotePropertyName "completedAt" -NotePropertyValue (Get-Date).ToString("o") -Force
                $State.completedIssues | Add-Member -NotePropertyName $hk -NotePropertyValue $heldRec -Force
                $State.heldIssues.PSObject.Properties.Remove($hk)
                Save-WorkflowState -Path $StateFile -State $State
                Set-ProjectBoardStatus -Number ([int]$hk) -TargetStatus "Done"
                continue
            }
            $matched = $single
        }
        if ($matched) {
            $approval = Test-IssueCodeownerApproval -Issue $matched -Codeowners $codeowners
            if ($approval.IsApproved) {
                Write-Host "==> APPROVED: Held issue #$hk was approved by CODEOWNER '$($approval.Approver)' via $($approval.Method)! Moving to active development." -ForegroundColor Green
                $heldRec.phase = "DETECTED"
                $heldRec.approvalStatus = "AUTHORIZED"
                $heldRec.approvedBy = $approval.Approver
                $heldRec.approvedAt = (Get-Date).ToString("o")
                $heldRec.checkpoints += "CODEOWNER_APPROVED"
                $heldRec.notes = "Approved by CODEOWNER via $($approval.Method). Promoted to active development."
                $State.activeIssues | Add-Member -NotePropertyName $hk -NotePropertyValue $heldRec -Force
                $State.heldIssues.PSObject.Properties.Remove($hk)
                Save-WorkflowState -Path $StateFile -State $State
                Set-ProjectBoardStatus -Number ([int]$hk) -TargetStatus "In Progress"
            } else {
                Write-Host "    Issue #$hk remains on HOLD (Waiting for approval by CODEOWNER: @$($codeowners -join ', @'))." -ForegroundColor DarkGray
            }
        }
    }

    # 2. Inspect incoming / updated issues
    foreach ($issue in $issues) {
        $numStr = [string]$issue.number
        $currentRecord = $State.activeIssues.$numStr
        $heldRecord = $State.heldIssues.$numStr
        $queuedRecord = $State.queuedIssues.$numStr
        $completedRecord = $State.completedIssues.$numStr

        if ($null -eq $currentRecord -and $null -eq $heldRecord -and $null -eq $queuedRecord -and $null -eq $completedRecord) {
            # New Issue Intake
            $author = if ($issue.author -and $issue.author.login) { $issue.author.login } else { "unknown" }
            $approval = Test-IssueCodeownerApproval -Issue $issue -Codeowners $codeowners

            $issueType = "feature"
            foreach ($label in $issue.labels) {
                if ($label.name -match "bug|defect|fix") { $issueType = "fix" }
                elseif ($label.name -match "doc|governance") { $issueType = "docs" }
                elseif ($label.name -match "arch|refactor") { $issueType = "arch" }
            }

            $slug = ($issue.title.ToLower() -replace '[^a-z0-9]+', '-').Trim('-')
            if ($slug.Length -gt 35) { $slug = $slug.Substring(0, 35).Trim('-') }
            $branchName = "$issueType/issue-$($issue.number)-$slug"

            $isStacked = $false
            $depPr = 0
            $depBranch = ""
            if ($ParentPR -gt 0) {
                $isStacked = $true
                $depPr = $ParentPR
                $depBranch = $ParentBranch
            } elseif ($issue.body -match "(?i)(?:depends on|stacked on|parent pr|parent issue)[:\s]+#?(\d+)") {
                $isStacked = $true
                $depPr = [int]$matches[1]
                try {
                    $parentInfo = gh pr view $depPr --json headRefName 2>$null | ConvertFrom-Json
                    if ($parentInfo) { $depBranch = $parentInfo.headRefName }
                } catch {}
            }

            if ($approval.IsApproved) {
                Write-Host "==> DETECTED & AUTHORIZED issue #$($issue.number): '$($issue.title)' by '@$author'" -ForegroundColor Green
                
                # Single-Issue In-Flight Policy: Only 1 issue active at a time
                $activeKeys = @($State.activeIssues.PSObject.Properties | ForEach-Object { $_.Name })
                if ($activeKeys.Count -gt 0) {
                    Write-Host "    [SINGLE-ISSUE POLICY] Issue #$($activeKeys[0]) is currently active. Queuing issue #$($issue.number)..." -ForegroundColor Yellow
                    $newRecord = [PSCustomObject]@{
                        issueNumber = $issue.number
                        title = $issue.title
                        author = $author
                        isCodeownerAuthor = ($approval.Method -eq "AUTHOR_IS_CODEOWNER")
                        approvalStatus = "AUTHORIZED"
                        approvedBy = $approval.Approver
                        approvedAt = (Get-Date).ToString("o")
                        issueType = $issueType
                        branchName = $branchName
                        phase = "QUEUED_AWAITING_ACTIVE_ISSUE"
                        updatedAt = $issue.updatedAt
                        detectedAt = (Get-Date).ToString("o")
                        checkpoints = @("DETECTED", "QUEUED")
                        isStacked = $isStacked
                        parentPR = $depPr
                        parentBranch = $depBranch
                        notes = "Authorized by CODEOWNER. Queued: single active issue in-flight policy enforced (Waiting for #$($activeKeys[0]) to complete)."
                    }
                    $State.queuedIssues | Add-Member -NotePropertyName $numStr -NotePropertyValue $newRecord -Force
                    Save-WorkflowState -Path $StateFile -State $State
                    Write-Host "    Checkpointed issue #$($issue.number) in phase QUEUED_AWAITING_ACTIVE_ISSUE." -ForegroundColor Gray
                    Set-ProjectBoardStatus -Number $issue.number -TargetStatus "Todo"
                } else {
                    $newRecord = [PSCustomObject]@{
                        issueNumber = $issue.number
                        title = $issue.title
                        author = $author
                        isCodeownerAuthor = ($approval.Method -eq "AUTHOR_IS_CODEOWNER")
                        approvalStatus = "AUTHORIZED"
                        approvedBy = $approval.Approver
                        approvedAt = (Get-Date).ToString("o")
                        issueType = $issueType
                        branchName = $branchName
                        phase = "DETECTED"
                        updatedAt = $issue.updatedAt
                        detectedAt = (Get-Date).ToString("o")
                        checkpoints = @("DETECTED")
                        isStacked = $isStacked
                        parentPR = $depPr
                        parentBranch = $depBranch
                        notes = "Authorized by CODEOWNER via $($approval.Method)."
                    }
                    $State.activeIssues | Add-Member -NotePropertyName $numStr -NotePropertyValue $newRecord -Force
                    Save-WorkflowState -Path $StateFile -State $State
                    Write-Host "    Checkpointed issue #$($issue.number) in phase DETECTED." -ForegroundColor Gray
                    Set-ProjectBoardStatus -Number $issue.number -TargetStatus "In Progress"
                }
            } else {
                Write-Host "==> HELD FOR APPROVAL: Issue #$($issue.number) created by non-codeowner '@$author'." -ForegroundColor Yellow
                Write-Host "    $($approval.Reason)" -ForegroundColor DarkYellow
                $newHeldRecord = [PSCustomObject]@{
                    issueNumber = $issue.number
                    title = $issue.title
                    author = $author
                    isCodeownerAuthor = $false
                    approvalStatus = "AWAITING_CODEOWNER_APPROVAL"
                    approvedBy = $null
                    approvedAt = $null
                    issueType = $issueType
                    branchName = $branchName
                    phase = "AWAITING_CODEOWNER_APPROVAL"
                    updatedAt = $issue.updatedAt
                    detectedAt = (Get-Date).ToString("o")
                    checkpoints = @("DETECTED", "HELD_FOR_APPROVAL")
                    isStacked = $isStacked
                    parentPR = $depPr
                    parentBranch = $depBranch
                    notes = $approval.Reason
                }

                $State.heldIssues | Add-Member -NotePropertyName $numStr -NotePropertyValue $newHeldRecord -Force
                Save-WorkflowState -Path $StateFile -State $State
                Write-Host "    Issue #$($issue.number) placed on HOLD until approved by CODEOWNER (@$($codeowners -join ', @'))." -ForegroundColor Gray
                Set-ProjectBoardStatus -Number $issue.number -TargetStatus "Todo"
            }
        } elseif ($currentRecord) {
            # Issue updated in GitHub
            if ($currentRecord.updatedAt -ne $issue.updatedAt) {
                $currentRecord.updatedAt = $issue.updatedAt
                if ($currentRecord.phase -in @("PR_OPENED", "DRAFT_PR_CREATED", "TESTED", "COMPLETED")) {
                    Write-Host "    Issue #$($issue.number) updated (Comment/activity in phase $($currentRecord.phase)). Maintaining current phase." -ForegroundColor DarkGray
                } else {
                    Write-Host "==> DETECTED requirement update on issue #$($issue.number): '$($issue.title)'" -ForegroundColor Yellow
                    $currentRecord.notes = "Issue updated on GitHub: requires re-analysis (ACT phase)."
                    $currentRecord.phase = "ACT_ANALYZING"
                    $currentRecord.checkpoints += "ACT_RETRIGGERED"
                }
                Save-WorkflowState -Path $StateFile -State $State
            }
        } elseif ($queuedRecord) {
            # Queued issue updated in GitHub
            if ($queuedRecord.updatedAt -ne $issue.updatedAt) {
                Write-Host "==> DETECTED update on queued issue #$($issue.number): '$($issue.title)'" -ForegroundColor DarkYellow
                $queuedRecord.updatedAt = $issue.updatedAt
                $queuedRecord.title = $issue.title
                Save-WorkflowState -Path $StateFile -State $State
            }
        }
    }

    # 3. Detect closed or deleted active issues
    $activeKeys = @($State.activeIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($k in $activeKeys) {
        $matched = $issues | Where-Object { [string]$_.number -eq $k }
        if (-not $matched) {
            $single = gh issue view $k --json state 2>$null | ConvertFrom-Json
            if ($single.state -eq "CLOSED") {
                Write-Host "==> Issue #$k was CLOSED on GitHub. Archiving from active state." -ForegroundColor Magenta
                $rec = $State.activeIssues.$k
                $rec.phase = "COMPLETED"
                $rec | Add-Member -NotePropertyName "completedAt" -NotePropertyValue (Get-Date).ToString("o") -Force
                $State.completedIssues | Add-Member -NotePropertyName $k -NotePropertyValue $rec -Force
                $State.activeIssues.PSObject.Properties.Remove($k)
                Save-WorkflowState -Path $StateFile -State $State
                Set-ProjectBoardStatus -Number ([int]$k) -TargetStatus "Done"

                # Single-Issue Policy: Auto-promote next queued issue if any (Bugs/fixes prioritized first)
                $queuedKeys = @($State.queuedIssues.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object {
                    $rec = $State.queuedIssues.$_
                    $priority = if ($rec.issueType -eq "fix") { 0 } else { 1 }
                    return "$priority-$([int]$_)"
                })
                if ($queuedKeys.Count -gt 0) {
                    $nextKey = $queuedKeys[0]
                    $nextRec = $State.queuedIssues.$nextKey
                    Write-Host "==> PROMOTING next queued issue #$nextKey to active execution!" -ForegroundColor Green
                    $nextRec.phase = "DETECTED"
                    $nextRec.checkpoints += "PROMOTED_FROM_QUEUE"
                    $nextRec.notes = "Promoted from queue after issue #$k completed."
                    $State.activeIssues | Add-Member -NotePropertyName $nextKey -NotePropertyValue $nextRec -Force
                    $State.queuedIssues.PSObject.Properties.Remove($nextKey)
                    Save-WorkflowState -Path $StateFile -State $State
                    Set-ProjectBoardStatus -Number ([int]$nextKey) -TargetStatus "In Progress"
                }
            }
        }
    }

    # 4. Synchronize Project Board status for current active and queued issues
    $currentActiveKeys = @($State.activeIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($cak in $currentActiveKeys) {
        Set-ProjectBoardStatus -Number ([int]$cak) -TargetStatus "In Progress"
    }
    $currentQueuedKeys = @($State.queuedIssues.PSObject.Properties | ForEach-Object { $_.Name })
    foreach ($cqk in $currentQueuedKeys) {
        Set-ProjectBoardStatus -Number ([int]$cqk) -TargetStatus "Todo"
    }
}

# Main Execution Entry
$state = Load-WorkflowState -Path $StateFilePath

if ($IssueNumber -gt 0) {
    Write-Host "Processing targeted single issue: #$IssueNumber" -ForegroundColor Cyan
    $numKey = [string]$IssueNumber
    $record = $state.activeIssues.$numKey
    $held = $state.heldIssues.$numKey

    if ($null -eq $record -and $null -eq $held) {
        Write-Host "Issue #$IssueNumber not found in local active or held state. Querying GitHub..." -ForegroundColor Yellow
        $issue = gh issue view $IssueNumber --json number,title,state,updatedAt,labels,body,author 2>$null | ConvertFrom-Json
        if ($issue) {
            Invoke-IssueSync -State $state -StateFile $StateFilePath
            $state = Load-WorkflowState -Path $StateFilePath
            $record = $state.activeIssues.$numKey
            $held = $state.heldIssues.$numKey
        } else {
            Write-Error "Could not retrieve issue #$IssueNumber from GitHub."
            exit 1
        }
    }

    if ($held) {
        Write-Warning "Issue #$IssueNumber is ON HOLD awaiting CODEOWNER approval."
        Write-Warning "Created by non-codeowner '@($held.author)'. A recognized CODEOWNER must comment '/approve' or apply 'approved-by-codeowner' label before development can begin."
        exit 1
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
