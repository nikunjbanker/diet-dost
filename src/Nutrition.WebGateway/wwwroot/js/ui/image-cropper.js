/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * ImageCropper
 * Lightweight, zero-dependency interactive canvas-based image region selector.
 * Provides touch-friendly drag handles, dark scrim overlay, aspect-ratio presets (1:1, full, freeform),
 * bounds clamping, and native-resolution sub-rectangle extraction.
 */
export class ImageCropper {
  /**
   * @param {Object} [options]
   * @param {HTMLCanvasElement} [options.canvas]
   * @param {HTMLImageElement} [options.sourceImage]
   */
  constructor({ canvas = null, sourceImage = null } = {}) {
    this._canvas = canvas;
    this._image = sourceImage;
    this._ctx = canvas ? canvas.getContext('2d') : null;

    // Display box coordinates (relative to canvas display size)
    this._box = { x: 0, y: 0, width: 0, height: 0 };
    this._activeHandle = null; // 'tl' | 'tr' | 'bl' | 'br' | 'move' | null
    this._dragStart = { x: 0, y: 0 };
    this._boxStart = { x: 0, y: 0, width: 0, height: 0 };

    this._handleRadius = 14; // Touch-friendly radius (28px diameter visual, 44px hit test)
    this._minBoxSize = 48;

    this._boundOnPointerDown = this._onPointerDown.bind(this);
    this._boundOnPointerMove = this._onPointerMove.bind(this);
    this._boundOnPointerUp = this._onPointerUp.bind(this);

    if (this._canvas) {
      this._bindEvents();
    }
  }

  /**
   * Clamps a bounding box strictly within specified boundary dimensions.
   * @param {{ x: number, y: number, width: number, height: number }} box
   * @param {number} boundW
   * @param {number} boundH
   * @param {number} [minSize=48]
   * @returns {{ x: number, y: number, width: number, height: number }}
   */
  clampBox(box, boundW, boundH, minSize = this._minBoxSize) {
    let width = Math.max(minSize, Math.min(box.width, boundW));
    let height = Math.max(minSize, Math.min(box.height, boundH));
    let x = Math.max(0, Math.min(box.x, boundW - width));
    let y = Math.max(0, Math.min(box.y, boundH - height));

    return {
      x: Math.round(x),
      y: Math.round(y),
      width: Math.round(width),
      height: Math.round(height)
    };
  }

  /**
   * Calculates a centered 1:1 square crop box for given bounds.
   * @param {number} boundW
   * @param {number} boundH
   * @returns {{ x: number, y: number, width: number, height: number }}
   */
  calculateSquarePreset(boundW, boundH) {
    const size = Math.round(Math.min(boundW, boundH) * 0.85);
    const x = Math.round((boundW - size) / 2);
    const y = Math.round((boundH - size) / 2);
    return { x, y, width: size, height: size };
  }

  /**
   * Calculates a full-bleed bounding box (100% of bounds).
   * @param {number} boundW
   * @param {number} boundH
   * @returns {{ x: number, y: number, width: number, height: number }}
   */
  calculateFullPreset(boundW, boundH) {
    return { x: 0, y: 0, width: Math.round(boundW), height: Math.round(boundH) };
  }

  /**
   * Scales display canvas coordinates to native source image pixels.
   * @param {{ x: number, y: number, width: number, height: number }} displayBox
   * @param {number} dispW
   * @param {number} dispH
   * @param {number} srcW
   * @param {number} srcH
   * @returns {{ x: number, y: number, width: number, height: number }}
   */
  mapDisplayToSourceCoords(displayBox, dispW, dispH, srcW, srcH) {
    if (!dispW || !dispH) return { x: 0, y: 0, width: srcW, height: srcH };

    const scaleX = srcW / dispW;
    const scaleY = srcH / dispH;

    const x = Math.round(displayBox.x * scaleX);
    const y = Math.round(displayBox.y * scaleY);
    const width = Math.round(displayBox.width * scaleX);
    const height = Math.round(displayBox.height * scaleY);

    return {
      x: Math.max(0, Math.min(x, srcW)),
      y: Math.max(0, Math.min(y, srcH)),
      width: Math.max(1, Math.min(width, srcW - x)),
      height: Math.max(1, Math.min(height, srcH - y))
    };
  }

  /**
   * Initializes the cropper with an image and mounts to canvas.
   * @param {HTMLImageElement} image
   */
  init(image) {
    this._image = image;
    this.resizeCanvas();
    // Default to 1:1 plate preset initially for food photography
    this._box = this.calculateSquarePreset(this._canvas.width, this._canvas.height);
    this.render();
  }

