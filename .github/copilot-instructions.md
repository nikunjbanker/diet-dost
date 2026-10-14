<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# GitHub Copilot Repository Instructions: Diet-Dost

This file provides system context, architectural guidelines, and engineering standards for GitHub Copilot (Chat, Workspace, and Code Completion) when working on the **Diet-Dost** repository.

---

## 1. Solution Overview & Technology Stack
- **Framework & Runtime**: Strictly **.NET 11** (`<TargetFramework>net11.0</TargetFramework>`) across all projects.
- **Orchestration**: .NET Aspire (`Aspire.AppHost.Sdk/13.5.4`). Solution file: `DietDost.slnx`.
- **Database & Persistence**: Entity Framework Core with SQLite (Universal UTC `ValueConverter`, `PRAGMA journal_mode=DELETE` on network SMB share). Swappable via `StorageInfrastructureExtensions`.
- **Frontend / PWA**: Native ES Modules, Vanilla CSS with custom properties (`styles.css`), HTML partials loader (`main.js` with `[data-include]`), Linear.app Obsidian dark design system (`DESIGN.md`).
- **AI & Multimodal Vision**: Microsoft Agent Framework with Google AI Gemini multimodal cascade (Gemini 3 Flash $\to$ Gemini 2.5 Flash $\to$ Gemini 2.5 Pro) with `PromptShieldValidator` content safety.
- **Cloud Infrastructure**: Azure Container Apps (Single Replica Invariant: `minReplicas: 1, maxReplicas: 1`), Azure Files persistent SMB share mounted at `/app/data`, Azure Key Vault with purge protection, Bicep IaC (`infra/*.bicep`).

---

## 2. Core Architectural Invariants

### 2.1 Native Clean Architecture & CQRS (Zero MediatR)
- **Do NOT introduce MediatR or external mediator packages**: Diet-Dost uses a native, zero-dependency CQRS dispatcher via `Microsoft.Extensions.DependencyInjection` (`IDispatcher`, `ICommandHandler<TCommand, TResult>`, `IQueryHandler<TQuery, TResult>`).
- **Thin Controllers**: Controllers in `Nutrition.WebGateway` must contain zero business logic or EF Core queries. They bind request parameters, dispatch commands/queries via `_dispatcher`, and return standardized `Result<T>` responses.
- **Universal Result Envelope**: Handlers return `Result<T>` with boolean `IsSuccess`, payload `Data`, error message, and HTTP status codes (`Result.Success`, `Result.Failure`, `Result.Forbidden`).

### 2.2 Clinical Dietetics Governance (Zero Assumption Rule)
- Adhere strictly to **ICMR-NIN 2024** and **WHO South Asian Medical Guidelines**.
- **The Zero-Assumption Intake Invariant**: NEVER invent, extrapolate, or hallucinate missing patient health metrics. If age, gender, height, weight, activity multiplier, or clinical condition is missing, throw `InvalidOperationException` or return HTTP 422 with a structured intake questionnaire.
- Free sugar ceiling: < 25g/day. Dietary fiber target: 30g/day.

### 2.3 AI Prompt Shield & Content Safety (OWASP Top 10 for LLM)
- All prompt construction and inputs must pass through `PromptShieldValidator`:
  - **Harmful / Violent**: Weapons, murder, physical violence, and self-harm strictly blocked.
  - **Sexually Explicit**: Adult and pornographic terms blocked.
  - **Communal & Hate Speech**: Religious hatred and sectarian slurs blocked.
  - **Prompt Injections & Jailbreaks**: Delimiter overrides ("DAN", system prompt extraction) blocked.
- Gemini API payloads must declare explicit `StrictSafetySettings` (`BLOCK_LOW_AND_ABOVE`).

### 2.4 Security & Secrets Governance
- **Sole Administrative Authority**: Contributor `@nikunjbanker` holds sole administrative authority over solution secrets, Azure Key Vault policies, and credentials.
- **Zero Hardcoded Secrets**: Never commit API keys, connection strings, or JWT keys into source code. Gitleaks enforces this in CI.
- **Debug-Only Demo Isolation**: Demo users (`free@`, `basic@`, `premium@`, `admin.demo@`, `superadmin@dietdost.app`) are guarded by `#if DEBUG` and `IAppEnvironment.AllowsDemoUsers`. In Release/production mode, demo seeding is suppressed and login returns `403 Forbidden`.

---

## 3. UI/UX Design System Guidelines (`DESIGN.md`)
- Single source of truth: [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md).
- **Surface Ladder**: Canvas (`#010102`), Surface 1 (`#0f1011`), Surface 2 (`#141516`), Surface 3 (`#18191a`), Surface 4 (`#191a1b`).
- **Borders & Radii**: Hairline borders (`border-hairline`, 1px solid `rgba(255,255,255,0.06)`), 8px card radius, 6px button radius.
- **Typography**: Inter / Geist Sans for body; JetBrains Mono for tabular numbers and nutritional data.
- **Zero Bright Saturated Colors**: Use muted Linear Violet (`#5e6ad2`), Emerald (`#27c380`), and Amber (`#e5a93c`).
- **Static Vector Armor**: Use vector placeholders (`placeholder-meal.svg`, `placeholder-progress.svg`) without `<script>` tags.

---

## 4. Standard Verification & Build Commands

```bash
# Build the entire solution (assert 0 warnings, 0 errors)
dotnet build DietDost.slnx

# Run all 234 automated tests
dotnet test --nologo

# Run the pre-commit AI Security Defense validator
pwsh -File scripts/verify-ai-security-defense.ps1

# Run the 5-tier Customer Acceptance Test (requires app running on port 5240)
pwsh -File tests/validate_e2e_tiers.ps1

# Synchronize the Architectural Decision Record (ADR) registry and index.json
pwsh -File scripts/sync-adr-index.ps1
```

---

## 5. Living Documentation & ADR Protocol
- Never append to monolithic log files.
- Record any architectural decision in an atomic file: `docs/adr/<domain>/ADR-<YYYYMMDD>-<NNN>-<slug>.md` (domains: `architecture`, `security`, `devops`, `presentation`, `governance`).
- Run `pwsh -File scripts/sync-adr-index.ps1` to update `docs/adr/index.json` (~4 KB) and `docs/adr/README.md`.
