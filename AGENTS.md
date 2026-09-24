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
3. **Step 2: Synchronize Living Documentation**:
   - Adhere to the Zero Documentation Drift Mandate (`docs/sdd/*.md`).
   - Append an entry to `docs/sdd/07_living_documentation_log.md`.
4. **Step 3: Push Branch & PR-Only Merge**:
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
