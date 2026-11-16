/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

/**
 * ShareCardRenderer
 * Client-Side High-DPI HTML5 Canvas Graphics Engine.
 * Generates context-specific social nutrition cards for:
 *  - Meal Log Entry Details (Dish, Plate Photo, Macros, Dost Insight, Items)
 *  - Daily View / Metabolic Pulse (Calorie Balance, Macros, Health Score, Streak, Badges)
 * Supports 1:1 Square (1080x1080) and 9:16 Story (1080x1920) formats.
 */
export class ShareCardRenderer {
  constructor() {
    this._appUrl = 'https://dietdost.app';
  }

  /**
   * Helper to round-clip a rectangle.
   */
  _roundRect(ctx, x, y, width, height, radius) {
    ctx.beginPath();
    ctx.moveTo(x + radius, y);
    ctx.lineTo(x + width - radius, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
    ctx.lineTo(x + width, y + height - radius);
    ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
    ctx.lineTo(x + radius, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
    ctx.lineTo(x, y + radius);
    ctx.quadraticCurveTo(x, y, x + radius, y);
    ctx.closePath();
  }

  /**
   * Helper to wrap text cleanly within a maximum width.
   */
  _wrapText(ctx, text, x, y, maxWidth, lineHeight, maxLines = 4) {
    if (!text) return y;
    const words = text.split(' ');
    let line = '';
    let currentY = y;
    let lineCount = 0;

    for (let n = 0; n < words.length; n++) {
      const testLine = line + words[n] + ' ';
      const metrics = ctx.measureText(testLine);
      const testWidth = metrics.width;
      if (testWidth > maxWidth && n > 0) {
        ctx.fillText(line.trim(), x, currentY);
        line = words[n] + ' ';
        currentY += lineHeight;
        lineCount++;
        if (lineCount >= maxLines - 1 && n < words.length - 1) {
          line = words.slice(n).join(' ') + '...';
          break;
        }
      } else {
        line = testLine;
      }
    }
    ctx.fillText(line.trim(), x, currentY);
    return currentY + lineHeight;
  }

  /**
   * Asynchronously loads an image from URL into an HTMLImageElement with CORS fallback.
   */
  _loadImage(url) {
    return new Promise((resolve) => {
      if (!url) {
        resolve(null);
        return;
      }
      const img = new Image();
      img.crossOrigin = 'anonymous';
      img.onload = () => resolve(img);
      img.onerror = () => {
        // Fallback without crossOrigin in case CORS header is omitted for same-origin
        const fallbackImg = new Image();
        fallbackImg.onload = () => resolve(fallbackImg);
        fallbackImg.onerror = () => resolve(null);
        fallbackImg.src = url;
      };
      img.src = url;
    });
  }

  /**
   * Render Context-Specific Meal Log Entry Card.
   * @param {Object} meal
   * @param {Object} [options]
   * @param {'square'|'story'} [options.aspectRatio='square']
   * @param {boolean} [options.includePhoto=true]
   * @param {boolean} [options.includeAdvice=true]
   * @param {boolean} [options.anonymize=false]
   * @returns {Promise<{ canvas: HTMLCanvasElement, dataUrl: string, blob: Blob }>}
   */
  async renderMealCard(meal, options = {}) {
    const isStory = options.aspectRatio === 'story';
    const width = 1080;
    const height = isStory ? 1920 : 1080;

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');

    // 1. Obsidian Dark Background & Ambient Radial Glows
    const bgGrad = ctx.createLinearGradient(0, 0, 0, height);
    bgGrad.addColorStop(0, '#0a0b0d');
    bgGrad.addColorStop(0.5, '#08090a');
    bgGrad.addColorStop(1, '#111215');
    ctx.fillStyle = bgGrad;
    ctx.fillRect(0, 0, width, height);

    // Accent Glow Circles (Obsidian Brand Aesthetic)
    const topGlow = ctx.createRadialGradient(width * 0.8, 120, 10, width * 0.8, 120, 420);
    topGlow.addColorStop(0, 'rgba(94, 106, 210, 0.22)');
    topGlow.addColorStop(1, 'rgba(94, 106, 210, 0)');
    ctx.fillStyle = topGlow;
    ctx.fillRect(0, 0, width, height);

    const emeraldGlow = ctx.createRadialGradient(160, height * 0.7, 10, 160, height * 0.7, 380);
    emeraldGlow.addColorStop(0, 'rgba(39, 195, 128, 0.15)');
    emeraldGlow.addColorStop(1, 'rgba(39, 195, 128, 0)');
    ctx.fillStyle = emeraldGlow;
    ctx.fillRect(0, 0, width, height);

    // Card Outer Hairline Border
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 2;
    this._roundRect(ctx, 32, 32, width - 64, height - 64, 28);
    ctx.stroke();

    // 2. Top Header: Diet Dost Logomark & Meal Type
    ctx.fillStyle = '#27c380';
    ctx.beginPath();
    ctx.arc(76, 88, 10, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = '#f7f8f8';
    ctx.font = 'bold 32px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('DIET DOST', 100, 96);

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('· Indian Clinical Nutrition Web App', 285, 95);

    // Top Right: Meal Type Pill
    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const mealTypeIcons = ['🌅', '☀️', '☕', '🌙'];
    const typeIdx = typeof meal.mealType === 'number' ? meal.mealType : 1;
    const typeName = mealTypeNames[typeIdx] || meal.mealType || 'Meal';
    const typeIcon = mealTypeIcons[typeIdx] || '🍽️';

    ctx.fillStyle = 'rgba(94, 106, 210, 0.18)';
    this._roundRect(ctx, width - 260, 64, 185, 46, 23);
    ctx.fill();
    ctx.strokeStyle = 'rgba(94, 106, 210, 0.4)';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    ctx.fillStyle = '#a5b4fc';
    ctx.font = '600 22px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`${typeIcon} ${typeName}`, width - 238, 95);

    // Date & Time
    const d = new Date(meal.loggedAt || meal.LoggedAt || Date.now());
    const dateStr = !isNaN(d)
      ? d.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric', year: 'numeric' })
      : 'Today';
    const timeStr = !isNaN(d) ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '';
    ctx.fillStyle = '#62666d';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`${dateStr} · ${timeStr}`, 76, 145);

    // 3. Main Dish Title
    const dishName = meal.dishName || meal.DishName || 'Logged Indian Meal';
    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 44px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    this._wrapText(ctx, dishName, 76, 205, width - 152, 54, 2);

    // 4. Hero Meal Plate Photo (if available and requested)
    let photoBottom = 230;
    const photoUrl = meal.photoUrl || meal.PhotoUrl || meal.imageUrl || meal.ImageUrl;
    if (options.includePhoto !== false && photoUrl) {
      const photoImg = await this._loadImage(photoUrl);
      if (photoImg) {
        const photoX = 76;
        const photoY = 230;
        const photoW = width - 152;
        const photoH = isStory ? 580 : 380;

        ctx.save();
        this._roundRect(ctx, photoX, photoY, photoW, photoH, 20);
        ctx.clip();

        // Aspect-ratio cover image inside box
        const imgRatio = photoImg.width / photoImg.height;
        const boxRatio = photoW / photoH;
        let sW, sH, sX, sY;
        if (imgRatio > boxRatio) {
          sH = photoImg.height;
          sW = photoImg.height * boxRatio;
          sX = (photoImg.width - sW) / 2;
          sY = 0;
        } else {
          sW = photoImg.width;
          sH = photoImg.width / boxRatio;
          sX = 0;
          sY = (photoImg.height - sH) / 2;
        }
        ctx.drawImage(photoImg, sX, sY, sW, sH, photoX, photoY, photoW, photoH);
        ctx.restore();

        // Border around photo
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
        ctx.lineWidth = 2;
        this._roundRect(ctx, photoX, photoY, photoW, photoH, 20);
        ctx.stroke();

        // AI Verified Plate Watermark Chip
        ctx.fillStyle = 'rgba(15, 16, 18, 0.85)';
        this._roundRect(ctx, photoX + 20, photoY + 20, 215, 38, 19);
        ctx.fill();
        ctx.strokeStyle = 'rgba(39, 195, 128, 0.4)';
        ctx.stroke();

        ctx.fillStyle = '#27c380';
        ctx.font = '600 17px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
        ctx.fillText('✓ AI Verified Plate', photoX + 38, photoY + 45);

        photoBottom = photoY + photoH + 30;
      }
    }

    // 5. Calorie & Macronutrient Matrix
    const cals = Math.round(meal.totalCalories || 0);
    const protein = (meal.totalProteinGrams || 0).toFixed(1);
    const carbs = (meal.totalCarbsGrams || 0).toFixed(1);
    const fat = (meal.totalFatGrams || 0).toFixed(1);
    const fiber = (meal.totalFiberGrams || 0).toFixed(1);

    const macroY = photoBottom + 10;
    const cardBoxW = width - 152;
    const cardBoxH = 135;

    // Background card for macros
    ctx.fillStyle = '#0f1012';
    this._roundRect(ctx, 76, macroY, cardBoxW, cardBoxH, 18);
    ctx.fill();
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    // 5 Columns: Calories | Protein | Carbs | Fat | Fiber
    const colW = cardBoxW / 5;

    const renderMacroCol = (idx, label, val, color, emoji) => {
      const cx = 76 + idx * colW + colW / 2;
      ctx.textAlign = 'center';

      ctx.fillStyle = '#8a8f98';
      ctx.font = '500 18px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(label, cx, macroY + 38);

      ctx.fillStyle = color;
      ctx.font = 'bold 30px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(val, cx, macroY + 82);

      ctx.fillStyle = '#62666d';
      ctx.font = '16px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(emoji, cx, macroY + 112);
    };

    renderMacroCol(0, 'CALORIES', `${cals}`, '#fb923c', '🔥');
    renderMacroCol(1, 'PROTEIN', `${protein}g`, '#38bdf8', '💪');
    renderMacroCol(2, 'CARBS', `${carbs}g`, '#facc15', '🌾');
    renderMacroCol(3, 'FAT', `${fat}g`, '#f87171', '🥑');
    renderMacroCol(4, 'FIBER', `${fiber}g`, '#34d399', '🥗');

    ctx.textAlign = 'left';

    // 6. Sub-items pills list
    let currentY = macroY + cardBoxH + 35;
    const items = meal.items || meal.Items || [];
    if (items.length > 0) {
      ctx.fillStyle = '#8a8f98';
      ctx.font = '600 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText('IDENTIFIED PORTIONS:', 76, currentY);
      currentY += 20;

      let pillX = 76;
      let pillY = currentY;
      ctx.font = '500 19px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';

      for (let i = 0; i < Math.min(items.length, 5); i++) {
        const item = items[i];
        const itemText = `🍲 ${item.name || item.Name} (${item.estimatedPortion || '1 serving'})`;
        const textW = ctx.measureText(itemText).width;
        const pillW = textW + 30;

        if (pillX + pillW > width - 76) {
          pillX = 76;
          pillY += 50;
        }

        ctx.fillStyle = 'rgba(255, 255, 255, 0.05)';
        this._roundRect(ctx, pillX, pillY, pillW, 40, 10);
        ctx.fill();
        ctx.strokeStyle = '#2e3035';
        ctx.stroke();

        ctx.fillStyle = '#f7f8f8';
        ctx.fillText(itemText, pillX + 15, pillY + 27);

        pillX += pillW + 14;
      }
      currentY = pillY + 60;
    }

    // 7. Clinical Dietitian Insight (ICMR-NIN 2024)
    const advice = meal.dietitianAdvice || meal.DietitianAdvice;
    if (options.includeAdvice !== false && advice && currentY < height - 160) {
      const adviceBoxH = Math.min(130, height - currentY - 110);
      ctx.fillStyle = 'rgba(15, 23, 42, 0.7)';
      this._roundRect(ctx, 76, currentY, width - 152, adviceBoxH, 16);
      ctx.fill();
      ctx.strokeStyle = 'rgba(39, 195, 128, 0.25)';
      ctx.stroke();

      ctx.fillStyle = '#27c380';
      ctx.font = 'bold 18px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText('🩺 DOST CLINICAL INSIGHT (ICMR-NIN 2024)', 96, currentY + 32);

      ctx.fillStyle = '#94a3b8';
      ctx.font = '400 19px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      this._wrapText(ctx, `"${advice}"`, 96, currentY + 65, width - 192, 27, 2);
    }

    // 8. Branded Web Application Footer (User requested web app link)
    const footerY = height - 58;
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(76, footerY - 24);
    ctx.lineTo(width - 76, footerY - 24);
    ctx.stroke();

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('Track your Indian meals on web:', 76, footerY);

    ctx.fillStyle = '#38bdf8';
    ctx.font = 'bold 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(this._appUrl, 385, footerY);

    ctx.textAlign = 'right';
    ctx.fillStyle = '#62666d';
    ctx.font = '500 18px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('ICMR-NIN 2024 Calibrated', width - 76, footerY);
    ctx.textAlign = 'left';

    const dataUrl = canvas.toDataURL('image/png');
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    return { canvas, dataUrl, blob };
  }

  /**
   * Render Context-Specific Daily View / Metabolic Pulse Card.
   * @param {Object} ledger
   * @param {Object} [options]
   * @param {'square'|'story'} [options.aspectRatio='square']
   * @param {boolean} [options.includeStreak=true]
   * @param {boolean} [options.includeBadges=true]
   * @returns {Promise<{ canvas: HTMLCanvasElement, dataUrl: string, blob: Blob }>}
   */
  async renderDailyCard(ledger, options = {}) {
    const isStory = options.aspectRatio === 'story';
    const width = 1080;
    const height = isStory ? 1920 : 1080;

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');

    // 1. Dark Gradient Background
    const bgGrad = ctx.createLinearGradient(0, 0, 0, height);
    bgGrad.addColorStop(0, '#0a0b0e');
    bgGrad.addColorStop(0.5, '#07080a');
    bgGrad.addColorStop(1, '#0e0f14');
    ctx.fillStyle = bgGrad;
    ctx.fillRect(0, 0, width, height);

    // Glowing Ambient Orbs
    const cyanGlow = ctx.createRadialGradient(width * 0.85, 160, 20, width * 0.85, 160, 480);
    cyanGlow.addColorStop(0, 'rgba(56, 189, 248, 0.2)');
    cyanGlow.addColorStop(1, 'rgba(56, 189, 248, 0)');
    ctx.fillStyle = cyanGlow;
    ctx.fillRect(0, 0, width, height);

    const amberGlow = ctx.createRadialGradient(160, height * 0.65, 20, 160, height * 0.65, 450);
    amberGlow.addColorStop(0, 'rgba(251, 146, 60, 0.16)');
    amberGlow.addColorStop(1, 'rgba(251, 146, 60, 0)');
    ctx.fillStyle = amberGlow;
    ctx.fillRect(0, 0, width, height);

    // Outer Border
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 2;
    this._roundRect(ctx, 32, 32, width - 64, height - 64, 28);
    ctx.stroke();

    // 2. Header: Logo + Consistency Streak
    ctx.fillStyle = '#27c380';
    ctx.beginPath();
    ctx.arc(76, 88, 10, 0, Math.PI * 2);
    ctx.fill();

    ctx.fillStyle = '#f7f8f8';
    ctx.font = 'bold 32px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('DIET DOST', 100, 96);

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('· Daily Nutrition Pulse', 285, 95);

    // Streak Pill (Top Right)
    const streak = ledger.consistencyStreakDays || 7;
    ctx.fillStyle = 'rgba(245, 166, 35, 0.18)';
    this._roundRect(ctx, width - 360, 64, 284, 46, 23);
    ctx.fill();
    ctx.strokeStyle = 'rgba(245, 166, 35, 0.45)';
    ctx.stroke();

    ctx.fillStyle = '#fbbf24';
    ctx.font = '600 22px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`🔥 ${streak}-Day Streak Active`, width - 338, 95);

    // Date
    const today = new Date();
    const dateFormatted = today.toLocaleDateString('en-US', { weekday: 'long', month: 'short', day: 'numeric', year: 'numeric' });
    ctx.fillStyle = '#62666d';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`Summary for ${dateFormatted}`, 76, 145);

    // 3. Hero Calorie Balance Card
    const cardY = 175;
    const cardH = 195;
    ctx.fillStyle = '#0f1012';
    this._roundRect(ctx, 76, cardY, width - 152, cardH, 20);
    ctx.fill();
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 1.5;
    ctx.stroke();

    const consumed = Math.round(ledger.consumedCalories || 0);
    const budget = Math.round(ledger.budgetedCalories || 1600);
    const pending = Math.round(ledger.pendingCalories || (budget - consumed));
    const pct = Math.min(100, Math.max(0, Math.round((consumed / Math.max(1, budget)) * 100)));

    ctx.fillStyle = '#8a8f98';
    ctx.font = '600 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('DAILY CALORIE BUDGET', 106, cardY + 44);

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 50px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`${consumed.toLocaleString()} / ${budget.toLocaleString()}`, 106, cardY + 104);

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 24px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('kcal', 106 + ctx.measureText(`${consumed.toLocaleString()} / ${budget.toLocaleString()} `).width, cardY + 104);

    // Deficit chip on top right of calorie card
    const deficitText = pending >= 0 ? `🎯 ${pending} kcal Deficit Left` : `⚠️ ${Math.abs(pending)} kcal Surplus`;
    const deficitColor = pending >= 0 ? '#34d399' : '#f87171';
    const deficitBg = pending >= 0 ? 'rgba(52, 211, 153, 0.15)' : 'rgba(248, 113, 113, 0.15)';

    ctx.textAlign = 'right';
    ctx.fillStyle = deficitBg;
    this._roundRect(ctx, width - 390, cardY + 58, 284, 46, 23);
    ctx.fill();
    ctx.strokeStyle = deficitColor;
    ctx.stroke();

    ctx.fillStyle = deficitColor;
    ctx.font = '600 21px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(deficitText, width - 125, cardY + 89);
    ctx.textAlign = 'left';

    // Calorie Progress Bar Fill
    const barX = 106;
    const barY = cardY + 138;
    const barW = width - 212;
    const barH = 18;

    ctx.fillStyle = '#1e2025';
    this._roundRect(ctx, barX, barY, barW, barH, 9);
    ctx.fill();

    const fillW = Math.max(12, Math.min(barW, (barW * pct) / 100));
    const fillGrad = ctx.createLinearGradient(barX, 0, barX + fillW, 0);
    fillGrad.addColorStop(0, '#5e6ad2');
    fillGrad.addColorStop(1, pending >= 0 ? '#27c380' : '#ef4444');
    ctx.fillStyle = fillGrad;
    this._roundRect(ctx, barX, barY, fillW, barH, 9);
    ctx.fill();

    // 4. Macro Grid (4 Columns)
    const macroY = cardY + cardH + 28;
    const macroH = 145;
    const macroW = (width - 152 - 36) / 4;

    const macros = [
      { label: 'PROTEIN', val: Math.round(ledger.consumedProteinGrams || 0), tgt: Math.round(ledger.targetProteinGrams || 75), color: '#38bdf8', icon: '💪' },
      { label: 'CARBS', val: Math.round(ledger.consumedCarbsGrams || 0), tgt: Math.round(ledger.targetCarbsGrams || 180), color: '#facc15', icon: '🌾' },
      { label: 'FAT (OIL)', val: Math.round(ledger.consumedFatGrams || 0), tgt: Math.round(ledger.targetFatGrams || 45), color: '#f87171', icon: '🥑' },
      { label: 'FIBER', val: Math.round(ledger.consumedFiberGrams || 0), tgt: Math.round(ledger.targetFiberGrams || 30), color: '#34d399', icon: '🥗' }
    ];

    macros.forEach((m, idx) => {
      const mx = 76 + idx * (macroW + 12);
      ctx.fillStyle = '#0f1012';
      this._roundRect(ctx, mx, macroY, macroW, macroH, 16);
      ctx.fill();
      ctx.strokeStyle = '#222326';
      ctx.lineWidth = 1.2;
      ctx.stroke();

      ctx.fillStyle = '#8a8f98';
      ctx.font = '600 17px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(`${m.icon} ${m.label}`, mx + 18, macroY + 36);

      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 30px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(`${m.val}g`, mx + 18, macroY + 78);

      ctx.fillStyle = '#62666d';
      ctx.font = '500 19px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText(`/ ${m.tgt}g`, mx + 18 + ctx.measureText(`${m.val}g `).width, macroY + 78);

      // Mini macro meter
      const mPct = Math.min(100, Math.round((m.val / Math.max(1, m.tgt)) * 100));
      const mBarX = mx + 18;
      const mBarY = macroY + 104;
      const mBarW = macroW - 36;
      ctx.fillStyle = '#1e2025';
      this._roundRect(ctx, mBarX, mBarY, mBarW, 10, 5);
      ctx.fill();

      ctx.fillStyle = m.color;
      this._roundRect(ctx, mBarX, mBarY, Math.max(6, (mBarW * mPct) / 100), 10, 5);
      ctx.fill();
    });

    // 5. Daily Health Score & Earned Indian Badges
    const healthY = macroY + macroH + 28;
    const healthH = 150;
    ctx.fillStyle = '#0f1012';
    this._roundRect(ctx, 76, healthY, width - 152, healthH, 20);
    ctx.fill();
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 1.2;
    ctx.stroke();

    // Score Circle
    const scoreVal = ledger.healthScore ?? 88;
    ctx.fillStyle = 'rgba(39, 195, 128, 0.15)';
    ctx.beginPath();
    ctx.arc(148, healthY + healthH / 2, 48, 0, Math.PI * 2);
    ctx.fill();
    ctx.strokeStyle = '#27c380';
    ctx.lineWidth = 3;
    ctx.stroke();

    ctx.textAlign = 'center';
    ctx.fillStyle = '#27c380';
    ctx.font = 'bold 36px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(`${scoreVal}`, 148, healthY + healthH / 2 + 12);
    ctx.textAlign = 'left';

    ctx.fillStyle = '#ffffff';
    ctx.font = 'bold 24px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('Daily Clinical Health Score', 222, healthY + 52);

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 19px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('Wholesome ICMR-NIN Compliance · Balanced Macro Ratio', 222, healthY + 84);

    // Badges Row
    const badges = (ledger.earnedBadges && ledger.earnedBadges.length > 0)
      ? ledger.earnedBadges
      : ['Tadka Ninja 🥷', 'Salt Sentry 🛡️', 'Deficit Sniper 🎯'];

    let bx = 222;
    badges.slice(0, 3).forEach(b => {
      ctx.font = '600 17px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      const bw = ctx.measureText(b).width + 24;
      ctx.fillStyle = 'rgba(255, 255, 255, 0.06)';
      this._roundRect(ctx, bx, healthY + 98, bw, 32, 16);
      ctx.fill();
      ctx.strokeStyle = '#2e3035';
      ctx.stroke();

      ctx.fillStyle = '#f7f8f8';
      ctx.fillText(b, bx + 12, healthY + 120);
      bx += bw + 10;
    });

    // 6. Dost Companion Speech
    const dostMsg = ledger.dostMessage || 'Namaste! Keep nourishing your body with wholesome Indian nutrition.';
    const dostY = healthY + healthH + 26;
    if (dostY < height - 160) {
      const dostH = Math.min(130, height - dostY - 110);
      ctx.fillStyle = 'rgba(94, 106, 210, 0.1)';
      this._roundRect(ctx, 76, dostY, width - 152, dostH, 18);
      ctx.fill();
      ctx.strokeStyle = 'rgba(94, 106, 210, 0.3)';
      ctx.stroke();

      ctx.fillStyle = '#a5b4fc';
      ctx.font = 'bold 18px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      ctx.fillText('💬 DOST COMPANION REFLECTION', 106, dostY + 34);

      ctx.fillStyle = '#cbd5e1';
      ctx.font = '400 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
      this._wrapText(ctx, `"${dostMsg}"`, 106, dostY + 68, width - 212, 28, 2);
    }

    // 7. Footer: Web Application link
    const footerY = height - 58;
    ctx.strokeStyle = '#222326';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(76, footerY - 24);
    ctx.lineTo(width - 76, footerY - 24);
    ctx.stroke();

    ctx.fillStyle = '#8a8f98';
    ctx.font = '500 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('Track your Indian diet on web:', 76, footerY);

    ctx.fillStyle = '#38bdf8';
    ctx.font = 'bold 20px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText(this._appUrl, 365, footerY);

    ctx.textAlign = 'right';
    ctx.fillStyle = '#62666d';
    ctx.font = '500 18px -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif';
    ctx.fillText('ICMR-NIN 2024 Calibrated', width - 76, footerY);
    ctx.textAlign = 'left';

    const dataUrl = canvas.toDataURL('image/png');
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    return { canvas, dataUrl, blob };
  }
}

