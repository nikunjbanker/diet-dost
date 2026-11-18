/*
 * Copyright (c) 2026 diet-dost and/or its contributors.
 * Licensed under the "GNU Affero General Public License v3.0 only" and
 * the "Server Side Public License, v 1"; you may not use this file except
 * in compliance with, at your election, the "GNU Affero General Public
 * License v3.0 only" or the "Server Side Public License, v 1".
 */

import { ShareCardRenderer } from '../ui/ShareCardRenderer.js';

/**
 * ShareService
 * Cross-Platform Social Sharing Service.
 * Manages device-adaptive dispatching:
 *  - Native Web Share API Level 2 on Mobile (<640px) with fallback
 *  - High-DPI Card generation via ShareCardRenderer
 *  - Clipboard Image Copy (Ctrl+V) on Desktop
 *  - Web Intents for WhatsApp Web, Facebook, X (Twitter), Email
 *  - DPDPA 2023 privacy compliance (no PII leakage)
 */
export class ShareService {
  /**
   * @param {Object} [options]
   * @param {import('../ui/toast.js').ToastService} [options.toastService]
   * @param {import('../core/event-bus.js').EventBus} [options.eventBus]
   */
  constructor({ toastService = null, eventBus = null } = {}) {
    this._toast = toastService;
    this._bus = eventBus;
    this._renderer = new ShareCardRenderer();
    this._appUrl = 'https://dev.diet-dost.in';
  }

  get renderer() {
    return this._renderer;
  }

  get appUrl() {
    return this._appUrl;
  }

