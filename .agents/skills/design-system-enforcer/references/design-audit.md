<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Design Audit

Use when the task is to audit existing UI rather than implement a single feature.

## Audit dimensions

### Consistency
- tokens
- typography
- spacing
- colors
- radii
- elevation
- components
- states

### UX
- hierarchy
- navigation
- feedback
- empty/loading/error states
- forms
- interaction patterns

### Responsive
- mobile
- tablet
- desktop
- wide layouts

### Accessibility
- keyboard
- focus
- contrast
- semantics
- accessible names
- touch

### Implementation
- token usage
- component reuse
- duplicated components
- hard-coded values
- CSS/style architecture
- DESIGN.md/token synchronization

## Finding format

For each finding:

- Severity
- Location
- Observed behavior
- Applicable DESIGN.md rule
- Impact
- Recommended correction
- Verification

Never invent a DESIGN.md rule.
