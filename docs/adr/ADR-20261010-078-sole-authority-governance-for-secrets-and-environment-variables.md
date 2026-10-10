/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

# ADR-20261010-078: Sole-Authority Governance for Secrets and Environment Variables Restricted to @nikunjbanker

- **Status**: Accepted
- **Date**: 2026-10-10
- **Author**: Diet-Dost Solution Architecture Team
- **Deciders**: Lead Architect, Principal Security Engineer, Core Contributor
- **Consulted**: Application, Infrastructure, and Security Teams
- **Informed**: All Contributors, Autonomous Agents

---

## 1. Context and Problem Statement
To ensure absolute confidentiality, data sovereignty, and zero unauthorized tampering with infrastructure bindings or sensitive credentials, clear role boundaries must be enforced regarding who can create, update, delete, view, or rotate repository/environment variables and secrets.

Unrestricted or ambiguously governed secret/variable management creates severe operational and security risks:
1. Accidental exposure or modification of production API keys (`GEMINI_API_KEY`, `JWT_KEY`).
2. Unauthorized tampering with administrative identity (`SUPER_ADMIN_EMAIL`) or security enforcement toggles (`REQUIRE_MOBILE_VERIFICATION`).
3. Accidental leakage of secrets into plaintext repository/environment variables by external contributors or automated agents.

A formal, defense-in-depth governance rule is required across engineering policy (`AGENTS.md`), deployment pipelines (`azure-app-deploy.yml`), repository access controls (`CODEOWNERS`), and automated regression tests.

---

## 2. Decision Drivers
- **Absolute Authority Mandate**: Exclusively `@nikunjbanker` (Repository Owner and Lead Architect) must have authority to create, update, or delete environment variables and secrets.
- **Zero-Unilateral Action Principle**: AI agents, human contributors, external collaborators, and automated workflows are strictly prohibited from creating, updating, deleting, or exposing secrets or environment variables without explicit direction or execution by `@nikunjbanker`.
- **Zero-Plaintext Secret Mandate**: Confidential settings must never be committed to code or set as plaintext GitHub variables; they must reside strictly as encrypted GitHub Secrets and Azure Key Vault secrets.
- **Multi-Layer Defense in Depth**: Enforce restrictions at repository policy, workflow job-level, workflow step-level bash script, and automated CI test barriers.

---

## 3. Decision Outcome
**Chosen Decision**: Codified and implemented sole-authority governance restricted strictly to `@nikunjbanker`:

1. **`AGENTS.md` (Section 2, Rule 17)**:
   - Added Rule 17 establishing that strictly and exclusively `@nikunjbanker` has authority to create, update, delete, view, or rotate any GitHub Secrets, GitHub Variables, Azure Key Vault Secrets, or Azure Container Apps settings.
   - Mandated zero unilateral execution of `gh secret`, `gh variable`, or `az keyvault secret` commands.
2. **`.github/workflows/azure-app-deploy.yml`**:
   - Job-level gate: `if: github.actor == 'nikunjbanker'`.
   - Step 10b runtime bash guard: Verifies `[ "${{ github.actor }}" != "nikunjbanker" ]` and fails fast with exit code 1 if violated.
3. **`.github/workflows/azure-infra-deploy.yml`**:
   - Job-level gate: `if: github.actor == 'nikunjbanker'`.
4. **`tests/Nutrition.EvalHarness.Tests/SecurityEnvironmentAndHeaderTests.cs`**:
   - Added `SecretAndEnvironmentVariableGovernance_MustBeStrictlyRestrictedToNikunjBanker` test continuously verifying that the workflow actor guards and `AGENTS.md` Rule 17 cannot be bypassed or removed.
5. **Living Documentation & Skill Synchronization**:
   - Updated `docs/sdd/10_configuration_and_secret_management_architecture.md` (Section 10).
   - Updated `.agents/skills/diet-dost-azure-deployment/SKILL.md` (Section 9).

---

## 4. Forward Roadmap Impact & Future Phase Compatibility
- **Phase 2 Mobile BFF**: Mobile configuration and client tokens inherit this strict single-authority perimeter with 0 exposure.
- **Phase 3 Cloud Database**: Azure SQL and Cosmos DB connection strings and Managed Identity bindings remain governed solely by `@nikunjbanker`.
- **Compliance & Audit**: Directly satisfies SOC 2 / ISO 27001 Access Control (Principle of Least Privilege) and OWASP Top 10 A01 / A05 requirements.

---

## 5. Verification & Validation Evidence
- **Automated Tests**: `SecretAndEnvironmentVariableGovernance_MustBeStrictlyRestrictedToNikunjBanker` passed.
- **Test Suite Status**: 211 / 211 tests passing (36 Domain, 175 EvalHarness), 0 warnings, 0 errors.
- **Living Documentation**: Registered in `docs/adr/README.md`, `docs/sdd/07_living_documentation_log.md`, and `docs/sdd/10_configuration_and_secret_management_architecture.md`.
