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
  - *Prompt Injections & Jailbreaks*: Role override ("DAN", system jailbreaks, delimiter escape).
- **Self-Learning Data Poisoning Defense**: Retraining data and continuous memory updates must be guarded against adversarial poisoning.
- **Google AI StrictSafetySettings**: Declared on all Gemini API calls at `BLOCK_LOW_AND_ABOVE`.

## 2. Secrets Governance & Sole Administrative Authority
- **Sole Approver & Authority**: Contributor `@nikunjbanker` holds sole administrative authority over solution secrets, Azure Key Vault access, and credentials.
- **Zero Hardcoded Secrets**: Secrets must never be stored in Git commits or code files.
- **Azure Key Vault Purge Protection**: Key Vault is provisioned with `enablePurgeProtection: true` and 90-day soft-delete.
- **Passwordless Managed Identity**: Azure Container Apps retrieves container images via `acrPullRole` without static credentials.

## 3. Pre-Deployment Security Gate
- [`.github/workflows/security-scan.yml`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.github/workflows/security-scan.yml) runs 8 scanners:
  Gitleaks, ESLint, SecurityCodeScan across all 7 projects, Trivy, Checkov, actionlint, AI Security Defense, and publishes a summary PR comment.
- **Zero-Deployment-on-Failure**: Any scanner failure aborts deployment pipelines immediately.
