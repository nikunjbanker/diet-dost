/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * Full End-to-End Multi-Tier & Multi-Database Lifecycle Acceptance Suite
 * Validates:
 * 1. Server-side ISO/IEC 18004 QR Code Generation & Mobile Approval Simulation & Ticket Exchange
 * 2. Multi-tier authentication (Free, Basic, Premium, Admin, SuperAdmin)
 * 3. Meal creation with food items
 * 4. Food item / meal updates (PUT) and persistence verification (GET)
 * 5. Progress photo & weight logging (for permitted tiers) and tier paywall gating
 * 6. Meal deletion & cleanup
 */

const BASE_URL = process.env.BASE_URL || 'http://localhost:5240';
const DEMO_PASSWORD = process.env.DEMO_PASSWORD || 'DietDost@Demo2026!';

const isDeployed = BASE_URL.includes('diet-dost.in');
const ALL_TIERS = [
  { email: 'free@dietdost.app', tier: 'Free', role: 'User', canProgress: false },
  { email: 'basic@dietdost.app', tier: 'Basic', role: 'User', canProgress: false },
  { email: 'premium@dietdost.app', tier: 'Premium', role: 'User', canProgress: true },
  { email: 'admin.demo@dietdost.app', tier: 'Premium', role: 'Admin', canProgress: true, localOnly: true },
  { email: 'superadmin@dietdost.app', tier: 'SuperAdmin', role: 'SuperAdmin', canProgress: true, localOnly: true }
];

const TIERS = isDeployed ? ALL_TIERS.filter(t => !t.localOnly) : ALL_TIERS;

// Minimal 1x1 transparent PNG binary buffer for multipart upload test
const TINY_PNG_BASE64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=';

function assert(condition, message) {
  if (!condition) {
    throw new Error(`ASSERTION FAILED: ${message}`);
  }
}

async function request(path, options = {}) {
  const url = `${BASE_URL.replace(/\/$/, '')}${path}`;
  const res = await fetch(url, options);
  let body = null;
  const contentType = res.headers.get('content-type') || '';
  if (contentType.includes('application/json')) {
    body = await res.json();
  } else {
    body = await res.text();
  }
  return { status: res.status, headers: res.headers, body };
}

async function runQrAuthenticationFlow() {
  console.log('\n--- [FLOW 1] QR Code Lifecycle: Server Generation -> Simulation -> Ticket Exchange ---');

  // Generate 32-byte cryptographically secure browser secret and SHA-256 hash
  const { createHash, randomBytes } = await import('crypto');
  const browserSecretToken = randomBytes(32).toString('hex');
  const browserSecretHash = createHash('sha256').update(browserSecretToken).digest('hex');

  // Step 1: Initiate QR Session
  const initRes = await request('/api/auth/qr/initiate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ browserSecretHash })
  });

  assert(initRes.status === 200, `QR initiate failed with status ${initRes.status}: ${JSON.stringify(initRes.body)}`);
  const { sessionId, challengeNonce, qrSvg, dataUri, confirmationCode, confirmationGlyph } = initRes.body;
  assert(sessionId && sessionId.length > 10, 'sessionId missing or invalid');
  assert(challengeNonce && challengeNonce.length > 10, 'challengeNonce missing');
  assert(qrSvg && qrSvg.startsWith('<svg') && qrSvg.endsWith('</svg>'), 'Server-side QR SVG missing or malformed');
  assert(dataUri && dataUri.startsWith('data:image/svg+xml;base64,'), 'QR data URI missing or malformed');
  assert(confirmationCode && confirmationCode.length >= 2, 'confirmationCode missing');
  assert(confirmationGlyph && confirmationGlyph.length > 3, 'confirmationGlyph missing');

  console.log(`  [PASS] QR Session Initiated: ID=${sessionId.substring(0, 12)}... Match=[${confirmationCode}] ${confirmationGlyph}`);
  console.log(`  [PASS] Server-Side ISO/IEC 18004 SVG generated (${qrSvg.length} bytes, DataURI length ${dataUri.length})`);

  // Step 2: Simulate Mobile Approval
  const simRes = await request('/api/auth/qr/simulate-approval', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId, challengeNonce })
  });

  assert(simRes.status === 200, `QR simulate-approval failed with status ${simRes.status}: ${JSON.stringify(simRes.body)}`);
  const { exchangeTicket } = simRes.body;
  assert(exchangeTicket && exchangeTicket.length > 10, 'exchangeTicket missing');

  console.log(`  [PASS] Mobile Approval Simulated: Ticket=${exchangeTicket.substring(0, 12)}...`);

  // Step 3: Ticket Exchange for JWT
  const exchRes = await request('/api/auth/qr/exchange', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId, exchangeTicket, browserSecretToken })
  });

  assert(exchRes.status === 200, `QR exchange failed with status ${exchRes.status}: ${JSON.stringify(exchRes.body)}`);
  const { token, user } = exchRes.body;
  assert(token && token.length > 20, 'JWT token missing from exchange');
  assert(user && user.email, 'User object missing from exchange');

  console.log(`  [PASS] Ticket Exchange Successful: User=${user.email}, Tier=${user.tier}`);
  return token;
}

