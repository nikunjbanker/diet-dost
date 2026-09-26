---
name: diet-dost-license-governance
description: Authoritative license governance, header enforcement, dual AGPLv3/SSPL v1 compliance, and automated verification guide for Diet-Dost (.NET 11, Web, Mobile).
---

# License Governance & Source Header Enforcement Specification: Diet-Dost

> **Licensing Framework**: Dual License under the **GNU Affero General Public License v3.0 only (AGPLv3)** and the **Server Side Public License, v 1 (SSPL)**  
> **Permissive Compatibility**: Apache License 2.0 compatible for external/shared library integrations  
> **Copyright Holder**: `diet-dost and/or its contributors` (2026)  
> **Enforcement Scope**: All primary source code files (`.cs`, `.js`, `.ts`, `.css`, `.sql`, `.ps1`)  

---

## 1. Executive Summary & Licensing Model

To protect **Diet-Dost** from predatory closed-source commercial re-hosting while empowering the open-source community, the project enforces a dual-licensing regime as established in `LICENSE` and `CONTRIBUTING.md`:

1. **GNU Affero General Public License v3.0 only (AGPLv3)**: Guarantees full copyleft freedom across network services.
2. **Server Side Public License, v 1 (SSPL)**: Ensures commercial SaaS providers contributing to or hosting the software provide corresponding service source code.
3. **Apache 2.0 Compatibility**: Modules, SDKs, or external imports specifically tagged may operate under Apache 2.0 compatible terms.

Every newly introduced or refactored source code file must maintain the authoritative copyright header at the top of the file before any package, using, or module imports.

---

## 2. Authoritative Header Templates

### 2.1 C# (`.cs`), JavaScript (`.js`), TypeScript (`.ts`), and CSS (`.css`)

Apply the standard block comment format:

```csharp
/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
```

### 2.2 PowerShell (`.ps1`)

Apply the PowerShell block comment format:

```powershell
<#
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
#>
```

### 2.3 SQL (`.sql`)

Apply the SQL block comment format:

```sql
/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
```

### 2.4 Markdown Documentation & Agent Skills (`.md`)

For markdown files without frontmatter, prepend to line 1. For skills (`SKILL.md`) with YAML frontmatter (`---`), place immediately after the closing `---` delimiter:

```markdown
<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->
```

### 2.5 Architecture Mermaid Diagrams (`.mermaid`)

Apply the Mermaid comment syntax (`%%`):

```mermaid
%%
%% Copyright (c) 2026 diet-dost and/or its contributors.
%% Licensed under the "GNU Affero General Public License v3.0 only" and
%% the "Server Side Public License, v 1"; you may not use this file except
%% in compliance with, at your election, the "GNU Affero General Public
%% License v3.0 only" or the "Server Side Public License, v 1".
%%
```

---

## 3. Scope & Exclusion Rules

### 3.1 Included File Extensions & Asset Types
- `.cs` — C# source files (Domain, Application, Infrastructure, WebGateway, AppHost, ServiceDefaults, Tests)
- `.js` / `.ts` — Frontend vanilla ES Modules, service workers (`sw.js`), and UI components
- `.css` — Design system and component stylesheet files
- `.sql` — Database migration scripts, DDL definitions, seed scripts
- `.ps1` — Automation, validation, and test harness scripts
- `.md` — Agent skills (`.agents/skills/**/SKILL.md`), rules (`.agents/rules/*.md`), CFT acceptance checklists (`docs/cft/*.md`), architecture specifications (`docs/architecture/*.md`), SDD documentation (`docs/sdd/*.md`), and solution rules (`AGENTS.md`)
- `.mermaid` — Solution architecture and multi-dimensional diagrams (`docs/architecture/diagrams/*.mermaid`)

### 3.2 Excluded Files & Directories
- **Build Artifacts & Cache**: `bin/`, `obj/`, `.vs/`, `.idea/`, `node_modules/`, `TestResults/`
- **Git & System Metadata**: `.git/`, `.gitignore`, `.gitattributes`
- **Plain Text & Data Formats**: `.json`, `.jsonc`, `.xml`, `.csv` (strict data formats where comments are invalid or non-standard)
- **Living Logs & Historical Archive**: `docs/sdd/logs/` and `docs/sdd/archive/` (immutable historical records)
- **Auto-Generated Output**: Entity Framework Core migration snapshots or Roslyn source-generated code containing `<auto-generated />` tags.

---

## 4. Automated Header Verification Playbook

Run the automated PowerShell verification script:

```powershell
./.agents/skills/diet-dost-license-governance/scripts/verify_license_headers.ps1
```

To automatically prepend missing headers across all eligible source files:

```powershell
./.agents/skills/diet-dost-license-governance/scripts/apply_license_headers.ps1
```

### 4.1 Verification Criteria
- [ ] 100% of non-generated `.cs`, `.js`, `.ts`, `.css`, `.sql`, `.ps1` files start with the license header.
- [ ] No duplicate headers exist on any file.
- [ ] `dotnet build` succeeds with 0 errors and 0 warnings.
- [ ] `dotnet test` executes with 100% pass rate.
