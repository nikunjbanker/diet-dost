# SDD 07: Living Documentation Index & Agentic Fragment Ledger
> **Specification Version**: `v2.0.0 (Agentic Fragment Architecture)`  
> **Pattern**: Distributed Changeset / Fragment Pattern (Zero Merge Conflicts & Zero Token Waste)  
> **Mandate**: Zero Documentation Drift Mandate & Token Economics Standard  

---

## 1. Living Documentation Architecture Overview

To achieve **zero Git merge conflicts** in multi-branch/stacked PR workflows and **conserve context window tokens** for agentic AI development, Diet-Dost uses the **Fragment / Changeset Pattern**:

1. **Legacy Historical Archive**:
   - All 42 historical entries from `LOG-20260914-001` through `LOG-20260926-042` are permanently preserved in [`docs/sdd/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/archive/living_log_2026_09_archive.md).
2. **Atomic Log Fragments (`docs/sdd/logs/`)**:
   - For every new feature, architectural milestone, or bug fix, agents and contributors create a **single, standalone file** inside [`docs/sdd/logs/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/):
     `docs/sdd/logs/LOG-<YYYYMMDD>-<NUMBER>-<slug>.md`
   - **Zero Merge Conflicts**: Because each PR creates an independent file, concurrent and stacked PRs can be merged and rebased without conflicting on a monolithic log.
   - **Zero String-Replace Failures**: Agents use `write_to_file` to create atomic logs in a single tool call, avoiding fragile line-offset math and chunk-matching retries.
   - **Token Economy**: Agents read and write only the small, relevant fragment (~350 tokens) rather than a 2,800-line monolith (~61,000 tokens).

---

## 2. Standardized Atomic Log Fragment Specification

Every new fragment in [`docs/sdd/logs/`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/) must follow this concise, high-density template:

```markdown
# [LOG-YYYYMMDD-NNN] <Descriptive Title>
- **Timestamp**: YYYY-MM-DDTHH:mm:ss+05:30
- **Driver / Agent**: AI Assistant & User Pair-Programming
- **Change Type**: [FEATURE] / [DEFECT_FIX] / [ARCHITECTURE] / [GOVERNANCE]
- **Affected Subsystems**: WebGateway / Application / Domain / Infrastructure / Mobile
- **Summary of Change**:
  <2-3 bullet points describing what changed and the technical rationale>
- **Associated PR & Stack**: PR #<number> (Base: <branch-name>)
- **Relevant SDDs & CFTs**: `docs/sdd/08_*.md`, `docs/cft/*.md`
- **Verification Result**:
  - `dotnet test`: 129 passed, 0 failed, 0 warnings
  - E2E Tier Validation: Verified across Free, Basic, Premium, Admin, SuperAdmin
- **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
```

---

## 3. Fragment Registry & Active Log Index

| Fragment ID | Title & Subsystem | Change Type | Path |
| :--- | :--- | :--- | :--- |
| **LOG-040** | Token Economics & SDD vs Skill Separation | `[GOVERNANCE]` | [`LOG-20260926-040-token-economics-standard.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/LOG-20260926-040-token-economics-standard.md) |
| **LOG-041** | Web App Plan Retention in Clean Architecture Skill | `[ARCHITECTURE]` | [`LOG-20260926-041-web-bff-retention.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/LOG-20260926-041-web-bff-retention.md) |
| **LOG-042** | Web App Plan Codification Standard in Memory | `[GOVERNANCE]` | [`LOG-20260926-042-web-app-plan-codification.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/LOG-20260926-042-web-app-plan-codification.md) |
| **LOG-043** | Transition to Distributed Fragment Pattern for Living SDD | `[ARCHITECTURE]` | [`LOG-20260926-043-living-doc-fragment-pattern.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/logs/LOG-20260926-043-living-doc-fragment-pattern.md) |

---

## 4. Archive Index

| Archive Ledger | Coverage | Entries | Path |
| :--- | :--- | :--- | :--- |
| **September 2026 Archive** | Initial bootstrap through mobile strategy | LOG-001 to LOG-042 | [`docs/sdd/archive/living_log_2026_09_archive.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/archive/living_log_2026_09_archive.md) |
