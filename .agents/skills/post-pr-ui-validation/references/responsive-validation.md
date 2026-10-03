<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Responsive Validation Guidance

The repository is authoritative for supported viewport/device profiles.

Discover before execution:
- browser/device projects
- viewport dimensions
- breakpoints
- mobile/tablet profiles
- screenshot baselines
- responsive test tags

If the repository does not define them, record the gap before using a reasonable
fallback configuration.

## Web/Desktop
Validate layout, navigation, primary flows, overflow, clipping and interaction.

## Tablet
Validate responsive reflow, navigation, spacing, touch interaction, overflow,
modals/dialogs and data-heavy components.

## Mobile
Validate navigation/menu, visibility, wrapping, overflow, touch targets, forms,
modals/dialogs and sticky/fixed elements.

A responsive validation result requires actual execution or documented direct
inspection. Passing a desktop test does not imply tablet/mobile success.
