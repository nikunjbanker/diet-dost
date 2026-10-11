<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Mandatory Git Branching & PR-Only Merge Mandate

## 1. Zero Direct-to-Main Policy
Direct commits or direct pushes to `main` are **strictly prohibited**. All merges must execute via GitHub Pull Requests.

## 2. Stale-Branch Prevention (Remote Fetch Before Branching)
1. **Always fetch latest remote state first**:
   ```bash
   git fetch origin
   ```
2. **For Independent Work (Branching off `main`)**:
   Always branch explicitly from `origin/main`:
   ```bash
   git checkout -b feature/<descriptive-name> origin/main
   git checkout -b fix/<defect-name> origin/main
   git checkout -b docs/<topic-name> origin/main
   ```
3. **Mandatory Lineage Verification Guard**:
   Assert commit hash match:
   ```bash
   git rev-parse HEAD
   git rev-parse origin/main
   # Both hashes MUST match identically.
   ```

## 3. GitHub Stacked PR Protocol (For Consecutive / In-Flight PRs)
When work builds upon an active unmerged PR on `feature/<parent-feature>`:
1. Fetch and branch off the remote parent tracking branch:
   ```bash
   git fetch origin
   git checkout -b feature/<child-feature> origin/feature/<parent-feature>
   ```
2. Set the GitHub PR Base branch to `feature/<parent-feature>` (NOT `main`).
3. Link and synchronize stack via GitHub CLI:
   ```bash
   gh stack link <parent-pr-number> <child-pr-number>
   gh stack sync
   ```
4. Embed the Stack Navigator Callout widget at the top of the PR body:
   ```markdown
   > [!NOTE]
   > ### 🥞 GitHub Stack #<id> (Layer X of Y)
   > 1. 🟢 **PR #<parent>**: `<title>` (Base: `<base>`)
   > 2. 🟡 **PR #<child> (This PR)**: `<title>` (Base: `<parent-branch>`)
   ```

## 4. Pre-PR Verification Gate
1. `dotnet build DietDost.slnx` (0 warnings, 0 errors on .NET 11).
2. `dotnet test --nologo` (100% pass rate).
3. `pwsh -File scripts/verify-ai-security-defense.ps1` (5 AI security vectors green).
4. `pwsh -File tests/validate_e2e_tiers.ps1` (Live 5-tier CFT on port 5240).
5. Synchronize ADRs via `pwsh -File scripts/sync-adr-index.ps1`.
