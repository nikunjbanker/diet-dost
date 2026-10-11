<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261011-091: Strict Safe Harbor Policy, Multi-LLM Content Safety Standards, and Autonomous Agent Validation Mandate

- **Status**: Accepted
- **Date**: 2026-10-11
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Security Operations, Legal Governance, AI/ML Engineering
- **Informed**: All Contributors, Autonomous AI Agents

---

## 1. Context and Problem Statement

As Diet-Dost approaches production readiness and cloud deployment, two vital security and governance dimensions required formal strengthening:

1. **Safe Harbor Legal Scope & Boundaries**:
   - The prior Safe Harbor statement was brief and lacked precise legal definitions distinguishing good-faith vulnerability research from out-of-scope, destructive, or extortionate activities.
   - The policy needed strict, legally sound, professional, and non-aggressive language defining explicit boundaries (prohibiting data exfiltration, DoS, ransomware tactics, social engineering) while reserving all statutory remedies under the Information Technology Act, 2000 (India), Digital Personal Data Protection Act, 2023 (DPDPA), Computer Fraud and Abuse Act (CFAA), and international cybercrime statutes without sounding hostile or offensive to ethical researchers.
2. **Multi-LLM Content Safety & Agent Governance Drift**:
   - While Google AI Gemini integrated explicit `StrictSafetySettings` (`BLOCK_LOW_AND_ABOVE`), the policy and automated verification needed generalization to govern Azure OpenAI and future LLM backends (Anthropic Claude, DeepSeek, AWS Bedrock, Meta Llama, Local ONNX/Ollama).
   - In autonomous and agent-assisted development environments (Antigravity, Cursor, Copilot), developers or agents might inadvertently introduce new LLM integrations or refactor existing ones without declaring provider-level strict content safety filters.
   - A mandatory protocol was required to ensure agents validate provider safety settings and proactively remind contributors whenever an unshielded or permissive LLM integration is detected.

---

## 2. Decision Outcome

We implemented comprehensive enhancements across documentation, repository rules, provider declarations, and automated CI gating:

### 2.1 Refined Safe Harbor Policy & Responsible Research Terms (`SECURITY.md` Section 2.4)
- **Good-Faith Research Protection (Section 2.4.1)**: Formally defines research conducted within scope and coordinated disclosure as authorized conduct, committing that Diet-Dost will not pursue civil litigation or criminal complaints against researchers acting responsibly.
- **Strict Scope Boundaries & Prohibited Conduct (Section 2.4.2)**: Non-negotiable boundaries explicitly exclude:
  1. Accessing, retaining, or exfiltrating user personal or sensitive health/dietary data.
  2. Degrading service availability via volumetric or DoS/DDoS attacks.
  3. Modifying or destroying database records, SMB file shares, or infrastructure.
  4. Extortion, ransom, or coercive vulnerability holding.
  5. Social engineering, phishing, or physical attacks.
  6. Testing beyond isolated accounts controlled by the researcher.
  7. Premature public disclosure prior to coordinated patch deployment.
- **Legal Reservation & Statutory Enforcement (Section 2.4.3)**: Expressly reserves all civil remedies, injunctive relief, and law enforcement referral in polite, professional, and legally precise language under:
  - **The Information Technology Act, 2000 (India)** (Sections 43, 66, 66B-D, 70, 72A),
  - **The Digital Personal Data Protection Act, 2023 (DPDPA, India)**,
  - **The Computer Fraud and Abuse Act (CFAA, 18 U.S.C. § 1030)** (United States), and
  - Equivalent international cybercrime and data privacy statutes.

### 2.2 Multi-LLM Content Safety Baseline (`SECURITY.md` Section 3.3)
- **Universal Provider Safety Standard**:
  - *Google AI Gemini*: Mandates explicit `StrictSafetySettings` set to `BLOCK_LOW_AND_ABOVE` across harassment, hate speech, sexual content, dangerous content, and civic integrity.
  - *Azure OpenAI Service*: Declares explicit `ContentSafetyPolicy` compliance governed by strict Azure AI Content Safety thresholds on Hate, Sexual, Violence, Self-Harm, and Jailbreak Detection.
  - *Future LLM Providers*: Mandates equivalent strict safety controls at the SDK/API layer before token generation. Permissive vendor defaults are prohibited.
- **Invariant Upstream Defenses**: All providers remain behind `PromptShieldValidator` pre-flight inspection and `[USER_MEAL_INTAKE_DATA]` delimiter isolation.

### 2.3 Autonomous Agent Validation & Proactive Reminder Mandate
- Codified an ironclad rule in [`.agents/rules/security-and-ai-defense-rules.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/rules/security-and-ai-defense-rules.md):
  - AI agents must inspect all classes implementing `IAiFoodAnalysisProvider` under `src/Nutrition.Infrastructure/AI/Providers/`.
  - If a contributor or agent introduces or modifies an LLM provider without explicit strict safety settings:
    - The agent MUST proactively issue the reminder:
      > *"⚠️ Content Safety Governance Alert: LLM provider '{ProviderName}' does not declare explicit strict content safety settings. Per SECURITY.md Section 3.3 and OWASP Top 10 for LLM, all LLM providers must enforce strict content moderation (equivalent to Google AI BLOCK_LOW_AND_ABOVE / Azure Strict Filters). Please configure provider-level safety settings before submitting."*
    - The agent MUST NOT generate code that omits, disables, or weakens provider safety controls.

### 2.4 Automated CI & Pre-Commit Enforcement
- Updated [`scripts/verify-ai-security-defense.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/scripts/verify-ai-security-defense.ps1) Vector 4:
  - Dynamically scans all classes implementing `IAiFoodAnalysisProvider`.
  - Asserts `StrictSafetySettings` (`BLOCK_LOW_AND_ABOVE`) for Google Gemini.
  - Asserts `ContentSafetyPolicy` for Azure OpenAI.
  - Asserts strict content safety configuration for any future provider, halting CI with a structured reminder if unconfigured.

---

## 3. Forward Roadmap Impact & Multi-Platform Compatibility

- **Multi-LLM Extensibility**: Future providers (Claude 3.5 Sonnet, DeepSeek V3, AWS Bedrock Titan, Local ONNX Runtime) have a deterministic contract and safety gate.
- **Legal Protection**: Comprehensive legal reservation protects user clinical records and platform integrity while providing transparent protections for genuine security researchers.
- **Agent Governance**: Proactive agent reminders ensure zero security drift as AI agents autonomously assist in codebase evolution.

---

## 4. Verification Evidence

1. `pwsh -File scripts/verify-ai-security-defense.ps1 -Mode All`:
   - Vector 1 (Slopsquatting): PASS
   - Vector 2 (Insecure Defaults): PASS
   - Vector 3 (Security Disablers): PASS
   - Vector 4 (OWASP LLM & Multi-LLM Content Safety): PASS (Google Gemini, Azure OpenAI, PromptShield, Delimiters)
   - Vector 5 (Agent State Bleed): PASS
   - Vector 6 (Living Diagrams): PASS
2. `dotnet test -c Release`: All 242 tests passing with 0 warnings and 0 errors.
3. `tests/validate_e2e_tiers.ps1`: 100% PASS across all 5 demo user tiers.
