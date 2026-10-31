<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261031-146: Gallery Photo Area Selection, Smooth Pinch/Wheel Crop Zoom, and Multi-Preset Storage

- **Status**: Accepted
- **Date**: 2026-10-31
- **Domain**: Presentation / Web / UX / Security
- **Deciders**: Diet-Dost Engineering & Architecture Team
- **Tracking Issue**: In-flight gallery photo crop selection feature

---

## 1. Context & Problem Statement

Prior to this enhancement, selecting photos from the device gallery (via `<input type="file">`, mobile photo library, or dropzone drag-and-drop) immediately initiated upload and processing of the entire uncropped image:
1. **Background Clutter & AI Vision Inaccuracies**: In complex dining scenarios (e.g., an Indian thali with multiple bowls, or dining tables with non-food items), users could not isolate the specific dish to log. Submitting uncropped wide-angle photos forced AI vision models to evaluate irrelevant background elements.
2. **Asymmetric UX**: Live camera captures featured an interactive cropping screen (`ImageCropper`), but gallery selections bypassed the cropper entirely.
3. **Missing Zoom & Aspect Presets in Cropper**: The cropper supported only `1:1 Plate` and `Full Photo`, lacking standard `4:3 Photo` dimensions and intuitive touch pinch-to-zoom or mouse wheel zoom controls to adjust the cropping box.
4. **Ergonomic Reselect Friction**: If a user selected the wrong photo from their gallery, there was no direct way from the crop screen to pick a different image without closing the modal and re-initiating the file picker.

---

## 2. Decision & Implementation

### 2.1 Zero-Hardware Gallery Cropper Interception (`camera-modal.js`)
Implemented `cameraModal.openCrop({ file, mode, onConfirm, onCancel, onReselect })`:
- Operates purely on the client-side `File` or `Blob` object without invoking `getUserMedia()`.
- Guarantees zero hardware camera access, zero camera permission prompts, zero battery drain, and zero green camera privacy indicator activation during gallery photo processing.
- Dynamically customizes modal headers (`✂️ Select Food Area` for meals, `✂️ Select Progress Area` for DEXA body check-ins) and hides hardware-specific controls (torch, camera flip).
- Injects a context-aware reselect action: changes `#btn-retake-photo` label to `🔄 Choose Another`, calling `onReselect` to reopen the system file picker directly.

### 2.2 Smooth Pinch-to-Zoom & Mouse Wheel Zoom (`image-cropper.js`)
Enhanced `ImageCropper` with smooth continuous scaling around the crop box center:
- **Mathematical Center Scaling (`scaleBoxAroundCenter`)**: Scales bounding box dimensions while clamping strictly within canvas display bounds and enforcing a minimum size (`minSize = 40px`).
- **Desktop Mouse Wheel (`_onWheel`)**: Intercepts `wheel` events over the crop box and scales smoothly by a 1.05 / 0.95 factor per tick with `e.preventDefault()`.
- **Mobile Touch Pinch Gesture (`_onTouchStart`, `_onTouchMove`, `_onTouchEnd`)**: Tracks two-finger touch distances, computing proportional scale factors for fluid pinch-to-zoom on iOS and Android viewports.
- Event listeners are fully unhooked in `destroy()` to prevent memory leaks.

### 2.3 Standard 4:3 Aspect Ratio Preset (`image-cropper.js`, `camera-modal.html`)
- Added `calculateStandardPreset(boundW, boundH)` computing a centered 4:3 (or 3:4 portrait) bounding box.
- Added `#btn-crop-preset-standard` (`📐 4:3 Photo`) to the controls toolbar in `camera-modal.html`.
- Updated HUD guidance pill to instruct users on drag handles, mouse scroll, and touch pinch gestures.

### 2.4 End-to-End Meal Logger & DEXA Progress Integration
- **`meal-logger.js`**: `fileInput`, `cameraFallbackInput`, and dropzone drop events are routed through `selectPhotoAreaAndUpload(file)`. The resulting cropped `File` is passed to `handleImageUpload(croppedFile)` for image compression and AI analysis.
- **`progress-modal.js`**: File selections and drop events route through `selectPhotoAreaAndPreview(file)`. The cropped `File` is stored in `this._capturedFile` and previewed for DEXA biometric submission.
- **`main.js`**: Injected `cameraModal` dependency into both `mealLogger` and `progressModal` registrations in the DI container, and exposed `window.openCropModal`.

---

## 3. Verification & Compliance Evidence

1. **JavaScript Unit Tests (`tests/js/image-cropper.test.js`)**:
   - `calculateStandardPreset produces a 4:3 centered bounding box`: Passed.
   - `scaleBoxAroundCenter scales box smoothly and clamps strictly within bounds`: Passed.
   - All 12 unit tests pass in 330ms (`node --test tests/js/*.test.js`).

2. **C# Contract Tests (`tests/Nutrition.EvalHarness.Tests/WebCameraContractAndSecurityTests.cs`)**:
   - `CameraModalHtml_ContainsStandardCropPresetAndGuidance`: Verified presence of `btn-crop-preset-standard` and scroll/pinch guidance in `camera-modal.html`.
   - `CameraModalJs_ImplementsOpenCropAndPresetContracts`: Verified `openCrop` method, standard preset wiring, and reselect contracts.
   - All 6 tests pass in 420ms (`dotnet test`).

3. **DevSecOps & Living Documentation Gates**:
   - SDD 03 (`docs/sdd/03_presentation_layer.md`) updated with section 22.
   - Adversarial Review in `docs/ADVERSARIAL_AND_SECURITY_REMEDIATION_PLAN.md` updated with GAP-43.
