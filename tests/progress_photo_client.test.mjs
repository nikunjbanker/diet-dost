/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import assert from 'node:assert/strict';
import test from 'node:test';
import { AuthService } from '../src/Nutrition.WebGateway/wwwroot/js/services/auth-service.js';
import { ApiClient } from '../src/Nutrition.WebGateway/wwwroot/js/services/api-client.js';
import { ProgressPhotosService } from '../src/Nutrition.WebGateway/wwwroot/js/services/progress-service.js';

test('progress photo fetch sends the stored bearer token and bypasses HTTP caching', async () => {
  const originalFetch = globalThis.fetch;
  const originalLocalStorage = globalThis.localStorage;
  globalThis.localStorage = {
    getItem: key => key === 'dd_jwt_token' ? 'test-token' : null
  };

  let requestUrl;
  let requestOptions;
  globalThis.fetch = async (url, options) => {
    requestUrl = url;
    requestOptions = options;
    return new Response(new Uint8Array([1, 2, 3]), {
      status: 200,
      headers: { 'Content-Type': 'image/jpeg' }
    });
  };

  try {
    const service = new ProgressPhotosService(new ApiClient());
    const blob = await service.getPhoto('photo/id');

    assert.equal(requestUrl, '/api/media/progress/photo%2Fid');
    assert.equal(requestOptions.headers.Authorization, 'Bearer test-token');
    assert.equal(requestOptions.credentials, 'same-origin');
    assert.equal(requestOptions.cache, 'no-store');
    assert.equal(blob.type, 'image/jpeg');
    assert.equal(blob.size, 3);
  } finally {
    globalThis.fetch = originalFetch;
    if (originalLocalStorage === undefined) delete globalThis.localStorage;
    else globalThis.localStorage = originalLocalStorage;
  }
});

test('sign out clears the session and emits the event used to revoke progress photo URLs', async () => {
  const originalLocalStorage = globalThis.localStorage;
  const originalWindow = globalThis.window;
  const events = [];
  globalThis.localStorage = {
    removedKey: null,
    removeItem(key) {
      this.removedKey = key;
    }
  };
  globalThis.window = {
    dispatchEvent(event) {
      events.push(event.type);
    }
  };

  try {
    const auth = new AuthService({ post: async () => ({}) });
    auth.currentUser = { id: 'user-1' };
    await auth.logout();

    assert.equal(globalThis.localStorage.removedKey, 'dd_jwt_token');
    assert.equal(auth.currentUser, null);
    assert.deepEqual(events, ['auth:logout']);
  } finally {
    if (originalLocalStorage === undefined) delete globalThis.localStorage;
    else globalThis.localStorage = originalLocalStorage;
    if (originalWindow === undefined) delete globalThis.window;
    else globalThis.window = originalWindow;
  }
});
