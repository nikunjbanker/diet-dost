<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261010-082: AI-Based Development Security Defense, Content Safety Shield & Pre-Commit Gating

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and AI/ML Teams
- **Informed**: All Contributors, Autonomous AI Agents

---

## 1. Context and Problem Statement
With the widespread adoption of AI agents, LLM code generators, and autonomous coding tools (e.g., Gemini, Copilot, Cursor, ChatGPT), repositories face new categories of security threats and anti-patterns:
1. **Supply Chain & Package Hallucination (Slopsquatting)**: AI models frequently invent non-existent package dependencies or introduce arbitrary untrusted libraries.
2. **"Lazy AI" Insecure Defaults & Disablers**: AI models bypass security checks to make code run (e.g. disabling TLS verification, wildcard CORS, hardcoded fallback secrets, empty `catch {}` error swallowing).
3. **AI Security Disablers**: Silencing linters or compilers with unscoped suppression comments (`// @ts-ignore`, `/* eslint-disable */`, `#pragma warning disable`, `// nosec`).
4. **OWASP Top 10 for LLM Threats**: Prompt injection (LLM01), sensitive data/PII disclosure (LLM06), and model denial of service (LLM04).
5. **Content Safety & Hate Speech Risks**: Lack of content safety filtering allowing harmful, violent, sexually explicit, or communal hate speech into prompts.
6. **Continuous Learned Memory Poisoning**: Adversarial manipulation of user feedback retraining pipelines (`ProcessFeedbackRetrainingAsync`) to degrade or poison model heuristics.

---

## 2. Decision Outcome
Implemented a comprehensive, defense-in-depth AI Security Defense architecture spanning pre-commit, runtime pre-flight, external model API policies, and CI/CD security scanning:

### 2.1 Git Pre-Commit Hook & Automated Defense Validator
- **Validator Script**: [`scripts/verify-ai-security-defense.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/verify-ai-security-defense.ps1) evaluates 5 core defense vectors across repository code:
  1. *Vector 1 (Slopsquatting)*: Asserts all NuGet package references match approved and vetted registries.
  2. *Vector 2 (Insecure Defaults)*: Detects and rejects TLS bypass (`DangerousAcceptAnyServerCertificateValidator`), wildcard CORS, hardcoded fallback secrets, empty catch blocks, and dangerous `eval()`.
  3. *Vector 3 (Security Disablers)*: Detects and rejects `@ts-ignore`, unscoped `eslint-disable`, `nosec`, and unjustified `#pragma warning disable`.
  4. *Vector 4 (OWASP LLM & Content Safety)*: Asserts prompt delimiter isolation (`[USER_MEAL_INTAKE_DATA]`), input sanitization, and pre-flight validation.
  5. *Vector 5 (Agent Artifact Bleed)*: Ensures no internal AI session tokens or transcripts are staged or committed.
- **Pre-Commit Hook**: [`.githooks/pre-commit`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.githooks/pre-commit) wired via `git config core.hooksPath .githooks`, automatically rejecting any `git commit` if staged changes violate AI security defense rules.

### 2.2 Enterprise Prompt Shield & Content Safety Pre-Validator
- Created [`PromptShieldValidator.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/PromptShieldValidator.cs) enforcing zero-tolerance pre-flight defense:
  - **Harmful & Violent Content**: Rejects weapons, self-harm, murder, and physical violence.
  - **Sexually Explicit Content**: Rejects adult, pornographic, and erotic terms.
  - **Communal & Hate Speech**: Rejects communal violence, sectarian incitement, and religious hatred.
  - **Prompt Injection & Jailbreaks**: Rejects role override, "DAN", system override, and prompt extraction attempts.
  - **Self-Learning Data Poisoning**: Rejects invalid markup, code, and non-food terms in feedback retraining.
  - Rejection produces a structured `IndianMealAnalysisResult` with `DishName = "Content Safety Policy Rejection"` and zero token spend.

### 2.3 Model API Safety Policies
- Enhanced [`GoogleGeminiProvider.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/src/Nutrition.Infrastructure/AI/Providers/GoogleGeminiProvider.cs) with `StrictSafetySettings` declaring `BLOCK_LOW_AND_ABOVE` across harassment, hate speech, sexually explicit, dangerous content, and civic integrity. Gracefully handles `finishReason == "SAFETY"`.

### 2.4 CI/CD Security-Scan Integration & Living Rules
- Added `ai-security-defense` job to [`.github/workflows/security-scan.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/security-scan.yml) as part of the parallel 7-scanner matrix.
- Codified **Rule 18** (Immutable Security-Scan Pipeline) and **Rule 19** (Mandatory AI-Based Development Defense, Content Safety & Pre-Commit Gating) in [`AGENTS.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/AGENTS.md).

---

## 3. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile**: Mobile apps consume the server-side `PromptShieldValidator`, ensuring consistent content safety across Web and Native Mobile clients.
- **Enterprise Multi-Model Expansion**: Any new AI providers (Anthropic, DeepSeek, Local ONNX) plug into `PromptShieldValidator` as an invariant pre-flight barrier.

---

## 4. Verification
- `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All`: 100% PASS across all 5 vectors.
- `dotnet test`: 234 / 234 tests passing (0 compiler warnings, 0 compiler errors).
- New test suite: [`AiPromptShieldAndContentSafetyTests.cs`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/tests/Nutrition.EvalHarness.Tests/AiPromptShieldAndContentSafetyTests.cs) passing across all attack scenarios.
