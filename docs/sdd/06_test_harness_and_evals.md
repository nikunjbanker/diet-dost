# Test Harnesses, AI Vision Evals & Quality Engineering
> **Specification Version**: `v1.1.0 (Production & Living SDD)`  
> **Methodology**: Closed-Loop Harness Engineering & Contract-First Validation  
> **Test Frameworks**: xUnit, Microsoft.NET.Test.Sdk  

---

## 1. Quality Engineering Blueprint

In adherence with Skill §7.1 and §7.2, all implementations must satisfy three verification tiers:
1. **Clinical Dietetics Unit Test Suite (`Nutrition.Domain.Tests`)**: Validates Mifflin-St Jeor math, WHO Asian-Indian BMI cutoffs, safety floors, deficit ceilings, and the clinical adjustment matrix.
2. **AI Multimodal Vision Evaluation Suite (`Nutrition.EvalHarness.Tests`)**: Evaluates JSON schema compliance, portion heuristic tolerances, and confidence gating threshold ($\ge 70\%$).
3. **OWASP Security Verification Suite**: Tests file armor, magic bytes, and input sanitization.

---

## 2. Benchmark Evaluation Fixtures

| Fixture ID | Dish & Component | Expected Nutrition | Evaluation Criteria |
|---|---|---|---|
| **FIX-01** | 2 Phulkas + 1 Katori Dal Tadka + Cucumber Salad | 380 kcal ± 10%, 14g Protein | Confidence $\ge 70\%$, Auto-populated in Review modal |
| **FIX-02** | 1 Masala Dosa + Sambar + Coconut Chutney | 450 kcal ± 10%, 8g Protein | Sodium flag triggered (>500mg), Cooking oil tracked |
| **FIX-03** | Blurry / Dark meal image (<1KB or occlusion) | N/A | Confidence $< 70\%$, Retake prompt triggered, 1-tap retake enabled |
| **FIX-04** | Metformin + Telmisartan with Coconut Water | Clinical Warning | Potassium alert triggered for ARB/ACE inhibitor |

---

## 3. CLI Test Execution

```bash
# Run all unit and eval harness suites
dotnet test --logger "console;verbosity=detailed"
```
