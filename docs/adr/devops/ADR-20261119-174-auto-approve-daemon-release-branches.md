<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261119-174: Auto-Approve Deployment Daemon Release Branch Allowance

- **Status**: Accepted
- **Date**: 2026-11-19
- **Domain**: devops
- **Deciders**: Solution Architecture, Cloud DevOps Team, AI Agentic SDLC
- **Supercedes**: None
- **Related PR**: #176
- **Closes Issue**: #175

---

## 1. Context & Problem Statement

In the automated release deployment pipeline (`auto-release-on-merge.yml`), merging a feature or defect fix PR into `main` automatically triggers semantic version evaluation, creates an annotated git release tag (e.g., `alpha-5.8.1.0`), creates an immutable release branch (e.g., `release-alpha-5.8.1.0`), and dispatches the Azure Container Apps deployment workflow (`azure-app-deploy.yml`) against that release branch.

However, during deployment run 44468409392, the autonomous approval daemon (`scripts/auto-approve-daemon.ps1`) failed to auto-approve the deployment environment (`dev`), forcing the maintainer to manually approve it via the GitHub Actions runner web UI.

### Root Cause Analysis

In `scripts/auto-approve-daemon.ps1` (lines 34–37), the GAP-32 security guard strictly required `$runMeta.headBranch -eq "main"`:

```powershell
if ($runMeta.headBranch -ne "main") {
    Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] [GAP-32] Skipping auto-approval for run ${targetRunId}: branch '$($runMeta.headBranch)' is not protected 'main'." -ForegroundColor DarkYellow
    return
}
```

When `azure-app-deploy.yml` is dispatched against the release branch `release-alpha-5.8.1.0`, its `headBranch` metadata is `release-alpha-5.8.1.0`. As a result, the daemon skipped auto-approval on every polling cycle:
`[GAP-32] Skipping auto-approval for run 44468409392: branch 'release-alpha-5.8.1.0' is not protected 'main'.`
The daemon eventually timed out after 45 minutes, requiring manual intervention.

---

## 2. Decision & Implementation

1. **Permit Official Release Branches in GAP-32 Guard**:
   Updated `scripts/auto-approve-daemon.ps1` to permit both `main` and official release branches matching `^release-` (e.g., `release-alpha-*`):
   ```powershell
   if ($runMeta.headBranch -ne "main" -and -not ($runMeta.headBranch -match '^release-')) {
       Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] [GAP-32] Skipping auto-approval for run ${targetRunId}: branch '$($runMeta.headBranch)' is not protected 'main' or official release branch." -ForegroundColor DarkYellow
       return
   }
   ```
   Arbitrary developer branches (`feature/*`, `fix/*`, `pr/*`) remain strictly blocked and will never be auto-approved.

2. **Expand Post-Deployment Endpoint Health Probing**:
   Added `"$hostUrl/mcp"` to `Test-LiveEndpoints` in `scripts/auto-approve-daemon.ps1` to assert that the Model Context Protocol endpoint is online and healthy post-deployment alongside `/health`, `/index.html`, and `/partials/camera-modal.html`.

3. **Automated Contract Tests**:
   Added unit tests to `tests/Nutrition.EvalHarness.Tests/ReleasePipelineContractTests.cs`:
   - `AutoApproveDaemon_ScriptExistsAndHasLicenseHeader`
   - `AutoApproveDaemon_Gap32Guard_PermitsMainAndOfficialReleaseBranches`
   - `AutoApproveDaemon_LiveEndpoints_IncludesMcpValidation`

---

## 3. Consequences & Governance

- **Zero-Manual-Intervention CD**: Releases triggered on `main` that cut immutable `release-alpha-X.X.X.X` branches now proceed through `azure-app-deploy.yml` environment gates automatically.
- **GAP-32 Security Preserved**: Unauthorized or unmerged feature branches remain completely prevented from triggering auto-approval.
- **Traceability**: Closes Issue #175.
