<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Rule: Product Skill Synchronization & Living Runbook Lifecycle Governance

## 1. Zero-Drift Mandate Between Skills and Codebase
- **Living Runbooks**: The skills located in `.agents/skills/` are executable, authoritative runbooks used by AI agents and contributors to implement, refactor, test, and deploy features.
- **Mandatory Synchronization**: Whenever any modification, feature, defect fix, or pipeline change alters:
  1. *Architecture or Layering*: (CQRS handlers, interfaces, controllers, Web BFF, DI registrations) -> Synchronize `diet-dost-clean-architecture`.
  2. *Authentication, Security, or Legal Policies*: (Auth policies, token formats, DPDPA consent, tier gating, `SECURITY.md`) -> Synchronize `diet-dost-user-management-security`.
  3. *Static Analysis, DevSecOps, or CI Gates*: (New security scanners, `.gitleaksignore`, `.checkov.yaml`, GitHub workflows) -> Synchronize `diet-dost-devsecops-pipeline`.
  4. *Cloud Infrastructure, Bicep, or Pre-Deployment*: (PowerShell setup scripts, dynamic Azure context, ACA, SMB shares, Key Vault) -> Synchronize `diet-dost-azure-deployment`.
  5. *UI/UX Design Tokens, Surfaces, or Audits*: (`DESIGN.md`, Obsidian surfaces, automated design audits) -> Synchronize `design-system-enforcer`.
  6. *Responsive Viewports & Mobile Readiness*: (Mobile UX, bottom sheets, viewport tests, touch targets) -> Synchronize `diet-dost-responsive-web-mobile-readiness` & `diet-dost-mobile-architecture`.
  7. *Customer & Functional Acceptance Tests*: (New CFT test scripts in `tests/`, viewport suites) -> Synchronize `post-pr-ui-validation`.
  8. *Database Persistence & Cloud Data*: (EF Core models, SQLite, Azure SQL, Cosmos DB) -> Synchronize `diet-dost-database-architecture`.
  9. *Licensing & Copyright Governance*: (License headers, dual AGPLv3/SSPL v1, third-party audits) -> Synchronize `diet-dost-license-governance`.
  10. *Autonomous IDD Lifecycle*: (State checkpointing, issue lifecycle automation) -> Synchronize `diet-dost-issue-driven-workflow`.
- **Atomic Pull Request Invariant**: Product skills MUST be updated in the **same Pull Request** as the code changes. Never leave skill synchronization as a deferred or follow-up task.

## 2. Skill Creation, Update, and Archival Protocol
- **When to Create a New Skill**:
  - Introduce a new skill ONLY when a new, distinct operational discipline, major architectural subsystem, or engineering capability is added that cannot be logically housed within an existing skill without causing thematic incoherence.
  - Every new skill must contain a standard `SKILL.md` with compliant YAML frontmatter (`name`, `description`).
- **When to Prune or Archive a Skill / Playbook**:
  - When an architectural pattern, library, or script is deprecated or superseded (e.g. migrating away from a library, retiring an obsolete pipeline), update the skill to document the migration path and archive obsolete playbooks into a designated `archive/` subfolder.
  - Never retain misleading, outdated CLI commands or deprecated configuration flags.
- **Skill Elimination Anti-Pattern (Zero Skill Sprawl)**:
  - Do NOT create fragmented, redundant micro-skills (e.g. separate skills for each individual test file or controller). Keep related capabilities unified within domain skills.

## 3. Token Economics Discipline (YAML Frontmatter Standard)
- **Baseline Prompt Injection**: The `name` and `description` of **every folder in `.agents/skills/`** are automatically injected into the agent's system prompt on every interaction.
- **Description Budget**: Keep the YAML `description` under **150 words**. Ensure it is dense with relevant trigger keywords, technologies, and scopes so the agent can discover it accurately.
- **Playbook Boundary vs. Declarative SDDs**:
  - Skill bodies (`SKILL.md`) are procedural "how-to" playbooks, concrete code recipes, step-by-step commands, and verification checklists.
  - Declarative specifications, complete schemas, and comprehensive rationale live in `docs/sdd/*.md` and `docs/adr/*.md` (which consume **0 baseline tokens** and are read on-demand via `view_file`).
  - **Strict Anti-Pattern**: NEVER copy-paste entire SDD specifications or living ADR logs into `SKILL.md`.

## 4. Mandatory Pre-Merge Skill Validation Checklist
Before opening or merging any Pull Request:
- [ ] Are all newly added scripts (e.g., in `scripts/` or `tests/`) referenced in their corresponding domain skills?
- [ ] Are all updated CLI commands, environment variables, or security flags accurately reflected in the skills?
- [ ] Does any skill contain stale file paths, obsolete flags, or deprecated references?
- [ ] Is the YAML frontmatter of all modified/new skills valid and concise?
- [ ] Has an atomic ADR fragment been recorded in `docs/adr/<domain>/` and indexed via `scripts/sync-adr-index.ps1`?
