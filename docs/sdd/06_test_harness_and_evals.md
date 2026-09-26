<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Test Harnesses, AI Vision Evals & Quality Engineering
> **Specification Version**: `v1.3.1 (Production & Living SDD)`  
> **Methodology**: Closed-Loop Harness Engineering & Contract-First Validation  
> **Test Frameworks**: xUnit, Microsoft.NET.Test.Sdk, Playwright / Browser Agent  

---

## 1. Quality Engineering Blueprint

In adherence with Skill §7.1 and §7.2, all implementations must satisfy eight verification tiers:
1. **Clinical Dietetics Unit Test Suite (`Nutrition.Domain.Tests`)**: Validates Mifflin-St Jeor math, WHO Asian-Indian BMI cutoffs, safety floors, deficit ceilings, and the clinical adjustment matrix.
2. **AI Multimodal Vision Evaluation Suite (`Nutrition.Vision.Evals`)**: Evaluates JSON schema compliance, portion heuristic tolerances, confidence gating threshold ($\ge 70\%$), Gemini 3 Flash thinking token support, and multi-model fallback cascade.
3. **Quantity Detection & Macro Recalculation**: Validates natural portion strings (`1.5 Cup`, `1 Katori`, `5-6 Slices`, `2 Phulkas`) parsing to numeric multipliers and synchronous recalculation of all 6 macronutrients.
4. **Dietary Fiber & Free Sugar Tracking**: Validates 30g/day Dietary Fiber (ICMR-NIN) target and < 25g/day Free Sugar (WHO threshold) tracking across meals, food items, and daily ledgers.
5. **Universal UTC Persistence & Timezone Normalization**: Validates EF Core `ValueConverter` converting all `DateTime` instances to UTC on write and restoring `DateTimeKind.Utc` on read, with circadian boundaries correctly calculated across user timezones.
6. **Food Diary & In-Place Meal Management**: Validates period filtering (1D, 7D, 30D, 90D, 365D), switchable Card/Grid views, Excel export formatting, and in-place meal deletion with daily ledger recalculation.
7. **Database Resilience & Integrity Verification**: Validates schema-aware idempotent column migrations (`PRAGMA table_info`), EF Core collection `ValueComparer` instances (preventing change-tracking loss and eliminating model configuration warnings), and non-PII diagnostic error logging.
8. **Universal SVG Fallback Verification**: Validates capturing-phase error listener substituting broken/missing image URIs with Obsidian Dark vector placeholders (`placeholder-meal.svg`, `placeholder-progress.svg`).

---

## 2. Benchmark Evaluation Fixtures

| Fixture ID | Dish & Component / Test Scenario | Expected Outcome | Evaluation Criteria |
|---|---|---|---|
| **FIX-01** | 2 Phulkas + 1 Katori Dal Tadka + Cucumber Salad | 380 kcal ± 10%, 14g Protein | Confidence $\ge 70\%$, Auto-populated in Review modal |
| **FIX-02** | 1 Masala Dosa + Sambar + Coconut Chutney | 450 kcal ± 10%, 8g Protein | Sodium flag triggered (>500mg), Cooking oil tracked |
| **FIX-03** | Blurry / Dark meal image (<1KB or occlusion) | N/A | Confidence $< 70\%$, Retake prompt triggered, 1-tap retake enabled |
| **FIX-04** | Metformin + Telmisartan with Coconut Water | Clinical Warning | Potassium alert triggered for ARB/ACE inhibitor |
| **FIX-05** | Model Transparency Badge Verification | AI Model Tag Visible in UI | If `AI:ShowModelDetails=true`, display badge with model name in review modal |
| **FIX-06** | Database Startup Schema Migration | 0 SQL CommandErrors | `PRAGMA table_info` checks prevent `duplicate column name` exceptions on repeated runs |
| **FIX-07** | Aspire Tracing Payload Inspection | Request & Response in Trace | `http.request.body` and `http.response.body` visible in Aspire trace details |
| **FIX-08** | GenAI Semantic Span Tagging | Child Span `ai.food_detection` | `gen_ai.system_prompt`, `user.diagnosed_conditions`, `user.medications` captured |
| **FIX-09** | Non-PII Diagnostic Logging | Sanitized Error Logs | Zero patient names, raw weights, or clinical metrics in repository error logs |
| **FIX-10** | Quantity Multiplier & Macro Recalculation | Instant reactive math | Updating portion from `1 Katori` to `1.5` updates calories, protein, carbs, fat, fiber, sugar by 1.5x |
| **FIX-11** | Dietary Fiber & Sugar Threshold Warnings | Clinical Advice generation | Meals with > 10g free sugar trigger WHO sugar warning; daily fiber deficiency highlighted |
| **FIX-12** | Universal UTC Temporal Persistence | ISO 8601 UTC in SQLite | Datetime values stored in SQLite are UTC strings; queries restore `DateTimeKind.Utc` |
| **FIX-13** | Food Diary Period & Export Generation | Complete 6-macro sheet | Filter by 1D/7D/30D/90D/365D; Excel export generates structured CSV with all 6 macros and cooking fat |
| **FIX-14** | Universal SVG Fallback Interception | Zero broken image icons | Inaccessible photo URIs instantly render Obsidian `placeholder-meal.svg` or `placeholder-progress.svg` |

---

## 3. CLI Test Execution

```bash
# Run all unit and eval harness suites (29 tests)
dotnet test --logger "console;verbosity=detailed"

# Validate WebGateway builds with zero warnings or errors
dotnet build src/Nutrition.WebGateway/Nutrition.WebGateway.csproj
```
