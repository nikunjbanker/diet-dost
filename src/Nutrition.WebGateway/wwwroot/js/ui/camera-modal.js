/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

import { ImageCropper } from './image-cropper.js';

/**
 * CameraModalController
 * Manages the live WebRTC camera viewfinder, hardware optical zoom / digital zoom,
 * torch/flashlight, camera switching, shutter snap, and interactive region cropping
 * for both camera frames and gallery image selection.
 */
export class CameraModalController {
  /**
   * @param {Object} options
   * @param {import('../services/camera-service.js').CameraService} options.cameraService
   * @param {import('./modal-stack-manager.js').ModalStackManager} [options.modalStackManager]
   * @param {import('./toast.js').ToastNotificationService} [options.toastService]
   */
  constructor({ cameraService, modalStackManager = null, toastService = null }) {
    this._camera = cameraService;
    this._stack = modalStackManager;
    this._toast = toastService;

    this._mode = 'meal'; // 'meal' | 'face' | 'body'
    this._cropSource = 'camera'; // 'camera' | 'gallery'
    this._onCaptureCallback = null;
    this._onCancelCallback = null;
    this._onReselectCallback = null;

    this._capturedRawFile = null;
    this._cropper = null;
    this._isTakingPhoto = false;

    this._bindEvents();
    this._bindPrivacyLifecycle();
  }

  get elements() {
    return {
      modal: document.getElementById('camera-modal'),
      title: document.getElementById('camera-modal-title'),
      statusDot: document.getElementById('camera-status-dot'),
      btnClose: document.getElementById('btn-close-camera'),
      btnTorch: document.getElementById('btn-camera-torch'),
      btnFlip: document.getElementById('btn-camera-flip'),
      videoStream: document.getElementById('camera-video-stream'),
      streamWrapper: document.getElementById('camera-stream-wrapper'),
      reticle: document.getElementById('camera-reticle'),
      reticleHint: document.getElementById('camera-reticle-hint'),
      zoomHud: document.getElementById('camera-zoom-hud'),
      zoomPills: document.querySelectorAll('.zoom-pill'),
      cropContainer: document.getElementById('camera-crop-container'),
      cropCanvas: document.getElementById('camera-crop-canvas'),
      errorScreen: document.getElementById('camera-error-screen'),
      errorTitle: document.getElementById('camera-error-title'),
      errorDesc: document.getElementById('camera-error-desc'),
      btnErrorGallery: document.getElementById('btn-camera-error-gallery'),
      shutterFlash: document.getElementById('camera-shutter-flash'),
      viewfinderControls: document.getElementById('viewfinder-controls-row'),
      cropControls: document.getElementById('crop-controls-row'),
      btnGalleryFallback: document.getElementById('btn-camera-gallery-fallback'),
      btnShutterSnap: document.getElementById('btn-shutter-snap'),
      btnCancel: document.getElementById('btn-cancel-camera'),
      btnRetake: document.getElementById('btn-retake-photo'),
      btnPresetSquare: document.getElementById('btn-crop-preset-square'),
      btnPresetStandard: document.getElementById('btn-crop-preset-standard'),
      btnPresetFull: document.getElementById('btn-crop-preset-full'),
      btnConfirmPhoto: document.getElementById('btn-confirm-use-photo')
    };
  }

