<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Accessibility Checklist

Use for new or modified UI.

## Structure

- [ ] Semantic elements used where appropriate
- [ ] Heading hierarchy is meaningful
- [ ] Landmark structure is appropriate

## Interaction

- [ ] Keyboard navigation works
- [ ] Focus is visible
- [ ] Focus order is logical
- [ ] Interactive controls have accessible names
- [ ] Form controls have labels
- [ ] Error states are communicated
- [ ] Loading states are communicated where needed
- [ ] Disabled states are distinguishable

## Visual

- [ ] Contrast is adequate
- [ ] Information is not conveyed by color alone
- [ ] Text remains readable at relevant sizes
- [ ] Motion does not create avoidable accessibility problems

## Assistive technology

- [ ] ARIA is used only where appropriate
- [ ] Dynamic updates are announced when required
- [ ] Decorative content is not unnecessarily exposed

If a DESIGN.md rule conflicts with an accessibility requirement, preserve accessibility and document the conflict.
