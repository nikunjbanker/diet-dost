# Test Harnesses, AI Vision Evals & Quality Engineering
> **Specification Version**: `v1.2.0 (Production & Living SDD)`  
> **Methodology**: Closed-Loop Harness Engineering & Contract-First Validation  
> **Test Frameworks**: xUnit, Microsoft.NET.Test.Sdk, Playwright / Browser Agent  

---

## 1. Quality Engineering Blueprint

In adherence with Skill §7.1 and §7.2, all implementations must satisfy five verification tiers:
1. **Clinical Dietetics Unit Test Suite (`Nutrition.Domain.Tests`)**: Validates Mifflin-St Jeor math, WHO Asian-Indian BMI cutoffs, safety floors, deficit ceilings, and the clinical adjustment matrix.
2. **AI Multimodal Vision Evaluation Suite (`Nutrition.Vision.Evals`)**: Evaluates JSON schema compliance, portion heuristic tolerances, confidence gating threshold ($\ge 70\%$), Gemini 3 Flash thinking token support, and multi-model fallback cascade.
3. **Database Resilience & Integrity Verification**: Validates schema-aware idempotent column migrations (`PRAGMA table_info`), EF Core collection `ValueComparer` instances (preventing change-tracking loss and eliminating model configuration warnings), and non-PII diagnostic error logging.
4. **OpenTelemetry & Observability Verification**: Verifies full HTTP request/response payload capture in root spans (`HttpPayloadTelemetryMiddleware`), GenAI semantic convention tags (`gen_ai.system_prompt`, `user.diagnosed_conditions`, `user.medications`), and non-PII structured logging scopes.
5. **OWASP Security & ASVS Verification Suite**: Tests file armor, magic bytes, prompt guardrails, and PII redaction.

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

---

## 3. CLI Test Execution

```bash
# Run all unit and eval harness suites
dotnet test --logger "console;verbosity=detailed"

# Validate WebGateway builds with zero warnings or errors
dotnet build src/Nutrition.WebGateway/Nutrition.WebGateway.csproj
```
