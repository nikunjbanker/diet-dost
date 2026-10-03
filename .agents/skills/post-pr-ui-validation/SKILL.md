---
name: post-pr-ui-validation
description: >
  Validates UI changes after a pull request is created or updated.
  Discovers repository-defined CFTs, acceptance criteria, UI test cases,
  responsive viewport requirements, browser configuration, and visual
  regression configuration. Executes applicable validation for desktop/web,
  tablet, and mobile, collects evidence, and produces a deterministic
  PASS/FAIL/BLOCKED validation report. Framework and cloud agnostic.
---

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Post-PR UI Validation

## Purpose

Perform independent UI validation after a pull request is created or updated.

For every PR that changes UI behavior, layout, styling, responsive behavior,
frontend code/configuration, or user-facing UI:

> Validate the affected UI after PR creation or PR update.

Use repository-defined CFTs, acceptance criteria, tests, responsive targets,
visual-regression configuration, and accessibility requirements as the source
of truth. Do not invent requirements.

## Core operating loop

SEE → THINK → DELEGATE → VERIFY → CORRECT → MEASURE

This skill primarily implements VERIFY → CORRECT → MEASURE.

## Phase 1 — Establish PR context

Identify:
- PR identifier
- source and target branches
- changed files
- changed UI components/routes
- changed styles/responsive behavior
- changed UI tests
- changed CFTs/acceptance criteria

If there is no UI impact, return `UI_VALIDATION_NOT_APPLICABLE` with evidence.

## Phase 2 — Discover repository truth

Search before execution for:
- CFTs and acceptance criteria
- feature specifications and ADRs
- UI specifications/design references
- Playwright, Cypress, Selenium/WebDriver or equivalent
- visual regression tests/baselines
- accessibility tests
- repository test scripts
- browser/device profiles
- desktop/tablet/mobile viewport definitions

Do not assume a framework. Use configured repository tooling.

## Phase 3 — Build validation matrix

Minimum targets:
- Web/Desktop
- Tablet
- Mobile

Each applicable target must be classified as:
- PASS
- FAIL
- BLOCKED
- NOT_APPLICABLE
- NOT_DEFINED

`BLOCKED` and `NOT_DEFINED` are never equivalent to PASS.

## Phase 4 — Execute validation

Prioritize:
1. PR-affected UI tests
2. CFT/acceptance-criteria tests
3. responsive tests
4. visual regression tests
5. relevant regression tests
6. broader UI suite when practical

Do not skip a required validation solely because a narrower test passed.

## Phase 5 — Responsive validation

For every affected page/component, validate repository-defined targets.

### Web/Desktop
Check:
- page load
- primary user flows
- navigation
- layout integrity
- clipping/overflow
- interactive controls

### Tablet
Check:
- responsive layout
- navigation
- component resizing
- wrapping and spacing
- touch usability
- horizontal overflow
- modal/dialog behavior
- tables/cards/forms where applicable

### Mobile
Check:
- responsive layout
- menu/navigation
- wrapping and visibility
- horizontal overflow
- touch usability
- forms
- modals/dialogs
- sticky/fixed elements
- viewport-specific behavior

Use configured device profiles when available. If none exist, record that fact before
selecting a reasonable validation configuration.

## Phase 6 — CFT traceability

For every applicable CFT requirement:
1. Locate the requirement.
2. Identify affected UI.
3. Map the requirement to a test or direct observation.
4. Execute/inspect the validation.
5. Record evidence.

Do not rewrite or silently reinterpret the CFT.

If verification is impossible, record `BLOCKED` or `UNKNOWN` and explain why.

## Phase 7 — Evidence

Acceptable evidence includes:
- actual test output
- screenshots
- browser traces/videos
- console output
- accessibility results
- visual diffs
- test reports
- PR checks

Evidence must come from the actual validation run.

Never fabricate screenshots, test results, coverage, CI results, or measurements.

## Phase 8 — Failure/revalidation loop

On failure:
1. Identify requirement.
2. Identify failing test/observation.
3. Diagnose likely root cause.
4. If authorized, implement the smallest appropriate correction.
5. Re-run affected validation.
6. Re-run related regression tests.
7. Update the report.

Do not declare success before corrected validation passes.

## Phase 9 — Final status

Use exactly one:
- `PASS` — all required validations passed with sufficient evidence.
- `FAIL` — one or more required validations failed.
- `BLOCKED` — required validation could not be executed or verified.

Missing evidence never becomes PASS.

## Safety

Never:
- invent requirements/tests/results
- fabricate evidence
- expose secrets
- bypass repository policies
- disable failing tests to obtain PASS
- change acceptance criteria to make validation pass
- silently overwrite important files
- modify production systems without authorization

## Output

Return:
- PR
- UI impact
- targets validated
- tests executed
- CFT coverage
- failures
- evidence
- final status
- next action

Create `pr-validation-report.md` using the repository/project report template when available.
