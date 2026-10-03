<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Claude Code Adapter

Use the canonical prompt and preserve its acceptance criteria.

Adapter instruction:

1. Read the repository's agent/instruction files first.
2. Inspect the PR diff and discover CFTs, UI tests, responsive configuration,
   browser/device profiles and visual regression setup.
3. Execute the repository-defined validation for Web/Desktop, Tablet and Mobile.
4. Collect real evidence.
5. Generate/update `pr-validation-report.md`.
6. If failures occur and code modification is authorized, fix, rerun and
   revalidate.
7. Never infer PASS from build success or missing evidence.

Verify current Claude Code invocation/syntax at recording time rather than
hard-coding potentially stale CLI syntax into permanent course material.
