<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Solution Engineering Rules & Instructions: Diet-Dost

This solution-level instruction file defines mandatory engineering and Git workflows for any AI agent or human contributor working on the **Diet-Dost** repository.

---

## 1. Mandatory Git Branching & PR-Only Merge Workflow

> [!IMPORTANT]
> **Zero Direct-to-Main Policy**: Direct commits or direct pushes to the `main` (or default production) branch are **strictly prohibited**.
> **Mandatory Remote Fetch Before Branching**: Branches must NEVER be created from stale or dirty local working branches. Always fetch from `origin` first to prevent dragged pre-squash commits and merge conflicts.
> **GitHub Stacked PR Workflow for Consecutive Features**: When work builds upon an in-flight unmerged PR, stack the child PR against the parent feature branch instead of `main`.
> **Zero-Unilateral-Decision Mandate**: In case of ANY doubt, ambiguity, or architectural decision, ALWAYS ask questions and seek confirmation from the user using interactive modal tools (`ask_question`). NEVER assume or decide unilaterally.

### Step-by-Step Workflow:
1. **Step 0: Pre-Flight Remote Fetch & Dedicated Branch Creation (Stale-Branch Prevention)**:
   > [!CRITICAL]
   > **Why PR Merge Conflicts Happen**:
   > 1. Running `git checkout -b <branch>` without a starting point branches from whatever local commit you currently have checked out (often an old commit or another feature branch).
   > 2. Running `git checkout -b <branch> main` branches from your **local** `main` branch, which is almost always **stale** because local `main` does not auto-update when PRs merge on GitHub.
   > 3. Branching before running `git fetch origin` means your local repository does not know about newly merged commits on remote `origin/main`.
   >
   > **The Ironclad Golden Rule**: Every new branch MUST be created directly from freshly fetched remote tracking branches (`origin/main` or `origin/<parent-feature>`), and the starting commit hash MUST be verified before writing any code.

   **Standard Execution Protocol**:
   a. **Fetch Latest Remote State First**:
      ```bash
      git fetch origin
      ```
   b. **For Independent Work (Branching off `main`)**:
      Always branch explicitly from `origin/main` using one of these bulletproof commands:
      ```bash
      git checkout -b feature/<descriptive-name> origin/main   # For new features or enhancements
      git checkout -b fix/<defect-name> origin/main           # For bug or defect fixes
      git checkout -b docs/<topic-name> origin/main           # For documentation changes
      ```
   c. **Mandatory Lineage Verification Guard (Assert Commit Match)**:
      Immediately after creating the branch, verify that HEAD points to the exact same commit as `origin/main`:
      ```bash
      # Both commands MUST output the exact same 40-character commit hash:
      git rev-parse HEAD
      git rev-parse origin/main
      ```
      *Failure Condition*: If `git rev-parse HEAD` does NOT equal `git rev-parse origin/main`, your branch is stale or dirty. **STOP immediately**, delete the branch (`git checkout main && git branch -D <branch>`), and recreate it cleanly from `origin/main`.

   d. **GitHub Stacked PR Protocol & Native Tooling (For Consecutive / Dependent PRs)**:
      When a new feature or defect fix depends upon an active, unmerged Pull Request (Parent PR A on `feature/<parent-feature>`):
      - **Fetch remote parent branch**:
        ```bash
        git fetch origin
        git checkout -b feature/<child-feature> origin/feature/<parent-feature>
        ```
      - **Verify Stacked Lineage**:
        ```bash
        git rev-parse HEAD
        git rev-parse origin/feature/<parent-feature>
        ```
        *Assert*: Both hashes must match identically.
      - **Set GitHub PR Base Branch to Parent Branch**:
        When opening the Pull Request in GitHub, set the **Base branch** to `feature/<parent-feature>` (NOT `main`).
      - **Official GitHub CLI Stack Extension (`github/gh-stack`)**:
        Ensure the official GitHub stack extension is installed:
        ```bash
        gh extension install github/gh-stack
        ```
      - **Mandatory Stack Linking & Synchronization (`gh stack link` & `gh stack sync`)**:
        Immediately after creating the child PR, formally register and sync the stack on GitHub:
        ```bash
        gh stack link <parent-pr-number> <child-pr-number>
        gh stack sync
        ```
        *Why*: Formally registers the chain in GitHub's native Stacked PR engine (e.g. `Stack #NNN`), eliminating unlinked PR drift and resolving the *"This pull request can be stacked with other pull requests"* unformalized prompt.
      - **Mandatory Stack Navigation Callout Widget in PR Descriptions**:
        Every PR in a stack MUST embed the standardized Markdown Stack Navigator Callout at the very top of its PR body:
        ```markdown
        > [!NOTE]
        > ### 🥞 GitHub Stack #<stack-id> (Layer X of Y)
        > 1. 🟢 **PR #<parent-pr>**: `<title>` (Base: `<base>`)
        > 2. 🟡 **PR #<child-pr> (This PR)**: `<title>` (Base: `<parent-branch>`)
        ```
        This guarantees instant bidirectional navigation for human reviewers, CI runners, and AI agents across GitHub UI and CLI.
      - **Sequential Bottom-Up Merging & Cascading Retargeting**:
        - Merge the stack strictly bottoms-up: Layer 1 (Base PR) merges first into `main`.
        - GitHub automatically updates the next layer's base branch to `main`.
        - Run `gh stack sync` locally to update branch tracking and prune merged layers.

   e. **Strict Anti-Patterns (NEVER DO THESE)**:
      | Forbidden Command / Action | Why It Is Strictly Forbidden | Consequence on GitHub PR |
      | :--- | :--- | :--- |
      | `git checkout -b <branch>` | Branches from whatever commit is currently checked out locally. | Drags old unrelated commits; causes merge conflicts. |
      | `git checkout -b <branch> main` | Branches from local `main` which is STALE unless manually pulled. | Missing latest upstream commits; severe merge conflicts. |
      | Branching without `git fetch origin` | Local Git has no knowledge of recent merges on GitHub. | Silent commit drift and divergent Git tree. |
      | Working on a dirty tree | Uncommitted/untracked files accidentally bleed into the new branch. | Accidental commit of unrelated files into PR diff. |

   f. **Mandatory Confirmation & Zero-Unilateral-Decision Protocol (Strict Ask Rule)**:
      - In case of ANY ambiguity, doubt, conflicting options (such as whether a branch should be stacked vs independent, or resolving structural conflicts), **STOP and ask the user for confirmation** using the interactive question tool (`ask_question`).
      - **Never make unilateral decisions or assumptions** on git branching topology, architectural boundaries, or data contracts without user alignment.
