# Solution Engineering Rules & Instructions: Diet-Dost

This solution-level instruction file defines mandatory engineering and Git workflows for any AI agent or human contributor working on the **Diet-Dost** repository.

---

## 1. Mandatory Git Branching & PR-Only Merge Workflow

> [!IMPORTANT]
> **Zero Direct-to-Main Policy**: Direct commits or direct pushes to the `main` (or default production) branch are **strictly prohibited**.

### Step-by-Step Workflow:
1. **Step 0: Always Create a Branch First**:
   Before modifying any code, configuration, or documentation, create and check out a dedicated branch:
   ```bash
   git checkout -b feature/<descriptive-name>   # For new features or enhancements
   git checkout -b fix/<defect-name>           # For bug or defect fixes
   git checkout -b docs/<topic-name>           # For documentation changes
   ```
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
