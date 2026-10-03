<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Antigravity Adapter

Use the canonical post-PR UI validation prompt.

Primary mechanism:
- workspace skill: `.agents/skills/post-pr-ui-validation/SKILL.md`
- invoke the skill after PR creation or PR update when UI-impacting changes exist

Before execution:
1. Inspect repository instructions/agent configuration.
2. Discover the actual test commands and browser/device profiles.
3. Use the skill's validation matrix and evidence rules.
4. Keep the PR validation report with the PR artifacts where project policy allows.

Do not hard-code Antigravity UI details into the skill. Verify current product
behavior before course recording.