2. **Step 1: Perform All Changes Strictly on the Branch**:
   - Implement the required changes, domain logic, and tests within this isolated branch.
   - Run local validation: `dotnet test` and build checks (targeting .NET 11 with 0 warnings).
3. **Step 2: Mandatory Live CFT & End-to-End User Tier Execution (Strict Pre-PR Gate)**:
   > [!CRITICAL]
   > **Zero Regression & Zero Assumption Pre-PR Mandate**:
   > Never raise a Pull Request relying solely on unit tests. Unit tests test isolated code in memory; they do NOT detect runtime wiring failures, asset 404s, CORS drops, or UI regression. Before committing and opening ANY PR:
   > 1. **Boot Local Service**: Launch `Nutrition.WebGateway` on `http://localhost:5240` (via background daemon or process).
   > 2. **Execute Multi-Tier CFT Suite**: Run `pwsh -File tests/validate_e2e_tiers.ps1` to validate all 5 demo user tiers (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app` with password `DietDost@Demo2026!`). Assert 100% pass rate across auth, ledger, AI quota gating, photo paywalls, and composite hydration.
   > 3. **Execute Interactive UI Verification (for Presentation Changes)**: Run browser verification (via `browser_subagent`) on `http://localhost:5240` to assert zero console errors, zero Cumulative Layout Shift (CLS), and verified UI badge/component behavior.
   > 4. **Mandatory PR Evidence Publishing**: The exact terminal output and verification logs from the live CFT run MUST be published directly into the PR description under a dedicated `## 5. Live Customer & Functional Acceptance Test (CFT) Execution Evidence` section.
4. **Step 3: Major Change Auto-Detection & Living Synchronization**:
   - **Auto-Detect Major Changes**: Any modification involving:
     1. *Layer or Architectural Boundaries* (Clean Architecture, Native CQRS handlers, Ports/Adapters, DI registrations).
     2. *Persistence & Security Additions* (new DB entities/tables, secret stores, encryption, migrations).
     3. *Security & Environment Boundary Gating* (Debug/Release environment isolation, auth scheme routing, `#if DEBUG` guards).
     4. *Clinical & Domain Logic Modifications* (ICMR-NIN 2024 algorithms, WHO cutoffs, macro distributions).
     5. *Tier Quotas & Feature Gating* (tier quotas, paywalls, role authorization rules).
   - **Mandatory Synchronization Actions**:
     - Synchronize `README.md` (technology matrix, architecture diagrams, file tree, test metrics).
     - Synchronize `docs/architecture/diagrams/*.mermaid` (solution architecture, security perimeter).
     - Synchronize `docs/sdd/*.md` (02_solution_architecture, 04_security_and_compliance, etc.).
     - Synchronize `.agents/skills/*.md` (main skill and companion skills to preserve single source of truth).
     - Write a dedicated atomic ADR fragment in `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md` (Fragment Pattern for 100% merge-conflict immunity) and register it in `docs/adr/README.md` and `docs/sdd/07_living_documentation_log.md`.
5. **Step 4: Push Branch & PR-Only Merge**:
   - Push your branch to the remote repository:
     ```bash
     git push -u origin <branch-name>
     ```
   - **All changes MUST be merged into `main` using a Pull Request (PR) only**.
   - Direct merges or pushes to `main` without PR review and CI green-light are strictly forbidden.
   - Every PR description MUST include:
     1. Stack Navigation Callout (if stacked)
     2. Executive Summary & Purpose
     3. Changes Summary table
     4. Forward Roadmap & Reusability Impact
     5. Live CFT Execution Evidence (terminal output log from `tests/validate_e2e_tiers.ps1` and browser verification)

---

## 2. Technical & Architecture Standards
1. **Target Framework**: Strictly target `.NET 11` (`<TargetFramework>net11.0</TargetFramework>`) across all projects.
2. **Zero-Warning Standard**: 0 warnings, 0 errors. Eliminate nullability warnings and resolve vulnerabilities.
3. **Standalone Aspire AppHost**: Use `<Project Sdk="Aspire.AppHost.Sdk/13.5.4">`.
4. **Clinical Dietetics Governance**: Adhere strictly to the Indian Medical Standards (ICMR-NIN 2024 & WHO guidelines) and the Zero-Assumption Rule specified in the solution skill.
5. **Mandatory End-to-End Tier Verification**: No refactoring, new feature implementation, or bug fix is complete without verifying actual product behavior across all user tiers using the seeded demo accounts.
6. **Zero Documentation Drift Standard**: Never omit synchronizing README, Mermaid diagrams, SDD docs, ADRs, and agent skills after implementing major architectural or security enhancements.
7. **Mandatory Confirmation Protocol**: In case of ANY doubt, ambiguity, or multiple implementation paths, ask questions and seek confirmation using interactive tools (`ask_question`); do not make unilateral decisions on your own.
8. **Cross-Platform Phased Migration & No-Big-Bang Standard**: Web PWA and Mobile (Android & iOS) modernization and BFF implementation must never be implemented as massive all-in-one PRs. All work must follow the phased, platform-by-platform GitHub Stacked PR Protocol defined in [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md).
9. **Mandatory Platform-Specific CFT Documentation & Cross-Platform Parity Verification**: Every platform feature must have an executable Customer & Functional Acceptance Test (CFT) checklist in [`docs/cft/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/). Contributors and agents must execute the relevant CFT documents across all platforms to guarantee 100% functional parity and zero regressions before branch merge.
10. **Token Economics & Agentic Architecture Standard (SDDs vs. Skills Separation)**:
    - **System Prompt Token Conservation**: In agentic AI environments, the `name` and `description` of **every skill folder in `.agents/skills/`** is injected into the agent's baseline system prompt on **every single turn**. Storing large architectural specifications, threat models, or living logs in `.agents/skills/` permanently inflates the prompt token overhead and raises operational costs.
    - **The `docs/` Boundary (0 Baseline Tokens)**: Declarative system specifications, data models, clinical rules, migration roadmaps, and ADRs MUST remain in `docs/sdd/` and `docs/adr/`. They consume **0 baseline tokens** and are read on-demand via `view_file` only when needed for a specific task.
    - **The `.agents/skills/` Boundary (Actionable Playbooks)**: Keep `.agents/skills/` strictly for imperative, procedural "how-to" playbooks, concrete code recipes (e.g. SkiaSharp compression, Android permissions, CQRS handlers), and verification checklists.
    - **Strict Anti-Pattern**: NEVER move, duplicate, or convert declarative architectural specifications (`docs/sdd/*.md`), ADRs (`docs/adr/*.md`), or archive ledgers into `.agents/skills/`.
11. **Authoritative Subsystem & Skill Mapping (Web App Plan Codification Standard)**:
    - **Web App Modernization & Web BFF**: The entire Web App development plan, client-side SOLID architecture in native ES Modules, and Web BFF facade implementation are codified inside [`.agents/skills/diet-dost-clean-architecture/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-clean-architecture/SKILL.md) and its actionable reference playbook [`references/web_bff_clean_architecture_playbook.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-clean-architecture/references/web_bff_clean_architecture_playbook.md).
    - **Zero-Skill-Sprawl Rule for Web**: Agents must **NOT** create a separate `diet-dost-web-architecture` skill. Keeping Web BFF unified with Clean Architecture prevents prompt token bloat on every interaction and eliminates architectural drift between backend CQRS query handlers and frontend composite aggregation.
    - **Cross-Platform Phased Roadmap**: Master rollout sequence across Web, Android, and iOS is codified in [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md).
    - **Platform CFT Acceptance Suites**: Web PWA verification is in [`docs/cft/cft_web_bff_and_clean_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_web_bff_and_clean_architecture.md); Mobile verification is in [`docs/cft/cft_mobile_mvp_cross_platform.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_mobile_mvp_cross_platform.md); Parity verification is in [`docs/cft/cft_cross_platform_functional_parity_matrix.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/cft_cross_platform_functional_parity_matrix.md).
    - **Enterprise Database & Cloud Persistence**: Governed by [`.agents/skills/diet-dost-database-architecture/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-database-architecture/SKILL.md) (Azure SQL Serverless Free Tier, Azure Cosmos DB Free Tier, PostgreSQL Flexible Server, DPDPA 2023, and mobile offline SQLite sync).
    - **License Governance & Header Enforcement**: Governed by [`.agents/skills/diet-dost-license-governance/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-license-governance/SKILL.md) (dual AGPLv3 / SSPL v1 compliance, block comment formatting, and automated validation scripts).
12. **Living Documentation & Architectural Decision Records (ADR) Architecture (Token Economics & Merge Conflict Immunity)**:
    - **The Monolith Anti-Pattern (Eliminated)**: Appending to a single monolithic log is strictly prohibited. Monolithic logs exceed agent tool buffer limits (>46 KB), burn excessive tokens on string-matching retries, and cause deterministic Git merge conflicts across concurrent/stacked PRs.
    - **The Fragment Pattern Standard**: Every new architectural modification, feature, or defect fix must create a dedicated atomic ADR fragment in `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md` using `write_to_file`.
    - **Quantitative Benefits**:
      - **100% Merge-Conflict Immunity**: Each PR introduces an independent file; Git never encounters conflicting diffs on rebase.
      - **87% Token Reduction**: Lowers log-related reasoning and context consumption from ~145,000 tokens to ~18,500 tokens across a 10-PR roadmap.
      - **Deterministic Tool Execution**: Single-shot `write_to_file` eliminates fragile line-offset searches and chunk replacement errors.
    - **Archive Governance**: Historical entries (LOG-001 through LOG-042) reside in [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md), while [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md) act as lean indexes and standard registries.
13. **Autonomous Issue-Driven Development (IDD) & Local State Resumption Standard**:
    - **Local Workstation Execution**: Due to cloud agent credit limits, autonomous issue handling executes locally on the developer workstation via GitHub CLI (`gh`).
    - **CODEOWNER Gating & Non-Codeowner Approval**: Autonomous development triggers ONLY for issues authored by a CODEOWNER (from `.github/CODEOWNERS`). Issues authored by non-codeowners are placed on HOLD and MUST NOT trigger development until approved by a CODEOWNER via a slash comment (`/approve`, `/proceed`, `/start`) or the `approved-by-codeowner` label.
    - **Closed-Loop Lifecycle**: Every issue follows the strict `DETECT -> ACT -> IMPLEMENT -> VALIDATE -> TEST / RETEST -> DRAFT PR` sequence codified in [`.agents/skills/diet-dost-issue-driven-workflow/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-issue-driven-workflow/SKILL.md).
    - **Fault-Tolerant State Checkpointing**: All in-flight issue workflows persist checkpoint state in `.agents/state/issue_workflow_state.json`. If execution is interrupted by system power-down or network drops, tasks are safely held in memory and resume from the exact last saved phase upon system restart.
    - **Zero-Unilateral-Decision Enforcement**: If an issue contains any ambiguity, conflicting options, or unconfirmed requirements, the agent MUST pause and ask the user for confirmation via `ask_question` before proceeding to implementation.
    - **CFT & ADR Synchronization**: Every issue resolution must create/update its corresponding platform CFT in `docs/cft/` and record an atomic ADR fragment in `docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md`.
14. **Native GitHub Stacked PR Protocol & Automation Standard**:
    - **Native gh-stack Engine**: Contributors and AI agents must manage consecutive dependent PRs using the official `github/gh-stack` extension (`gh stack link <parent-pr> <child-pr>`, `gh stack sync`). Never leave stacked PRs in an unformalized state on GitHub.
    - **Zero-Ambiguity PR Navigation**: All stacked PRs must prepend the standardized markdown Stack Navigator callout (`### 🥞 GitHub Stack #<id> (Layer X of Y)`) in the PR description with direct bidirectional links to parent and child PRs.
    - **Cascading Bottom-Up Merge Order**: Stacks must be merged sequentially from base to top. Merging Layer 1 allows GitHub to auto-retarget Layer 2 to `main`, followed by local `gh stack sync`.
15. **Zero-Throwaway Engineering & Forward-Roadmap Reusability Standard**:
    - **Zero Disposable Code Policy**: MVP (Phase 1 / Alpha 01) implementations must NEVER be treated as throwaway prototypes or quick hacks. All domain models, value objects, CQRS handlers, DTO schemas, and client-side modules must be engineered in concert with future roadmap milestones (Phase 2 Mobile, Phase 3 Enterprise Cloud DB, Phase 4 Automated CI/CD).
    - **Backend & Domain Reusability**: CQRS query and command handlers built in `Nutrition.Application` for Web BFF must be directly consumable by Mobile BFF (`/api/mobile/v1/*`) with 0 duplicate clinical math. Persistence abstractions (`INutritionContext`) in Phase 1 SQLite must be strictly decoupled from provider quirks to enable seamless multi-provider injection for Phase 3 Azure SQL Serverless.
    - **Living CFT Acceptance Inheritance**: All Customer & Functional Acceptance Tests in `docs/cft/` are reusable, living verification harnesses. Phase 1 Web CFT suites define the invariant baseline that Phase 2 Mobile CFT and Master Parity Matrix inherit and re-validate. Tests must remain parameterized and valid across SQLite, Azure Files SMB, Azure SQL, and all clients.
    - **Mandatory Forward Roadmap Section in ADRs**: Every atomic ADR fragment (`docs/adr/ADR-<YYYYMMDD>-<NNN>-<slug>.md`) MUST include a dedicated section titled `Forward Roadmap Impact & Future Phase Compatibility`, certifying that the decision supports upcoming phases and creates zero technical debt.
16. **Authoritative UI/UX Design System Standard (DESIGN.md Compliance)**:
    - **Single Source of Truth**: [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md) is the absolute authority for all frontend UI/UX, surfaces, typography, tokens, and components across Diet-Dost.
    - **Mandatory Pre-UI Inspection**: Contributors and AI agents MUST inspect `DESIGN.md` and the existing design system tokens in `styles.css` prior to introducing or altering any visual components.
    - **Zero-Ad-Hoc Visual Conventions**: Never introduce arbitrary bright or saturated colors, atmospheric gradients, or custom non-standard border radiuses. Use the canonical four-step surface ladder (`canvas` #010102 → `surface-1` #0f1011 → `surface-2` #141516 → `surface-3` #18191a → `surface-4` #191a1b), hairline borders (`#23252a`), and Linear lavender-blue (`#5e6ad2`) chromatic accent.
    - **Mandatory Verification**: Every UI modification must be verified for `DESIGN.md` token compliance, responsive layout across all viewports (Mobile, Tablet, Desktop), touch target minimums ($\ge 44 \times 44\,\text{px}$), and accessibility contrast.
17. **Mandatory Secret & Environment Variable Governance Rule (Sole Authority: @nikunjbanker)**:
    - **Sole Authority Mandate**: Strictly and exclusively `@nikunjbanker` (Repository Owner & Lead Architect) has permission or authority to create, update, delete, view, or rotate any GitHub Environment Secrets, Repository Secrets, GitHub Environment Variables, Repository Variables, Azure Key Vault Secrets, or Azure Container App configuration settings.
    - **Zero-Unilateral Action Mandate**: AI agents, contributors, collaborators, and automated workflows are **strictly prohibited** from creating, updating, deleting, or exposing secrets or environment variables. No agent or contributor shall execute commands such as `gh secret set`, `gh secret delete`, `gh variable set`, `gh variable delete`, `az keyvault secret set`, or `az keyvault secret delete` unless explicitly instructed, authorized, or executed directly in an authenticated session belonging exclusively to `@nikunjbanker`.
    - **Zero-Plaintext Secret Mandate**: High-entropy or confidential variables (including `SUPER_ADMIN_EMAIL`, `REQUIRE_MOBILE_VERIFICATION`, `JWT_KEY`, `GEMINI_API_KEY`, `AZURE_OPENAI_API_KEY`) must NEVER be stored as plaintext environment variables (`gh variable set`) or in unencrypted source files (`appsettings.json`); they must reside exclusively as encrypted GitHub Secrets and Azure Key Vault secrets managed solely by `@nikunjbanker`.
    - **Workflow Actor Gating**: All CI/CD workflows and deployment pipelines (`azure-app-deploy.yml`, `azure-infra-deploy.yml`) that access, synchronize, or deploy secrets and environment variables MUST enforce explicit job-level (`if: github.actor == 'nikunjbanker'`) actor verification guards.
    - **Automated CI Regression Barrier**: Unit and integration tests (`SecurityEnvironmentAndHeaderTests.cs`) must continuously assert and fail CI if any deployment pipeline omits this actor restriction.

