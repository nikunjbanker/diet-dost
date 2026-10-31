/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * CameraService
 * Provides hardware-abstracted access to browser MediaDevices WebRTC camera streaming,
 * optical/sensor zoom, camera switching, torch/flashlight toggling, high-resolution snapshot capture,
 * and zero-leak track teardown.
 */
export class CameraService {
  /**
   * @param {Object} [deps] Optional injected dependencies for testability
   * @param {Navigator} [deps.navigatorRef]
   * @param {Window} [deps.windowRef]
   */
  constructor({ navigatorRef = (typeof navigator !== 'undefined' ? navigator : null), windowRef = (typeof window !== 'undefined' ? window : null) } = {}) {
    this._navigator = navigatorRef;
    this._window = windowRef;

    this._stream = null;
    this._activeFacingMode = 'environment';
    this._currentZoom = 1.0;
    this._isTorchOn = false;
  }

  /**
   * Evaluates if WebRTC camera access is supported in current browser and context.
   * Requires secure context (HTTPS or localhost) and MediaDevices API.
   * @returns {boolean}
   */
  isSupported() {
    if (!this._window || !this._window.isSecureContext) return false;
    return !!(this._navigator?.mediaDevices?.getUserMedia);
  }

  /**
   * Queries existing camera permission status where supported by Permissions API.
   * @returns {Promise<'granted'|'prompt'|'denied'|'unknown'>}
   */
  async checkPermission() {
    try {
      if (this._navigator?.permissions?.query) {
        const result = await this._navigator.permissions.query({ name: 'camera' });
        return result.state;
      }
    } catch {
      // Some browsers don't support { name: 'camera' } in permissions.query
    }
    return 'unknown';
  }

  get isActive() {
    return !!(this._stream && this._stream.active);
  }

  get currentFacingMode() {
    return this._activeFacingMode;
  }

  get currentZoom() {
    return this._currentZoom;
  }

  get isTorchOn() {
    return this._isTorchOn;
  }

  /**
   * Starts live video camera stream.
   * @param {Object} [options]
   * @param {'environment'|'user'} [options.facingMode='environment']
   * @param {number} [options.idealWidth=1920]
   * @param {number} [options.idealHeight=1080]
   * @returns {Promise<MediaStream>}
   */
  async startStream({ facingMode = 'environment', idealWidth = 1920, idealHeight = 1080 } = {}) {
    if (!this.isSupported()) {
      const err = new Error('Camera is not supported or not running in a secure context (HTTPS/localhost).');
      err.name = 'NotSupportedError';
      throw err;
    }

    // Stop any existing stream before starting a new one
    this.stopStream();
    this._activeFacingMode = facingMode;

    const constraints = {
      audio: false,
      video: {
        facingMode: { ideal: facingMode },
        width: { ideal: idealWidth },
        height: { ideal: idealHeight }
      }
    };

    try {
      this._stream = await this._navigator.mediaDevices.getUserMedia(constraints);
      this._currentZoom = 1.0;
      this._isTorchOn = false;
      return this._stream;
    } catch (err) {
      // Retry without width/height constraints if overconstrained
      if (err.name === 'OverconstrainedError') {
        this._stream = await this._navigator.mediaDevices.getUserMedia({
          audio: false,
          video: { facingMode: { ideal: facingMode } }
        });
        return this._stream;
      }
      throw err;
    }
  }

  /**
   * Stops all tracks of the active media stream and nullifies references.
   * Guarantees camera hardware LED turns off immediately.
   */
  stopStream() {
    if (this._stream) {
      try {
        const tracks = this._stream.getTracks();
        tracks.forEach(track => {
          try {
            track.stop();
          } catch {
            // Ignore track stop errors
          }
        });
      } catch {
        // Ignore stream access errors
      }
      this._stream = null;
    }
    this._isTorchOn = false;
  }

  /**
   * Enumerates available video input devices to determine if switching front/back is possible.
   * @returns {Promise<MediaDeviceInfo[]>}
   */
  async getAvailableCameras() {
    if (!this._navigator?.mediaDevices?.enumerateDevices) return [];
    try {
      const devices = await this._navigator.mediaDevices.enumerateDevices();
      return devices.filter(d => d.kind === 'videoinput');
    } catch {
      return [];
    }
  }

