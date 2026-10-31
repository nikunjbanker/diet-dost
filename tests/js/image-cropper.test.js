/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import test from 'node:test';
import assert from 'node:assert/strict';

test('ImageCropper coordinate calculations and bounds clamping', async (t) => {
  const { ImageCropper } = await import('../../src/Nutrition.WebGateway/wwwroot/js/ui/image-cropper.js');

  await t.test('clampBox prevents coordinates from escaping image boundaries', () => {
    const cropper = new ImageCropper();
    // image is 1000x800
    const clamped = cropper.clampBox({ x: -50, y: -20, width: 1200, height: 900 }, 1000, 800);
    assert.equal(clamped.x >= 0, true);
    assert.equal(clamped.y >= 0, true);
    assert.equal(clamped.x + clamped.width <= 1000, true);
    assert.equal(clamped.y + clamped.height <= 800, true);
  });

  await t.test('applySquarePreset produces a 1:1 centered bounding box', () => {
    const cropper = new ImageCropper();
    // image is 1920x1080
    const square = cropper.calculateSquarePreset(1920, 1080);
    assert.equal(square.width, square.height);
    assert.equal(square.width <= 1080, true);
    // Centered horizontally: x should be (1920 - width) / 2
    assert.equal(square.x, Math.round((1920 - square.width) / 2));
    assert.equal(square.y, Math.round((1080 - square.height) / 2));
  });

  await t.test('mapDisplayToSourceCoords scales UI canvas coordinates to native image pixels', () => {
    const cropper = new ImageCropper();
    // UI display width 400x300, native image 1920x1080 (downscaled 400/1920)
    const displayBox = { x: 50, y: 30, width: 200, height: 150 };
    const sourceCoords = cropper.mapDisplayToSourceCoords(displayBox, 400, 300, 1920, 1080);

    assert.equal(sourceCoords.x, Math.round(50 * (1920 / 400)));
    assert.equal(sourceCoords.y, Math.round(30 * (1080 / 300)));
    assert.equal(sourceCoords.width, Math.round(200 * (1920 / 400)));
    assert.equal(sourceCoords.height, Math.round(150 * (1080 / 300)));
  });
});
