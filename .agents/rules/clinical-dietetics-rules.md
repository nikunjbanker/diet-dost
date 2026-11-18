<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Clinical Dietetics Standards & Zero-Assumption Intake Invariant

## 1. Zero-Assumption Intake Invariant
- **NEVER invent, hallucinate, assume, or extrapolate missing patient health metrics**.
- If any required metric (Age, Gender, Height, Weight, Activity Multiplier, or Diagnosed Condition) is missing, code MUST throw `InvalidOperationException` or return HTTP 422 with a structured intake questionnaire.
- Defaulting missing patient metrics to arbitrary numbers (e.g., 25 years old or 70 kg) is a critical clinical violation.

## 2. Indian Medical Standards (ICMR-NIN 2024 & WHO South Asian)
- **WHO South Asian BMI Cutoffs**:
  - Underweight: $< 18.5$
  - Normal: $18.5 - 22.9$
  - Overweight: $23.0 - 24.9$
  - Obese: $\ge 25.0$
- **BMR Calculation**: Mifflin-St Jeor equation.
- **Safety Starvation Floors**:
  - Female: Strict floor of 1,200 kcal/day.
  - Male: Strict floor of 1,500 kcal/day.
- **Daily Deficit Ceilings**:
  - Maximum safe deficit: 500 kcal/day for sedentary/mild, 750 kcal/day absolute ceiling.
- **Nutritional Ceilings & Targets**:
  - Free Sugar Ceiling: $< 25$ g/day (WHO recommendation).
  - Dietary Fiber Target: $30$ g/day (ICMR-NIN 2024 guideline).
  - Cereal-to-Pulse Protein Ratio: Aim for $3:1$ ratio to balance amino acid profiles (Lysine/Methionine).

## 3. Clinical Adjustments Matrix
- **Type 2 Diabetes**: Carbohydrates capped at 45–50% of total calories, low Glycemic Index (GI) priority, high soluble fiber.
- **Hypertension (HTN)**: Sodium ceiling $< 2,000$ mg/day (5g table salt), potassium-rich whole foods promoted (unless on ACE/ARB).
- **Hypothyroidism**: TDEE multiplier reduced by 10–12% to reflect lowered basal metabolic rate.
- **NAFLD / Fatty Liver**: Saturated fat $< 7\%$ of daily energy, zero high-fructose syrups or refined sugars.

## 4. Dynamic Daily Health Score Rules (ICMR-NIN 2024 & HEI-2020)
- **Zero-Log Morning Baseline**: Score MUST display `--` with status `"Awaiting First Meal 🌅"` when zero meals are logged. Never display 85 or 100 on an empty day.
- **Multi-Pillar Evaluation (100 Points)**: Evaluates Caloric Envelope (30 pts), Protein Adequacy (25 pts), Clinical Moderation (Oil $\le 25\text{g}$, Sugar $< 25\text{g}$, Sodium $\le 2000\text{mg}$ / $1500\text{mg}$) (25 pts), Dietary Fiber $\ge 30\text{g}$ (10 pts), and Hydration/Habits (10 pts).
- **Starvation Penalty**: Severely underfed intake ($< 50\%$ budget) must be heavily penalized to prevent eating disorder / crash diet gaming.
- **Qualitative Status Brackets**:
  - `0`: `"Awaiting First Meal 🌅"`
  - `1–49`: `"Needs Attention ⚠️"`
  - `50–69`: `"Fair Adherence ⚖️"`
  - `70–84`: `"Good Progress 🥗"`
  - `85–100`: `"Optimal ICMR Adherence 🌟"`

## 5. Circadian Consistency Streak Rules
- **Local Timezone Mandate**: Day boundaries MUST be calculated in the user's local timezone (`profile.Timezone ?? "Asia/Kolkata"`). Never use server UTC.
- **Qualifying Day Threshold**: At least 1 logged, verified meal with $\ge 100\text{ kcal}$. Soft-deleted meals (`IsDeleted = true`) are strictly excluded.
- **Active Grace Window**: If yesterday was logged, today's streak remains displayed as active (`🔥 N-Day Consistency Streak`) until midnight local time.
- **Reset Condition**: Resets to 0 only if both today and yesterday have no qualifying meals.
