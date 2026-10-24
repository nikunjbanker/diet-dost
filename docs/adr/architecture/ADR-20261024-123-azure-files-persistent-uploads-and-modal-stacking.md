<!--
  Copyright (c) 2026 diet-dost and/or its contributors.
  Licensed under the "GNU Affero General Public License v3.0 only" and
  the "Server Side Public License, v 1"; you may not use this file except
  in compliance with, at your election, the "GNU Affero General Public
  License v3.0 only" or the "Server Side Public License, v 1".
-->

# ADR-20261024-123: Azure Files Persistent Static Uploads Serving, Seed Media Mirroring, and LIFO Modal Stacking Architecture

> **Date / Timestamp**: 2026-10-24T13:15:00+05:30  
> **Status**: ACCEPTED  
> **Driver / Agent / Deciders**: Solution Architect (Antigravity Agent) & Human Lead Architect (Nikunj Banker)  
> **Change Type**: `[ARCHITECTURE]`, `[PERSISTENCE]`, `[PRESENTATION]`, `[SECURITY]`  
> **Affected Subsystems**: WebGateway (`Extensions/WebApplicationExtensions.cs`, `Extensions/DatabaseInitializationExtensions.cs`, `wwwroot/js/ui/modal-stack-manager.js`, `wwwroot/js/ui/progress-modal.js`, `wwwroot/js/ui/review-modal.js`, `wwwroot/js/ui/admin-modal.js`, `wwwroot/js/ui/MealDiaryView.js`, `wwwroot/partials/*.html`, `wwwroot/styles.css`), Tests (`Nutrition.EvalHarness.Tests`)  
> **Associated PR & Branch**: `fix/mobile-modals-and-azure-files-photos` (Tracking Issue #87)  
> **Relevant SDDs & Rules**: [`docs/sdd/02_solution_architecture.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/02_solution_architecture.md), [`docs/sdd/03_presentation_layer.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/03_presentation_layer.md), [`docs/sdd/04_security_and_compliance.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/docs/sdd/04_security_and_compliance.md), [`DESIGN.md`](file:///c:/Users/nikunj.banker/source/repos/diet-dost/DESIGN.md)  

---

## 1. Executive Summary & Rationale

This architectural decision resolves critical mobile usability, modal lifecycle, and media asset delivery issues in the Diet-Dost solution:
1. **Zero-Blob Azure Files SMB Static Uploads Delivery**:
   - Strictly upholds the user requirement to utilize the existing mounted Azure File Share (`/app/data/wwwroot/uploads`) without introducing Azure Blob Storage SDKs, extra Azure resources, or storage connection strings.
   - Configures ASP.NET Core `PhysicalFileProvider` on the persistent `{StorageOptions.WebRootPath}/uploads` directory mapped to request path `/uploads`.
   - Corrected seed photo filename mismatch (`sample_thali.jpg` -> `sample_lunch_thali.jpg`) in `DatabaseInitializationExtensions.cs`.
   - Implemented automated startup seed media mirroring (`EnsureSeedMediaAssetsMirrored`) which synchronizes seed meal and progress photos from the container's `/app/wwwroot/uploads` to the persistent Azure File Share SMB mount upon first run, preventing 404 errors on deployed environments.
2. **Dynamic LIFO Modal Stack Manager (`modal-stack-manager.js`)**:
   - Replaced fragmented and conflicting `z-index` inline definitions (`z-index: 10005` on admin sub-modals, `z-index: 120` on delete confirmation, `z-index: 1200` on lightbox) with a centralized `ModalStackManager`.
   - Assigns dynamic z-index (`100010 + depth * 20`) ensuring that child dialogs (such as Create User, Edit User, Delete Confirmation, and Lightbox Photo Inspection) strictly render on top of their parent modals.
   - Manages body scroll-lock (`body.modal-open`): ensures scroll-lock is applied on the first opened modal and is ONLY revoked when the last remaining modal in the stack is closed, preventing scroll hijacking when dismissing sub-modals.
3. **Modal Save & Cancel Lifecycle Guarantee**:
   - Updated `progress-modal.js` to invoke `this.close()` on both Save and Cancel button clicks instead of switching tabs, returning the user cleanly to the dashboard.
   - Removed redundant inline `onclick="openProgressModal('...')"` from tab navigation buttons to avoid recursive modal re-opening.
   - Added an accessible header close button (`#btn-review-header-close`) in `review-modal.html` and wired it in `review-modal.js` for one-tap dismissal on mobile devices.
4. **Offline Local Clinical AI Engine Confidence Guarantee**:
   - Verified that the offline local clinical heuristics (`MicrosoftAgentFoodVisionService.cs`) return `OverallConfidenceScore = 0.90` (passing `IsConfidenceGatedPassed >= 0.70`), ensuring the review modal does not get stuck or trigger forced retake alerts on offline responses.

---

## 2. Context & Root Cause Analysis

1. **Azure File Share Photo Invisibility**:
   - In containerized deployments (`Containerfile` and Bicep), the persistent volume is mounted at `/app/data`, with `Storage__WebRootPath=/app/data/wwwroot`.
   - `LocalPhotoStorageService` stores meal photos at `/app/data/wwwroot/uploads/meals/...` and returns relative URLs `/uploads/meals/...`.
   - However, ASP.NET Core `app.UseStaticFiles()` only binds to `app.Environment.WebRootPath` (`/app/wwwroot`). Because `/app/data/wwwroot/uploads` was not registered in the static files middleware pipeline, all photo requests returned HTTP 404.
   - In addition, demo database seeding referenced `/uploads/meals/sample_thali.jpg`, whereas the actual committed seed file was named `sample_lunch_thali.jpg`.
2. **Modal Stacking & Occlusion**:
   - Desktop and mobile layouts had conflicting z-indices: `.review-modal-overlay` had `z-index: 100010`, but sub-modals `#admin-create-user-modal` and `#admin-edit-user-modal` had inline `z-index: 10005`, causing them to render underneath `#admin-modal`.
   - `.delete-modal-overlay` had `z-index: 120` and `.review-lightbox-overlay` had `z-index: 1200`, both rendering behind the main modal overlay.
   - Closing any child modal called `document.body.classList.remove('modal-open')`, removing background scroll lock while the parent modal was still open.
3. **Modal Dismissal Lifecycle Defects**:
   - `progress-modal.js` called `this.switchTab('pane-timeline-gallery')` in `handleSubmit` and `this.switchTab('pane-face-progress')` in `btnCancelUpload`, preventing the modal from closing.
   - `review-modal.html` lacked an explicit close button in the header, making mobile dismissal difficult when reviewing detected items.

---

## 3. Decision & Technical Architecture

### 3.1 Persistent Static File Serving (`WebApplicationExtensions.cs`)
```csharp
// Persistent Azure File Share SMB Mount Static Media Serving (/uploads)
var storageOptions = app.Services.GetService<IOptions<StorageOptions>>()?.Value;
var persistentUploadsDir = !string.IsNullOrWhiteSpace(storageOptions?.WebRootPath)
    ? Path.Combine(storageOptions.WebRootPath, "uploads")
    : Path.Combine(app.Environment.WebRootPath, "uploads");

if (!Directory.Exists(persistentUploadsDir))
{
    Directory.CreateDirectory(persistentUploadsDir);
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(persistentUploadsDir),
    RequestPath = "/uploads"
});
```

### 3.2 Dynamic Modal Stack Manager (`modal-stack-manager.js`)
```javascript
export class ModalStackManager {
  constructor(baseZIndex = 100010, step = 20) {
    this._stack = [];
    this._baseZIndex = baseZIndex;
    this._step = step;
  }

  push(modalEl) {
    if (!modalEl) return this._baseZIndex;
    const existingIndex = this._stack.indexOf(modalEl);
    if (existingIndex !== -1) this._stack.splice(existingIndex, 1);
    this._stack.push(modalEl);
    const assignedZ = this._baseZIndex + (this._stack.length - 1) * this._step;
    modalEl.style.zIndex = `${assignedZ}`;
    if (this._stack.length === 1) document.body.classList.add('modal-open');
    return assignedZ;
  }

  pop(modalEl) {
    if (!modalEl) return;
    const idx = this._stack.indexOf(modalEl);
    if (idx !== -1) this._stack.splice(idx, 1);
    if (this._stack.length === 0) document.body.classList.remove('modal-open');
  }
}
```

---

## 4. Verification & Evidence

1. **Automated Unit & Contract Tests**:
   - `PhotoStorageAndStaticFileServingTests.cs`: Verified external `WebRootPath` storage, `PhysicalFileProvider` photo location, middleware registration, and seed photo filename.
   - `ModalStackingAndLifecycleContractTests.cs`: Verified removal of low z-indexes (`120`, `1200`, `10005`), verification of `this.close()` on Save/Cancel, and header close button presence.
   - `LocalClinicalEngineFallbackTests.cs`: Verified offline clinical engine confidence score >= 0.70 and full macro calculations.
   - Entire solution test run: 275 tests passed, 0 failed, 0 warnings.
2. **Live Multi-Tier CFT Suite (`tests/validate_e2e_tiers.ps1`)**:
   - 100% pass across all 5 user tiers (`free`, `basic`, `premium`, `admin.demo`, `superadmin`).
   - Verified HTTP 200 on `/uploads/meals/sample_lunch_thali.jpg` and `/uploads/progress/face_baseline.svg`.
3. **Browser Subagent Interactive Verification**:
   - Tested on mobile viewport (390x844).
   - Confirmed sub-modal stacking, clean modal dismissal on Save and Cancel, and 0 console errors.

---

## 5. Adversarial Review

* **Path Traversal Security**: ASP.NET Core `PhysicalFileProvider` strictly normalizes paths and forbids directory traversal outside the root `uploads` volume. Any `../` attempts result in 404s.
* **Stack Race Conditions**: `ModalStackManager.push` deduplicates modal instances, ensuring idempotence. `pop` only removes `modal-open` when the stack count reaches 0.
* **Zero Blob Storage Footprint**: Verified no NuGet packages (`Azure.Storage.Blobs`) or storage account connection strings were introduced.
