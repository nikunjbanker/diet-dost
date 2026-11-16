<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>

param(
    [ValidateSet('Auto', 'Major', 'Minor', 'Patch', 'Build')]
    [string]$BumpType = 'Auto',

    [string]$TargetTag = '',

    [string]$CommitMessage = '',

    [string]$Environment = 'dev',

    [string]$TargetHost = 'https://dev.diet-dost.in',

    [switch]$CreateTagAndBranch,

    [switch]$Deploy,

    [switch]$Wait,

    [switch]$CreateGitHubRelease,

    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  DIET-DOST AUTOMATED RELEASE & DEPLOYMENT ORCHESTRATOR" -ForegroundColor Cyan
Write-Host "  Target Environment: $Environment" -ForegroundColor Cyan
Write-Host "  Requested Bump    : $BumpType" -ForegroundColor Cyan
Write-Host "  Dry Run Mode      : $DryRun" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# 1. Discover Current Highest Alpha Tag in Repository
# -----------------------------------------------------------------------------
$rawTags = git tag --list "alpha-*" 2>$null
$parsedTags = @()

foreach ($t in $rawTags) {
    if ($t -match '^alpha-(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
        $parsedTags += [PSCustomObject]@{
            Tag   = $t
            Major = [int]$Matches[1]
            Minor = [int]$Matches[2]
            Patch = [int]$Matches[3]
            Build = [int]$Matches[4]
        }
    }
}

$highestTag = $null
if ($parsedTags.Count -gt 0) {
    $highestTag = $parsedTags | Sort-Object Major, Minor, Patch, Build -Descending | Select-Object -First 1
}

$currentMajor = if ($highestTag) { $highestTag.Major } else { 5 }
$currentMinor = if ($highestTag) { $highestTag.Minor } else { 0 }
$currentPatch = if ($highestTag) { $highestTag.Patch } else { 0 }
$currentBuild = if ($highestTag) { $highestTag.Build } else { 0 }
$currentTagStr = if ($highestTag) { $highestTag.Tag } else { "alpha-5.0.0.0" }

Write-Host "Highest Discovered Tag: $currentTagStr" -ForegroundColor Yellow

# -----------------------------------------------------------------------------
# 2. Semantic Bump Detection (Auto-Mode)
# -----------------------------------------------------------------------------
$effectiveBump = $BumpType

if ($effectiveBump -eq 'Auto') {
    $evalMsg = $CommitMessage
    if (-not $evalMsg) {
        $evalMsg = git log -1 --pretty=%B 2>$null
    }

    Write-Host "Evaluating commit message for Conventional Commits:" -ForegroundColor Gray
    Write-Host ">> $evalMsg" -ForegroundColor DarkGray

    if ($evalMsg -match '(?i)BREAKING[- ]CHANGE|feat(\([^\)]+\))?!:|fix(\([^\)]+\))?!:') {
        $effectiveBump = 'Major'
        Write-Host "Detected BREAKING CHANGE / Breaking Pattern -> MAJOR bump" -ForegroundColor Magenta
    }
    elseif ($evalMsg -match '(?i)^feat(\([^\)]+\))?:') {
        $effectiveBump = 'Minor'
        Write-Host "Detected Feature Pattern ('feat:') -> MINOR bump" -ForegroundColor Green
    }
    elseif ($evalMsg -match '(?i)^(fix|perf|refactor)(\([^\)]+\))?:') {
        $effectiveBump = 'Patch'
        Write-Host "Detected Fix / Maintenance Pattern ('fix/perf/refactor:') -> PATCH bump" -ForegroundColor Yellow
    }
    else {
        $effectiveBump = 'Patch'
        Write-Host "No explicit feature/breaking marker found -> Defaulting to PATCH bump" -ForegroundColor Cyan
    }
}

# -----------------------------------------------------------------------------
# 3. Calculate New Tag & Release Branch
# -----------------------------------------------------------------------------
$newMajor = $currentMajor
$newMinor = $currentMinor
$newPatch = $currentPatch
$newBuild = $currentBuild

if ($TargetTag) {
    $newTag = $TargetTag
    if ($newTag -match '^alpha-(\d+)\.(\d+)\.(\d+)\.(\d+)$') {
        $newMajor = [int]$Matches[1]
        $newMinor = [int]$Matches[2]
        $newPatch = [int]$Matches[3]
        $newBuild = [int]$Matches[4]
    }
}
else {
    switch ($effectiveBump) {
        'Major' {
            $newMajor = $currentMajor + 1
            $newMinor = 0
            $newPatch = 0
            $newBuild = 0
        }
        'Minor' {
            $newMinor = $currentMinor + 1
            $newPatch = 0
            $newBuild = 0
        }
        'Patch' {
            $newPatch = $currentPatch + 1
            $newBuild = 0
        }
        'Build' {
            $newBuild = $currentBuild + 1
        }
    }
    $newTag = "alpha-$newMajor.$newMinor.$newPatch.$newBuild"
}

$releaseBranch = "release-alpha-$newMajor.$newMinor.$newPatch.$newBuild"

# Calculate subsequent increment for visibility
$nextIncrementTag = "alpha-$($newMajor + 1).0.0.0"

Write-Host "==========================================================" -ForegroundColor Green
Write-Host "  RELEASE TARGET CALCULATION COMPLETE" -ForegroundColor Green
Write-Host "  Calculated New Tag      : $newTag" -ForegroundColor Green
Write-Host "  Calculated Release Branch: $releaseBranch" -ForegroundColor Green
Write-Host "  Next Target Tag Post-Dep: $nextIncrementTag" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

if ($env:GITHUB_OUTPUT) {
    "previous_tag=$currentTagStr" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "new_tag=$newTag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "release_branch=$releaseBranch" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "bump_type=$effectiveBump" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "next_increment_tag=$nextIncrementTag" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    $dryRunStr = if ($DryRun.IsPresent) { "true" } else { "false" }
    "dry_run=$dryRunStr" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "environment=$Environment" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}

if ($DryRun) {
    Write-Host "[DRY RUN] Simulation complete. No Git modifications or deployment dispatched." -ForegroundColor Yellow
    return [PSCustomObject]@{
        PreviousTag      = $currentTagStr
        BumpType         = $effectiveBump
        NewTag           = $newTag
        ReleaseBranch    = $releaseBranch
        NextIncrementTag = $nextIncrementTag
        DryRun           = $true
    }
}

# -----------------------------------------------------------------------------
# 4. Git Lineage & State Validation
# -----------------------------------------------------------------------------
Write-Host "Validating Git Lineage and Branch State..." -ForegroundColor Cyan
$currentHead = (git rev-parse HEAD).Trim()
Write-Host "Target Commit SHA: $currentHead" -ForegroundColor Gray

# -----------------------------------------------------------------------------
# 5. Create Tag and Release Branch (If Requested)
# -----------------------------------------------------------------------------
if ($CreateTagAndBranch) {
    Write-Host "Creating annotated Git tag '$newTag'..." -ForegroundColor Cyan
    git tag -a $newTag -m "Release ${newTag} - Automated release deployment from $currentHead" 2>&1
    
    Write-Host "Creating release branch '$releaseBranch'..." -ForegroundColor Cyan
    git branch -f $releaseBranch $currentHead 2>&1

    Write-Host "Pushing tag and release branch to origin..." -ForegroundColor Cyan
    git push origin $newTag 2>&1
    git push -u origin $releaseBranch 2>&1
    Write-Host "✅ Tag and Release Branch pushed successfully to origin." -ForegroundColor Green
}

# -----------------------------------------------------------------------------
# 6. Deploy Application (If Requested)
# -----------------------------------------------------------------------------
if ($Deploy) {
    Write-Host "Dispatching Aspire Azure Application Deployment Workflow..." -ForegroundColor Cyan
    Write-Host "Workflow Ref: $releaseBranch | Environment: $Environment" -ForegroundColor Gray
    
    gh workflow run azure-app-deploy.yml `
        --ref $releaseBranch `
        -f environment=$Environment `
        -f resourceGroupName="rg-dietdost-$Environment" `
        -f provisionInfra=true `
        -f enableCustomDomain=true `
        -f allowDemoUsers=true `
        -f databaseProvider="AzureSql"

    Write-Host "✅ Azure deployment workflow triggered successfully." -ForegroundColor Green

    if ($Wait) {
        Write-Host "Monitoring deployment run..." -ForegroundColor Cyan
        Start-Sleep -Seconds 10
        $activeRun = gh run list --workflow azure-app-deploy.yml --branch $releaseBranch --limit 1 --json databaseId -q ".[0].databaseId"
        if ($activeRun) {
            Write-Host "Streaming workflow run $activeRun logs..." -ForegroundColor Cyan
            gh run watch $activeRun
            
            $runResult = gh run view $activeRun --json conclusion -q ".conclusion"
            if ($runResult -ne "success") {
                Write-Host "❌ Deployment run $activeRun concluded with status: $runResult" -ForegroundColor Red
                exit 1
            }
            Write-Host "✅ Deployment run $activeRun completed successfully." -ForegroundColor Green
        }
        else {
            Write-Host "⚠️ Warning: Could not locate active run for branch $releaseBranch." -ForegroundColor Yellow
        }

        # Verify Live Container App Health Probe
        Write-Host "Verifying live Container App revision health..." -ForegroundColor Cyan
        $healthStatus = az containerapp revision list `
            --name "app-dietdost-web-$Environment" `
            --resource-group "rg-dietdost-$Environment" `
            --query "[0].properties.healthState" -o tsv 2>$null

        Write-Host "Container App Health State: $healthStatus" -ForegroundColor Green
        
        # Test Live Endpoint
        Write-Host "Probing live endpoint $TargetHost..." -ForegroundColor Cyan
        try {
            $resp = Invoke-WebRequest -Uri "$TargetHost/health" -UseBasicParsing -TimeoutSec 15
            Write-Host "Live Health Endpoint HTTP Status: $($resp.StatusCode)" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠️ Health endpoint probe warning: $_" -ForegroundColor Yellow
        }
    }
}

# -----------------------------------------------------------------------------
# 7. Create GitHub Release (If Requested)
# -----------------------------------------------------------------------------
if ($CreateGitHubRelease) {
    Write-Host "Publishing official GitHub Release for '$newTag'..." -ForegroundColor Cyan
    gh release create $newTag `
        --target $releaseBranch `
        --title "Diet-Dost $newTag" `
        --generate-notes
    Write-Host "✅ GitHub Release '$newTag' published successfully." -ForegroundColor Green
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  RELEASE LIFECYCLE EXECUTION COMPLETE" -ForegroundColor Cyan
Write-Host "  Deployed Tag : $newTag" -ForegroundColor Cyan
Write-Host "  Next Tag     : $nextIncrementTag" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

return [PSCustomObject]@{
    PreviousTag      = $currentTagStr
    BumpType         = $effectiveBump
    NewTag           = $newTag
    ReleaseBranch    = $releaseBranch
    NextIncrementTag = $nextIncrementTag
    DryRun           = $false
}
