<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261018-113: Adversarial Agentic AI Engineering Standards Alignment, Structured Outputs & CNCF OpenTelemetry GenAI Observability

> **Status**: `ACCEPTED`  
> **Date**: `2026-10-18`  
> **Scope**: Agentic AI Architecture, Anthropic Agent Standards, Microsoft Agent Framework, OpenAI Structured Outputs, CNCF OpenTelemetry GenAI, Observability  
> **Target Release**: Alpha 01 Showcase Release (`dev.diet-dost.in`)

---

## 1. Context & Problem Statement

An adversarial architectural evaluation of the **Diet-Dost** codebase was conducted against the premier Agentic AI development frameworks:
1. **Anthropic ("Building Effective Agents")**: Augmenting LLMs with simple, composable patterns; deterministic tool routing; strict guardrails; token hygiene.
2. **Microsoft (Semantic Kernel / AutoGen / Azure AI Foundry)**: Standardized connectors, enterprise observability, schema grounding, and resilient fallback choreography.
3. **OpenAI (Structured Outputs & Evals)**: Formal JSON Schema contracts, token-level constrained decoding, synthetic eval harnesses, and disjoint test sets.
4. **Google DeepMind / Antigravity**: Autonomous issue-driven loops, living documentation synchronization, and strict zero-unilateral-decision protocols.

### Architectural Gaps Identified
While Diet-Dost pioneered industry-leading patterns (including the **Deterministic Anchor Principle**, pre-commit 7-vector AI defense, living SDDs, and multi-tier live CFT execution), several high-leverage agentic engineering gaps were identified:
1. **Open-Loop Prompt Parsing**: `AiJsonParser` relied primarily on string manipulation and regular expression heuristics to locate JSON substrings rather than fast-path schema decoding.
2. **Unenforced Provider Schemas**: Model requests in `GoogleGeminiProvider` requested `response_mime_type = "application/json"` but omitted declarative `response_schema` constraints, leaving open the risk of structural schema hallucination.
3. **Observability Gap in GenAI Telemetry**: While system spans tracked `gen_ai.system` and model IDs, token-level telemetry (`gen_ai.usage.input_tokens`, `gen_ai.usage.output_tokens`, `gen_ai.usage.total_tokens`) and `gen_ai.response.finish_reasons` were missing, violating CNCF OpenTelemetry GenAI Semantic Conventions.

---

## 2. Decision & Architecture

### 1. Declarative JSON Schema Structured Outputs
In [`GoogleGeminiProvider.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/Providers/GoogleGeminiProvider.cs), we defined a static, declarative `StructuredOutputSchema` adhering to the Gemini API generation configuration contract:
- Root object requires: `dishName`, `identifiedItems`, `totalCalories`, `totalProteinGrams`, `totalCarbsGrams`, `totalFatGrams`.
- `identifiedItems` array enforces sub-objects with typed numeric macros (`calories`, `proteinGrams`, etc.).
- Injected `response_schema = StructuredOutputSchema` directly into `generationConfig` for all vision and text analysis calls.

### 2. High-Performance Dual-Path Parser with Deterministic Rollups
In [`AiJsonParser.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/AiJsonParser.cs):
- **Fast-Path**: If the response is clean JSON (`trimmed.StartsWith('{') && trimmed.EndsWith('}')`), it immediately deserializes directly into `IndianMealAnalysisResult` with zero intermediate string allocations.
- **Deterministic Macro Verification**: Implemented `EnforceDeterministicTotals` to guarantee that all macros (calories, protein, carbs, fat, fiber, sugar, sodium) are mathematically computed from verified items if the model returns zeros or omit totals.
- **Resilient Fallback**: Retained sanitized fence-stripping (`\`\`\`json` unwrapping and brace scanning) to gracefully handle non-compliant or legacy provider responses.

### 3. CNCF OpenTelemetry GenAI Semantic Attribute Compliance
In [`NutritionTelemetry.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Application/Common/NutritionTelemetry.cs), we registered standard OpenTelemetry GenAI semantic keys:
- `gen_ai.usage.input_tokens`: Ingested from provider `usageMetadata.promptTokenCount` (Gemini) and `Usage.InputTokenCount` (Azure OpenAI).
- `gen_ai.usage.output_tokens`: Ingested from provider `usageMetadata.candidatesTokenCount` (Gemini) and `Usage.OutputTokenCount` (Azure OpenAI).
- `gen_ai.usage.total_tokens`: Ingested from provider `usageMetadata.totalTokenCount` (Gemini) and `Usage.TotalTokenCount` (Azure OpenAI).
- `gen_ai.response.finish_reasons`: Ingested from candidate `finishReason` (`STOP`, `SAFETY`, `MAX_TOKENS`).

Enforced token metric collection consistently across both [`GoogleGeminiProvider.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/Providers/GoogleGeminiProvider.cs) and [`AzureOpenAiProvider.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/Providers/AzureOpenAiProvider.cs).

### 4. Living Architecture & Diagram Synchronization
- Synchronized Section 2.11 in [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md).
- Updated specification version to `v1.6.0` and appended entry to Section 9.4 in [`docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/11_ai_in_sdlc_and_agentic_workflows_architecture.md).
- Synchronized canonical diagrams [`solution_architecture.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/solution_architecture.mermaid) and [`devops_observability.mermaid`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/architecture/diagrams/devops_observability.mermaid), along with [`scripts/sync-architecture-diagrams.py`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/sync-architecture-diagrams.py) and embedded Markdown diagrams in [`README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/README.md) to reflect the exact active baseline of **259 Automated Tests** (36 Domain + 223 Eval Harness).
- Registered this decision fragment in `docs/adr/architecture/` and regenerated index files via `scripts/sync-adr-index.ps1`.

---

## 3. Consequences & Validation

- **Compliance**: Full adherence to Anthropic, Microsoft, and OpenAI agentic AI and observability standards.
- **Correctness**: 259/259 automated unit and evaluation harness tests pass with 0 warnings.
- **Cost & Quota Transparency**: Token-level observability provides real-time visibility into LLM usage and prompt costs across Application Insights and distributed tracing dashboards.
