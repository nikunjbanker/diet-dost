<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20260926-040: Token Economics & Agentic Memory Rule: SDD vs. Skill Separation Standard
- **Timestamp**: 2026-09-26T13:51:00+05:30
- **Driver / Agent**: AI Assistant (Agentic Infrastructure & Token Economics) & User Pair-Programming
- **Status**: ACCEPTED
- **Change Type**: `[TOKEN_ECONOMICS]`, `[GOVERNANCE]`, `[SKILL]`, `[MEMORY]`
- **Affected Subsystems**: Governance / Memory (`AGENTS.md`), Solution Skill (`indian-diet-calorie-tracker`)
- **Summary of Change**:
  - Codified why SDDs MUST remain in `docs/sdd/` (0 baseline tokens) and NOT be moved to `.agents/skills/`.
  - Skill metadata is injected into the agent system prompt on every interaction, while SDDs are read on-demand via `view_file`.
  - Added Standard 10 to `AGENTS.md` and Rule 11 to `indian-diet-calorie-tracker/SKILL.md`.
- **Associated PR & Stack**: PR #20 (Base: `feature/azure-deployment-strategy-and-skill`)
- **Relevant SDDs & CFTs**: `docs/sdd/00_sdd_index.md`, `docs/sdd/08_cross_platform_bff_clean_architecture_migration_plan.md`
- **Verification Result**:
  - `dotnet test`: 129 passed, 0 failed, 0 warnings
  - Solution memory: 100% codified across primary solution skill and `AGENTS.md`
- **Sign-Off Status**: `VERIFIED & SYNCHRONIZED`
