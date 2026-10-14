<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Security, Prompt Shield & Secrets Governance

## 1. Enterprise AI Prompt Shield & Content Safety (OWASP Top 10 for LLM)
- **Zero Harmful / Violent / Sexual / Communal Content Policy**: Every prompt constructed or processed in Diet-Dost MUST pass through `PromptShieldValidator`.
- Intercepts and rejects:
  - *Harmful & Violent Content*: Weapons, violence, bomb-making, self-harm.
  - *Sexually Explicit Content*: Pornography, explicit sexual terms.
  - *Communal & Hate Speech*: Religious disharmony, communal slurs, hate speech.
  - *Prompt Injections & Jailbreaks (OWASP LLM01)*: Role override ("DAN", system jailbreaks, delimiter escape).
- **Prompt Injection Delimiter Isolation**: User inputs must always be enclosed in `[USER_MEAL_INTAKE_DATA]` delimiters.
- **Self-Learning Data Poisoning Defense (OWASP LLM03)**: Retraining data and continuous memory updates must be guarded via `PromptShieldValidator.ValidateFeedback`.
- **Universal Multi-LLM Provider Native Safety Baseline**:
  - *Google AI Gemini*: Declared on all API calls via `StrictSafetySettings` set to `BLOCK_LOW_AND_ABOVE` across harassment, hate speech, sexual content, dangerous content, and civic integrity.
  - *Azure OpenAI Service*: Enforces Azure AI Content Safety filters configured with Strict (Low threshold) blocking on Hate, Sexual, Violence, Self-Harm, and Jailbreak Detection.
  - *Future LLM Providers (Claude, DeepSeek, Bedrock, Llama, ONNX/Ollama)*: Every implementation of `IAiFoodAnalysisProvider` or AI agent reasoning MUST enforce equivalent strict provider-level safety settings or moderation filters before token generation. Permissive defaults or disabling safety filters is strictly forbidden.
- **Autonomous Agent Validation & Proactive Reminder Mandate**:
  - AI agents (Antigravity, Cursor, Copilot) MUST inspect all AI provider implementations in `src/Nutrition.Infrastructure/AI/` and `src/Nutrition.Application/Agents/`.
  - Agents MUST verify that strict safety settings and pre-flight prompt shielding are present.
  - *Mandatory Agent Reminder*: If a developer or contributor adds or modifies an LLM provider without explicit strict safety settings, the agent MUST proactively output the reminder:
    > *"⚠️ Content Safety Governance Alert: LLM provider '{ProviderName}' does not declare explicit strict content safety settings. Per SECURITY.md Section 3.3 and OWASP Top 10 for LLM, all LLM providers must enforce strict content moderation (equivalent to Google AI BLOCK_LOW_AND_ABOVE / Azure Strict Content Filters). Please configure provider-level safety settings before submitting."*
  - Agents MUST NOT generate code that omits, disables, or weakens provider safety settings.

## 2. Secrets Governance & Sole Administrative Authority
- **Sole Approver & Authority**: Contributor `@nikunjbanker` holds sole administrative authority over solution secrets, Azure Key Vault access, and credentials.
- **Zero Hardcoded Secrets**: Secrets must never be stored in Git commits or code files.
- **Azure Key Vault Purge Protection**: Key Vault is provisioned with `enablePurgeProtection: true` and 7-day soft-delete.
- **Passwordless Managed Identity**: Azure Container Apps retrieves container images via `acrPullRole` without static credentials.

## 3. Pre-Deployment Security Gate
- [`.github/workflows/security-scan.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/security-scan.yml) runs 8 scanners:
  Gitleaks, ESLint, Roslyn Security/DevSkim/CodeQL across all 7 projects, Trivy, Checkov, actionlint, AI Security Defense (OWASP LLM 5-Vector Validator), and publishes a summary PR comment.
- **Zero-Deployment-on-Failure**: Any scanner failure aborts deployment pipelines immediately.

## 4. Safe Harbor Policy & Legal Terms of Engagement
- **Good-Faith Research**: Protected under Safe Harbor when adhering to coordinated disclosure, no data exfiltration, no DoS, and testing only against controlled test accounts.
- **Prohibited Conduct**: Unauthorized access to user health/dietary data, DoS/DDoS, data destruction, extortion/bounty demands, social engineering, or public disclosure before verified deployment.
- **Statutory Enforcement & Legal Reservation**: Activities conducted in bad faith, outside scope, or violating user privacy forfeit Safe Harbor protection and are subject to legal remedies, civil action, and criminal referral under the **Information Technology Act, 2000 (India)**, **Digital Personal Data Protection Act, 2023 (DPDPA)**, **Computer Fraud and Abuse Act (CFAA)**, and relevant global cybercrime laws.
