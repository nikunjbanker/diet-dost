<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261031-144: Mobile Web Camera Viewfinder, Optical/Digital Zoom HUD, and Touch-Friendly Interactive Cropper

- **Status**: Accepted
- **Date**: 2026-10-31
- **Domain**: Presentation / Web / UX / Security
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: In-flight mobile web camera capability

---

## 1. Context & Problem Statement
When accessing Diet-Dost on mobile devices and modern desktop browsers, users could previously only select pre-existing photos from their device photo gallery. Users could not take live meal photos directly from their camera.
The core issues identified were:
1. **HTTP Permissions-Policy Block**: `SecurityHeadersMiddleware.cs` set `Permissions-Policy: camera=(), microphone=()`, which instructed the browser to block all `getUserMedia()` calls unconditionally at the HTTP level.
2. **Missing In-Browser Live Viewfinder**: There was no interactive camera viewfinder component to capture meals, switch between front/rear cameras, or toggle the flashlight/torch in low-light environments.
3. **No Live Zoom Capability**: When taking photos of dining tables, users could not zoom in or out to frame individual food items properly.
4. **No Post-Capture Region Selection (Cropping)**: Users capturing a wide table or multi-item Indian thali could not select a specific plate or bowl to send to the AI vision estimator, forcing full-frame submission that included background clutter.
5. **Biometric Check-In Needs**: The DEXA Body & Biometric Progress Check-In modal needed front-camera auto-selection for Face Telemetry and rear-camera auto-selection for Full Body check-ins.

---

## 2. Decision & Implementation

### 2.1 HTTP Permissions-Policy Update (`SecurityHeadersMiddleware.cs`)
Updated `SecurityHeadersMiddleware.cs` from `camera=()` to `camera=(self)` while preserving `geolocation=(self)` and disabling `microphone=()`:
```http
Permissions-Policy: geolocation=(self), camera=(self), microphone=()
```
This isolates hardware camera access exclusively to the first-party origin, preventing unauthorized iframe execution.

### 2.2 In-Browser Hardware Camera Abstraction (`CameraService`)
Created `src/Nutrition.WebGateway/wwwroot/js/services/camera-service.js`:
- Interrogates `navigator.mediaDevices.getUserMedia()` with fallback to legacy `navigator.getUserMedia`.
- Proactively enumerates available video input devices (`enumerateCameras()`) to identify front (`user`) and rear (`environment`) cameras.
- Hardware capability inspection via `track.getCapabilities()` to check for native optical zoom (`capabilities.zoom`) and torch (`capabilities.torch`).
- Implements hardware optical zoom constraint application (`track.applyConstraints({ advanced: [{ zoom }] })`) with smooth CSS/canvas digital zoom scaling fallback.
- **Zero-Leak Stream Lifecycle Guard**: Terminating all `MediaStreamTrack` instances immediately upon modal dismissal, cancellation, or page visibility change (`visibilitychange`), shutting down camera hardware and turning off privacy indicator LEDs.

### 2.3 Obsidian Viewfinder & Shutter Modal (`CameraModalController`, `camera-modal.html`, `styles.css`)
Created `src/Nutrition.WebGateway/wwwroot/js/ui/camera-modal.js` and `camera-modal.html`:
- Styled according to `DESIGN.md` Linear Obsidian design system tokens (`#010102`, `#0f1011`, `#23252a`, `#5e6ad2`).
- Integrated with `ModalStackManager` for ESC key handling and backdrop dismissal.
- Live reticle overlay with Indian meal plate guidance.
- Zoom HUD with `1x`, `2x`, and `3x` pill selectors alongside touch pinch-to-zoom gesture recognition.
- Tactile haptic feedback (`navigator.vibrate([40])`) and shutter flash animation on photo snap.
- High-fidelity still capture using `ImageCapture.takePhoto()` when supported, falling back to full-resolution offscreen HTML5 `<canvas>` snapshot extraction.

### 2.4 Interactive Region-of-Interest Selection (`ImageCropper`)
Created `src/Nutrition.WebGateway/wwwroot/js/ui/image-cropper.js`:
- Zero-dependency canvas region selector with 4 touch handles (minimum 44x44px target size) and dark scrim.
- 3x3 framing grid guide.
- Aspect ratio presets: `1:1 Plate` (centered square) and `Full Photo` (reset selection).
- Strict coordinate clamping preventing negative dimensions or out-of-bounds selection, extracting pristine sub-rectangle blobs matching source resolution.

### 2.5 Meal Logger & Progress Modal Wiring
- `meal-logger.js`: Wired `#btn-take-photo` to open `CameraModalController({ mode: 'meal' })`.
- `progress-modal.js`: Wired `#btn-progress-take-photo` with automatic camera selection (`face` mode chooses front camera `user`, `body` mode chooses rear camera `environment`).

---

## 3. Verification & Compliance Evidence
- **Automated C# Contract Tests**: `WebCameraContractAndSecurityTests.cs` and `SecurityEnvironmentAndHeaderTests.cs` assert `camera=(self)` and `geolocation=(self)` HTTP headers.
- **Node.js Unit Tests**:
  - `tests/js/camera-service.test.js`: Validates `isSupported`, `stopStream`, `setZoom`, and secure context checks (6/6 pass).
  - `tests/js/image-cropper.test.js`: Validates bounds clamping, 1:1 plate preset, and display-to-source coordinate mapping (4/4 pass).
- **Security & Adversarial Review**: Verified zero unstopped camera tracks, strict input coordinate clamping, and absence of device label leakage to unauthenticated APIs.