  resizeCanvas() {
    if (!this._canvas || !this._image) return;
    const container = this._canvas.parentElement;
    const maxW = container ? container.clientWidth || 360 : 360;
    const maxH = container ? container.clientHeight || 420 : 420;

    const imgAspect = (this._image.naturalWidth || this._image.width) / (this._image.naturalHeight || this._image.height);
    let canvasW = maxW;
    let canvasH = Math.round(maxW / imgAspect);

    if (canvasH > maxH) {
      canvasH = maxH;
      canvasW = Math.round(maxH * imgAspect);
    }

    this._canvas.width = canvasW;
    this._canvas.height = canvasH;
  }

  applySquarePreset() {
    if (!this._canvas) return;
    this._box = this.calculateSquarePreset(this._canvas.width, this._canvas.height);
    this.render();
  }

  applyFullPreset() {
    if (!this._canvas) return;
    this._box = this.calculateFullPreset(this._canvas.width, this._canvas.height);
    this.render();
  }

  /**
   * Renders image, dark scrim, bounding box, rule of thirds grid, and touch handles.
   */
  render() {
    if (!this._ctx || !this._canvas || !this._image) return;
    const { width: w, height: h } = this._canvas;
    const ctx = this._ctx;

    ctx.clearRect(0, 0, w, h);

    // 1. Draw source image
    ctx.drawImage(this._image, 0, 0, w, h);

    // 2. Draw dark scrim outside the crop box
    ctx.save();
    ctx.fillStyle = 'rgba(8, 10, 15, 0.65)';
    ctx.beginPath();
    ctx.rect(0, 0, w, h);
    // Cut out crop box using counter-clockwise path
    ctx.rect(this._box.x + this._box.width, this._box.y, -this._box.width, this._box.height);
    ctx.fill();
    ctx.restore();

    // 3. Draw crop box border with Linear lavender accent
    ctx.save();
    ctx.strokeStyle = '#5e6ad2';
    ctx.lineWidth = 2;
    ctx.strokeRect(this._box.x, this._box.y, this._box.width, this._box.height);

    // 4. Draw rule-of-thirds grid inside crop box
    ctx.strokeStyle = 'rgba(255, 255, 255, 0.22)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    const thirdW = this._box.width / 3;
    const thirdH = this._box.height / 3;
    // Verticals
    ctx.moveTo(this._box.x + thirdW, this._box.y);
    ctx.lineTo(this._box.x + thirdW, this._box.y + this._box.height);
    ctx.moveTo(this._box.x + thirdW * 2, this._box.y);
    ctx.lineTo(this._box.x + thirdW * 2, this._box.y + this._box.height);
    // Horizontals
    ctx.moveTo(this._box.x, this._box.y + thirdH);
    ctx.lineTo(this._box.x + this._box.width, this._box.y + thirdH);
    ctx.moveTo(this._box.x, this._box.y + thirdH * 2);
    ctx.lineTo(this._box.x + this._box.width, this._box.y + thirdH * 2);
    ctx.stroke();

    // 5. Draw 4 corner handles
    this._drawHandle(ctx, this._box.x, this._box.y); // TL
    this._drawHandle(ctx, this._box.x + this._box.width, this._box.y); // TR
    this._drawHandle(ctx, this._box.x, this._box.y + this._box.height); // BL
    this._drawHandle(ctx, this._box.x + this._box.width, this._box.y + this._box.height); // BR
    ctx.restore();
  }

