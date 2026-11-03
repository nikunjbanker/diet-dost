<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261103-158: Authenticated Progress Photo Rendering Without URL Tokens

- **Status**: Accepted
- **Date**: 2026-11-03
- **Domain**: Presentation / Web / Security
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: Mobile progress photo visibility

## Context & Problem Statement

Progress-photo metadata and comparison requests already use the authenticated API client, but browser `<img>` requests to the protected media endpoint cannot attach the stored bearer token. Desktop browsers may appear to work when their HttpOnly session cookie is present, masking the failure in JWT-only mobile or private-browser sessions. Cacheable media responses can also make an unauthenticated request appear successful when bytes were previously cached.

## Decision

- Retrieve progress photos through `ApiClient.getBlob()` so requests include the bearer header and same-origin session cookies, and bypass the HTTP cache.
- Render the response bytes with short-lived `blob:` object URLs. Revoke them when a photo is removed, the account changes, the user signs out, or authorization expires; do not persist image bytes or put credentials in the URL.
- Keep the authenticated, owner-scoped `/api/media/progress/{id}` endpoint and its Admin/SuperAdmin policies unchanged.
- Return `Cache-Control: private, no-store` for protected progress media.
- On media errors, log the failure and show only the neutral existing placeholder.

## Consequences

- Progress photos render consistently when a mobile browser has a bearer token but no session cookie.
- Browser HTTP caches cannot reuse protected progress-photo response bytes across sessions.
- The web client holds photo bytes only in memory for as long as the active comparison/gallery needs them.
- Native mobile clients and the progress-photo API contract remain unchanged.

## Verification

- `node --experimental-default-type=module --test tests/progress_photo_client.test.mjs` — passed.
- `dotnet test tests/Nutrition.EvalHarness.Tests/Nutrition.EvalHarness.Tests.csproj --filter FullyQualifiedName~MediaStreamingSecurityTests --nologo` — 7 passed.
- Browser validation at desktop (`1440x900`) and mobile (`390x844`) confirmed loaded dashboard, detail, and timeline photos with no direct protected media URLs in image elements.

## Forward Roadmap Impact & Future Phase Compatibility

The change is confined to the web presentation adapter and media response headers. It preserves the shared authenticated media endpoint and owner policy for future web and mobile clients, introduces no duplicate clinical/domain logic, and does not change persistence or the API contract.