async function loginUser(email, password) {
  const res = await request('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ emailOrMobile: email, password })
  });
  assert(res.status === 200, `Login failed for ${email} (status: ${res.status}): ${JSON.stringify(res.body)}`);
  return res.body.token;
}

async function runTierLifecycle(tierConfig) {
  const { email, tier, role, canProgress } = tierConfig;
  console.log(`\n--- [FLOW 2] Full Lifecycle for Tier: ${tier} (${email}, Role: ${role}) ---`);

  // 1. Authenticate
  const token = await loginUser(email, DEMO_PASSWORD);
  const authHeaders = {
    'Authorization': `Bearer ${token}`,
    'Content-Type': 'application/json'
  };

  // 2. Verify /api/auth/me
  const meRes = await request('/api/auth/me', { headers: authHeaders });
  assert(meRes.status === 200, `/api/auth/me failed for ${email}`);
  console.log(`  [PASS] Authenticated Identity: Email=${meRes.body.email || email}`);

  // 3. Verify /api/profile
  const profRes = await request('/api/profile', { headers: authHeaders });
  assert(profRes.status === 200, `/api/profile failed for ${email}`);
  console.log(`  [PASS] Clinical Profile Hydrated: TargetCalories=${profRes.body.targetCalories || profRes.body.targetDailyCalories} kcal`);

  // 4. Meal Creation with Food Items
  const uniqueMutationId = `test-mutation-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
  const mealPayload = {
    mealType: 1, // Lunch
    dishName: 'Palak Paneer & Roti Lunch',
    notes: 'Lifecycle automated test meal',
    clientMutationId: uniqueMutationId,
    clientTimestampUtc: new Date().toISOString(),
    items: [
      {
        name: 'Palak Paneer',
        estimatedPortion: '1 katori (150g)',
        quantity: 1.0,
        calories: 240,
        proteinGrams: 12.5,
        carbsGrams: 8.0,
        fatGrams: 18.0,
        fiberGrams: 3.5,
        sugarGrams: 2.0
      },
      {
        name: 'Tandoori Roti',
        estimatedPortion: '1 medium',
        quantity: 1.0,
        calories: 120,
        proteinGrams: 3.5,
        carbsGrams: 24.0,
        fatGrams: 1.0,
        fiberGrams: 2.0,
        sugarGrams: 0.5
      }
    ]
  };

  const createMealRes = await request('/api/meals/confirm', {
    method: 'POST',
    headers: { ...authHeaders, 'X-Client-Mutation-Id': uniqueMutationId },
    body: JSON.stringify(mealPayload)
  });

  assert(createMealRes.status === 200, `Meal creation failed (status: ${createMealRes.status}): ${JSON.stringify(createMealRes.body)}`);
  const createdMeal = createMealRes.body.meal;
  assert(createdMeal && createdMeal.id, 'Created meal missing ID');
  assert(createdMeal.items && createdMeal.items.length === 2, `Expected 2 food items, got ${createdMeal.items?.length}`);
  console.log(`  [PASS] Meal Created: ID=${createdMeal.id}, TotalCalories=${createdMeal.totalCalories} kcal, Items=${createdMeal.items.length}`);

  const mealId = createdMeal.id;

  // 5. Update Food Item & Meal (PUT)
  // Modify second item (Tandoori Roti: 120 kcal/unit, quantity changed from 1.0 to 2.0 -> 240 kcal)
  createdMeal.items[1].estimatedPortion = '2 medium pieces';
  createdMeal.items[1].quantity = 2.0;
  createdMeal.items[1].calories = 120;
  createdMeal.items[1].proteinGrams = 3.5;
  createdMeal.items[1].carbsGrams = 24.0;
  createdMeal.items[1].fatGrams = 1.0;
  createdMeal.totalCalories = 480;
  createdMeal.notes = 'Lifecycle updated test meal (extra roti)';

  const updateMealRes = await request(`/api/meals/${mealId}`, {
    method: 'PUT',
    headers: authHeaders,
    body: JSON.stringify(createdMeal)
  });

  assert(updateMealRes.status === 200, `Meal update failed (status: ${updateMealRes.status}): ${JSON.stringify(updateMealRes.body)}`);
  const updatedMeal = updateMealRes.body.meal;
  assert(updatedMeal && updatedMeal.totalCalories === 480, `Expected total calories 480, got ${updatedMeal?.totalCalories}`);
  console.log(`  [PASS] Meal & Food Items Updated: ID=${mealId}, NewTotalCalories=${updatedMeal.totalCalories} kcal`);

  // 6. Verify Persistence via GET /api/meals/{id}
  const getMealRes = await request(`/api/meals/${mealId}`, { headers: authHeaders });
  assert(getMealRes.status === 200, `GET meal failed (status: ${getMealRes.status})`);
  assert(getMealRes.body.id === mealId, 'Retrieved meal ID mismatch');
  assert(getMealRes.body.totalCalories === 480, `Persisted calories mismatch: ${getMealRes.body.totalCalories}`);
  const rotiItem = getMealRes.body.items.find(i => i.name === 'Tandoori Roti');
  assert(rotiItem && rotiItem.estimatedPortion === '2 medium pieces', 'Updated food item description not persisted');
  console.log(`  [PASS] Persistence Verified via GET: Database correctly stored updated food items & totals.`);

  // 7. Progress Photo & Comparison Gating (Tier-gated: Premium/Admin/SuperAdmin only)
  const compareRes = await request('/api/progress-photos/comparison', { headers: authHeaders });
  const listPhotosRes = await request('/api/progress-photos', { headers: authHeaders });

  if (canProgress) {
    assert(compareRes.status === 200, `Expected 200 OK for ${tier} progress comparison, got ${compareRes.status}`);
    assert(listPhotosRes.status === 200, `Expected 200 OK for ${tier} progress photos listing, got ${listPhotosRes.status}`);
    console.log(`  [PASS] Progress Photos & Comparison Granted (HTTP 200) for Tier ${tier}`);

    // Test Photo & Weight Upload
    const pngBytes = Buffer.from(TINY_PNG_BASE64, 'base64');
    const boundary = `----WebKitFormBoundary${Date.now()}`;
    let multipartBody = `--${boundary}\r\n`;
    multipartBody += `Content-Disposition: form-data; name="weightKg"\r\n\r\n68.5\r\n`;
    multipartBody += `--${boundary}\r\n`;
    multipartBody += `Content-Disposition: form-data; name="photoType"\r\n\r\n0\r\n`;
    multipartBody += `--${boundary}\r\n`;
    multipartBody += `Content-Disposition: form-data; name="notes"\r\n\r\nAutomated E2E Progress Log\r\n`;
    multipartBody += `--${boundary}\r\n`;
    multipartBody += `Content-Disposition: form-data; name="image"; filename="progress_test.png"\r\n`;
    multipartBody += `Content-Type: image/png\r\n\r\n`;

    const headerBuf = Buffer.from(multipartBody, 'utf-8');
    const footerBuf = Buffer.from(`\r\n--${boundary}--\r\n`, 'utf-8');
    const fullMultipart = Buffer.concat([headerBuf, pngBytes, footerBuf]);

    const progressRes = await request('/api/progress-photos/upload', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': `multipart/form-data; boundary=${boundary}`
      },
      body: fullMultipart
    });

    assert(progressRes.status === 200 || progressRes.status === 201, `Progress upload failed for ${tier} (status ${progressRes.status}): ${JSON.stringify(progressRes.body)}`);
    console.log(`  [PASS] Progress Photo & Weight Upload Succeeded (Weight: 68.5kg) for Tier ${tier}`);
  } else {
    assert(compareRes.status === 403, `Expected 403 Forbidden for ${tier} progress comparison, got ${compareRes.status}`);
    assert(listPhotosRes.status === 403, `Expected 403 Forbidden for ${tier} progress photos listing, got ${listPhotosRes.status}`);
    console.log(`  [PASS] Progress Photo & Comparison Correctly Gated (HTTP 403 Forbidden) for Tier ${tier}`);
  }

  // 8. Delete test meal (Clean database state)
  const deleteRes = await request(`/api/meals/${mealId}`, {
    method: 'DELETE',
    headers: authHeaders
  });
  assert(deleteRes.status === 200, `Meal deletion failed (status: ${deleteRes.status})`);
  console.log(`  [PASS] Test Meal Cleaned Up: ID=${mealId}`);
}

async function main() {
  console.log('======================================================================');
  console.log(`  END-TO-END LIFECYCLE ACCEPTANCE SUITE (${BASE_URL})`);
  console.log('======================================================================');

  // Step 1: Run Server-side QR Code flow
  await runQrAuthenticationFlow();

  // Step 2: Run Full Lifecycle across all 5 tiers
  for (const tierConfig of TIERS) {
    await runTierLifecycle(tierConfig);
  }

  console.log('\n======================================================================');
  console.log('  ALL LIFECYCLE TESTS (QR, AUTH, MEAL LOG/UPDATE, PROGRESS) PASSED 100%! ');
  console.log('======================================================================\n');
}

main().catch(err => {
  console.error('\n❌ LIFECYCLE SUITE FAILED:', err.message);
  process.exit(1);
});
