<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261010-087: Product Skill Synchronization Governance, Living Runbook Standards, and DevSecOps Pipeline Codification

- **Status**: Accepted
- **Date**: 2026-10-10
- **Domain**: Governance & DevSecOps Architecture
- **Author**: Diet-Dost Core Architectural Pair
- **Decision Makers**: `@nikunjbanker` (Sole Authority)

## 1. Context and Problem Statement
As the Diet-Dost solution matures with advanced cloud pre-deployment automation (`scripts/setup-azure-pre-deployment.ps1`), an 8-job static analysis and security scanning gate (`.github/workflows/security-scan.yml`), automated cross-platform CFT suites (`tests/*.mjs`, `tests/*.ps1`), and root `SECURITY.md` governance, AI agents and contributors need authoritative, up-to-date runbooks.
- **The Skill Drift Risk**: Without strict governance, skills in `.agents/skills/` risk drifting from actual codebase implementations (e.g. referencing retired monolithic logs, outdated flags, or missing newly introduced CLI automation).
- **Missing DevSecOps Skill**: While application-level security was codified in `diet-dost-user-management-security`, the entire CI/CD 8-job static analysis security gate, local security defense execution (`verify-ai-security-defense.ps1`), Checkov IaC suppressions, and Gitleaks fingerprinting lacked a dedicated operational runbook.
- **Agent Rule Mandate**: An explicit rule was required in `.agents/rules/` and `AGENTS.md` to enforce continuous synchronization, addition, and archival of skills in lockstep with codebase evolutions.

## 2. Decision and Implementation
1. **Established `.agents/rules/skill-synchronization-governance.md`**:
   - Codified mandatory synchronization triggers: any change to architecture, security policies, test harnesses, infrastructure scripts, or design tokens requires synchronizing corresponding skills in the **same Pull Request**.
   - Defined criteria for adding new skills (distinct domain/discipline) and archiving obsolete playbooks.
   - Enforced token economics discipline: YAML `description` kept under 150 words with dense trigger keywords, while deep specs remain in `docs/sdd/` and `docs/adr/`.
2. **Created Dedicated `diet-dost-devsecops-pipeline` Skill**:
   - Added [`.agents/skills/diet-dost-devsecops-pipeline/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-devsecops-pipeline/SKILL.md) covering:
     - 8-job security scanning matrix in `.github/workflows/security-scan.yml` (Gitleaks, ESLint, SecurityCodeScan, Trivy, Checkov, actionlint, AI Security Defense, Security Gate Summary).
     - Local CLI execution commands for pre-push validation.
     - Vulnerability triage SOP and Safe Harbor alignment with `SECURITY.md`.
     - Zero hardcoded secrets and `.gitleaksignore` commit fingerprinting rules.
3. **Synchronized All Existing Solution Skills (12 Skills Total)**:
   - `diet-dost-azure-deployment`: Added PowerShell pre-deployment automation (`setup-azure-pre-deployment.ps1`), dynamic Azure context (`az account show`), Key Vault firewall rules, and ADR sync.
   - `diet-dost-user-management-security`: Added `SECURITY.md` compliance, 8-job static analysis CI gate, local prompt defense validator, and modernized ADR fragment instructions.
   - `post-pr-ui-validation`: Mapped Diet-Dost's full 10-test automated suite (multi-tier `validate_e2e_tiers.ps1`, viewport `verify_cft_viewports.mjs`, mobile flows `verify_mobile_cft_core_flows.mjs`, registration `verify_registration_and_cft.mjs`, etc.).
   - `design-system-enforcer`: Added automated token and surface ladder compliance test `tests/audit_design_system_compliance.mjs`.
   - `diet-dost-responsive-web-mobile-readiness`: Added automated viewport testing scripts and ADR sync.
   - `diet-dost-mobile-architecture`: Added automated headless BFF verification (`verify_mobile_bff.mjs`) to checklist.
   - `diet-dost-clean-architecture` & `diet-dost-database-architecture`: Replaced obsolete monolithic log references with domain ADR fragments and `scripts/sync-adr-index.ps1`.
   - `indian-diet-calorie-tracker`: Modernized master SDD skill to domain ADR fragment architecture.
4. **Updated `AGENTS.md` and `CLAUDE.md`**:
   - Registered `skill-synchronization-governance.md` in Modular Antigravity Rules.
   - Added Rule 20 ("Mandatory Product Skill Synchronization & Living Runbook Lifecycle Governance Rule") to `AGENTS.md`.
   - Added skill synchronization guidelines to `CLAUDE.md`.

## 3. Consequences
- **Positive**:
  - 100% synchronization between product code, automated tests, DevOps scripts, and agent skills.
  - Zero skill drift guaranteed via pre-PR verification rules.
  - AI agents can discover and execute DevSecOps security scans locally before pushing code.
  - Token economics strictly preserved (all YAML descriptions under 150 words).
- **Forward Roadmap Impact & Future Phase Compatibility**:
  - Supports Phase 2 Mobile, Phase 3 Enterprise Cloud Persistence, and continuous CI/CD automation by guaranteeing that runbooks evolve concurrently with each milestone without technical debt.
