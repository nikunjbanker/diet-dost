<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Clinical Dietetics Specification (Indian Population)
> **Specification Version**: `v1.3.1 (Production & Living SDD)`  
> **Standards Basis**: ICMR-NIN 2024 Dietary Guidelines for Indians & WHO South Asian Consultation  
> **Target Population**: Adult Indian Male (Ref: 65 kg) & Female (Ref: 55 kg)  

---

## 1. Core Clinical Directives

### 1.1 Zero-Assumption Intake Rule
- **Never guess user metrics**: Age, biological sex, current weight, height, activity level, health conditions, or active medications must be explicitly provided.
- **Never guess preparation oil or ghee**: When food is logged, the engine prompts for visible cooking fat if ambiguous.
- **Validation Rule**: Incomplete profile intake triggers strict validation blocking target calculation until all required fields are satisfied.

### 1.2 ICMR-NIN 2024 & WHO South Asian Standards
1. **Cooking Oil Ceiling**: Maximum **20g to 25g visible cooking fat** per person per day (mustard, groundnut, sunflower, sesame, ghee).
2. **Cereal-to-Pulse Ratio**: Minimum **3:1 ratio** of cereals/millets to pulses to achieve complete essential amino acid profile (lysine + methionine complementation).
3. **WHO Asian-Indian BMI Cutoffs**:
   - Underweight: `< 18.5 kg/m²`
   - Normal / Healthy: `18.5 – 22.9 kg/m²` *(Cardiometabolic risk elevates above 23 kg/m² in South Asians)*
   - Overweight: `23.0 – 24.9 kg/m²`
   - Obese Class I: `25.0 – 29.9 kg/m²`
   - Obese Class II: `≥ 30.0 kg/m²`
4. **WHO Sodium Limits**: Maximum **5g salt/day (< 2,000 mg sodium/day)** for healthy individuals; **< 1,500 mg sodium/day (< 3.75g salt)** for hypertensive individuals.
5. **WHO Free Sugar Guidelines**: **< 5% of total caloric intake** (< 25g/day).
6. **WHO Trans Fat Elimination**: **< 1% of total energy**; vanaspati/dalda strictly flagged.

### 1.3 Caloric Safety Floors & Deficit Ceilings
- **Starvation Safety Floor**: Daily targets must **NEVER drop below 1,200 kcal/day for women or 1,500 kcal/day for men** without medical supervision.
- **Maximum Safe Deficit**: Deficit must **NEVER exceed 1,000 kcal/day** or a weight loss rate of **> 0.75 kg/week (recommended: 0.5 kg/week)** to prevent cholelithiasis, metabolic slowdown, and muscle wasting.

### 1.4 Circadian Day Boundaries & Timezone Alignment
- **Temporal Synchronization**: All biological metabolic events (meal timing, fasting intervals, daily deficits) are synchronized to the user's local `Timezone` (e.g. `Asia/Kolkata` IST).
- **Circadian Day Boundary**: A user's eating day is bucketed based on user-local civil midnight to midnight:
  $$\text{UserLocalDate} = \text{DateOnly.FromDateTime}(\text{TimeZoneInfo.ConvertTimeFromUtc}(\text{Meal.LoggedAtUtc}, \text{UserTz}))$$
- **Cross-Border Interoperability**: Regardless of server hosting region or UTC persistence format, the clinical dietitian engine dynamically computes meal groupings using the user's localized circadian day.

---

## 2. Energy & Macronutrient Formulas

### 2.1 Mifflin-St Jeor Basal Metabolic Rate (BMR)
- **Men**: $\text{BMR} = (10 \times \text{weight in kg}) + (6.25 \times \text{height in cm}) - (5 \times \text{age}) + 5$
- **Women**: $\text{BMR} = (10 \times \text{weight in kg}) + (6.25 \times \text{height in cm}) - (5 \times \text{age}) - 161$

### 2.2 Total Daily Energy Expenditure (TDEE) Multipliers
- **Sedentary (< 5,000 steps)**: $\text{BMR} \times 1.20$
- **Lightly Active (5,000 – 7,500 steps)**: $\text{BMR} \times 1.375$
- **Moderately Active (7,500 – 10,000 steps)**: $\text{BMR} \times 1.55$
- **Very Active (> 12,000 steps)**: $\text{BMR} \times 1.725$

### 2.3 Indian Macronutrient Target Distribution
- **Protein**: $1.2\text{g to } 1.6\text{g per kg of ideal body weight}$ (25–30% of energy).
- **Complex Carbohydrates**: 40–45% of total calories (Jowar, Bajra, Ragi, Whole wheat phulka, Brown rice).
- **Healthy Fats**: 25–30% of total calories, strictly tracking visible cooking fat.
- **Dietary Fiber Target**: Minimum **30g/day** (ICMR-NIN 2024 standard). Key sources: unpolished millets, whole pulses, leafy vegetables (palak, methi), salads (kakdi/cucumber), and psyllium husk.
- **Free Sugar Ceiling**: Maximum **< 25g/day (< 5% of total calories)** per WHO guidelines. Flag all refined sugars, jaggery (gur - GI 84), sweetened beverages, and mithai.

