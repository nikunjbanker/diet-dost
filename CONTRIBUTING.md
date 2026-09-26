<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Contributing to Diet Dost

Thank you for your interest in contributing to **Diet Dost**! To maintain a healthy open-source ecosystem while protecting the project from predatory commercial hosting and maintaining enterprise-grade code quality, we use a dual-licensing model and strict GitHub branch governance.

---

## 1. Licensing Agreement

By contributing your code or documentation to the `diet-dost` repository, you agree that your contributions will be licensed under the project's dual license:
1. **GNU Affero General Public License v3.0 only (AGPLv3)**
2. **Server Side Public License, v 1 (SSPL)**

You retain the copyright to your individual contributions, but you grant the project managers and the community the right to use, modify, and distribute your code under these terms.

### Permissive Compatibility
If you import or adapt external code, that code must be under a license compatible with our dual license (such as Apache License 2.0 or MIT).

### Mandatory License Header
Every new source file (`.cs`, `.js`, `.ts`, `.css`, `.sql`, `.ps1`), Markdown documentation (`.md`), and Mermaid diagram (`.mermaid`) must include the project's standard license header.

#### Block Comment (`.cs`, `.js`, `.ts`, `.css`, `.sql`):
```csharp
/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
```

#### Markdown Comment (`.md`):
```markdown
<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->
```

You can automatically verify that all files contain valid headers by running:
```powershell
pwsh -File .agents/skills/diet-dost-license-governance/scripts/verify_license_headers.ps1
```

---

## 2. GitHub Governance & Branch Rulesets

The `main` branch is actively protected by GitHub Rulesets. All contributors and collaborators must adhere to the following workflow:

### Zero Direct-to-Main Policy
* **Direct pushes to `main` are strictly blocked**. All changes must arrive via a Pull Request (PR).
* Branch deletion and force pushes (`git push --force`) on `main` are disabled.

### Code Owners & Approval Requirement
* **Automated Review Routing**: Under [`.github/CODEOWNERS`](.github/CODEOWNERS), all repository files are owned by the maintainer, [`@nikunjbanker`](https://github.com/nikunjbanker).
* **Mandatory Review**: All PRs submitted by contributors and collaborators require at least **1 approving review from the Code Owner (`@nikunjbanker`)** before they can be merged.
* **Dismiss Stale Approvals on Push**: If you push new commits to an existing PR, any previous approval is automatically reset to ensure every incremental change is reviewed.

---

## 3. Step-by-Step Development Workflow

### Step 1: Pre-Flight Fetch & Dedicated Branch
Always fetch from `origin` before creating a new branch to avoid stale histories:

```bash
# Update local remote tracking branches
git fetch origin

# Create your dedicated branch off origin/main
git checkout -b feature/<descriptive-name> origin/main   # For new features
git checkout -b fix/<defect-name> origin/main           # For bug fixes
git checkout -b chore/<task-name> origin/main           # For maintenance/docs
```

### Step 2: Code Quality & Local Testing
* Strictly target `.NET 11` (`net11.0`).
* Maintain the **Zero-Warning Standard**: 0 compiler warnings, 0 nullable reference warnings, 0 test failures.
* Validate locally before pushing:
  ```bash
  dotnet build --configuration Release
  dotnet test --configuration Release
  ```

### Step 3: Open a Pull Request
1. Push your branch to your fork or the repository:
   ```bash
   git push -u origin <your-branch-name>
   ```
2. Open a Pull Request against the `main` branch.
3. Fill out the PR template with a clear description of your changes and verification steps.
4. GitHub will automatically tag [`@nikunjbanker`](https://github.com/nikunjbanker) for Code Owner review.
