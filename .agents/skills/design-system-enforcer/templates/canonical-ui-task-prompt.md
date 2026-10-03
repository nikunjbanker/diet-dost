<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Canonical UI Task Prompt

ROLE
You are implementing UI/UX in an existing application.

OBJECTIVE
Implement the requested UI change while following the repository's `design-system-enforcer` skill and `DESIGN.md`.

TASK
[DESCRIBE THE UI TASK]

CONTEXT
Before modifying UI:
1. Read DESIGN.md.
2. Inspect relevant existing pages/components.
3. Identify reusable design tokens and components.
4. Preserve existing product behavior.

CONSTRAINTS
- DESIGN.md is the UI/UX source of truth.
- Reuse existing components where appropriate.
- Use design tokens instead of arbitrary hard-coded visual values.
- Do not invent a competing design convention.
- Preserve accessibility and responsive behavior.
- Do not modify unrelated functionality.

ACCEPTANCE CRITERIA
- UI follows DESIGN.md.
- Existing components/tokens are reused where appropriate.
- Relevant states are implemented.
- Responsive behavior is handled.
- Accessibility is preserved.
- Existing behavior remains intact.

VERIFICATION
- Review against DESIGN.md.
- Inspect the diff.
- Run relevant tests/build/lint.
- Perform browser/visual verification when available.
- Report anything not verified.

OUTPUT
Return:
1. Changes made.
2. DESIGN.md rules/tokens applied.
3. Components reused/created.
4. Verification performed.
5. Deviations or unknowns.
