<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# GitHub Copilot Adapter

Use the canonical prompt and preserve its acceptance criteria.

Recommended repository integration:
- project instructions
- `.github/copilot-instructions.md`
- `.github/instructions/`
- supported prompt/custom-agent mechanisms

Adapter instruction:

1. Read applicable repository instructions.
2. Inspect the PR diff.
3. Discover CFTs, UI tests, responsive profiles and visual regression setup.
4. Validate Web/Desktop, Tablet and Mobile where applicable.
5. Capture real evidence.
6. Generate/update `pr-validation-report.md`.
7. On failure, diagnose and revalidate after authorized correction.
8. Never fabricate results or convert BLOCKED/UNKNOWN into PASS.

Verify current GitHub Copilot capabilities before course recording.
