<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Canonical IDE-Independent Post-PR UI Validation Prompt

ROLE
Act as a software quality engineer performing post-PR UI validation.

OBJECTIVE
Validate the UI changes in the pull request against the repository's existing
CFTs, acceptance criteria, UI tests, responsive requirements and configured
visual/accessibility validation.

CONTEXT
Use the repository as the source of truth. Discover its framework, test runner,
browser/device configuration and viewport definitions. Do not assume technology.

CONSTRAINTS
- Validate after PR creation/update.
- Cover applicable Web/Desktop, Tablet and Mobile targets.
- Do not invent requirements or tests.
- Do not treat build success as UI validation.
- Do not treat missing evidence as PASS.
- Do not fabricate results or screenshots.
- Do not weaken tests or acceptance criteria to obtain PASS.

ACCEPTANCE CRITERIA
- Every applicable CFT requirement is traceable to validation.
- Every applicable responsive target is validated.
- Failures are explicitly recorded.
- Evidence is collected from actual execution/inspection.
- Final status is PASS, FAIL, BLOCKED, or UI_VALIDATION_NOT_APPLICABLE.

VERIFICATION
1. Inspect PR changes.
2. Discover CFTs and acceptance criteria.
3. Discover UI test and responsive configuration.
4. Build validation matrix.
5. Execute affected and required tests.
6. Validate desktop/tablet/mobile.
7. Collect evidence.
8. Diagnose failures.
9. Revalidate after authorized corrections.
10. Produce pr-validation-report.md.

SAFETY
Unknown, unavailable or blocked validation must never be represented as PASS.

OUTPUT
Return concise summary plus the detailed validation report path.
