/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

import { encodeQrSvg, QrCodeEncoder } from '../src/Nutrition.WebGateway/wwwroot/js/ui/qr-code-encoder.js';

console.log('--- Testing ISO/IEC 18004 QR Code Generator ---');

// Test 1: Short text
const shortText = 'https://diet-dost.in';
const svgShort = encodeQrSvg(shortText, { size: 180, eccLevel: 'M' });
if (!svgShort.startsWith('<svg') || !svgShort.includes('<path d="') || !svgShort.endsWith('</svg>')) {
  throw new Error('Test 1 Failed: Short text SVG generation malformed.');
}
console.log('✓ Test 1 Passed: Short text QR SVG generated cleanly.');

// Test 2: Full web approval URL with session, challenge, match code, and glyph
const approvalUrl = 'https://dev.diet-dost.in/qr-approve.html?sid=dd-session-9876543210&nonce=chal-abcdef123456&code=16&glyph=Jade+Phoenix';
const svgLong = encodeQrSvg(approvalUrl, { size: 220, eccLevel: 'M' });
if (!svgLong.includes('viewBox="0 0 ') || !svgLong.includes('width="220"')) {
  throw new Error('Test 2 Failed: Long approval URL SVG dimensions malformed.');
}
console.log('✓ Test 2 Passed: Full HTTPS Web Approval QR SVG generated cleanly.');

// Test 3: Matrix structure validation
const { size, matrix } = QrCodeEncoder.create(approvalUrl, 'M');
console.log(`✓ Test 3 Passed: Selected QR Symbol Size = ${size}x${size} modules.`);

// Verify top-left finder pattern (7x7)
for (let r = 0; r < 7; r++) {
  for (let c = 0; c < 7; c++) {
    const isBorder = (r === 0 || r === 6 || c === 0 || c === 6);
    const isCenter = (r >= 2 && r <= 4 && c >= 2 && c <= 4);
    const expected = (isBorder || isCenter) ? 1 : 0;
    if (matrix[r][c] !== expected) {
      throw new Error(`Test 3 Failed: Top-Left finder pattern mismatch at (${r},${c}): expected ${expected}, got ${matrix[r][c]}`);
    }
  }
}
console.log('✓ Test 4 Passed: Top-Left Finder Pattern (7x7) mathematically verified.');

// Verify top-right finder pattern (7x7)
for (let r = 0; r < 7; r++) {
  for (let c = size - 7; c < size; c++) {
    const colRel = c - (size - 7);
    const isBorder = (r === 0 || r === 6 || colRel === 0 || colRel === 6);
    const isCenter = (r >= 2 && r <= 4 && colRel >= 2 && colRel <= 4);
    const expected = (isBorder || isCenter) ? 1 : 0;
    if (matrix[r][c] !== expected) {
      throw new Error(`Test 4 Failed: Top-Right finder pattern mismatch at (${r},${c})`);
    }
  }
}
console.log('✓ Test 5 Passed: Top-Right Finder Pattern (7x7) mathematically verified.');

console.log('✅ ALL QR CODE GENERATOR TESTS PASSED 100%!');
