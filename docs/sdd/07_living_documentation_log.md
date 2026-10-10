<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# SDD 07: Living Documentation Index & Architectural Decision Records (ADRs)
> **Specification Version**: `v2.1.0 (Unified ADR Architecture)`  
> **Pattern**: Distributed Atomic ADR Fragment Pattern (Zero Merge Conflicts & Zero Token Waste)  
> **Authoritative ADR Registry**: [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md)  
> **Mandate**: Zero Documentation Drift Mandate & Token Economics Standard  

---

## 1. Unified Living Architecture Overview

To achieve **zero Git merge conflicts** in multi-branch/stacked PR workflows and **conserve context window tokens** for agentic AI development, all architectural decisions, technology evolutions, and living changes are unified into the dedicated **`docs/adr/`** directory.

1. **Dedicated ADR Repository & Registry**:
   - The central index, lifecycle rules, and templates reside in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md).
2. **Atomic ADR Fragments (`docs/adr/`)**:
   - For every new feature, architectural milestone, or major change, agents and contributors create a **single, standalone file** inside [`docs/adr/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/):
     `docs/adr/ADR-<YYYYMMDD>-<NUMBER>-<slug>.md`
   - **Zero Merge Conflicts**: Because each PR creates an independent file, concurrent and stacked PRs can be merged and rebased without conflicting on a monolithic log.
   - **Zero String-Replace Failures**: Agents use `write_to_file` to create atomic records in a single tool call, avoiding fragile line-offset math and chunk-matching retries.
   - **Token Economy**: Stored in `docs/adr/` (outside `.agents/skills/`), consuming **0 baseline prompt tokens**. Agents read and write only the small, relevant record (~350 tokens) rather than an entire monolithic history.
3. **Historical Decision Archive**:
   - Historical entries (`LOG-001` through `LOG-042`) covering early inception through mobile strategy are preserved with complete data integrity in [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md).

---

## 2. Standardized ADR Fragment Template Specification

Every new record in [`docs/adr/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/) must follow this unified, high-density specification (codified in [`docs/adr/template.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/template.md)), combining the original atomic fragment metadata with standard MADR decision sections:

```markdown
# ADR-[YYYYMMDD-NNN]: [Short Title of Architectural Decision]

> **Date / Timestamp**: YYYY-MM-DDTHH:mm:ss+05:30  
> **Status**: [PROPOSED | ACCEPTED | SUPERSEDED | DEPRECATED]  
> **Driver / Agent / Deciders**: [AI Assistant & User Pair-Programming]  
> **Change Type**: [ARCHITECTURE | FEATURE | DEFECT_FIX | GOVERNANCE | SECURITY | DATABASE | TOKEN_ECONOMICS]  
> **Affected Subsystems**: [WebGateway | Application | Domain | Infrastructure | Mobile | Presentation]  
> **Associated PR & Stack**: PR #<number> (Base: <branch-name>)  
> **Relevant SDDs & CFTs**: [`docs/sdd/08_*.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/), [`docs/cft/*.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/cft/)  

---

## 1. Executive Summary & Change Rationale
* <2-3 bullet points describing what changed and the technical rationale>
* <Key user, architecture, clinical, or performance motivation>

---

## 2. Context and Problem Statement
[Describe the context, technical challenges, and driving constraints that require an architectural decision. What problem are we solving?]

---

## 3. Decision Drivers
* [Driver 1: e.g., Token Economics - 0 baseline system prompt tokens]
* [Driver 2: e.g., Zero merge conflicts across concurrent/stacked PRs]
* [Driver 3: e.g., Clinical correctness / ICMR-NIN 2024 compliance]
* [Driver 4: e.g., Zero duplicate documentation or code]

---

## 4. Considered Options
* **Option 1**: [Description of Option 1]
* **Option 2**: [Description of Option 2]
* **Option 3**: [Description of Option 3]

---

## 5. Decision Outcome
* **Chosen Option**: [Option X: Title]
* **Justification**: [Explain why this option was chosen over alternatives. Detail the trade-offs accepted.]

---

## 6. Consequences & Trade-Offs

### Positive Consequences:
* [Positive impact 1]
* [Positive impact 2]

### Negative Consequences / Accepted Trade-Offs:
* [Negative impact or trade-off 1]
* [Mitigation for negative impact]

---

## 7. Verification & Compliance Results
* **Automated Builds & Tests**:
  - `dotnet build`: 0 warnings, 0 errors
  - `dotnet test`: 129 passed, 0 failed, 0 warnings
* **E2E Tier Validation**: Verified across user tiers (Free, Basic, Premium, Admin, SuperAdmin)
* **Living Documentation Synchronization**: Registered in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and [`docs/sdd/07_living_documentation_log.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/07_living_documentation_log.md)
* **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
```

---

## 3. Active ADR Registry Summary & Domain Matrix

The authoritative register and full decision details are canonically maintained in [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) and indexed via machine-readable [`docs/adr/index.json`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/index.json):

| Subsystem Domain | Focus & Architectural Scope | Active Decisions | Authoritative Directory |
| :--- | :--- | :--- | :--- |
| **🏛️ Architecture & Domain Logic** | Clean Architecture, CQRS, Database persistence, Options Pattern, BFF facades | **13** | [`docs/adr/architecture/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/architecture/) |
| **🛡️ Security & Identity** | Authentication, RBAC, Key Vault secrets, OWASP LLM defense, CodeQL/SAST gates | **12** | [`docs/adr/security/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/security/) |
| **🚀 DevOps & Cloud Infrastructure** | Azure Container Apps, .NET Aspire deployment, SQLite SMB persistence, OIDC pipelines | **3** | [`docs/adr/devops/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/devops/) |
| **🎨 Presentation & UI/UX** | Client hydration, responsive mobile/tablet layout, touch ergonomics, modals | **4** | [`docs/adr/presentation/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/presentation/) |
| **⚖️ Governance & Standards** | Token economics, Stacked PR protocols, ADR architecture, issue-driven workflow | **12** | [`docs/adr/governance/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/governance/) |

> **Automated Synchronization**: This index is maintained without manual table-editing errors via `pwsh -File scripts/sync-adr-index.ps1` (or `python3 scripts/sync-adr-index.py`). See [`docs/adr/README.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/README.md) for individual records.


---

## 4. Archive Index

| Archive Ledger | Coverage | Entries | Path |
| :--- | :--- | :--- | :--- |
| **September 2026 Archive** | Initial bootstrap through mobile strategy | LOG-001 to LOG-042 | [`docs/adr/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/adr/archive/living_log_2026_09_archive.md) |
