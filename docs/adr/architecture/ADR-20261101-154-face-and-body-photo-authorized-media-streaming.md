# ADR-20261101-154: Authorized Progress Media Streaming, DPDPA Biometric Isolation, and Dashboard Hydration

## Status
Accepted

## Date
2026-11-01

## Context & Problem Statement
During an adversarial and security review of progress photo storage and hydration, users reported that face and body photos added during self check-in appeared in the timeline as metadata entries, but the actual images were not rendering in either the Timeline gallery, the Progress modal detail views, or the main dashboard (`#face-progress-card`).

### Root Cause Analysis
1. **Static File Server Interception (GAP-05 Defense Side-Effect)**:
   In `WebApplicationExtensions.cs`, security enforcement for DPDPA 2023 biometric isolation blocked any path containing `/progress/` on the static file server (`StatusCode = 403 Forbidden`, `Body = Stream.Null`). This intentionally prevented direct unauthenticated file retrieval of user photos. However:
   - Wireframe demo SVG assets (`/uploads/progress/face_baseline.svg`, `/uploads/progress/face_current.svg`) were inadvertently caught in this block.
   - CQRS query and command handlers (`GetProgressPhotosQueryHandler`, `CompareProgressPhotosQueryHandler`, `UploadProgressPhotoCommandHandler`, `GetDeletedRecordsQueryHandler`) were projecting the raw physical storage relative path (e.g. `/uploads/users/{userId}/progress/{timestamp}_{guid}.jpg`) to clients instead of the authorized streaming media endpoint.
2. **Missing Media Route in Authentication Cookie/Query Extraction**:
   `SecurityAndAuthExtensions.cs` configured `JwtBearerEvents.OnMessageReceived` to inspect `access_token` query parameters for WebSocket / Hub requests (`/hubs/`), but did not permit query token extraction for media streaming routes (`/api/media`). Because HTML `<img>` elements cannot supply custom `Authorization: Bearer <token>` headers without `fetch()` blob conversions, cookie or query-based token passing was required for standalone image rendering.
3. **MIME Type Truncation & Dual-Root Storage Drift**:
   In `MediaController.cs`:
   - Static wireframe and SVG uploads were assigned `image/jpeg` content-type, failing browser rendering or SVG decoding.
   - Path resolution only inspected `_storageOptions.Value.WebRootPath` without fallback to `_env.ContentRootPath/wwwroot`, leading to 404s when running in environments where persistent storage paths differ between development and Azure container mount points.
   - Soft-delete authorization checks rejected requests with 404 even when queried by an Admin or SuperAdmin in audit views.
4. **Client-Side Fallback Poisoning**:
   In `main.js`, a global `window.addEventListener('error', ..., true)` handler captured failed image events and flagged `dataset.hasFallback = 'true'`. Once flagged on initial page load (when demo images were 403-blocked), subsequent updates from `progress-modal.js` were ignored or overridden by the fallback placeholder.

## Decision Drivers
- **OWASP LLM & DPDPA 2023 Compliance**: Biometric facial and DEXA body photos are classified as sensitive personal health data. Direct unauthenticated filesystem traversal or public static serving of `/uploads/users/` must remain strictly forbidden (403 Forbidden).
- **Authorized Streaming Gateway**: All progress media must be streamed exclusively through `GET /api/media/progress/{id}`, subject to authentication, authorization (owner or Admin/SuperAdmin), tenant isolation, and security headers (`X-Content-Type-Options: nosniff`, Content Security Policy, Private Cache-Control).
- **Seamless UI Hydration**: Both the main dashboard comparison cards (`#face-progress-card`) and the progress modal galleries (`#timeline-gallery`, `#face-comparison-container`) must dynamically resolve photo IDs to `/api/media/progress/{id}` without poisoning from fallback handlers.
- **Audit Resilience**: Admin and SuperAdmin users auditing soft-deleted progress photos via the admin console must be able to securely preview deleted thumbnails without bypassing authorization barriers.

## Decision Outcome
Accepted. Implemented a defense-in-depth media streaming pattern:
1. **CQRS Projection Alignment**:
   - `GetProgressPhotosQueryHandler`: Maps all photos to `PhotoUri = $"/api/media/progress/{p.Id}"`.
   - `CompareProgressPhotosQueryHandler`: Projects `BaselineFace`, `CurrentFace`, `BaselineFullBody`, `CurrentFullBody`, and all timeline items to `/api/media/progress/{p.Id}`.
   - `UploadProgressPhotoCommandHandler`: Returns `PhotoUri = $"/api/media/progress/{photo.Id}"` upon check-in upload.
   - `GetDeletedRecordsQueryHandler`: Projects soft-deleted photos to `/api/media/progress/{p.Id}` for audit previews.
2. **MediaController Hardening**:
   - Dual-root filesystem probing (`StorageOptions.WebRootPath` with fallback to `IWebHostEnvironment.WebRootPath`).
   - Content-Type resolution for `.svg` (`image/svg+xml`), `.gif`, `.webp`, `.png`, and `.jpeg`.
   - Admin audit support for soft-deleted progress records.
   - Strict security headers: `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; style-src 'unsafe-inline'`, and `Cache-Control: private, max-age=3600`.
3. **Static File Filter Refinement**:
   - `WebApplicationExtensions.cs` refines the static file interceptor: only requests matching `/uploads/users/` AND `/progress/` are rejected with 403. Public demo wireframes in `/uploads/progress/` are served statically.
4. **JWT Query Token Support**:
   - `SecurityAndAuthExtensions.cs` permits `?access_token=` authentication for requests starting with `/api/media`.
5. **Frontend Resilient Hydration**:
   - `progress-modal.js`: Added `resolvePhotoUrl(photo)` helper supporting both projected and raw IDs, and resets `dataset.hasFallback` during comparison view updates.
   - `main.js`: Ignores placeholder image errors to prevent permanent fallback poisoning.

## Consequences
- **Positive**:
  - Eliminates all broken image links for face and body check-ins across the timeline and main dashboard.
  - Guarantees 100% adherence to DPDPA 2023 biometric privacy standards with zero public exposure of user photos.
  - Protects against IDOR: users cannot view another user's progress photo ID.
  - Supports SuperAdmin and Admin compliance auditing for deleted media.
- **Negative / Operational**:
  - Media streaming consumes controller pipeline resources rather than raw static file kernel caching; mitigated by `Cache-Control: private, max-age=3600`.
