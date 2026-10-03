<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Post-PR UI Validation Skill Package

Purpose: reusable, framework/cloud/IDE-independent post-PR UI validation.

## Contents

- `SKILL.md` — core reusable Antigravity skill
- `references/invocation-policy.md` — when to invoke it
- `references/validation-matrix.md` — required validation dimensions
- `references/responsive-validation.md` — web/tablet/mobile checks
- `references/evidence-rules.md` — evidence and anti-fabrication rules
- `templates/pr-validation-report.md` — report template
- `adapters/canonical-prompt.md` — IDE-independent prompt
- `adapters/antigravity.md` — Antigravity adapter
- `adapters/claude-code.md` — Claude Code adapter
- `adapters/github-copilot.md` — GitHub Copilot adapter

## Installation

Copy `SKILL.md` and its supporting directories to:

`.agents/skills/post-pr-ui-validation/`

Then connect the invocation policy to the repository's PR/agent workflow.

## Design principle

CFT = WHAT
Skill = HOW
Harness = WITH WHAT TOOLS
Loop = WHEN/WHY TO REPEAT
Evidence = PROOF
Human review = FINAL RESPONSIBILITY
