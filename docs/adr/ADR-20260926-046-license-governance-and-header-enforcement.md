<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260926-046: License Governance, Dual AGPLv3/SSPL v1 Compliance & Header Enforcement

> **Date**: 2026-09-26  
> **Status**: ACCEPTED  
> **Driver / Deciders**: Antigravity Living Documentation Engine & User Pair-Programming  
> **Change Type**: `[GOVERNANCE]`, `[COMPLIANCE]`, `[LICENSING]`  
> **Affected Subsystems**: Entire Solution (`.agents/skills/diet-dost-license-governance/`, `AGENTS.md`, `.github/CODEOWNERS`)  
> **Associated PR & Stack**: PR #21 (Base: `main`)  
> **Relevant SDDs & CFTs**: [`docs/sdd/00_sdd_index.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/00_sdd_index.md), [`.agents/skills/diet-dost-license-governance/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-license-governance/SKILL.md)  

---

## 1. Executive Summary & Purpose

In alignment with the repository's foundational [`LICENSE`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/LICENSE) and [`CONTRIBUTING.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/CONTRIBUTING.md), this decision records the formal establishment of the **License Governance & Source Header Enforcement** standard across the Diet-Dost solution.

The project operates under a protective dual-licensing regime:
1. **GNU Affero General Public License v3.0 only (AGPLv3)**
2. **Server Side Public License, v 1 (SSPL)**
3. Optional Apache 2.0 compatibility for permissively imported external libraries / client SDKs.

---

## 2. Key Deliverables & Architectural Actions

1. **Dedicated Governance Skill**:
   - Codified [`.agents/skills/diet-dost-license-governance/SKILL.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-license-governance/SKILL.md) defining authoritative header templates across C#, JavaScript, TypeScript, CSS, SQL, and PowerShell.
   - Preserves token conservation by maintaining procedural and automation scripts inside the skill boundary while avoiding prompt bloat.

2. **Automated Header Application & Verification Scripts**:
   - Created [`.agents/skills/diet-dost-license-governance/scripts/apply_license_headers.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-license-governance/scripts/apply_license_headers.ps1) for idempotent batch header injection.
   - Created [`.agents/skills/diet-dost-license-governance/scripts/verify_license_headers.ps1`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/.agents/skills/diet-dost-license-governance/scripts/verify_license_headers.ps1) for automated CI / pre-commit validation.

3. **Solution-Wide Header Application**:
   - Evaluated 198 total candidate files across the entire workspace:
     - 164 source files (`.cs`, `.js`, `.css`, `.ps1`)
     - 29 Markdown documentation and playbook files across `.agents/skills/`, `.agents/rules/`, `docs/cft/`, `docs/sdd/`, `docs/architecture/`, and `AGENTS.md`
     - 5 Mermaid architectural diagram files (`docs/architecture/diagrams/*.mermaid`)
   - Applied the authoritative dual AGPLv3 / SSPL v1 header formatted appropriately for each target syntax (C-style block comments `/* ... */`, PowerShell `<# ... #>`, HTML `<!-- ... -->`, and Mermaid `%%`).
   - For agent skills (`SKILL.md`), preserved mandatory YAML frontmatter at line 1 and cleanly injected the license comment immediately following the closing delimiter.
   - Verification check passed with 100% compliance (`198/198 files compliant`).

4. **GitHub Code Ownership (`.github/CODEOWNERS`)**:
   - Established `.github/CODEOWNERS` assigning global and subsystem code ownership to repository owner `@nikunjbanker`.
   - Ensures any future external pull requests trigger review requests for `@nikunjbanker` while allowing full operational freedom for repository maintenance.

5. **Zero Regression & Build Validation**:
   - `dotnet build --configuration Release`: Build succeeded with **0 warnings and 0 errors**.
   - `dotnet test --configuration Release`: 100% test pass rate (**129/129 tests passed**).