  /**
   * Detects if current client is a mobile device.
   * @returns {boolean}
   */
  isMobile() {
    if (typeof window === 'undefined') return false;
    return window.innerWidth < 640 || /Android|iPhone|iPad|iPod|webOS|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);
  }

  /**
   * Checks if the browser supports sharing image files via Web Share API Level 2.
   * @returns {boolean}
   */
  canShareFiles() {
    if (typeof navigator === 'undefined' || typeof navigator.share !== 'function' || typeof navigator.canShare !== 'function') {
      return false;
    }
    try {
      const testFile = new File([''], 'test.png', { type: 'image/png' });
      return navigator.canShare({ files: [testFile] });
    } catch {
      return false;
    }
  }

  /**
   * Format structured share text for a Meal Log Entry.
   * @param {Object} meal
   * @param {string} [appUrl]
   * @returns {string}
   */
  generateMealShareText(meal, appUrl = this._appUrl) {
    if (!meal) return '';
    const mealTypeNames = ['Breakfast', 'Lunch', 'Snack', 'Dinner'];
    const typeIdx = typeof meal.mealType === 'number' ? meal.mealType : 1;
    const typeName = mealTypeNames[typeIdx] || meal.mealType || 'Meal';
    const dish = meal.dishName || meal.DishName || 'Logged Meal';
    const cals = Math.round(meal.totalCalories || 0);
    const p = (meal.totalProteinGrams || 0).toFixed(1);
    const c = (meal.totalCarbsGrams || 0).toFixed(1);
    const f = (meal.totalFatGrams || 0).toFixed(1);
    const fib = (meal.totalFiberGrams || 0).toFixed(1);

    let text = `🍽️ ${typeName}: ${dish}\n`;
    text += `🔥 ${cals} kcal | 💪 ${p}g P | 🌾 ${c}g C | 🥑 ${f}g F | 🥗 ${fib}g Fiber\n`;
    text += `🩺 Calibrated with ICMR-NIN 2024 Indian clinical standards.\n`;
    text += `Track your Indian meals on web: ${appUrl}`;
    return text;
  }

  /**
   * Format structured share text for Daily View / Metabolic Pulse.
   * @param {Object} ledger
   * @param {string} [appUrl]
   * @returns {string}
   */
  generateDailyShareText(ledger, appUrl = this._appUrl) {
    if (!ledger) return '';
    const consumed = Math.round(ledger.consumedCalories || 0);
    const budget = Math.round(ledger.budgetedCalories || 1600);
    const pending = Math.round(ledger.pendingCalories || (budget - consumed));
    const p = Math.round(ledger.consumedProteinGrams || 0);
    const c = Math.round(ledger.consumedCarbsGrams || 0);
    const f = Math.round(ledger.consumedFatGrams || 0);
    const fib = Math.round(ledger.consumedFiberGrams || 0);
    const streak = ledger.consistencyStreakDays ?? 0;
    const score = (ledger.healthScore !== undefined && ledger.healthScore !== null) ? ledger.healthScore : 0;

    const deficitStr = pending >= 0 ? `🎯 ${pending} kcal Deficit` : `⚠️ ${Math.abs(pending)} kcal Surplus`;

    let text = `🌟 My Daily Nutrition Pulse on Diet Dost\n`;
    text += `🔥 ${consumed} / ${budget} kcal (${deficitStr})\n`;
    text += `💪 ${p}g P | 🌾 ${c}g C | 🥑 ${f}g F | 🥗 ${fib}g Fiber\n`;
    text += `🏆 Daily Health Score: ${score}/100\n`;
    text += `🔥 ${streak}-Day Consistency Streak!\n`;
    text += `Track your Indian diet on web: ${appUrl}`;
    return text;
  }

  /**
   * Copy image Blob directly to clipboard (Desktop 1-Click Hero Feature).
   * @param {Blob} blob
   * @returns {Promise<boolean>}
   */
  async copyImageToClipboard(blob) {
    if (!blob) return false;
    try {
      if (typeof navigator !== 'undefined' && navigator.clipboard && typeof navigator.clipboard.write === 'function') {
        const ClipboardItemCtor = typeof window !== 'undefined' && window.ClipboardItem ? window.ClipboardItem : ClipboardItem;
        const item = new ClipboardItemCtor({ 'image/png': blob });
        await navigator.clipboard.write([item]);
        if (this._toast) {
          this._toast.success('Card image copied to clipboard! Paste directly with Ctrl+V');
        }
        return true;
      }
    } catch (err) {
      console.warn('[ShareService] Clipboard image write failed:', err);
    }
    return false;
  }

  /**
   * Copy text directly to clipboard.
   * @param {string} text
   * @returns {Promise<boolean>}
   */
  async copyTextToClipboard(text) {
    if (!text) return false;
    try {
      if (typeof navigator !== 'undefined' && navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
        await navigator.clipboard.writeText(text);
        if (this._toast) {
          this._toast.success('Summary copied to clipboard!');
        }
        return true;
      }
    } catch (err) {
      console.warn('[ShareService] Clipboard text write failed:', err);
    }
    return false;
  }

  /**
   * Primary action to share a Meal Log Entry.
   * On Mobile with Web Share API L2: dispatches native share sheet with card image.
   * On Desktop or fallback: opens the Share & Export Studio modal.
   * @param {Object} meal
   * @returns {Promise<void>}
   */
  async shareMeal(meal) {
    if (!meal) return;
    const shareText = this.generateMealShareText(meal);

    // If mobile with file sharing support, attempt direct native share sheet
    if (this.isMobile() && this.canShareFiles()) {
      try {
        const card = await this._renderer.renderMealCard(meal, { aspectRatio: 'square' });
        const file = new File([card.blob], 'diet-dost-meal.png', { type: 'image/png' });
        await navigator.share({
          title: `Diet Dost - ${meal.dishName || 'Meal Entry'}`,
          text: shareText,
          files: [file]
        });
        return;
      } catch (err) {
        // User aborted or file share failed; fallback to modal
        if (err.name === 'AbortError') return;
        console.warn('[ShareService] Native share failed, opening studio:', err);
      }
    }

    // Open Desktop / Responsive Share Studio Modal
    if (this._bus) {
      this._bus.emit('share:open', {
        type: 'meal',
        data: meal,
        text: shareText
      });
    }
  }

  /**
   * Primary action to share Daily View / Metabolic Pulse.
   * On Mobile with Web Share API L2: dispatches native share sheet with card image.
   * On Desktop or fallback: opens the Share & Export Studio modal.
   * @param {Object} ledger
   * @returns {Promise<void>}
   */
  async shareDaily(ledger) {
    if (!ledger) return;
    const shareText = this.generateDailyShareText(ledger);

    // If mobile with file sharing support, attempt direct native share sheet
    if (this.isMobile() && this.canShareFiles()) {
      try {
        const card = await this._renderer.renderDailyCard(ledger, { aspectRatio: 'square' });
        const file = new File([card.blob], 'diet-dost-daily.png', { type: 'image/png' });
        await navigator.share({
          title: 'Diet Dost - Daily Nutrition Pulse',
          text: shareText,
          files: [file]
        });
        return;
      } catch (err) {
        if (err.name === 'AbortError') return;
        console.warn('[ShareService] Native share failed, opening studio:', err);
      }
    }

    // Open Desktop / Responsive Share Studio Modal
    if (this._bus) {
      this._bus.emit('share:open', {
        type: 'daily',
        data: ledger,
        text: shareText
      });
    }
  }
}

