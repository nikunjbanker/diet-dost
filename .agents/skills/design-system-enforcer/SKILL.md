---
name: design-system-enforcer
description: Enforces a repository's DESIGN.md as the authoritative UI/UX design specification for new development, ongoing development, UI changes, refactoring, responsive work, accessibility work, and visual verification. Use whenever a task creates, modifies, reviews, fixes, or refactors application UI/UX.
---

<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Design System Enforcer

## Mission

Implement UI/UX work against the repository's `DESIGN.md`.

`DESIGN.md` is the application's UI/UX source of truth. Treat it as an engineering constraint and persistent design context, not as optional inspiration.

This skill is IDE-, framework-, language-, cloud-, and vendor-independent.

It applies to:

- new UI/features
- ongoing feature development
- UI enhancements
- UI bug fixes
- refactoring
- responsive/mobile work
- accessibility work
- component creation or modification
- design-system changes
- UI audits
- visual verification

## Non-negotiable rule

Before touching UI code:

1. Locate `DESIGN.md`.
2. Read the applicable contents.
3. Inspect the existing design-system implementation.
4. Inspect relevant reusable components and pages.
5. Determine the applicable design tokens and interaction rules.
6. Plan the smallest coherent change.
7. Implement.
8. Verify.

Do not bypass this process because a UI change appears small.

## Authority order

Use this precedence:

1. `DESIGN.md`
2. Existing design-system/token implementation
3. Existing reusable components
4. Existing application UX conventions
5. Accessibility requirements
6. Explicit product requirements
7. Framework conventions
8. Agent judgment

If a request conflicts with `DESIGN.md`, identify the conflict. Do not silently bypass the design system.

## Discovery

Search for `DESIGN.md` before UI work.

Preferred locations:

- repository root
- application root
- documented design-system directory

If multiple `DESIGN.md` files exist:

- determine their scope;
- identify global vs local rules;
- do not arbitrarily choose one;
- ask for clarification if precedence is materially ambiguous.

Do not create a competing `DESIGN.md` without explicit instruction.

## Design extraction

Identify only the rules relevant to the task, including where applicable:

- colors and semantic colors
- typography
- spacing
- radii
- shadows/elevation
- borders
- layout/grid/container rules
- responsive behavior
- component variants
- component states
- interaction/motion
- accessibility guidance
- dark/light theme behavior
- imagery/iconography rules

Do not assume values from the original website that are not present in the project's local `DESIGN.md`.

## Token-first implementation

Prefer semantic design tokens and existing primitives over raw visual values.

If an equivalent token exists, do not introduce a hard-coded:

- color
- font family
- font size
- spacing value
- radius
- shadow
- breakpoint
- animation value

If the design system genuinely lacks a required value:

1. determine whether an existing token can be reused;
2. inspect project conventions;
3. if a new design decision is required, update/document `DESIGN.md` first when authorized;
4. synchronize the implementation token;
5. use the new token consistently.

Do not solve token gaps with one-off CSS overrides.

## Existing application first

Before creating a component:

1. Search for an equivalent component.
2. Inspect its variants and states.
3. Inspect similar pages.
4. Reuse or extend it where appropriate.

Avoid duplicate components that create design-system islands.

## Product behavior

UI work must not accidentally change:

- routes
- APIs
- business logic
- validation
- authentication
- authorization
- state management
- persistence
- analytics
- error handling
- existing feature behavior

Unless explicitly requested, keep UI changes behavior-preserving.

## Responsive design

Evaluate relevant viewport classes:

- mobile
- tablet
- desktop
- wide desktop

Check:

- no unintended horizontal scrolling
- usable navigation
- readable typography
- usable forms/buttons
- coherent grids/cards
- intentional table overflow
- usable dialogs
- touch targets
- documented breakpoints

Use `DESIGN.md` breakpoints when defined. Otherwise use established project conventions rather than inventing arbitrary breakpoints.

See `references/responsive-checklist.md` for the detailed checklist.

## Accessibility

Preserve or improve accessibility:

- semantic structure
- keyboard navigation
- visible focus
- sufficient contrast
- labels and accessible names
- appropriate ARIA
- screen-reader behavior
- loading/error/success feedback
- reduced-motion behavior where applicable
- touch usability

If a design rule conflicts with accessibility, preserve accessibility and document the conflict.

See `references/accessibility-checklist.md`.

## Component state completeness

For interactive components, consider applicable states:

- default
- hover
- focus
- active
- selected
- disabled
- loading
- success
- error
- empty
- expanded
- collapsed

Implement states required by the component's actual behavior and the design system. Do not add decorative states with no product meaning.

## Avoid generic AI UI drift

Do not introduce generic AI-generated visual patterns merely because they are common:

- arbitrary gradients
- excessive rounded cards
- excessive shadows
- glassmorphism
- decorative blobs
- oversized hero sections
- random accent colors
- unnecessary badges/icons
- arbitrary animation
- generic dashboard layouts
- one-off spacing systems

If `DESIGN.md` explicitly specifies such patterns, follow the specification.

## Design changes

When a request changes the visual system itself:

1. determine whether it is a design-system change;
2. update `DESIGN.md` first when appropriate and authorized;
3. synchronize design tokens;
4. update affected components;
5. verify representative pages.

Preferred flow:

`DESIGN.md → tokens → primitives → pages → visual verification`

Never update dozens of components individually when a token-level change is the correct solution.

## Verification

Before declaring the task complete:

- check changed UI against `DESIGN.md`;
- inspect the diff for hard-coded visual values;
- confirm component reuse;
- check responsive behavior;
- check accessibility;
- run relevant tests/build/lint;
- use browser/visual verification when available;
- report anything that could not be verified.

Do not claim visual verification unless the rendered UI was actually inspected.

See `references/visual-verification.md`.

## Failure classification

When UI does not match the intended design, classify before patching:

1. wrong token
2. wrong component
3. wrong composition
4. wrong responsive behavior
5. wrong interaction state
6. missing/ambiguous `DESIGN.md` rule
7. implementation defect

Do not accumulate CSS overrides without identifying the root cause.

## Agent safety

Never:

- fabricate design requirements;
- invent missing product behavior;
- silently replace or delete `DESIGN.md`;
- claim verification that did not occur;
- change unrelated files unnecessarily;
- expose secrets;
- overwrite important design-system files without appropriate review;
- use generated screenshots or conceptual images as evidence of real UI behavior.

## getdesign.md / Linear-derived DESIGN.md

If the repository uses the Linear-derived `DESIGN.md` from getdesign.md, follow the repository's actual local file.

Do not hard-code assumptions from the public reference into this skill.

The getdesign.md Linear entry describes itself as an independent analysis of publicly observable patterns and states that it is not affiliated with or endorsed by Linear. Treat it as a design reference, not official Linear design-system documentation.

## IDE portability

The canonical engineering intent is this skill.

Antigravity, Claude Code, GitHub Copilot, or another coding agent may use different invocation/configuration mechanisms. Do not duplicate the full skill into every IDE configuration.

Use lightweight IDE adapters that point the agent to this skill.

## Output contract

When the task completes, report:

### Design System Status
- `DESIGN.md`: found / not found
- applicable rules/tokens
- components reused
- new components, if any
- responsive verification
- accessibility verification
- tests/build/lint
- visual verification
- deviations
- unknowns

Use one of:

- `COMPLIANT`
- `PARTIALLY COMPLIANT`
- `BLOCKED`

Do not report `COMPLIANT` if important applicable rules could not be verified.
