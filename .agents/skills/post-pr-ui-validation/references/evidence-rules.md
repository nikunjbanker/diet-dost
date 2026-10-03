<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# Evidence Rules

## Valid evidence
- test-run output
- browser trace
- actual screenshot
- visual diff
- accessibility report
- CI/PR check result
- console/network evidence when relevant

## Invalid evidence
- agent assertion without execution
- generated mock screenshot
- assumed CI result
- copied result from an earlier commit
- desktop result presented as mobile/tablet proof

## Provenance
Record:
- commit/ref validated
- test command
- target/device
- timestamp when available
- result
- evidence path/reference

Unknown is a valid value.