  _drawHandle(ctx, x, y) {
    ctx.save();
    ctx.fillStyle = '#ffffff';
    ctx.strokeStyle = '#5e6ad2';
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(x, y, 7, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }

  _bindEvents() {
    this._canvas.addEventListener('pointerdown', this._boundOnPointerDown);
    window.addEventListener('pointermove', this._boundOnPointerMove);
    window.addEventListener('pointerup', this._boundOnPointerUp);
    window.addEventListener('pointercancel', this._boundOnPointerUp);
  }

  _getCanvasPoint(e) {
    const rect = this._canvas.getBoundingClientRect();
    return {
      x: e.clientX - rect.left,
      y: e.clientY - rect.top
    };
  }

  _detectHandle(pt) {
    const hitRadius = 24; // Generous 48px hit target
    const corners = [
      { name: 'tl', x: this._box.x, y: this._box.y },
      { name: 'tr', x: this._box.x + this._box.width, y: this._box.y },
      { name: 'bl', x: this._box.x, y: this._box.y + this._box.height },
      { name: 'br', x: this._box.x + this._box.width, y: this._box.y + this._box.height }
    ];

    for (const c of corners) {
      const dist = Math.hypot(pt.x - c.x, pt.y - c.y);
      if (dist <= hitRadius) return c.name;
    }

    // Inside crop box -> move
    if (
      pt.x >= this._box.x &&
      pt.x <= this._box.x + this._box.width &&
      pt.y >= this._box.y &&
      pt.y <= this._box.y + this._box.height
    ) {
      return 'move';
    }

    return null;
  }

  _onPointerDown(e) {
    const pt = this._getCanvasPoint(e);
    const handle = this._detectHandle(pt);
    if (!handle) return;

    this._activeHandle = handle;
    this._dragStart = { x: pt.x, y: pt.y };
    this._boxStart = { ...this._box };
    this._canvas.setPointerCapture?.(e.pointerId);
    e.preventDefault();
  }

  _onPointerMove(e) {
    if (!this._activeHandle) return;
    const pt = this._getCanvasPoint(e);
    const dx = pt.x - this._dragStart.x;
    const dy = pt.y - this._dragStart.y;
    const boundW = this._canvas.width;
    const boundH = this._canvas.height;

    let newBox = { ...this._boxStart };

    if (this._activeHandle === 'move') {
      newBox.x = this._boxStart.x + dx;
      newBox.y = this._boxStart.y + dy;
    } else if (this._activeHandle === 'tl') {
      newBox.x = this._boxStart.x + dx;
      newBox.y = this._boxStart.y + dy;
      newBox.width = this._boxStart.width - dx;
      newBox.height = this._boxStart.height - dy;
    } else if (this._activeHandle === 'tr') {
      newBox.y = this._boxStart.y + dy;
      newBox.width = this._boxStart.width + dx;
      newBox.height = this._boxStart.height - dy;
    } else if (this._activeHandle === 'bl') {
      newBox.x = this._boxStart.x + dx;
      newBox.width = this._boxStart.width - dx;
      newBox.height = this._boxStart.height + dy;
    } else if (this._activeHandle === 'br') {
      newBox.width = this._boxStart.width + dx;
      newBox.height = this._boxStart.height + dy;
    }

    this._box = this.clampBox(newBox, boundW, boundH);
    this.render();
    e.preventDefault();
  }

  _onPointerUp() {
    this._activeHandle = null;
  }

  /**
   * Extracts the selected region from the high-resolution source image and returns a JPEG Blob.
   * @param {number} [quality=0.95]
   * @returns {Promise<Blob>}
   */
  async getCroppedBlob(quality = 0.95) {
    if (!this._image || !this._canvas) {
      throw new Error('ImageCropper is not initialized with an image.');
    }

    const srcW = this._image.naturalWidth || this._image.width;
    const srcH = this._image.naturalHeight || this._image.height;
    const dispW = this._canvas.width;
    const dispH = this._canvas.height;

    // Map display coordinates to source resolution
    const sourceCoords = this.mapDisplayToSourceCoords(this._box, dispW, dispH, srcW, srcH);

    const offscreen = document.createElement('canvas');
    offscreen.width = sourceCoords.width;
    offscreen.height = sourceCoords.height;
    const ctx = offscreen.getContext('2d');
    if (!ctx) throw new Error('Offscreen 2D context unavailable.');

    ctx.imageSmoothingEnabled = true;
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(
      this._image,
      sourceCoords.x,
      sourceCoords.y,
      sourceCoords.width,
      sourceCoords.height,
      0,
      0,
      sourceCoords.width,
      sourceCoords.height
    );

    return new Promise((resolve, reject) => {
      offscreen.toBlob(
        (blob) => {
          if (!blob) {
            reject(new Error('Failed to extract cropped image blob.'));
            return;
          }
          resolve(blob);
        },
        'image/jpeg',
        quality
      );
    });
  }

  destroy() {
    if (this._canvas) {
      this._canvas.removeEventListener('pointerdown', this._boundOnPointerDown);
    }
    window.removeEventListener('pointermove', this._boundOnPointerMove);
    window.removeEventListener('pointerup', this._boundOnPointerUp);
    window.removeEventListener('pointercancel', this._boundOnPointerUp);
    this._canvas = null;
    this._image = null;
    this._ctx = null;
  }
}