  /**
   * Flips between rear ('environment') and front ('user') camera.
   * @returns {Promise<MediaStream>}
   */
  async flipCamera() {
    const nextFacing = this._activeFacingMode === 'environment' ? 'user' : 'environment';
    return this.startStream({ facingMode: nextFacing });
  }

  /**
   * Checks if torch / flashlight is supported by the active video track.
   * @returns {boolean}
   */
  hasTorch() {
    if (!this._stream) return false;
    const track = this._stream.getVideoTracks()[0];
    if (!track || typeof track.getCapabilities !== 'function') return false;
    const caps = track.getCapabilities();
    return !!caps.torch;
  }

  /**
   * Toggles or sets torch / flashlight state.
   * @param {boolean} [enabled]
   * @returns {Promise<boolean>}
   */
  async setTorch(enabled = !this._isTorchOn) {
    if (!this.hasTorch()) return false;
    const track = this._stream.getVideoTracks()[0];
    try {
      await track.applyConstraints({
        advanced: [{ torch: enabled }]
      });
      this._isTorchOn = enabled;
      return true;
    } catch (err) {
      console.warn('[CameraService] Torch toggle failed:', err);
      return false;
    }
  }

  /**
   * Returns zoom capabilities of active video track if hardware zoom is supported.
   * @returns {{ min: number, max: number, step: number }|null}
   */
  getZoomCapabilities() {
    if (!this._stream) return null;
    const track = this._stream.getVideoTracks()[0];
    if (!track || typeof track.getCapabilities !== 'function') return null;
    const caps = track.getCapabilities();
    if (caps.zoom) {
      return {
        min: caps.zoom.min || 1,
        max: caps.zoom.max || 5,
        step: caps.zoom.step || 0.1
      };
    }
    return null;
  }

  /**
   * Sets zoom level on active video track via hardware constraints.
   * @param {number} level
   * @returns {Promise<boolean>}
   */
  async setZoom(level) {
    if (!this._stream) return false;
    const track = this._stream.getVideoTracks()[0];
    if (!track) return false;

    const caps = this.getZoomCapabilities();
    if (!caps) return false;

    const clampedZoom = Math.max(caps.min, Math.min(caps.max, level));
    try {
      if (typeof track.applyConstraints === 'function') {
        await track.applyConstraints({
          advanced: [{ zoom: clampedZoom }]
        });
        this._currentZoom = clampedZoom;
        return true;
      }
    } catch (err) {
      console.warn('[CameraService] Apply zoom constraints failed:', err);
    }
    return false;
  }

  /**
   * Captures a high-resolution snapshot from video element or ImageCapture API.
   * Returns a standard immutable File object ready for optimization or upload.
   * @param {HTMLVideoElement} videoEl
   * @returns {Promise<File>}
   */
  async captureFrame(videoEl) {
    const timestamp = Date.now();
    const fileName = `camera_capture_${timestamp}.jpg`;

    // 1. Try ImageCapture API if supported for full sensor resolution
    if (this._stream && this._window && 'ImageCapture' in this._window) {
      const track = this._stream.getVideoTracks()[0];
      if (track && track.readyState === 'live') {
        try {
          // @ts-ignore
          const imageCapture = new this._window.ImageCapture(track);
          const photoBlob = await imageCapture.takePhoto();
          if (photoBlob && photoBlob.size > 0) {
            return new File([photoBlob], fileName, { type: 'image/jpeg', lastModified: timestamp });
          }
        } catch (icErr) {
          console.warn('[CameraService] ImageCapture.takePhoto() fallback to canvas:', icErr);
        }
      }
    }

    // 2. High-resolution canvas snapshot fallback
    if (!videoEl || !videoEl.videoWidth || !videoEl.videoHeight) {
      throw new Error('Video stream is not ready for frame capture.');
    }

    const canvas = document.createElement('canvas');
    canvas.width = videoEl.videoWidth;
    canvas.height = videoEl.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Canvas 2D context unavailable.');

    // High quality draw
    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(videoEl, 0, 0, canvas.width, canvas.height);

    return new Promise((resolve, reject) => {
      canvas.toBlob(
        (blob) => {
          if (!blob) {
            reject(new Error('Failed to generate image blob from camera frame.'));
            return;
          }
          const file = new File([blob], fileName, { type: 'image/jpeg', lastModified: timestamp });
          resolve(file);
        },
        'image/jpeg',
        0.95
      );
    });
  }
}
