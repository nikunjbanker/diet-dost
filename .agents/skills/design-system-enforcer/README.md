<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# design-system-enforcer

Reusable agent skill for enforcing a repository's `DESIGN.md` across new and ongoing UI development.

## Structure

- `SKILL.md` — canonical skill
- `references/` — progressive-disclosure checklists
- `adapters/` — lightweight IDE/environment adapters
- `templates/` — canonical UI task prompt

## Installation

Copy `SKILL.md` to:

```text
.agents/skills/design-system-enforcer/SKILL.md
```

Keep `DESIGN.md` at the repository root when practical.

Then connect the lightweight adapter appropriate to the coding environment.

## Operating model

```text
DESIGN.md
    ↓
design-system-enforcer
    ↓
IDE adapter
    ↓
existing tokens/components
    ↓
application UI
    ↓
verification
```

The skill is the source of implementation rules. IDE adapters should remain small.

## Applies to

- new development
- ongoing development
- enhancements
- bug fixes
- refactoring
- responsive work
- accessibility work
- design-system changes
- UI audits

## Source note

The skill is designed around the current getdesign.md description of `DESIGN.md` as a single source of truth for visual design tokens and component guidance. The local repository `DESIGN.md` always takes precedence over assumptions about any external reference.
