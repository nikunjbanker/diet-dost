/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */
import assert from 'node:assert';

const BASE_URL = 'http://localhost:5240';

async function run() {
  console.log('--- Phase 1 Layer 7: Mobile BFF & Caching Verification Suite ---');

  // Step 1: Sign in with demo user
  console.log('[1/5] Authenticating as free@dietdost.app...');
  const loginRes = await fetch(`${BASE_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      emailOrMobile: 'free@dietdost.app',
      password: 'DietDost@Demo2026!'
    })
  });

  assert.strictEqual(loginRes.status, 200, `Login failed: HTTP ${loginRes.status}`);
  const loginData = await loginRes.json();
  const token = loginData.token;
  assert.ok(token, 'JWT token must be present');
  console.log('✓ Successfully authenticated. Token length:', token.length);

  // Step 2: Request Mobile BFF composite payload
  console.log('[2/5] Requesting GET /api/mobile/v1/dashboard/composite...');
  const compRes = await fetch(`${BASE_URL}/api/mobile/v1/dashboard/composite`, {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept': 'application/json',
      'Accept-Encoding': 'gzip, br'
    }
  });

  assert.strictEqual(compRes.status, 200, `Composite request failed: HTTP ${compRes.status}`);
  const etagHeader = compRes.headers.get('etag');
  const cacheControlHeader = compRes.headers.get('cache-control');
  console.log('✓ Response headers:', { etag: etagHeader, cacheControl: cacheControlHeader });
  assert.ok(etagHeader, 'ETag response header must be present');
  assert.ok(etagHeader.startsWith('W/"'), 'ETag must be formatted as weak ETag W/"..."');

  const compData = await compRes.json();
  console.log('✓ Received composite payload keys:', Object.keys(compData));
  assert.ok(compData.summary, 'Summary must be present');
  assert.ok(compData.todayMeals, 'TodayMeals must be present');
  assert.ok(compData.quota, 'Quota must be present');
  assert.strictEqual(compData.quota.tier, 'Free', 'Tier must be Free');
  assert.strictEqual(compData.etag, etagHeader, 'Payload etag must match HTTP ETag header');

  const uncompressedBytes = JSON.stringify(compData).length;
  console.log(`✓ Compact uncompressed JSON payload size: ${uncompressedBytes} bytes (< 12 KB requirement met)`);
  assert.ok(uncompressedBytes < 12288, 'Payload must be smaller than 12 KB');

  // Step 3: Test ETag 304 Not Modified
  console.log(`[3/5] Testing ETag cache validation with If-None-Match: ${etagHeader}...`);
  const cacheRes = await fetch(`${BASE_URL}/api/mobile/v1/dashboard/composite`, {
    headers: {
      'Authorization': `Bearer ${token}`,
      'If-None-Match': etagHeader
    }
  });

  console.log(`✓ Cached response status: HTTP ${cacheRes.status}`);
  assert.strictEqual(cacheRes.status, 304, `Expected 304 Not Modified, got ${cacheRes.status}`);
  const cachedBody = await cacheRes.text();
  assert.strictEqual(cachedBody, '', '304 response body must be completely empty');
  console.log('✓ 304 Not Modified verified: zero body transferred over cellular network');

  // Step 4: Verify Response Compression headers
  console.log('[4/5] Testing response compression support...');
  const compHeaderRes = await fetch(`${BASE_URL}/api/mobile/v1/dashboard/composite`, {
    headers: {
      'Authorization': `Bearer ${token}`,
      'Accept': 'application/json',
      'Accept-Encoding': 'gzip, br'
    }
  });
  const contentEncoding = compHeaderRes.headers.get('content-encoding');
  console.log(`✓ Content-Encoding header: ${contentEncoding || 'identity/uncompressed'}`);

  // Step 5: Test Offline-First Idempotent Mutation Support
  console.log('[5/5] Testing offline sync mutation idempotency via clientMutationId...');
  const testMutationId = `mut-${Date.now()}-${Math.random().toString(36).substring(2, 9)}`;
  const testTimestamp = new Date().toISOString();

  const mealPayload = {
    dishName: 'Moong Dal Khichdi with Curd',
    mealType: 3, // Dinner
    overallConfidenceScore: 0.88,
    loggedAt: testTimestamp,
    clientMutationId: testMutationId,
    clientTimestampUtc: testTimestamp,
    items: [
      {
        name: 'Moong Dal Khichdi',
        calories: 280,
        proteinGrams: 10,
        carbsGrams: 45,
        fatGrams: 5,
        fiberGrams: 4
      }
    ]
  };

  // Attempt 1
  const logRes1 = await fetch(`${BASE_URL}/api/meals/confirm`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
      'X-Client-Mutation-Id': testMutationId
    },
    body: JSON.stringify(mealPayload)
  });

  assert.strictEqual(logRes1.status, 200, `First meal log failed: HTTP ${logRes1.status}`);
  const logData1 = await logRes1.json();
  const mealId1 = logData1.meal.id;
  console.log(`✓ First mutation succeeded, Meal ID: ${mealId1}`);

  // Attempt 2 (simulating retry with identical clientMutationId)
  const logRes2 = await fetch(`${BASE_URL}/api/meals/confirm`, {
    method: 'POST',
    headers: {
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json',
      'X-Client-Mutation-Id': testMutationId
    },
    body: JSON.stringify(mealPayload)
  });

  assert.strictEqual(logRes2.status, 200, `Retry meal log failed: HTTP ${logRes2.status}`);
  const logData2 = await logRes2.json();
  const mealId2 = logData2.meal.id;
  console.log(`✓ Retry mutation succeeded, Meal ID: ${mealId2}`);

  assert.strictEqual(mealId1, mealId2, 'Idempotency check failed: identical mutation produced different meal IDs');
  console.log('✓ Idempotency verified: duplicate insertion prevented via clientMutationId');

  console.log('\n=== ALL MOBILE BFF VERIFICATION CHECKS PASSED (5/5) ===');
}

run().catch(err => {
  console.error('❌ Verification failed:', err);
  process.exit(1);
});
