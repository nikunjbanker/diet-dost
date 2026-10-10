<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Claude Code Guidelines: Diet-Dost (.NET 11, Aspire, Clean Architecture)

This document provides concise, actionable instructions for Claude Code when developing, refactoring, or testing in the **Diet-Dost** repository.

## Commands

```bash
# Build (Mandatory 0 warnings, 0 errors)
dotnet build DietDost.slnx

# Run tests (242 automated tests across 7 projects)
dotnet test --nologo

# Run specific project tests
dotnet test tests/Nutrition.Domain.Tests/Nutrition.Domain.Tests.csproj
dotnet test tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj

# Run pre-commit AI Security Defense validator
pwsh -File scripts/verify-ai-security-defense.ps1

# Run live 5-tier end-to-end customer acceptance tests (port 5240)
pwsh -File tests/validate_e2e_tiers.ps1

# Sync ADR index and domain registry
pwsh -File scripts/sync-adr-index.ps1
```

## Solution Architecture & Key Invariants

1. **Target Runtime**: Strictly `.NET 11` (`net11.0`) across all 7 projects in `DietDost.slnx`.
2. **Native Zero-Dependency CQRS**:
   - **Never import MediatR**: Use native `IDispatcher`, `ICommandHandler<TCommand, TResult>`, and `IQueryHandler<TQuery, TResult>`.
   - Handlers live in `Nutrition.Application`.
   - Controllers in `Nutrition.WebGateway` are thin: extract claims, dispatch via `_dispatcher`, map `Result<T>` to HTTP status.
3. **Database & Persistence**:
   - EF Core with SQLite, configured in `StorageInfrastructureExtensions`.
   - Universal UTC conversion via `ValueConverter<DateTime, DateTime>` (`DateTimeKind.Utc`).
   - Azure Files SMB share mount at `/app/data/diet_dost.db` with `PRAGMA journal_mode = DELETE;`.
   - Single replica invariant: `minReplicas: 1, maxReplicas: 1` on Azure Container Apps.
4. **Clinical Dietetics (ICMR-NIN 2024 & WHO)**:
   - **Zero-Assumption Rule**: Never guess or estimate missing patient health metrics. Throw `InvalidOperationException` or return HTTP 422 if metrics are missing.
   - Asian-Indian BMI thresholds: Normal 18.5–22.9, Overweight 23.0–24.9, Obese $\ge 25.0$.
   - Safety floors: 1,200 kcal/day (female), 1,500 kcal/day (male).
   - Free sugar ceiling: < 25g/day. Dietary fiber: 30g/day.
5. **AI Vision & Prompt Security**:
   - Multi-model cascade: Gemini 3 Flash $\to$ Gemini 2.5 Flash $\to$ Gemini 2.5 Pro.
   - All inputs must pass `PromptShieldValidator` (checks Harmful, Violent, Sexual, Communal, and Self-Learning data poisoning).
   - Gemini API calls must declare explicit `StrictSafetySettings` (`BLOCK_LOW_AND_ABOVE`).
6. **Security & Secrets Governance**:
   - Contributor `@nikunjbanker` holds sole administrative authority over secrets.
   - Zero hardcoded secrets: inject via environment variables or Azure Key Vault.
   - Key Vault has purge protection enabled (`enablePurgeProtection: true`).
   - Demo credentials are guarded with `#if DEBUG` and auto-deactivated in Release mode.
7. **UI/UX Design System (`DESIGN.md`)**:
   - Linear.app Obsidian dark surface ladder: Canvas `#010102`, Surface 1 `#0f1011`, Surface 2 `#141516`.
   - Native ES Modules with zero bundler. HTML partials via `main.js` `[data-include]`.
   - Hairline borders (`1px solid rgba(255,255,255,0.06)`). Muted accents (Violet `#5e6ad2`, Emerald `#27c380`).

## Git & PR Governance

- **Zero Direct-to-Main**: Direct commits or pushes to `main` are strictly prohibited.
- Always fetch remote before branching: `git fetch origin && git checkout -b feature/<name> origin/main`.
- For stacked PRs on an in-flight branch, branch off `origin/feature/<parent>` and use `gh stack link`.
- In case of doubt or ambiguity, ask the user before making unilateral architectural decisions.
- Record architectural changes in atomic fragments: `docs/adr/<domain>/ADR-<YYYYMMDD>-<NNN>-<slug>.md` and run `scripts/sync-adr-index.ps1`.