---

## 3. Clinical Matrix: Illness, Medication Adjustments & Food Prescriptions

| Condition & Medications | Clinical Calculation Adjustments | Therapeutic Indian Foods to Recommend | Contraindicated Foods | Critical Medication Interactions |
|---|---|---|---|---|
| **Diabetes / High Blood Sugar**<br>*(Metformin, Insulin, Glimepiride)* | • Net carbs capped to **35%–40%**.<br>• Max meal GL < 10.<br>• No crash deficits (>500 kcal). | • Whole moong, Methi paratha/dana.<br>• Karela, Jamun seed powder.<br>• Barley rotis, Besan chilla.<br>• Raw cucumber salad pre-meal. | • White rice, Maida, Poha, Sabudana.<br>• Gur / Jaggery *(GI 84)*.<br>• Fruit juices, sweetened chai. | On **Insulin/Sulfonylureas**: Never skip meals or prolonged fasting. Keep 15g fast glucose handy. |
| **Hypertension / High BP**<br>*(Amlodipine, Telmisartan, Ramipril)* | • Stricter sodium: **< 1,500 mg sodium/day**.<br>• Target Potassium: 3,500–4,700 mg/day (DASH). | • Lauki, Torai, Palak.<br>• Unsalted chaas with roasted jeera.<br>• Garlic (allicin), Flaxseed powder. | • Achaar, Papad, Namkeen, Sev.<br>• Baking soda in dhoklas/idlis.<br>• Packaged soup cubes, soy sauce. | With **ACE inhibitors / ARBs (Telmisartan)**: Prohibit potassium diet salts and excessive coconut water (fatal hyperkalemia risk). |
| **Hypothyroidism**<br>*(Levothyroxine: Thyronorm)* | • **TDEE reduced by 12%** to offset suppressed metabolic rate. | • Brazil nuts (Selenium), Sunflower seeds.<br>• Moringa / Drumstick, Pumpkin seeds.<br>• Iodized salt within sodium limits. | • **Raw goitrogens**: Raw cabbage, cauliflower, broccoli, radish.<br>• High-dose unfermented soy. | **Timing Rule**: Must be taken on an empty stomach with plain water. Prohibit tea, milk, breakfast, calcium/iron for **60 min**. |
| **High Cholesterol / Dyslipidemia**<br>*(Atorvastatin, Rosuvastatin)* | • Saturated fat capped to **< 7%**.<br>• Soluble fiber: 10g–15g/day. | • Isabgol (psyllium husk), Steel-cut oats.<br>• Walnuts, Chia seeds, Methi seeds.<br>• Cold-pressed mustard oil in moderation. | • Vanaspati, Dalda, Palm oil.<br>• Excessive Ghee (>1 tsp), Khoya/Mawa.<br>• Bakery puffs, fried street snacks. | Grapefruit / Pomelo (Chakotra) prohibited with Statins (CYP3A4 inhibition). |
| **PCOS / PCOD**<br>*(Metformin, Inositol)* | • High protein: **1.3g–1.5g/kg IBW**.<br>• Anti-inflammatory, low-GI carb focus. | • Spearmint tea (anti-androgen).<br>• Haldi-ginger infusion, Cinnamon.<br>• Sprouted lentils, Tofu, Paneer. | • Refined flours, dairy desserts, sugary milk tea. | Combine with post-meal 10-minute brisk walk for optimal insulin sensitization. |
| **Hyperuricemia / Gout**<br>*(Febuxostat, Allopurinol)* | • Hydration mandate: **3.5L to 4L/day**.<br>• Avoid acute caloric deprivation. | • Low-fat curd, Fresh cherries, Lemon water.<br>• Cucumber, Lauki, Oats. | • Organ meats, Sardines/Mackerel.<br>• High yeast extracts, HFCS.<br>• Concentrated pulses limited to 1 katori. | Avoid aggressive deficits which spike uric acid and trigger painful acute gout flares. |
| **Fatty Liver (NAFLD)**<br>*(Saroglitazar, Vitamin E)* | • Gradual pace capped at **0.75 kg/week**.<br>• Zero added fructose/refined sugars. | • Amla (antioxidant), Black coffee.<br>• Green leafy subzis, Turmeric. | • High-fructose corn syrup, Fruit juices.<br>• Excessive mango/chikoo, Deep fried curries. | Rapid weight loss (>1.5 kg/wk) exacerbates hepatic steatohepatitis. |
