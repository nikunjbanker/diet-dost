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

   d. **GitHub Stacked PR Protocol (For Consecutive / Dependent PRs)**:
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
      - **GitHub Stacked PR Mechanics**:
        - GitHub displays ONLY the diff introduced by the child feature against the parent branch.
        - When the parent PR merges into `main`, GitHub automatically updates the child PR's base branch to `main`.
        - Stacked PRs keep review sizes small, eliminate merge conflicts between dependent features, and prevent duplicate commits across PRs.

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
3. **Step 2: Perform End-to-End User Tier Validation**:
   - Run end-to-end product verification across all 5 demo user tiers (`free@dietdost.app`, `basic@dietdost.app`, `premium@dietdost.app`, `admin.demo@dietdost.app`, `superadmin@dietdost.app` with password `DietDost@Demo2026!`).
   - Validate that tier quotas, feature gating (photo comparison, data export, analytics history), and role permissions function accurately in the actual product with 0 runtime or console errors.
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
     - Append an entry to `docs/sdd/07_living_documentation_log.md`.
5. **Step 4: Push Branch & PR-Only Merge**:
   - Push your branch to the remote repository:
     ```bash
     git push -u origin <branch-name>
     ```
   - **All changes MUST be merged into `main` using a Pull Request (PR) only**.
   - Direct merges or pushes to `main` without PR review and CI green-light are strictly forbidden.

---

## 2. Technical & Architecture Standards
1. **Target Framework**: Strictly target `.NET 11` (`<TargetFramework>net11.0</TargetFramework>`) across all projects.
2. **Zero-Warning Standard**: 0 warnings, 0 errors. Eliminate nullability warnings and resolve vulnerabilities.
3. **Standalone Aspire AppHost**: Use `<Project Sdk="Aspire.AppHost.Sdk/13.5.4">`.
4. **Clinical Dietetics Governance**: Adhere strictly to the Indian Medical Standards (ICMR-NIN 2024 & WHO guidelines) and the Zero-Assumption Rule specified in the solution skill.
5. **Mandatory End-to-End Tier Verification**: No refactoring, new feature implementation, or bug fix is complete without verifying actual product behavior across all user tiers using the seeded demo accounts.
6. **Zero Documentation Drift Standard**: Never omit synchronizing README, Mermaid diagrams, SDD docs, and agent skills after implementing major architectural or security enhancements.
7. **Mandatory Confirmation Protocol**: In case of ANY doubt, ambiguity, or multiple implementation paths, ask questions and seek confirmation using interactive tools (`ask_question`); do not make unilateral decisions on your own.
8. **Cross-Platform Phased Migration & No-Big-Bang Standard**: Web PWA and Mobile (Android & iOS) modernization and BFF implementation must never be implemented as massive all-in-one PRs. All work must follow the phased, platform-by-platform GitHub Stacked PR Protocol defined in [`docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md).
9. **Mandatory Platform-Specific CFT Documentation & Cross-Platform Parity Verification**: Every platform feature must have an executable Customer & Functional Acceptance Test (CFT) checklist in [`docs/cft/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/). Contributors and agents must execute the relevant CFT documents across all platforms to guarantee 100% functional parity and zero regressions before branch merge.
10. **Token Economics & Agentic Architecture Standard (SDDs vs. Skills Separation)**:
    - **System Prompt Token Conservation**: In agentic AI environments, the `name` and `description` of **every skill folder in `.agents/skills/`** is injected into the agent's baseline system prompt on **every single turn**. Storing large architectural specifications, threat models, or living logs in `.agents/skills/` permanently inflates the prompt token overhead and raises operational costs.
    - **The `docs/sdd/` Boundary (0 Baseline Tokens)**: Declarative system specifications, data models, clinical rules, and migration roadmaps MUST remain in `docs/sdd/`. They consume **0 baseline tokens** and are read on-demand via `view_file` only when needed for a specific task.
    - **The `.agents/skills/` Boundary (Actionable Playbooks)**: Keep `.agents/skills/` strictly for imperative, procedural "how-to" playbooks, concrete code recipes (e.g. SkiaSharp compression, Android permissions, CQRS handlers), and verification checklists.
    - **Strict Anti-Pattern**: NEVER move, duplicate, or convert declarative architectural specifications (`docs/sdd/*.md`) or living logs (`docs/sdd/07_living_documentation_log.md`) into `.agents/skills/`.


