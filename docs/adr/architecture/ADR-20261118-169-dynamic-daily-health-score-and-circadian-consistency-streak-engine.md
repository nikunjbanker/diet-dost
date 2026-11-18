<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261118-169: Dynamic Daily Health Score & Circadian Consistency Streak Engine

- **Status**: Accepted
- **Date**: 2026-11-18
- **Domain**: Architecture / Clinical Dietetics / Presentation
- **Deciders**: Diet-Dost Engineering & Clinical Nutrition Architecture Team
- **Tracking Issue**: #169

---

## 1. Context & Problem Statement
Prior to this architectural enhancement, the **Daily Health Score** (0–100) and **Consistency Streak** (days) were pseudo-static or hardcoded:
1. `DailyCalorieLedger.cs` initialized `HealthScore = 85` and `ConsistencyStreakDays = 1`. If zero meals were logged on a day, recalculation evaluated to `100` points because no over-budget or limit deductions were triggered.
2. `ConsistencyStreakDays` was never dynamically calculated based on actual meal logging history.
3. Presentation layers (`hero-hud.html`, `companion-card.html`, `daily-hud.js`, `share-service.js`, `ShareCardRenderer.js`) contained static fallbacks (`85`, `Wholesome Compliance`, `7-Day Consistency Streak`, `|| 7`, `|| 88`), leading to UI desynchronization and misleading patient feedback.

---

## 2. Decision & Implementation

### 2.1 Multi-Pillar ICMR-NIN 2024 Daily Health Score ($S \in [0, 100]$)
Implemented a scientific, multi-pillar diet quality formula in `DailyCalorieLedger.RecalculateLedger`:
1. **Morning Baseline / Zero Logs**: If $N_{\text{meals}} = 0$ or $C_{\text{consumed}} < 50$, `HealthScore = 0`, `HealthScoreStatusText = "Awaiting First Meal 🌅"`, and `EarnedBadges.Clear()`.
2. **Pillar 1: Caloric Budget Adherence ($P_{\text{calorie}}$, Max 30 pts)**: Penalizes both over-budget spikes ($-1$ pt per 25 kcal) and severe starvation underfeeding ($< 500$ kcal).
3. **Pillar 2: Protein Adequacy ($P_{\text{protein}}$, Max 25 pts)**: Evaluates $R_{\text{protein}} = \text{Consumed} / \text{Target}$. $\ge 85\% \implies 25\text{ pts}$; $65\%-84\% \implies 18\text{ pts}$; $45\%-64\% \implies 12\text{ pts}$; $< 45\% \implies 6\text{ pts}$.
4. **Pillar 3: Clinical Moderation ($P_{\text{moderation}}$, Max 25 pts)**: ICMR-NIN 2024 ceilings: Visible cooking oil $\le 25\text{g}$ (10 pts), Free added sugar $\le 25\text{g}$ (8 pts), Sodium $\le 2,000\text{mg}$ / $1,500\text{mg}$ (7 pts). Pro-rated if underfed to prevent empty unearned scores.
5. **Pillar 4: Dietary Fiber ($P_{\text{fiber}}$, Max 10 pts)**: ICMR-NIN 2024 target $\ge 30\text{g}$. $\ge 25\text{g} \implies 10\text{ pts}$; $15\text{g}-24\text{g} \implies 6\text{ pts}$; $< 15\text{g} \implies 2\text{ pts}$.
6. **Pillar 5: Hydration & Habits ($P_{\text{habits}}$, Max 10 pts)**: Water intake ($\ge 2.5\text{L} \implies 5\text{ pts}$) + circadian cadence ($\ge 2$ meal types logged $\implies 5\text{ pts}$).

### 2.2 Qualitative Classification & Badge Engine
- Dynamic Status Text: `Awaiting First Meal 🌅` ($0$), `Needs Attention ⚠️` ($1-49$), `Fair Adherence ⚖️` ($50-69$), `Good Progress 🥗` ($70-84$), `Optimal ICMR Adherence 🌟` ($85-100$).
- Dynamic Badges: *Tadka Ninja 🥷*, *Salt Sentry 🛡️*, *Protein Champion 🏆*, *Fiber Crusader 🌾*, *Deficit Sniper 🎯*, *Sugar Guardian 🍯*, *Hydration Maharaja 💧*.

### 2.3 Timezone-Aware Circadian Consistency Streak Engine
Implemented `ClinicalCalculators.CalculateConsistencyStreak`:
- **Local Timezone Alignment**: Buckets meals using `TimeZoneInfo.ConvertTimeFromUtc(meal.LoggedAt, userTz)` (`profile.Timezone ?? "Asia/Kolkata"`).
- **Qualifying Day Threshold**: At least 1 logged, non-deleted meal with $\ge 100\text{ kcal}$.
- **Active Grace Window**: If yesterday was logged but today has no meals yet, the streak remains displayed as active (`🔥 N-Day Consistency Streak`) until midnight local time.
- Integrated into `ClinicalDietitianService.GetOrCreateDailyLedgerAsync` with 0 extra database roundtrips.

### 2.4 Presentation & Export Synchronization
- `hero-hud.html`: Initial placeholders render `--`, `Awaiting First Meal 🌅`, and `<span class="badge-chip empty">Log meals to earn badges ✨</span>`.
- `companion-card.html`: Targets `#streak-counter-text` without overwriting the SVG fire icon.
- `daily-hud.js`: Synchronously updates score circle, score status text, streak counter, and dynamic badge chips.
- `ShareCardRenderer.js` & `share-service.js`: Removed hardcoded `|| 7` and `|| 88`, using exact dynamic metrics.

---

## 3. Adversarial Threat Review & Defense Verification (Confidence: 98%)
Evaluated against 9 adversarial attack vectors:
1. **Timezone Divergence**: Mitigated via explicit IANA/Windows timezone conversion.
2. **Phantom Morning 100 Score**: Mitigated via zero-meal early exit (`Score = 0`, status `"Awaiting First Meal 🌅"`).
3. **Starvation Masking**: Mitigated via severe calorie underfeeding scaling.
4. **Junk Food Calorie Matching**: Mitigated via multi-pillar nutrient density evaluation.
5. **Deleted Meal Inflation**: Mitigated via EF Core query filter `!IsDeleted` and in-memory guards.
6. **Future Timestamp Spoofing**: Mitigated via clock-skew filter `LoggedAt <= UtcNow.AddHours(1)`.
7. **Static HTML Desync**: Mitigated via neutral `--` placeholders and zero-CLS hydration.
8. **Social Share Desync**: Mitigated via direct dynamic property references.
9. **Stale Badge Leak**: Mitigated via dynamic placeholder `<span class="badge-chip empty">...</span>`.

---

## 4. Verification & Compliance Evidence
- **Domain Tests**: 65/65 tests passed in `Nutrition.Domain.Tests` (including 9 new clinical score & streak tests).
- **Eval Harness**: 423/423 tests passed in `Nutrition.EvalHarness.Tests` (including updated BFF contract snapshots).
- **Zero Warnings**: Compiles with 0 warnings, 0 errors targeting .NET 11.