  _bindEvents() {
    const el = this.elements;

    // Close & Cancel
    el.btnClose?.addEventListener('click', () => this.close());
    el.btnCancel?.addEventListener('click', () => this.close());

    // Torch
    el.btnTorch?.addEventListener('click', async () => {
      const nextTorch = !this._camera.isTorchOn;
      const success = await this._camera.setTorch(nextTorch);
      if (success) {
        el.btnTorch.classList.toggle('active', nextTorch);
      }
    });

    // Flip Camera
    el.btnFlip?.addEventListener('click', async () => {
      try {
        const stream = await this._camera.flipCamera();
        if (el.videoStream) {
          el.videoStream.srcObject = stream;
        }
        this._updateControlsState();
      } catch (err) {
        this._toast?.warning('Could not switch camera.');
      }
    });

    // Zoom Pills
    el.zoomPills.forEach(pill => {
      pill.addEventListener('click', async () => {
        const zoomVal = parseFloat(pill.dataset.zoom || '1.0');
        await this._applyZoom(zoomVal);
      });
    });

    // Pinch-to-zoom on touch screens
    this._initPinchToZoom();

    // Shutter Snap
    el.btnShutterSnap?.addEventListener('click', () => this.takePhoto());

    // Gallery Fallback buttons
    el.btnGalleryFallback?.addEventListener('click', () => {
      this.close();
      const fileInput = document.getElementById('file-input');
      fileInput?.click();
    });

    el.btnErrorGallery?.addEventListener('click', () => {
      this.close();
      const fileInput = document.getElementById('file-input');
      fileInput?.click();
    });

    // Crop Controls
    el.btnRetake?.addEventListener('click', () => {
      if (this._cropSource === 'gallery') {
        const reselect = this._onReselectCallback;
        this.close();
        if (typeof reselect === 'function') {
          reselect();
        }
      } else {
        this._resumeLiveStream();
      }
    });

    el.btnPresetSquare?.addEventListener('click', () => {
      el.btnPresetSquare?.classList.add('active');
      el.btnPresetStandard?.classList.remove('active');
      el.btnPresetFull?.classList.remove('active');
      this._cropper?.applySquarePreset();
    });

    el.btnPresetStandard?.addEventListener('click', () => {
      el.btnPresetStandard?.classList.add('active');
      el.btnPresetSquare?.classList.remove('active');
      el.btnPresetFull?.classList.remove('active');
      this._cropper?.applyStandardPreset();
    });

    el.btnPresetFull?.addEventListener('click', () => {
      el.btnPresetFull?.classList.add('active');
      el.btnPresetSquare?.classList.remove('active');
      el.btnPresetStandard?.classList.remove('active');
      this._cropper?.applyFullPreset();
    });

    el.btnConfirmPhoto?.addEventListener('click', () => this._confirmSelection());
  }

  _bindPrivacyLifecycle() {
    // Auto-stop video stream when user switches away from browser tab or window
    document.addEventListener('visibilitychange', () => {
      if (document.hidden && this._camera.isActive) {
        console.log('[CameraModal] Tab hidden: stopping camera stream for privacy and battery conservation.');
        this._camera.stopStream();
      } else if (!document.hidden && this.isOpen && !this._cropper && this._cropSource === 'camera') {
        // Resume stream when returning to tab
        this._startStreamForCurrentMode().catch(() => {});
      }
    });

    window.addEventListener('beforeunload', () => {
      this._camera.stopStream();
    });

    window.addEventListener('pagehide', () => {
      this._camera.stopStream();
    });

    window.addEventListener('popstate', () => {
      if (this.isOpen) {
        this.close();
      }
    });
  }

  _initPinchToZoom() {
    const el = this.elements;
    if (!el.streamWrapper) return;

    let initialDistance = 0;
    let initialZoom = 1.0;

    el.streamWrapper.addEventListener('touchstart', (e) => {
      if (e.touches.length === 2) {
        initialDistance = Math.hypot(
          e.touches[0].clientX - e.touches[1].clientX,
          e.touches[0].clientY - e.touches[1].clientY
        );
        initialZoom = this._camera.currentZoom;
      }
    }, { passive: true });

    el.streamWrapper.addEventListener('touchmove', async (e) => {
      if (e.touches.length === 2 && initialDistance > 0) {
        const currentDistance = Math.hypot(
          e.touches[0].clientX - e.touches[1].clientX,
          e.touches[0].clientY - e.touches[1].clientY
        );
        const scale = currentDistance / initialDistance;
        const targetZoom = Math.max(1.0, Math.min(3.5, initialZoom * scale));
        await this._applyZoom(targetZoom);
      }
    }, { passive: true });
  }

