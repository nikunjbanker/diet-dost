<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

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
