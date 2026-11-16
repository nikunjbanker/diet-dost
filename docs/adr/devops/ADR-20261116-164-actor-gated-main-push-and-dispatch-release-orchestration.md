<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261116-164: Combined Actor and Main Branch Gating for Automated Release Pipeline

- **Status**: Accepted
- **Date**: 2026-11-16
- **Domain**: DevOps / CI-CD / Release Governance
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: #149
- **Refines**: ADR-161, ADR-163

---

## 1. Context & Problem Statement
In ADR-161, `.github/workflows/auto-release-on-merge.yml` auto-triggered on all pushes to `main`. In ADR-163, this was shifted to on-demand `workflow_dispatch` only. While manual dispatch protected against unintended deployments, repository maintainers desired the convenience of continuous deployment whenever the project lead (`@nikunjbanker`) merges or pushes changes to `main`, while strictly preventing automated releases when other contributors, bots, or unverified actors merge changes.

---

## 2. Decision & Implementation

### 2.1 AND Condition: `main` Branch AND Pushed by `@nikunjbanker`
Re-enabled the `push: branches: [main]` trigger in `.github/workflows/auto-release-on-merge.yml`, alongside `workflow_dispatch`, while enforcing an explicit composite `AND` condition across all three workflow stages:
```yaml
if: |
  github.actor == 'nikunjbanker' &&
  (github.ref_name == 'main' || github.ref == 'refs/heads/main')
```

### 2.2 Operational Behavior
1. **Push / Merge to `main` by `@nikunjbanker`**:
   - Triggers automatically.
   - Evaluates commit for Conventional Commits semantic bump (`Major`, `Minor`, `Patch`).
   - Cuts tag (`alpha-<version>`) and release branch (`release-alpha-<version>`).
   - Deploys application to Azure Container Apps in `rg-dietdost-dev` (`dev.diet-dost.in`).
   - Verifies container revision health (`Healthy` / HTTP 200).
   - Publishes official GitHub Release.
2. **Push / Merge to `main` by Any Other Actor (Bots / Collaborators)**:
   - Condition `github.actor == 'nikunjbanker'` evaluates to `false`.
   - All jobs are skipped immediately. Zero cloud deployments or tags occur.
3. **Manual Dispatch via `workflow_dispatch`**:
   - Maintainer `@nikunjbanker` can manually trigger a release and override bump type, tag, or environment at any time.

---

## 3. Verification & Compliance Evidence
- **Automated Contract Tests**: Updated `tests/Nutrition.EvalHarness.Tests/ReleasePipelineContractTests.cs` to assert:
  - Presence of `branches: - main` push trigger.
  - Presence of `workflow_dispatch:` trigger.
  - Presence of `github.actor == 'nikunjbanker'` authorization gate.
  - Presence of `github.ref_name == 'main'` branch guard.
  - All 7 contract tests passed cleanly with 0 failures.
- **Zero-Warning Standard**: Entire solution builds with 0 warnings and 0 errors under .NET 11.
- **AI Security Defense**: All 7 vectors verified and passed (`verify-ai-security-defense.ps1 -Mode All`).

