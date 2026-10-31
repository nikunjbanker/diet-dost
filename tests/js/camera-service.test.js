/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import test from 'node:test';
import assert from 'node:assert/strict';

// Test that camera-service module can be imported and exhibits required contract
test('CameraService Contract and Lifecycle', async (t) => {
  const { CameraService } = await import('../../src/Nutrition.WebGateway/wwwroot/js/services/camera-service.js');
  
  await t.test('isSupported returns false when navigator.mediaDevices is absent', () => {
    const service = new CameraService({
      navigatorRef: {},
      windowRef: { isSecureContext: true }
    });
    assert.equal(service.isSupported(), false);
  });

  await t.test('isSupported returns false when context is insecure', () => {
    const service = new CameraService({
      navigatorRef: { mediaDevices: { getUserMedia: () => {} } },
      windowRef: { isSecureContext: false }
    });
    assert.equal(service.isSupported(), false);
  });

  await t.test('isSupported returns true when mediaDevices and secureContext present', () => {
    const service = new CameraService({
      navigatorRef: { mediaDevices: { getUserMedia: () => {} } },
      windowRef: { isSecureContext: true }
    });
    assert.equal(service.isSupported(), true);
  });

  await t.test('stopStream terminates all tracks and clears references', async () => {
    let stopped = false;
    const mockTrack = {
      stop: () => { stopped = true; },
      getCapabilities: () => ({})
    };
    const mockStream = {
      active: true,
      getTracks: () => [mockTrack],
      getVideoTracks: () => [mockTrack]
    };
    const service = new CameraService({
      navigatorRef: {
        mediaDevices: {
          getUserMedia: async () => mockStream,
          enumerateDevices: async () => []
        }
      },
      windowRef: { isSecureContext: true }
    });

    await service.startStream();
    assert.equal(service.isActive, true);

    service.stopStream();
    assert.equal(stopped, true);
    assert.equal(service.isActive, false);
  });

  await t.test('setZoom applies hardware constraints when supported', async () => {
    let appliedConstraints = null;
    const mockTrack = {
      stop: () => {},
      getCapabilities: () => ({ zoom: { min: 1, max: 5, step: 0.1 } }),
      applyConstraints: async (c) => { appliedConstraints = c; }
    };
    const mockStream = {
      active: true,
      getTracks: () => [mockTrack],
      getVideoTracks: () => [mockTrack]
    };
    const service = new CameraService({
      navigatorRef: {
        mediaDevices: {
          getUserMedia: async () => mockStream,
          enumerateDevices: async () => []
        }
      },
      windowRef: { isSecureContext: true }
    });

    await service.startStream();
    const success = await service.setZoom(2.5);
    assert.equal(success, true);
    assert.deepEqual(appliedConstraints, { advanced: [{ zoom: 2.5 }] });
    assert.equal(service.currentZoom, 2.5);
  });
});
