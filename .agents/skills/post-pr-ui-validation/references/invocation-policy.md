<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Post-PR Validation Invocation Policy

## Trigger

After a pull request is:
- created, or
- updated with new commits,

evaluate whether the change is UI-impacting.

## UI-impacting examples
- frontend source
- UI components
- pages/routes
- styles/CSS
- responsive logic
- frontend configuration
- UI tests
- accessibility configuration
- visual regression configuration

## Action

If UI-impacting:
1. Invoke `post-pr-ui-validation`.
2. Require final PASS, FAIL or BLOCKED.
3. Do not mark validation complete without a report.

If not UI-impacting:
return `UI_VALIDATION_NOT_APPLICABLE` with the reason.

## Revalidation

Any subsequent PR commit that changes validated UI or related tests requires
post-PR validation to be rerun.