  async _applyZoom(zoomVal) {
    const el = this.elements;
    await this._camera.setZoom(zoomVal);

    // Apply digital video scale as enhancement / fallback
    if (el.videoStream) {
      el.videoStream.style.transform = `scale(${zoomVal})`;
      el.videoStream.style.transformOrigin = 'center center';
    }

    // Update active pill
    el.zoomPills.forEach(p => {
      const pZoom = parseFloat(p.dataset.zoom || '1.0');
      p.classList.toggle('active', Math.abs(pZoom - zoomVal) < 0.35);
    });
  }

  get isOpen() {
    const el = this.elements;
    return !!(el.modal && el.modal.style.display !== 'none');
  }

  /**
   * Opens the camera viewfinder modal.
   * @param {Object} [options]
   * @param {'meal'|'face'|'body'} [options.mode='meal']
   * @param {(file: File) => void} [options.onCapture]
   * @param {() => void} [options.onCancel]
   */
  async open({ mode = 'meal', onCapture = null, onCancel = null } = {}) {
    this._mode = mode;
    this._cropSource = 'camera';
    this._onCaptureCallback = onCapture;
    this._onCancelCallback = onCancel;
    this._onReselectCallback = null;
    this._capturedRawFile = null;
    this._destroyCropper();

    const el = this.elements;
    if (!el.modal) return;

    // Reset layout
    this._showLiveViewfinderUI();
    this._updateModeLabels(mode);

    if (el.statusDot) el.statusDot.style.display = 'inline-block';
    if (el.btnRetake) {
      el.btnRetake.textContent = '🔄 Retake';
      el.btnRetake.title = 'Retake Photo';
    }

    // Modal Stack Manager integration
    if (this._stack) {
      this._stack.push(el.modal);
    } else {
      document.body.classList.add('modal-open');
    }
    el.modal.style.display = 'flex';

    // Verify browser support
    if (!this._camera.isSupported()) {
      this._showError('Camera Not Supported', 'Your browser does not support in-app camera streaming or requires a secure HTTPS connection. Please select an existing photo from your gallery.');
      return;
    }

    try {
      await this._startStreamForCurrentMode();
      await this._updateControlsState();
    } catch (err) {
      console.warn('[CameraModal] Camera start error:', err);
      if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
        this._showError('Camera Access Denied', 'Camera permission was denied. Please allow camera access in your browser site permissions to take photos, or use your photo library.');
      } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
        this._showError('No Camera Found', 'No camera hardware was detected on this device. You can choose a photo from your gallery instead.');
      } else {
        this._showError('Camera Unavailable', err.message || 'Unable to access the camera hardware. Please check your camera permissions.');
      }
    }
  }

  /**
   * Opens the modal directly in crop/region selection mode for an existing image file (e.g. from gallery).
   * Camera hardware is NOT accessed or activated.
   * @param {Object} options
   * @param {File} options.file
   * @param {'meal'|'face'|'body'} [options.mode='meal']
   * @param {(file: File) => void} [options.onConfirm]
   * @param {() => void} [options.onCancel]
   * @param {() => void} [options.onReselect]
   */
  async openCrop({ file, mode = 'meal', onConfirm = null, onCancel = null, onReselect = null } = {}) {
    if (!file) return;
    this._mode = mode;
    this._cropSource = 'gallery';
    this._onCaptureCallback = onConfirm;
    this._onCancelCallback = onCancel;
    this._onReselectCallback = onReselect;
    this._capturedRawFile = file;

    const el = this.elements;
    if (!el.modal) return;

    // Ensure any active camera stream is stopped
    this._camera.stopStream();

    // Modal stack integration
    if (this._stack) {
      this._stack.push(el.modal);
    } else {
      document.body.classList.add('modal-open');
    }
    el.modal.style.display = 'flex';

    // Hide camera-specific UI
    if (el.statusDot) el.statusDot.style.display = 'none';
    if (el.btnTorch) el.btnTorch.style.display = 'none';
    if (el.btnFlip) el.btnFlip.style.display = 'none';

    if (mode === 'face') {
      if (el.title) el.title.textContent = '✂️ Select Face Telemetry Area';
    } else if (mode === 'body') {
      if (el.title) el.title.textContent = '✂️ Select Body Composition Area';
    } else {
      if (el.title) el.title.textContent = '✂️ Select Food Area';
    }

    if (el.btnRetake) {
      el.btnRetake.textContent = '🔄 Choose Another';
      el.btnRetake.title = 'Choose a different photo from gallery';
    }

    const defaultPreset = (mode === 'body') ? 'standard' : 'square';
    await this._initCropSelection(file, defaultPreset);
  }

  async _startStreamForCurrentMode() {
    const el = this.elements;
    const facingMode = this._mode === 'face' ? 'user' : 'environment';
    const stream = await this._camera.startStream({ facingMode });

    if (el.videoStream) {
      el.videoStream.srcObject = stream;
      el.videoStream.style.transform = 'scale(1.0)';
      await el.videoStream.play().catch(() => {});
    }
    el.errorScreen && (el.errorScreen.style.display = 'none');
    el.streamWrapper && (el.streamWrapper.style.display = 'flex');
  }

  async _updateControlsState() {
    const el = this.elements;
    // Check multiple cameras for flip
    const cameras = await this._camera.getAvailableCameras();
    if (el.btnFlip) {
      el.btnFlip.style.display = cameras.length > 1 ? 'inline-flex' : 'none';
    }

    // Check torch capability
    const hasTorch = this._camera.hasTorch();
    if (el.btnTorch) {
      el.btnTorch.style.display = hasTorch ? 'inline-flex' : 'none';
      el.btnTorch.classList.toggle('active', this._camera.isTorchOn);
    }
  }

  _updateModeLabels(mode) {
    const el = this.elements;
    if (mode === 'face') {
      if (el.title) el.title.textContent = '👤 Face Telemetry Camera';
      if (el.reticleHint) el.reticleHint.textContent = 'Center your face inside the framing reticle';
      if (el.reticle) el.reticle.className = 'camera-reticle reticle-face';
    } else if (mode === 'body') {
      if (el.title) el.title.textContent = '🧍 Body Composition Camera';
      if (el.reticleHint) el.reticleHint.textContent = 'Stand back to frame full body inside guide';
      if (el.reticle) el.reticle.className = 'camera-reticle reticle-body';
    } else {
      if (el.title) el.title.textContent = '📸 AI Food Camera';
      if (el.reticleHint) el.reticleHint.textContent = 'Center Indian meal plate inside guide';
      if (el.reticle) el.reticle.className = 'camera-reticle reticle-plate';
    }
  }

  _showLiveViewfinderUI() {
    const el = this.elements;
    if (el.streamWrapper) el.streamWrapper.style.display = 'flex';
    if (el.cropContainer) el.cropContainer.style.display = 'none';
    if (el.errorScreen) el.errorScreen.style.display = 'none';
    if (el.viewfinderControls) el.viewfinderControls.style.display = 'flex';
    if (el.cropControls) el.cropControls.style.display = 'none';
  }

  _showError(title, desc) {
    const el = this.elements;
    if (el.streamWrapper) el.streamWrapper.style.display = 'none';
    if (el.cropContainer) el.cropContainer.style.display = 'none';
    if (el.errorScreen) el.errorScreen.style.display = 'flex';
    if (el.errorTitle) el.errorTitle.textContent = title;
    if (el.errorDesc) el.errorDesc.textContent = desc;
  }

  /**
   * Snaps a photo from the live video stream.
   */
  async takePhoto() {
    if (this._isTakingPhoto || !this._camera.isActive) return;
    this._isTakingPhoto = true;

    const el = this.elements;

    // Haptic feedback for tactile shutter sensation
    if (typeof navigator !== 'undefined' && typeof navigator.vibrate === 'function') {
      navigator.vibrate([40]);
    }

    // Trigger visual shutter flash animation
    if (el.shutterFlash) {
      el.shutterFlash.classList.add('flash-active');
      setTimeout(() => el.shutterFlash?.classList.remove('flash-active'), 180);
    }

    try {
      const file = await this._camera.captureFrame(el.videoStream);
      this._capturedRawFile = file;

      // Stop camera stream while reviewing/cropping to save battery & turn off hardware LED
      this._camera.stopStream();

      // Transition to interactive crop & selection screen
      const defaultPreset = (this._mode === 'body') ? 'standard' : 'square';
      await this._initCropSelection(file, defaultPreset);
    } catch (err) {
      console.warn('[CameraModal] Capture frame failed:', err);
      this._toast?.error('Failed to capture photo. Please try again.');
    } finally {
      this._isTakingPhoto = false;
    }
  }

  async _initCropSelection(file, defaultPreset = 'square') {
    const el = this.elements;
    if (!el.cropCanvas) return;

    if (el.streamWrapper) el.streamWrapper.style.display = 'none';
    if (el.cropContainer) el.cropContainer.style.display = 'flex';
    if (el.viewfinderControls) el.viewfinderControls.style.display = 'none';
    if (el.cropControls) el.cropControls.style.display = 'flex';

    // Set presets active state
    el.btnPresetSquare?.classList.toggle('active', defaultPreset === 'square');
    el.btnPresetStandard?.classList.toggle('active', defaultPreset === 'standard');
    el.btnPresetFull?.classList.toggle('active', defaultPreset === 'full');

    const img = new Image();
    const objectUrl = URL.createObjectURL(file);

    await new Promise((resolve, reject) => {
      img.onload = () => resolve(img);
      img.onerror = reject;
      img.src = objectUrl;
    });

    URL.revokeObjectURL(objectUrl);

    this._destroyCropper();
    this._cropper = new ImageCropper({
      canvas: el.cropCanvas,
      sourceImage: img
    });
    this._cropper.init(img, defaultPreset);
  }

  async _resumeLiveStream() {
    this._destroyCropper();
    this._showLiveViewfinderUI();
    try {
      await this._startStreamForCurrentMode();
      await this._updateControlsState();
    } catch (err) {
      this._showError('Camera Resume Failed', 'Could not resume camera stream.');
    }
  }

  async _confirmSelection() {
    const el = this.elements;
    if (!this._cropper || !this._capturedRawFile) return;

    const btn = el.btnConfirmPhoto;
    if (btn) {
      btn.disabled = true;
      btn.textContent = 'Processing...';
    }

    try {
      const croppedBlob = await this._cropper.getCroppedBlob(0.95);
      const finalFile = new File(
        [croppedBlob],
        this._capturedRawFile.name,
        { type: 'image/jpeg', lastModified: Date.now() }
      );

      const callback = this._onCaptureCallback;
      this.close();

      if (typeof callback === 'function') {
        callback(finalFile);
      }
    } catch (err) {
      console.warn('[CameraModal] Crop extraction failed:', err);
      this._toast?.error('Failed to crop photo. Using original image.');
      const callback = this._onCaptureCallback;
      this.close();
      if (typeof callback === 'function' && this._capturedRawFile) {
        callback(this._capturedRawFile);
      }
    } finally {
      if (btn) {
        btn.disabled = false;
        btn.textContent = '✅ Use Selection';
      }
    }
  }

  _destroyCropper() {
    if (this._cropper) {
      this._cropper.destroy();
      this._cropper = null;
    }
  }

  /**
   * Closes the camera modal, halts all active streams, and resets state.
   */
  close() {
    this._camera.stopStream();
    this._destroyCropper();

    const el = this.elements;
    if (el.modal) {
      el.modal.style.display = 'none';
      if (this._stack) {
        this._stack.pop(el.modal);
      } else {
        document.body.classList.remove('modal-open');
      }
    }

    if (typeof this._onCancelCallback === 'function') {
      this._onCancelCallback();
    }
    this._onCaptureCallback = null;
    this._onCancelCallback = null;
    this._onReselectCallback = null;
  }
}
