<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Post-PR UI Validation Matrix

| Target | CFT | Functional | Responsive | Visual | Accessibility | Evidence | Status |
|---|---|---|---|---|---|---|---|
| Web/Desktop | Required | Required | Required | If configured | If configured | Required | |
| Tablet | Required | Required | Required | If configured | If configured | Required | |
| Mobile | Required | Required | Required | If configured | If configured | Required | |

## Status vocabulary
- PASS
- FAIL
- BLOCKED
- NOT_APPLICABLE
- NOT_DEFINED

Never convert BLOCKED or NOT_DEFINED into PASS.

## Traceability rule
Every required CFT item must map to:
1. a test, or
2. a direct observation,
3. with evidence.

## Change-triggered scope
Prioritize:
1. directly changed UI
2. affected routes/components
3. responsive behavior
4. related regression coverage
5. broader UI suite when practical
